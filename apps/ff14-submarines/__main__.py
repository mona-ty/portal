from __future__ import annotations

import argparse
import json
import os
import subprocess
import logging
import time
from datetime import datetime, timedelta

from PIL import Image

from .config import AppConfig, Region, ensure_dirs
from .logging_setup import setup_logging
from .overlay import ask_region
from .hotkey import start_hotkey_listener
from .capture import CaptureRegion, capture_region
from .ocr import ocr_image, extract_submarine_etas
from .auto_setup_py import (
    detect_region_once as py_detect_once,
    watch_and_detect as py_watch_detect,
)


def cmd_setup(cfg: AppConfig) -> None:
    logging.info("Starting overlay for region selection...")
    try:
        initial = None
        if cfg.region:
            initial = (cfg.region.x, cfg.region.y, cfg.region.width, cfg.region.height)
        res = ask_region(initial)
        cfg.region = Region(x=res.x, y=res.y, width=res.width, height=res.height)
        cfg.save()
        logging.info("Region saved: %s", cfg.region)
    except KeyboardInterrupt:
        logging.info("Region selection cancelled")


def cmd_run(cfg: AppConfig, enable_hotkey: bool = False) -> None:
    if not cfg.region:
        logging.error("No capture region configured. Run with --setup first.")
        return

    # Lazy import calendar service to avoid forcing auth immediately
    from .calendar import get_service, ensure_event

    service = None
    last_seen = {}  # name -> minutes remaining

    # Optional: global hotkey to (re)calibrate on demand
    if enable_hotkey:
        def on_hotkey():
            try:
                logging.info("Hotkey pressed: trying instant auto-detect...")
                helper = os.path.join(os.getcwd(), "native", "auto_setup.exe")
                if os.path.exists(helper):
                    # Try immediate detection first (native)
                    p = subprocess.run([helper], capture_output=True, text=True, encoding="utf-8")
                    if p.returncode == 0 and p.stdout.strip():
                        obj = json.loads(p.stdout.strip())
                        cfg.region = Region(x=int(obj["x"]), y=int(obj["y"]), width=int(obj["width"]), height=int(obj["height"]))
                        cfg.save()
                        logging.info("Hotkey: region updated to %s", cfg.region)
                        return
                    logging.info("Instant detect failed; watching briefly (60s)...")
                    p = subprocess.run([helper, "--watch", "--interval=10000", "--timeout=60000"], capture_output=True, text=True, encoding="utf-8")
                    if p.returncode == 0 and p.stdout.strip():
                        obj = json.loads(p.stdout.strip())
                        cfg.region = Region(x=int(obj["x"]), y=int(obj["y"]), width=int(obj["width"]), height=int(obj["height"]))
                        cfg.save()
                        logging.info("Hotkey: region updated to %s", cfg.region)
                        return
                # Python fallback (no native helper)
                res = py_watch_detect(timeout_s=60, interval_s=10, tesseract_cmd=cfg.tesseract_path, lang=f"{cfg.tesseract_lang}+eng")
                if res:
                    cfg.region = Region(x=res.x, y=res.y, width=res.width, height=res.height)
                    cfg.save()
                    logging.info("Hotkey(Py): region updated to %s", cfg.region)
                else:
                    logging.warning("Hotkey detect did not find region. Open the submarine list and try again.")
            except Exception:
                logging.exception("Hotkey detection error")

        stop = start_hotkey_listener("ctrl+alt+s", on_hotkey)
        if stop:
            logging.info("Global hotkey registered: Ctrl+Alt+S to auto-detect region")
        else:
            logging.warning("Global hotkey not available on this platform")

    logging.info("Monitoring started. Interval=%ss", cfg.capture_interval_sec)
    while True:
        try:
            region = CaptureRegion(
                left=cfg.region.x,
                top=cfg.region.y,
                width=cfg.region.width,
                height=cfg.region.height,
            )
            img = capture_region(region)
            text = ocr_image(
                img,
                cfg.tesseract_path,
                cfg.tesseract_lang,
                enable_preprocess=cfg.enable_preprocess,
                preprocess_scale=cfg.preprocess_scale,
                preprocess_threshold=cfg.preprocess_threshold,
                preprocess_sharpen=cfg.preprocess_sharpen,
                psm=cfg.tesseract_psm,
                oem=cfg.tesseract_oem,
            )
            etas = extract_submarine_etas(text)

            if etas:
                logging.info("OCR detected %d entries", len(etas))
                # Avoid creating service until needed (first success)
                if service is None:
                    from .calendar import get_service as _get
                    service = _get()

                for eta in etas:
                    prev = last_seen.get(eta.name)
                    if (
                        prev is None
                        or abs(prev - eta.remaining_minutes)
                        >= cfg.min_minutes_threshold_update
                    ):
                        start = eta.eta
                        end = start + timedelta(minutes=cfg.event_duration_minutes)
                        title = f"潜水艦 {eta.name} 帰還"
                        key = f"ff14-sub:{eta.name}"
                        event_id = ensure_event(
                            service,
                            cfg.calendar_id,
                            title,
                            start,
                            end,
                            key,
                            cfg.reminder_minutes,
                        )
                        logging.info(
                            "Ensured event for %s at %s (id=%s)",
                            eta.name,
                            start.strftime("%Y-%m-%d %H:%M"),
                            event_id,
                        )
                        last_seen[eta.name] = eta.remaining_minutes
            else:
                logging.info("No submarine lines recognized this cycle")

        except Exception as e:
            logging.exception("Error in main loop: %s", e)

        time.sleep(max(10, cfg.capture_interval_sec))


