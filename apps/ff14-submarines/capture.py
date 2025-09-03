from __future__ import annotations

from dataclasses import dataclass
from typing import Optional, Tuple

from PIL import Image
import mss


@dataclass
class CaptureRegion:
    left: int
    top: int
    width: int
    height: int

    def to_mss(self) -> dict:
        return {"left": self.left, "top": self.top, "width": self.width, "height": self.height}


def capture_region(region: CaptureRegion) -> Image.Image:
    with mss.mss() as sct:
        monitor = region.to_mss()
        img = sct.grab(monitor)
        return Image.frombytes("RGB", img.size, img.rgb)

