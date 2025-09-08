#!/usr/bin/env python3
import csv
import os
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MK = ROOT / 'notes' / 'MK'
TASKS = ROOT / 'TASKS' / 'artifacts'
OUT = TASKS / 'mk_attachments_dry_run.csv'


def read_text_best_effort(p: Path) -> str:
    for enc in ('utf-8', 'utf-8-sig', 'cp932', 'shift_jis'):
        try:
            return p.read_text(encoding=enc)
        except Exception:
            continue
    return p.read_text(errors='ignore')


def relpath(from_path: Path, to_path: Path) -> str:
    try:
        return str(to_path.relative_to(from_path.parent)).replace('\\','/')
    except Exception:
        return os.path.relpath(str(to_path), str(from_path.parent)).replace('\\','/')


def is_local_asset(url: str) -> bool:
    if re.match(r"^[a-z]+://", url, flags=re.IGNORECASE):
        return False
    if url.startswith('data:'):
        return False
    return True


def main():
    TASKS.mkdir(parents=True, exist_ok=True)
    rows = []
    for f in sorted(MK.rglob('*.md')):
        low = str(f).lower()
        if any(seg in low for seg in ['70_templates', '90_archive', '.obsidian\\', '/.obsidian/']):
            continue
        text = read_text_best_effort(f)
        # Find markdown links and wiki embeds to files
        # Markdown images/files: ![alt](path) or [text](path)
        for m in re.finditer(r"!??\[[^\]]*\]\(([^)\s]+)\)", text):
            url = m.group(1)
            if not is_local_asset(url):
                continue
            path_part = url.split('#', 1)[0]
            path_part = path_part.replace('%20', ' ')
            target_path = (f.parent / path_part)
            if not target_path.exists():
                continue
            # skip if already under 60_attachments
            try:
                target_path.relative_to(MK / '60_attachments')
                already = True
            except Exception:
                already = False
            if already:
                continue
            new_path = MK / '60_attachments' / target_path.name
            rows.append({
                'note': str(f.relative_to(ROOT)),
                'current_link': url,
                'attachment_src': str(target_path),
                'proposed_new_path': str(new_path),
                'updated_link_to': relpath(f, new_path)
            })

        # Wiki embeds: ![[path]] or [[path]] to non-md assets
        for m in re.finditer(r"!??\[\[([^\]]+)\]\]", text):
            inner = m.group(1)
            base = inner.split('|',1)[0].split('#',1)[0]
            if base.lower().endswith('.md'):
                continue
            if not is_local_asset(base):
                continue
            target_path = (f.parent / base)
            if not target_path.exists():
                continue
            try:
                target_path.relative_to(MK / '60_attachments')
                already = True
            except Exception:
                already = False
            if already:
                continue
            new_path = MK / '60_attachments' / target_path.name
            rows.append({
                'note': str(f.relative_to(ROOT)),
                'current_link': inner,
                'attachment_src': str(target_path),
                'proposed_new_path': str(new_path),
                'updated_link_to': relpath(f, new_path)
            })

    with OUT.open('w', newline='', encoding='utf-8') as fp:
        w = csv.DictWriter(fp, fieldnames=['note','current_link','attachment_src','proposed_new_path','updated_link_to'])
        w.writeheader()
        w.writerows(rows)
    print(f"Attachments dry-run written: {OUT} ({len(rows)} moves)")


if __name__ == '__main__':
    main()
