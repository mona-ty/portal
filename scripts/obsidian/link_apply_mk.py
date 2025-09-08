#!/usr/bin/env python3
import difflib
import re
from pathlib import Path
import csv

ROOT = Path(__file__).resolve().parents[1]
MK = ROOT / 'notes' / 'MK'
TASKS = ROOT / 'TASKS'
AUDIT = TASKS / 'mk_link_audit.csv'
DIFF_PATH = TASKS / 'mk_link_apply_dry.diff'
PLAN_CSV = TASKS / 'mk_link_apply_plan.csv'


def read_text_best_effort(p: Path) -> str:
    for enc in ('utf-8', 'utf-8-sig', 'cp932', 'shift_jis'):
        try:
            return p.read_text(encoding=enc)
        except Exception:
            continue
    return p.read_text(errors='ignore')


def build_replacements(rows):
    per_file = {}
    for r in rows:
        if r.get('status') != 'broken':
            continue
        cur = r.get('current', '')
        prop = r.get('proposal', '')
        if not cur or not prop:
            continue
        file = r['file']
        per_file.setdefault(file, []).append((cur, prop, r.get('link_type')))
    return per_file


def replace_links(text: str, replacements):
    changed = False
    new = text
    for cur, prop, ltype in replacements:
        if ltype and 'wikilink' in ltype:
            # Replace inside [[...]] before optional '|' or '#' anchor
            pattern = re.compile(r"(!?\[\[)" + re.escape(cur) + r"(?=(\]|\||#))")
            new2 = pattern.sub(r"\1" + prop, new)
        else:
            # Markdown link target in (...)
            pattern = re.compile(r"(!?\[[^\]]*\]\()" + re.escape(cur) + r"(\))")
            new2 = pattern.sub(r"\1" + prop + r"\2", new)
        if new2 != new:
            changed = True
            new = new2
    return changed, new


def main():
    if not AUDIT.exists():
        print(f"Audit not found: {AUDIT}")
        return
    rows = list(csv.DictReader(AUDIT.open(encoding='utf-8')))
    mapping = build_replacements(rows)
    plan_rows = []
    diffs = []
    changed_files = 0
    for rel, reps in mapping.items():
        p = ROOT / rel
        if not p.exists():
            continue
        old = read_text_best_effort(p)
        ok, new = replace_links(old, reps)
        if not ok:
            continue
        changed_files += 1
        rel_unix = rel.replace('\\', '/')
        plan_rows.append({'file': rel_unix, 'replacements': '; '.join([f"{a} -> {b}" for a, b, _ in reps])})
        diff = difflib.unified_diff(old.splitlines(True), new.splitlines(True), fromfile=f"a/{rel_unix}", tofile=f"b/{rel_unix}")
        diffs.extend(list(diff))

    with DIFF_PATH.open('w', encoding='utf-8', newline='\n') as fp:
        for line in diffs:
            fp.write(line)
            if not line.endswith('\n'):
                fp.write('\n')
    with PLAN_CSV.open('w', newline='', encoding='utf-8') as fp:
        w = csv.DictWriter(fp, fieldnames=['file','replacements'])
        w.writeheader()
        w.writerows(plan_rows)
    print(f"Link apply dry-run: files={changed_files}, diff={DIFF_PATH}, plan={PLAN_CSV}")


if __name__ == '__main__':
    main()
