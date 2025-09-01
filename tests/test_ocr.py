import os
import sys
import importlib.util
import unittest
from datetime import datetime, timezone, timedelta


def load_ocr_module():
    here = os.path.dirname(os.path.abspath(__file__))
    repo_root = os.path.abspath(os.path.join(here, os.pardir))
    ocr_path = os.path.join(repo_root, "ff14-submarines", "ocr.py")
    spec = importlib.util.spec_from_file_location("ff14_submarines.ocr", ocr_path)
    mod = importlib.util.module_from_spec(spec)
    assert spec and spec.loader
    spec.loader.exec_module(mod)  # type: ignore
    return mod


ocr = load_ocr_module()


class TestOCRExtraction(unittest.TestCase):
    def setUp(self):
        # Fixed now for deterministic ETA checks
        self.now = datetime(2024, 1, 2, 3, 4, tzinfo=timezone.utc)

    def test_hours_and_minutes(self):
        text = "山甲号 [Rank89] [帰還: 残り 1時間 49分]"
        etas = ocr.extract_submarine_etas(text, now=self.now)
        self.assertEqual(len(etas), 1)
        e = etas[0]
        self.assertEqual(e.name, "山甲号")
        self.assertEqual(e.remaining_minutes, 109)
        self.assertEqual(e.eta, self.now + timedelta(minutes=109))

    def test_minutes_only(self):
        text = "乙二号 [Rank90] [帰還: 残り 49分]"
        etas = ocr.extract_submarine_etas(text, now=self.now)
        self.assertEqual(len(etas), 1)
        e = etas[0]
        self.assertEqual(e.name, "乙二号")
        self.assertEqual(e.remaining_minutes, 49)

    def test_full_width_digits_and_colon(self):
        text = "丙参号 [Rank99] [帰還： 残り ３時間 ０５分]"
        etas = ocr.extract_submarine_etas(text, now=self.now)
        self.assertEqual(len(etas), 1)
        e = etas[0]
        self.assertEqual(e.name, "丙参号")
        self.assertEqual(e.remaining_minutes, 185)

    def test_keep_shortest_per_name(self):
        text = (
            "丁四号 [Rank80] [帰還: 残り 2時間 00分]\n"
            "丁四号 [Rank80] [帰還: 残り 1時間 30分]"
        )
        etas = ocr.extract_submarine_etas(text, now=self.now)
        self.assertEqual(len(etas), 1)
        e = etas[0]
        self.assertEqual(e.name, "丁四号")
        self.assertEqual(e.remaining_minutes, 90)

    def test_limit_to_four_entries(self):
        lines = [f"艦{i} [Rank80] [帰還: 残り {i}分]" for i in range(1, 7)]
        text = "\n".join(lines)
        etas = ocr.extract_submarine_etas(text, now=self.now)
        self.assertLessEqual(len(etas), 4)


if __name__ == "__main__":
    unittest.main()

