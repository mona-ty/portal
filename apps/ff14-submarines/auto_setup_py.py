from __future__ import annotations

import time
from dataclasses import dataclass
from typing import Optional, Tuple, Dict

import mss
from PIL import Image
import pytesseract


@dataclass
class DetectedRegion:
    x: int
    y: int
    width: int
    height: int


def _capture_fullscreen() -> Image.Image:
    with mss.mss() as sct:
        # Primary monitor full grab
        mon = sct.monitors[0]
        img = sct.grab(mon)
        pil = Image.frombytes("RGB", img.size, img.rgb)
        return pil


def _find_roi_by_tsv(img: Image.Image, lang: str = "jpn+eng") -> Optional[DetectedRegion]:
    # Use pytesseract data dict to cluster by line
    data = pytesseract.image_to_data(img, lang=lang, config="--psm 6", output_type=pytesseract.Output.DICT)
    n = len(data["text"]) if data and "text" in data else 0
    if n == 0:
        return None
    # Group words by (block, par, line)
    groups: Dict[Tuple[int, int, int], Dict[str, int]] = {}

    def match_token(t: str) -> bool:
        if not t:
            return False
        t = t.strip()
        if not t:
            return False
        # Heuristics: contains Rank or Japanese time hints (分/時間) or pure digits
        if "rank" in t.lower():
            return True
        try:
            if any(ch.isdigit() for ch in t):
                # very loose; will rely on line grouping
                pass_digit = True
            else:
                pass_digit = False
        except Exception:
            pass_digit = False
        if pass_digit:
            return True
        # UTF-8 decoded already; look for 分, 時間
        if ("分" in t) or ("時間" in t) or ("残" in t):
            return True
        return False

    matched_lines = set()
    for i in range(n):
        txt = data["text"][i]
        if not match_token(txt):
            continue
        key = (data.get("block_num", [0])[i], data.get("par_num", [0])[i], data.get("line_num", [0])[i])
        matched_lines.add(key)

    if not matched_lines:
        return None

    minL, minT, maxR, maxB = 10**9, 10**9, -1, -1
    for i in range(n):
        key = (data.get("block_num", [0])[i], data.get("par_num", [0])[i], data.get("line_num", [0])[i])
        if key not in matched_lines:
            continue
        left, top = int(data["left"][i]), int(data["top"][i])
        width, height = int(data["width"][i]), int(data["height"][i])
        if width <= 0 or height <= 0:
            continue
        L, T, R, B = left, top, left + width, top + height
        if L < minL:
            minL = L
        if T < minT:
            minT = T
        if R > maxR:
            maxR = R
        if B > maxB:
            maxB = B

    if maxR <= 0 or maxB <= 0:
        return None
    pad = 12
    x = max(0, minL - pad)
    y = max(0, minT - pad)
    w = (maxR - minL) + pad * 2
    h = (maxB - minT) + pad * 2
    return DetectedRegion(x, y, w, h)


def detect_region_once(tesseract_cmd: Optional[str] = None, lang: str = "jpn+eng") -> Optional[DetectedRegion]:
    if tesseract_cmd:
        pytesseract.pytesseract.tesseract_cmd = tesseract_cmd
    img = _capture_fullscreen()
    return _find_roi_by_tsv(img, lang=lang)


def watch_and_detect(timeout_s: int = 300, interval_s: int = 20, tesseract_cmd: Optional[str] = None, lang: str = "jpn+eng") -> Optional[DetectedRegion]:
    start = time.time()
    while time.time() - start < timeout_s:
        res = detect_region_once(tesseract_cmd=tesseract_cmd, lang=lang)
        if res:
            return res
        time.sleep(max(5, interval_s))
    return None

