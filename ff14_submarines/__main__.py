from __future__ import annotations

import argparse
import logging
import time
from datetime import datetime, timedelta

from PIL import Image

from .config import AppConfig, Region, ensure_dirs
from .logging_setup import setup_logging
from .overlay import ask_region
from .capture import CaptureRegion, capture_region
from .ocr import ocr_image, extract_submarine_etas


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


def cmd_run(cfg: AppConfig) -> None:
    if not cfg.region:
        logging.error("No capture region configured. Run with --setup first.")
        return

    # Lazy import calendar service to avoid forcing auth immediately
    from .calendar import get_service, ensure_event

    service = None
    last_seen = {}  # name -> minutes remaining

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
            text = ocr_image(img, cfg.tesseract_path, cfg.tesseract_lang)
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
    args = parser.parse_args()

    cfg = AppConfig.load()
    if args.setup:
        cmd_setup(cfg)
    else:
        cmd_run(cfg)


if __name__ == "__main__":
    main()

