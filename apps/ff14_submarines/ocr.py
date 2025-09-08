from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
import re
from typing import List, Optional


_FW_DIGITS = {ord(c): ord("0") + i for i, c in enumerate("０１２３４５６７８９")}
_FW_PUNCT = {
    ord("："): ord(":"),
    ord("［"): ord("["),
    ord("］"): ord("]"),
    ord("　"): ord(" "),
}


def _to_halfwidth(s: str) -> str:
    """Convert common full-width digits/punctuations to half-width.
    Keeps Kanji like 時/分 as-is.
    """
    return s.translate(_FW_DIGITS).translate(_FW_PUNCT)


@dataclass
class SubmarineETA:
    name: str
    remaining_minutes: int
    eta: datetime


_BRACKET_PAIR = re.compile(r"[\[［]([^\]］]*)[\]］]")
_HOURS_RE = re.compile(r"(?P<h>\d{1,3})\s*時間")
_MINS_RE = re.compile(r"(?P<m>\d{1,3})\s*分")
_HM_COLON_RE = re.compile(r"(?P<h>\d{1,3})\s*:\s*(?P<m>\d{1,2})")


def _parse_remaining(segment: str) -> Optional[int]:
    """Parse remaining minutes from a normalized segment.

    Supports:
    - "1時間 49分"
    - "49分"
    - "1:49" (after normalization of full-width colon/digits)
    """
    seg = _to_halfwidth(segment)

    # 1) HH:MM format
    m = _HM_COLON_RE.search(seg)
    if m:
        h = int(m.group("h"))
        mm = int(m.group("m"))
        return h * 60 + mm

    # 2) Kanji units
    h = 0
    m = 0
    mh = _HOURS_RE.search(seg)
    if mh:
        try:
            h = int(mh.group("h"))
        except ValueError:
            h = 0
    mm = _MINS_RE.search(seg)
    if mm:
        try:
            m = int(mm.group("m"))
        except ValueError:
            m = 0

    if h == 0 and m == 0:
        return None
    return h * 60 + m


def extract_submarine_etas(text: str, now: Optional[datetime] = None) -> List[SubmarineETA]:
    """Extract up to 4 soonest submarine ETAs from OCR text.

    - Normalizes full-width digits/colon/brackets
    - Handles formats like "1時間 49分", "49分", "３：０５"
    - Deduplicates by name, keeping the shortest remaining time
    - Returns at most 4 entries sorted by remaining minutes
    """
    if now is None:
        now = datetime.now(timezone.utc)
    elif now.tzinfo is None:
        # Make timezone-aware to keep arithmetic consistent
        now = now.replace(tzinfo=timezone.utc)

    lines = [ln.strip() for ln in text.splitlines() if ln.strip()]

    best_by_name: dict[str, int] = {}
    for raw in lines:
        line = _to_halfwidth(raw)

        # Name: take the part before the first bracket group if present
        name_part = line
        m_first = _BRACKET_PAIR.search(line)
        if m_first:
            # name is before the first bracket's opening bracket
            idx = m_first.start()
            name_part = line[:idx].strip()

        name = name_part.strip()
        if not name:
            # fallback: try to pull something before any space
            name = line.split()[0] if line.split() else ""
        if not name:
            continue

        # Remaining segment: try the last bracket content first; if none, use trailing text
        segments = [g for g in _BRACKET_PAIR.findall(raw)]
        remain_seg = segments[-1] if segments else line

        remaining = _parse_remaining(remain_seg)
        if remaining is None:
            # Try on the whole line as a fallback, but avoid numbers inside [Rank..]
            tail = _BRACKET_PAIR.sub(" ", raw)
            remaining = _parse_remaining(tail)

        if remaining is None or remaining <= 0:
            continue

        if name not in best_by_name or remaining < best_by_name[name]:
            best_by_name[name] = remaining

    items = [
        SubmarineETA(name=n, remaining_minutes=mins, eta=now + timedelta(minutes=mins))
        for n, mins in best_by_name.items()
    ]
    items.sort(key=lambda x: x.remaining_minutes)
    return items[:4]


__all__ = ["SubmarineETA", "extract_submarine_etas"]

