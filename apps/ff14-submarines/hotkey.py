from __future__ import annotations

import ctypes
import threading
import logging
from typing import Callable, Optional


if hasattr(ctypes, "windll"):
    user32 = ctypes.windll.user32
else:
    user32 = None


MOD_ALT = 0x0001
MOD_CONTROL = 0x0002
MOD_SHIFT = 0x0004
WM_HOTKEY = 0x0312


def _vk_from_key(key: str) -> Optional[int]:
    key = key.lower()
    if len(key) == 1 and "a" <= key <= "z":
        return ord(key.upper())
    mapping = {
        "f1": 0x70,
        "f2": 0x71,
        "f3": 0x72,
        "f4": 0x73,
        "f5": 0x74,
        "f6": 0x75,
        "f7": 0x76,
        "f8": 0x77,
        "f9": 0x78,
        "f10": 0x79,
        "f11": 0x7A,
        "f12": 0x7B,
        "0": 0x30,
        "1": 0x31,
        "2": 0x32,
        "3": 0x33,
        "4": 0x34,
        "5": 0x35,
        "6": 0x36,
        "7": 0x37,
        "8": 0x38,
        "9": 0x39,
    }
    return mapping.get(key)


def _parse_hotkey(hotkey: str) -> Optional[tuple[int, int]]:
    parts = [p.strip().lower() for p in hotkey.replace("+", " ").split() if p.strip()]
    mods = 0
    key: Optional[str] = None
    for p in parts:
        if p in ("ctrl", "control"):
            mods |= MOD_CONTROL
        elif p in ("alt",):
            mods |= MOD_ALT
        elif p in ("shift",):
            mods |= MOD_SHIFT
        else:
            key = p
    if key is None:
        return None
    vk = _vk_from_key(key)
    if vk is None:
        return None
    return mods, vk


def start_hotkey_listener(hotkey: str, on_press: Callable[[], None]) -> Optional[Callable[[], None]]:
    """
    Registers a global hotkey (Windows) and dispatches on_press() on hit.
    Returns a stop() callable, or None if registration failed or unsupported OS.
    """
    if user32 is None:
        logging.warning("Global hotkey unsupported on this OS")
        return None

    parsed = _parse_hotkey(hotkey)
    if not parsed:
        logging.error("Invalid hotkey string: %s", hotkey)
        return None
    mods, vk = parsed

    HOTKEY_ID = 0xA11C  # arbitrary ID

    def worker():
        if not user32.RegisterHotKey(None, HOTKEY_ID, mods, vk):
            logging.error("RegisterHotKey failed. Try running as admin or change hotkey.")
            return
        try:
            msg = ctypes.wintypes.MSG()
        except AttributeError:
            # Define MSG if missing
            class MSG(ctypes.Structure):
                _fields_ = [
                    ("hwnd", ctypes.c_void_p),
                    ("message", ctypes.c_uint),
                    ("wParam", ctypes.c_uintptr),
                    ("lParam", ctypes.c_uintptr),
                    ("time", ctypes.c_uint),
                    ("pt_x", ctypes.c_long),
                    ("pt_y", ctypes.c_long),
                ]

            msg = MSG()

        try:
            while True:
                r = user32.GetMessageW(ctypes.byref(msg), None, 0, 0)
                if r == 0 or r == -1:
                    break
                if msg.message == WM_HOTKEY and msg.wParam == HOTKEY_ID:
                    # Run callback in a separate thread to avoid blocking
                    threading.Thread(target=on_press, daemon=True).start()
                user32.TranslateMessage(ctypes.byref(msg))
                user32.DispatchMessageW(ctypes.byref(msg))
        finally:
            user32.UnregisterHotKey(None, HOTKEY_ID)

    t = threading.Thread(target=worker, name="HotkeyListener", daemon=True)
    t.start()

    def stop():
        # Post a WM_QUIT to end the loop (use Windows thread id which equals Thread.ident on CPython/Windows)
        if t.ident is not None:
            user32.PostThreadMessageW(ctypes.c_uint(int(t.ident)), 0x0012, 0, 0)

    return stop
