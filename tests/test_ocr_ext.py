import unittest
from datetime import datetime, timezone

from tests.test_ocr import load_ocr_module  # reuse dynamic loader


ocr = load_ocr_module()


class TestOCRExtendedPatterns(unittest.TestCase):
    def setUp(self):
        self.now = datetime(2024, 1, 2, 3, 4, tzinfo=timezone.utc)

    def test_colon_format(self):
        text = "艦A [Rank80] [残り 1:05]"
        etas = ocr.extract_submarine_etas(text, now=self.now)
        self.assertEqual(len(etas), 1)
        self.assertEqual(etas[0].remaining_minutes, 65)

    def test_fullwidth_brackets_and_digits(self):
        text = "艦B ［Rank88］ ［残り ２時間 ０分］"
        etas = ocr.extract_submarine_etas(text, now=self.now)
        self.assertEqual(len(etas), 1)
        self.assertEqual(etas[0].remaining_minutes, 120)

    def test_ignore_rank_number(self):
        text = "艦C [Rank90] [残り 49分]"
        etas = ocr.extract_submarine_etas(text, now=self.now)
        self.assertEqual(len(etas), 1)
        self.assertEqual(etas[0].remaining_minutes, 49)


if __name__ == "__main__":
    unittest.main()

