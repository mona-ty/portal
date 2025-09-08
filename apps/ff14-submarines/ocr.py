from __future__ import annotations

import re
from dataclasses import dataclass
from datetime import datetime, timedelta
from typing import Dict, List, Optional

import pytesseract
from PIL import Image, ImageOps, ImageFilter


# 正常な日本語表記のパターン（全角/半角や空白の揺れに許容）
SUB_LINE_PATTERNS = [
    # 例: Name [Rank89] [残り 1時間 49分]（Rank/残り側の括弧は全角/半角どちらも可）
    re.compile(
        r"^\\s*(?P<name>[\\w\\-\\u3000-\\u30FF\\u4E00-\\u9FFF]+)\\s*"
        r"(?:(?:\\[|［)\\s*Rank\\d+\\s*(?:\\]|］))?\\s*"  # optional [Rank..] or ［Rank..］
        r"(?:(?:\\[|［).*?(?:\\]|］)\\s*)*"             # optional extra bracket blocks before 残り
        r"(?:\\[|［)?\\s*残り\\s*(?P<hours>\\d+)\\s*時間\\s*(?P<minutes>\\d+)\\s*分\\s*(?:\\]|］)?",
        re.MULTILINE,
    ),
    # 例: Name [Rank89] [残り 49分]
    re.compile(
        r"^\\s*(?P<name>[\\w\\-\\u3000-\\u30FF\\u4E00-\\u9FFF]+)\\s*"
        r"(?:(?:\\[|［)\\s*Rank\\d+\\s*(?:\\]|］))?\\s*"
        r"(?:(?:\\[|［).*?(?:\\]|］)\\s*)*"
        r"(?:\\[|［)?\\s*残り\\s*(?P<minutes>\\d+)\\s*分\\s*(?:\\]|］)?",
        re.MULTILINE,
    ),
    # 例: Name [Rank80] [残り 1:05]（HH:MM / 全角コロンは normalize で半角化）
    re.compile(
        r"^\\s*(?P<name>[\\w\\-\\u3000-\\u30FF\\u4E00-\\u9FFF]+)\\s*"
        r"(?:(?:\\[|［)\\s*Rank\\d+\\s*(?:\\]|］))?\\s*"
        r"(?:(?:\\[|［).*?(?:\\]|］)\\s*)*"
        r"(?:\\[|［)?\\s*残り\\s*(?P<hours>\\d{1,2})\\s*:\\s*(?P<minutes>\\d{2})\\s*(?:\\]|］)?",
        re.MULTILINE,
    ),
]

@dataclass
class SubmarineETA:
    name: str
    eta: datetime  # local time
    remaining_minutes: int


def _to_half_width_digits(t: str) -> str:
    # 全角数字を半角に統一
    table = {ord(f): ord("0") + i for i, f in enumerate("０１２３４５６７８９")}
    return t.translate(table)


def normalize_text(t: str) -> str:
    # よくあるOCRの混同と記号の統一
    t = _to_half_width_digits(t)
    replacements = {
        "：": ":",
        # 似た形のゆらぎ
        "殘": "残",
        "歸": "帰",
        # アルファベット/数字混同
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
            # Keep the shortest remaining time per name (最新OCRを優先)
            if name not in found or remaining < found[name].remaining_minutes:
                found[name] = SubmarineETA(name=name, eta=eta, remaining_minutes=remaining)

    # ゲームのリスト最大に合わせて4件まで
    return list(found.values())[:4]


def preprocess_image(
    img: Image.Image,
    *,
    scale: float = 2.0,
    threshold: Optional[int] = 180,
    sharpen: bool = True,
) -> Image.Image:
    # グレースケール→拡大→（任意）しきい値二値化→シャープ
    g = ImageOps.grayscale(img)
    if scale and scale != 1.0:
        new_size = (max(1, int(g.width * scale)), max(1, int(g.height * scale)))
        g = g.resize(new_size)
    if threshold is not None:
        g = g.point(lambda p: 255 if p >= int(threshold) else 0)
    if sharpen:
        g = g.filter(ImageFilter.SHARPEN)
    return g


def ocr_image(
    img: Image.Image,
    tesseract_cmd: Optional[str],
    lang: str = "jpn",
    *,
    enable_preprocess: bool = True,
    preprocess_scale: float = 2.0,
    preprocess_threshold: Optional[int] = 180,
    preprocess_sharpen: bool = True,
    psm: int = 6,
    oem: Optional[int] = None,
) -> str:
    if tesseract_cmd:
        pytesseract.pytesseract.tesseract_cmd = tesseract_cmd
    use_img = img
    if enable_preprocess:
        use_img = preprocess_image(
            img,
            scale=preprocess_scale,
            threshold=preprocess_threshold,
            sharpen=preprocess_sharpen,
        )
    # Tesseract options
    opts = [f"--psm {psm}"]
    if oem is not None:
        opts.append(f"--oem {oem}")
    config = " ".join(opts)
    return pytesseract.image_to_string(use_img, lang=lang, config=config)

