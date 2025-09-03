from __future__ import annotations

import tkinter as tk
from dataclasses import dataclass
from typing import Optional, Tuple


@dataclass
class OverlayResult:
    x: int
    y: int
    width: int
    height: int


def ask_region(initial: Optional[Tuple[int, int, int, int]] = None) -> OverlayResult:
    root = tk.Tk()
    root.attributes("-alpha", 0.3)
    root.attributes("-topmost", True)
    root.overrideredirect(True)
    root.configure(bg="cyan")

    # Fullscreen overlay
    root.geometry(f"{root.winfo_screenwidth()}x{root.winfo_screenheight()}+0+0")

    # Resizable rectangle window inside the overlay using a Toplevel
    rect = tk.Toplevel(root)
    rect.overrideredirect(True)
    rect.attributes("-alpha", 0.4)
    rect.configure(bg="red")

    # Initial size/pos
    w, h = 600, 300
    x, y = 100, 100
    if initial:
        x, y, w, h = initial
    rect.geometry(f"{w}x{h}+{x}+{y}")

    info = tk.Label(
        root,
        text="ドラッグで移動 / 右ドラッグでサイズ変更\nEnterで確定 / Escでキャンセル",
        bg="black",
        fg="white",
    )
    info.pack(side=tk.TOP, anchor=tk.NW)

    # Mouse drag to move
    state = {"dx": 0, "dy": 0, "resizing": False}

    def start_move(e):
        state["dx"], state["dy"] = e.x, e.y

    def do_move(e):
        rx, ry = rect.winfo_x(), rect.winfo_y()
        rect.geometry(f"+{rx + (e.x - state['dx'])}+{ry + (e.y - state['dy'])}")

    def start_resize(e):
        state["resizing"] = True
        state["dx"], state["dy"] = e.x, e.y

    def do_resize(e):
        if not state["resizing"]:
            return
        w = max(50, rect.winfo_width() + (e.x - state["dx"]))
        h = max(50, rect.winfo_height() + (e.y - state["dy"]))
        rect.geometry(f"{w}x{h}")
        state["dx"], state["dy"] = e.x, e.y

    rect.bind("<Button-1>", start_move)
    rect.bind("<B1-Motion>", do_move)

    rect.bind("<Button-3>", start_resize)
    rect.bind("<B3-Motion>", do_resize)

    result = {}

    def on_enter(e=None):
        result["x"], result["y"] = rect.winfo_x(), rect.winfo_y()
        result["w"], result["h"] = rect.winfo_width(), rect.winfo_height()
        root.destroy()

    def on_escape(e=None):
        result.clear()
        root.destroy()

    root.bind("<Return>", on_enter)
    root.bind("<Escape>", on_escape)

    root.mainloop()

    if not result:
        raise KeyboardInterrupt("Region selection cancelled")
    return OverlayResult(result["x"], result["y"], result["w"], result["h"])