def main() -> None:
    ensure_dirs()
    setup_logging()

    parser = argparse.ArgumentParser(description="FF14 submarines return time capturer")
    parser.add_argument("--setup", action="store_true", help="Select capture region and save config")
    parser.add_argument(
        "--auto-setup",
        action="store_true",
        help="Auto-detect region in background via native helper (Windows)",
    )
    parser.add_argument(
        "--hotkey",
        action="store_true",
        help="Register global hotkey Ctrl+Alt+S to (re)calibrate region",
    )
    args = parser.parse_args()

    cfg = AppConfig.load()
    if args.setup:
        cmd_setup(cfg)
    elif args.auto_setup:
        # Prefer native helper; fallback to pure Python (no compiler required)
        helper = os.path.join(os.getcwd(), "native", "auto_setup.exe")
        if os.path.exists(helper):
            try:
                logging.info("Running auto_setup helper (watch up to 5 minutes)...")
                proc = subprocess.run(
                    [helper, "--watch", "--interval=20000", "--timeout=300000"],
                    capture_output=True,
                    text=True,
                    encoding="utf-8",
                )
                if proc.returncode == 0:
                    obj = json.loads(proc.stdout.strip())
                    cfg.region = Region(x=int(obj["x"]), y=int(obj["y"]), width=int(obj["width"]), height=int(obj["height"]))
                    cfg.save()
                    logging.info("Auto-detected region saved: %s", cfg.region)
                    return
                else:
                    logging.warning("Native helper failed: %s", proc.stderr.strip())
            except Exception:
                logging.exception("Failed to run auto_setup helper; falling back to Python")
        # Python fallback
        logging.info("Python fallback: watching screen up to 5 minutes for submarine list...")
        res = py_watch_detect(timeout_s=300, interval_s=20, tesseract_cmd=cfg.tesseract_path, lang=f"{cfg.tesseract_lang}+eng")
        if res:
            cfg.region = Region(x=res.x, y=res.y, width=res.width, height=res.height)
            cfg.save()
            logging.info("Auto-detected region saved: %s", cfg.region)
        else:
            logging.error("Auto-setup did not detect region. Open the list briefly and retry.")
    else:
        cmd_run(cfg, enable_hotkey=args.hotkey)


if __name__ == "__main__":
    main()

