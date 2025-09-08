#!/usr/bin/env python3
import csv
import os
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
NOTES = ROOT / 'notes'
MK = NOTES / 'MK'
ART = ROOT / 'TASKS' / 'artifacts'
OUT = ART / 'dir_inventory.csv'

CANON = {'00_inbox','10_research','20_notes','30_projects','40_prompts','50_code','60_attachments','70_templates','90_archive','.obsidian'}


def dir_stats(base: Path):
    rows = []
    for d in sorted([p for p in base.rglob('*') if p.is_dir()] + [base]):
        try:
            rel = d.relative_to(ROOT)
        except Exception:
            continue
        files = list(d.iterdir())
        total_files = 0
        md_files = 0
        total_size = 0
        for p in d.rglob('*'):
            if p.is_file():
                total_files += 1
                if p.suffix.lower() == '.md':
                    md_files += 1
                try:
                    total_size += p.stat().st_size
                except Exception:
                    pass
        # count references to this directory by path in md files
        link_refs = 0
        dir_token_fwd = str(rel).replace('\\','/') + '/'
        dir_token_bak = str(rel).replace('/','\\') + '\\'
        for p in ROOT.rglob('*.md'):
            try:
                t = p.read_text(encoding='utf-8', errors='ignore')
            except Exception:
                continue
            if dir_token_fwd in t or dir_token_bak in t:
                link_refs += 1
        top = rel.parts[1] if len(rel.parts) > 1 else rel.parts[0] if rel.parts else ''
        rows.append({
            'dir': str(rel),
            'top': top,
            'total_files': total_files,
            'md_files': md_files,
            'total_size': total_size,
            'link_refs': link_refs,
            'is_mk': 'yes' if str(rel).startswith('notes/MK') or rel == Path('notes/MK') else 'no',
            'is_canon_top': 'yes' if (d.parent == MK and d.name in CANON) else 'no'
        })
    return rows


def main():
    ART.mkdir(parents=True, exist_ok=True)
    rows = []
    # top-level under notes
    for d in sorted([p for p in NOTES.iterdir() if p.is_dir()]):
        rows.extend(dir_stats(d))
    # include notes root
    rows.append({'dir': 'notes', 'top': 'notes', 'total_files': sum(1 for _ in NOTES.rglob('*') if _.is_file()), 'md_files': sum(1 for _ in NOTES.rglob('*.md')), 'total_size': 0, 'link_refs': 0, 'is_mk': 'n/a', 'is_canon_top': 'n/a'})

    with OUT.open('w', newline='', encoding='utf-8') as fp:
        w = csv.DictWriter(fp, fieldnames=['dir','top','total_files','md_files','total_size','link_refs','is_mk','is_canon_top'])
        w.writeheader()
        w.writerows(rows)
    print(f"Wrote: {OUT} rows={len(rows)}")


if __name__ == '__main__':
    main()
