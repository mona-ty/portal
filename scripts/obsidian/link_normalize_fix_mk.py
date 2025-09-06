#!/usr/bin/env python3
import csv
import os
import re
import difflib
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MK = ROOT / 'notes' / 'MK'
TASKS = ROOT / 'tasks'
ART = TASKS / 'artifacts'
AUDIT1 = TASKS / 'mk_link_audit.csv'
AUDIT2 = ART / 'mk_link_audit.csv'
PLAN = ART / 'mk_link_normalize_plan.csv'
DIFF = ART / 'mk_link_normalize_dry.diff'

INVALID_CHARS = '<>:"|?*'
RESERVED_NAMES = {"CON","PRN","AUX","NUL","COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9","LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"}


def read_text_best_effort(p: Path) -> str:
    for enc in ('utf-8', 'utf-8-sig', 'cp932', 'shift_jis'):
        try:
            return p.read_text(encoding=enc)
        except Exception:
            continue
    return p.read_text(errors='ignore')


def write_text_utf8(p: Path, text: str):
    p.write_text(text, encoding='utf-8', newline='\n')


def sanitize_component(name: str) -> str:
    for ch in INVALID_CHARS:
        name = name.replace(ch, '-')
    name = name.replace('/', '-').replace('\\', '-')
    name = name.strip(' .')
    if not name:
        name = 'untitled'
    if name.upper() in RESERVED_NAMES:
        name = name + '_'
    return name


def ensure_md_filename(name: str) -> str:
    if not name.lower().endswith('.md'):
        name += '.md'
    return name


def relpath(from_path: Path, to_path: Path) -> str:
    try:
        return str(to_path.relative_to(from_path.parent)).replace('\\','/')
    except Exception:
        return os.path.relpath(str(to_path), str(from_path.parent)).replace('\\','/')


def replace_links_in_text(text: str, link_type: str, current: str, proposal: str) -> str:
    if 'wikilink' in (link_type or ''):
        pattern = re.compile(r"(!?\[\[)" + re.escape(current) + r"(?=(\]|\||#))")
        return pattern.sub(r"\1" + proposal, text)
    else:
        pattern = re.compile(r"(!?\[[^\]]*\]\()" + re.escape(current) + r"(\))")
        return pattern.sub(r"\1" + proposal + r"\2", text)


def create_md_stub(path: Path, title: str):
    path.parent.mkdir(parents=True, exist_ok=True)
    today = datetime.today().strftime('%Y-%m-%d')
    content = f"---\ntitle: \"{title}\"\nstatus: draft\ncreated: {today}\nupdated: {today}\n---\n\n# {title}\n\n> Stub note (auto-generated).\n"
    write_text_utf8(path, content)


def create_placeholder(path: Path):
    path.parent.mkdir(parents=True, exist_ok=True)
    try:
        path.write_bytes(b"")
    except Exception:
        with open(path, 'wb') as f:
            pass


def classify_and_propose(src_note: Path, link_type: str, current: str):
    cur = current.strip()
    # Empty/invalid
    if not cur or cur == '...':
        new = 'untitled.md' if 'wikilink' in (link_type or '') else 'untitled.md'
        target = src_note.parent / new
        return 'note', new, target
    # pipe-separated extensions
    if '|' in cur and re.search(r"\.(png|jpg|jpeg|gif|webp)(\||$)", cur, flags=re.IGNORECASE):
        new = relpath(src_note, MK / '60_attachments' / 'placeholder.png')
        target = MK / '60_attachments' / 'placeholder.png'
        return 'asset', new, target
    # explicit asset extensions
    if re.search(r"\.(png|jpg|jpeg|gif|webp|pdf|svg|bmp)$", cur, flags=re.IGNORECASE):
        fname = sanitize_component(Path(cur).name)
        new = relpath(src_note, MK / '60_attachments' / fname)
        target = MK / '60_attachments' / fname
        return 'asset', new, target
    # default: treat as note (may contain subfolders)
    parts = re.split(r"[\\/]+", cur)
    parts = [sanitize_component(p) for p in parts if p]
    if not parts:
        parts = ['untitled']
    parts[-1] = ensure_md_filename(parts[-1])
    target = src_note.parent
    for seg in parts:
        target = target / seg
    new = relpath(src_note, target)
    return 'note', new, target


def main():
    apply = False
    import sys
    if '--apply' in sys.argv:
        apply = True
    ART.mkdir(parents=True, exist_ok=True)
    audit_path = AUDIT1 if AUDIT1.exists() else AUDIT2
    if not audit_path.exists():
        print(f"Audit not found: {AUDIT1} or {AUDIT2}")
        return
    rows = list(csv.DictReader(audit_path.open(encoding='utf-8')))
    targets = [r for r in rows if r.get('status') == 'not_found']
    plan_rows = []
    diffs = []
    changed_files = 0
    creates = 0
    for r in targets:
        rel = r['file']
        link_type = r.get('link_type','')
        current = r.get('current','')
        src = ROOT / rel
        if not src.exists():
            continue
        kind, proposal, target = classify_and_propose(src, link_type, current)
        old = read_text_best_effort(src)
        new_text = replace_links_in_text(old, link_type, current, proposal)
        if new_text != old:
            changed_files += 1
            diff = difflib.unified_diff(old.splitlines(True), new_text.splitlines(True), fromfile=f"a/{rel}", tofile=f"b/{rel}")
            diffs.extend(list(diff))
            if apply:
                write_text_utf8(src, new_text)
        # plan/create targets
        if kind == 'note':
            # create markdown stub if missing
            if not target.exists():
                plan_rows.append({'file': rel, 'action': 'create_note', 'target': str(target.relative_to(ROOT)).replace('\\','/'), 'link_replaced_to': proposal})
                if apply:
                    create_md_stub(target, target.stem)
                    creates += 1
        else:
            # asset placeholder
            if not target.exists():
                plan_rows.append({'file': rel, 'action': 'create_asset', 'target': str(target.relative_to(ROOT)).replace('\\','/'), 'link_replaced_to': proposal})
                if apply:
                    create_placeholder(target)
                    creates += 1

    with PLAN.open('w', newline='', encoding='utf-8') as fp:
        w = csv.DictWriter(fp, fieldnames=['file','action','target','link_replaced_to'])
        w.writeheader()
        w.writerows(plan_rows)
    with DIFF.open('w', encoding='utf-8', newline='\n') as fp:
        for line in diffs:
            fp.write(line)
            if not line.endswith('\n'):
                fp.write('\n')
    mode = 'APPLIED' if apply else 'dry-run'
    print(f"Link normalize {mode}: files_changed={changed_files}, created={creates}, plan={PLAN}, diff={DIFF}")


if __name__ == '__main__':
    main()

