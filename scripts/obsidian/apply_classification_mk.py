#!/usr/bin/env python3
import csv
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MK = ROOT / 'notes' / 'MK'
TASKS = ROOT / 'TASKS'
PROPOSALS = TASKS / 'mk_classification_proposals.csv'


def safe_move(src: Path, dst_dir: Path) -> Path:
    dst_dir.mkdir(parents=True, exist_ok=True)
    base = src.stem
    ext = src.suffix
    cand = dst_dir / (base + ext)
    i = 2
    while cand.exists():
        cand = dst_dir / (f"{base}-{i}{ext}")
        i += 1
    src.rename(cand)
    return cand


def main():
    only = None
    limit = None
    dry = False
    # args: --only FOLDER --limit N --dry-run
    args = sys.argv[1:]
    i = 0
    while i < len(args):
        a = args[i]
        if a == '--only' and i + 1 < len(args):
            only = args[i + 1]
            i += 2
            continue
        if a == '--limit' and i + 1 < len(args):
            try:
                limit = int(args[i + 1])
            except Exception:
                limit = None
            i += 2
            continue
        if a in ('--dry-run','-n'):
            dry = True
            i += 1
            continue
        i += 1

    if not PROPOSALS.exists():
        print(f"Proposals not found: {PROPOSALS}")
        sys.exit(1)
    rows = list(csv.DictReader(PROPOSALS.open(encoding='utf-8')))
    targets = [r for r in rows if r.get('action') == 'move']
    if only:
        targets = [r for r in targets if r.get('proposed_folder') == only]
    if limit is not None:
        targets = targets[:limit]

    ts = time.strftime('%Y%m%d-%H%M%S')
    report = TASKS / f'mk_moves_applied_{only or "all"}_{ts}.csv'
    out_rows = []
    moved = 0
    skipped = 0
    for r in targets:
        rel = Path(r['file'])
        src = ROOT / rel
        if not src.exists():
            out_rows.append({'old': str(rel), 'new': '', 'status': 'missing', 'note': ''})
            skipped += 1
            continue
        # Skip already in destination folder
        dest_key = r['proposed_folder']
        dest_dir = MK / dest_key
        try:
            if dry:
                newp = dest_dir / src.name
                out_rows.append({'old': str(rel), 'new': str(newp.relative_to(ROOT)), 'status': 'dry-run', 'note': ''})
            else:
                newp = safe_move(src, dest_dir)
                out_rows.append({'old': str(rel), 'new': str(newp.relative_to(ROOT)), 'status': 'moved', 'note': ''})
                moved += 1
        except Exception as e:
            out_rows.append({'old': str(rel), 'new': '', 'status': 'error', 'note': str(e)})
            skipped += 1

    with report.open('w', newline='', encoding='utf-8') as fp:
        w = csv.DictWriter(fp, fieldnames=['old','new','status','note'])
        w.writeheader()
        w.writerows(out_rows)
    print(f"Applied moves: moved={moved}, skipped={skipped}, report={report}")


if __name__ == '__main__':
    main()
