from __future__ import annotations

import json
import os
from dataclasses import dataclass, asdict
from typing import Optional


DATA_DIR = os.path.join(os.getcwd(), "data")
LOG_DIR = os.path.join(DATA_DIR, "logs")
CONFIG_PATH = os.path.join(DATA_DIR, "config.json")
TOKEN_PATH = os.path.join(DATA_DIR, "token.json")


def ensure_dirs() -> None:
    os.makedirs(DATA_DIR, exist_ok=True)
    os.makedirs(LOG_DIR, exist_ok=True)


@dataclass
class Region:
    x: int
    y: int
    width: int
    height: int


@dataclass
class AppConfig:
    # Screen capture area
    region: Optional[Region] = None

    # Tesseract settings
    tesseract_path: str = r"C:\\Program Files\\Tesseract-OCR\\tesseract.exe"
    tesseract_lang: str = "jpn"

    # Main loop
    capture_interval_sec: int = 300  # 5 minutes

    # Calendar
    calendar_id: str = "primary"
    reminder_minutes: int = 10
    event_duration_minutes: int = 10

    # Parsing / matching
    min_minutes_threshold_update: int = 2  # update event when change >= 2 min

    @staticmethod
    def load(path: str = CONFIG_PATH) -> "AppConfig":
        if os.path.exists(path):
            with open(path, "r", encoding="utf-8") as f:
                raw = json.load(f)
            region = raw.get("region")
            region_obj = (
                Region(**region) if isinstance(region, dict) and region else None
            )
            cfg = AppConfig(
                region=region_obj,
                tesseract_path=raw.get("tesseract_path", AppConfig().tesseract_path),
                tesseract_lang=raw.get("tesseract_lang", "jpn"),
                capture_interval_sec=raw.get("capture_interval_sec", 300),
                calendar_id=raw.get("calendar_id", "primary"),
                reminder_minutes=raw.get("reminder_minutes", 10),
                event_duration_minutes=raw.get("event_duration_minutes", 10),
                min_minutes_threshold_update=raw.get(
                    "min_minutes_threshold_update", 2
                ),
            )
            return cfg
        cfg = AppConfig()
        cfg.save(path)
        return cfg

    def save(self, path: str = CONFIG_PATH) -> None:
        ensure_dirs()
        data = asdict(self)
        if isinstance(self.region, Region):
            data["region"] = asdict(self.region)
        with open(path, "w", encoding="utf-8") as f:
            json.dump(data, f, ensure_ascii=False, indent=2)

