#!/usr/bin/env python3
import re
import unicodedata
from pathlib import Path
import csv

ROOT = Path(__file__).resolve().parents[1]
NOTES_DIR = ROOT / 'notes'
REPORTS_DIR = ROOT / 'reports'
REPORT_CSV = REPORTS_DIR / 'notes_rename_proposals.csv'


def read_text_best_effort(p: Path) -> str:
    for enc in ('utf-8', 'utf-8-sig', 'cp932', 'shift_jis'):
        try:
            return p.read_text(encoding=enc)
        except Exception:
            continue
    return p.read_text(errors='ignore')


def first_heading(text: str):
    for line in text.splitlines():
        if line.strip().startswith('#'):
            # get content after leading #'s
            title = re.sub(r"^#+\s*", "", line.strip())
            if title:
                return title
    return None


def slugify(s: str) -> str:
    # Keep Japanese and common CJK; replace whitespace with hyphen; remove unsafe chars
    s = s.strip()
    s = unicodedata.normalize('NFC', s)
    s = s.replace('\u3000', ' ')  # ideographic space
    s = re.sub(r"\s+", "-", s)
    # remove characters that are problematic on Windows file systems
    s = re.sub(r"[\\/\?%\*:|\"<>]", "", s)
    # collapse consecutive hyphens
    s = re.sub(r"-+", "-", s)
    return s.strip('-')


def detect_mojibake(name: str) -> bool:
    # Heuristic: presence of replacement char or long runs of symbols
    return '\ufffd' in name or '�' in name


def main():
    REPORTS_DIR.mkdir(parents=True, exist_ok=True)
    files = sorted(NOTES_DIR.rglob('*.md'))
    rows = []
    for f in files:
        rel = f.relative_to(ROOT)
        text = read_text_best_effort(f)
        title = first_heading(text)
        base = f.stem
        reason_parts = []
        # prefer H1 title if present; else stem
        basis = title if title else base
        if title:
            reason_parts.append('use-h1-title')
        else:
            reason_parts.append('use-file-stem')
        if detect_mojibake(f.name):
            reason_parts.append('mojibake-suspected')
        # normalize unicode and unsafe chars
        suggested_stem = slugify(basis)
        if not suggested_stem:
            suggested_stem = slugify(base)
            reason_parts.append('fallback-stem')
        suggested_name = suggested_stem + f.suffix
        same = (suggested_name == f.name)
        rows.append({
            'file': str(rel),
            'title_or_stem': basis,
            'suggested_name': suggested_name,
            'same_as_current': 'yes' if same else 'no',
            'reason': ','.join(reason_parts)
        })
    with open(REPORT_CSV, 'w', newline='', encoding='utf-8') as fp:
        writer = csv.DictWriter(fp, fieldnames=['file','title_or_stem','suggested_name','same_as_current','reason'])
        writer.writeheader()
        writer.writerows(rows)
    print(f"Rename proposals written: {REPORT_CSV} ({len(rows)} files)")


if __name__ == '__main__':
    main()

