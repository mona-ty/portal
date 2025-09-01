from __future__ import annotations

import re
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from typing import Dict, List, Optional, Tuple

import pytesseract
from PIL import Image


SUB_LINE_PATTERNS = [
    # name [Rank89] [探索中:残り時間 1時間49分]
    re.compile(
        r"^\s*(?P<name>[\w\-\u3000-\u30FF\u4E00-\u9FFF]+)\s*\[Rank\d+\]\s*\[?\s*探索中\s*[:：]?\s*残り時間\s*(?P<hours>\d+)\s*時間\s*(?P<minutes>\d+)\s*分\s*\]?",
        re.MULTILINE,
    ),
    # name [...] [探索中:残り 49分]
    re.compile(
        r"^\s*(?P<name>[\w\-\u3000-\u30FF\u4E00-\u9FFF]+)\s*\[Rank\d+\]\s*\[?\s*探索中\s*[:：]?\s*残り(?:時間)?\s*(?P<minutes>\d+)\s*分\s*\]?",
        re.MULTILINE,
    ),
]


@dataclass
class SubmarineETA:
    name: str
    eta: datetime  # local time
    remaining_minutes: int


def normalize_text(t: str) -> str:
    # Common OCR confusions: 全角/半角やコロンなど
    replacements = {
        "：": ":",
        "時間": "時間",
        "分": "分",
        "探査": "探索",  # 誤認の揺れ
        "殘": "残",
        "閒": "間",
        "O": "0",
        "o": "0",
        "l": "1",
        "I": "1",
    }
    for k, v in replacements.items():
        t = t.replace(k, v)
    return t


def extract_submarine_etas(text: str, now: Optional[datetime] = None) -> List[SubmarineETA]:
    now = now or datetime.now().astimezone()
    text = normalize_text(text)
    found: Dict[str, SubmarineETA] = {}

    for pat in SUB_LINE_PATTERNS:
        for m in pat.finditer(text):
            name = m.group("name").strip()
            h = int(m.groupdict().get("hours") or 0)
            mnts = int(m.group("minutes"))
            remaining = h * 60 + mnts
            eta = now + timedelta(minutes=remaining)
            # Keep the shortest remaining time per name (latest OCR most likely)
            if name not in found or remaining < found[name].remaining_minutes:
                found[name] = SubmarineETA(name=name, eta=eta, remaining_minutes=remaining)

    # limit to 4 entries (game limit)
    return list(found.values())[:4]


def ocr_image(img: Image.Image, tesseract_cmd: Optional[str], lang: str = "jpn") -> str:
    if tesseract_cmd:
        pytesseract.pytesseract.tesseract_cmd = tesseract_cmd
    # PSM 6: Assume a single uniform block of text
    config = "--psm 6"
    return pytesseract.image_to_string(img, lang=lang, config=config)

