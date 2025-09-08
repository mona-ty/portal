#!/usr/bin/env python3
import csv
import os
import re
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MK = ROOT / 'notes' / 'MK'
TASKS = ROOT / 'TASKS'
ART = TASKS / 'artifacts'
AUDIT = TASKS / 'mk_link_audit.csv'
AUDIT_ALT = ART / 'mk_link_audit.csv'
PLAN = ART / 'mk_stub_create_plan.csv'


def write_stub(p: Path, title: str):
    p.parent.mkdir(parents=True, exist_ok=True)
    today = datetime.today().strftime('%Y-%m-%d')
    content = f"---\ntitle: \"{title}\"\nstatus: draft\ncreated: {today}\nupdated: {today}\n---\n\n# {title}\n\n> Stub note (auto-generated to resolve a link).\n"
    p.write_text(content, encoding='utf-8', newline='\n')


INVALID_CHARS = '<>:"|?*'
RESERVED_NAMES = {"CON","PRN","AUX","NUL","COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9","LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"}


def sanitize_component(name: str) -> str:
    # replace invalid characters
    for ch in INVALID_CHARS:
        name = name.replace(ch, '-')
    name = name.replace('/', '-').replace('\\', '-')
    name = name.strip(' .')
    if not name:
        name = 'untitled'
    if name.upper() in RESERVED_NAMES:
        name = name + '_'
    return name


def resolve_target(src_note: Path, current: str, link_type: str):
    # Only create .md stubs
    if re.match(r"^[a-z]+://", current, flags=re.IGNORECASE):
        return None
    base = current
    if 'wikilink' in (link_type or ''):
        base = current.split('|', 1)[0].split('#', 1)[0]
        # split possible subfolders and sanitize each
        parts = re.split(r"[\\/]+", base)
        parts = [sanitize_component(p) for p in parts if p]
        if not parts:
            parts = ['untitled']
        if not parts[-1].lower().endswith('.md'):
            parts[-1] = parts[-1] + '.md'
        target = src_note.parent
        for seg in parts:
            target = target / seg
    else:
        # markdown link
        base = current.split('#', 1)[0]
        parts = re.split(r"[\\/]+", base)
        parts = [sanitize_component(p) for p in parts if p]
        if not parts:
            parts = ['untitled']
        if not parts[-1].lower().endswith('.md'):
            parts[-1] = parts[-1] + '.md'
        target = src_note.parent
        for seg in parts:
            target = target / seg
    return target


def title_from_path(p: Path) -> str:
    return p.stem


def main():
    apply = False
    import sys
    if '--apply' in sys.argv:
        apply = True

    audit_path = AUDIT if AUDIT.exists() else AUDIT_ALT
    if not audit_path.exists():
        print(f"Audit not found: {AUDIT} or {AUDIT_ALT}")
        return

    rows = list(csv.DictReader(audit_path.open(encoding='utf-8')))
    plan_rows = []
    created = 0
    for r in rows:
        if r.get('status') != 'not_found':
            continue
        rel = r['file']
        link_type = r.get('link_type','')
        current = r.get('current','')
        src_note = ROOT / rel
        if not src_note.exists():
            continue
        tgt = resolve_target(src_note, current, link_type)
        if not tgt:
            continue
        if tgt.exists():
            continue
        plan_rows.append({'note': str(src_note.relative_to(ROOT)).replace('\\','/'), 'create': str(tgt.relative_to(ROOT)).replace('\\','/'), 'link': current})
        if apply:
            write_stub(tgt, title_from_path(tgt))
            created += 1

    with PLAN.open('w', newline='', encoding='utf-8') as fp:
        w = csv.DictWriter(fp, fieldnames=['note','create','link'])
        w.writeheader()
        w.writerows(plan_rows)
    mode = 'APPLIED' if apply else 'dry-run'
    print(f"Stub notes {mode}: planned={len(plan_rows)}, created={created}, plan={PLAN}")


if __name__ == '__main__':
    main()
