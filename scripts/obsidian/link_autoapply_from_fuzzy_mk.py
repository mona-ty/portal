#!/usr/bin/env python3
import csv
import re
import difflib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MK = ROOT / 'notes' / 'MK'
TASKS = ROOT / 'TASKS'
ART = TASKS / 'artifacts'
FUZZY = ART / 'mk_link_fuzzy_proposals.csv'
FUZZY_ALT = TASKS / 'artifacts' / 'mk_link_fuzzy_proposals.csv'
DIFF = ART / 'mk_link_autoapply_dry.diff'
PLAN = ART / 'mk_link_autoapply_plan.csv'


def read_text_best_effort(p: Path) -> str:
    for enc in ('utf-8', 'utf-8-sig', 'cp932', 'shift_jis'):
        try:
            return p.read_text(encoding=enc)
        except Exception:
            continue
    return p.read_text(errors='ignore')


def parse_suggestions(s: str):
    # format: relpath|stem|score; relpath|stem|score; ...
    out = []
    if not s:
        return out
    parts = [p.strip() for p in s.split(';') if p.strip()]
    for part in parts:
        try:
            rel, stem, score = part.split('|', 2)
            out.append((rel, stem, float(score)))
        except Exception:
            continue
    return out


def replace_links(text: str, current: str, proposal: str, link_type: str):
    changed = False
    new = text
    if link_type and 'wikilink' in link_type:
        pattern = re.compile(r"(!?\[\[)" + re.escape(current) + r"(?=(\]|\||#))")
        new2 = pattern.sub(r"\1" + proposal, new)
    else:
        pattern = re.compile(r"(!?\[[^\]]*\]\()" + re.escape(current) + r"(\))")
        new2 = pattern.sub(r"\1" + proposal + r"\2", new)
    if new2 != new:
        changed = True
        new = new2
    return changed, new


def main():
    th = 0.85
    margin = 0.10
    apply = False
    # optional args: --threshold 0.85, --margin 0.10, --apply
    import sys
    args = sys.argv[1:]
    i = 0
    while i < len(args):
        a = args[i]
        if a == '--threshold' and i + 1 < len(args):
            try:
                th = float(args[i + 1])
            except Exception:
                pass
            i += 2
            continue
        if a == '--margin' and i + 1 < len(args):
            try:
                margin = float(args[i + 1])
            except Exception:
                pass
            i += 2
            continue
        if a == '--apply':
            apply = True
            i += 1
            continue
        i += 1

    ART.mkdir(parents=True, exist_ok=True)
    fuzzy_path = FUZZY_ALT if FUZZY_ALT.exists() else FUZZY
    if not fuzzy_path.exists():
        print(f"Fuzzy proposals not found: {FUZZY} or {FUZZY_ALT}")
        return
    rows = list(csv.DictReader(fuzzy_path.open(encoding='utf-8')))
    plan_rows = []
    diffs = []
    changed_files = 0
    for r in rows:
        file_rel = r['file']
        link_type = r['link_type']
        current = r['current']
        sugg = parse_suggestions(r['suggestions'])
        if not sugg:
            continue
        qualified = [(rel, stem, score) for (rel, stem, score) in sugg if score >= th]
        if not qualified:
            continue
        # sort by score DESC
        qualified.sort(key=lambda x: -x[2])
        # margin rule: top1 - top2 >= margin (or only one candidate)
        if len(qualified) > 1 and (qualified[0][2] - qualified[1][2] < margin):
            continue
        rel_path, stem, score = qualified[0]
        # proposal should keep anchors if present in current link (handled by replacement patterns)
        proposal = rel_path
        p = ROOT / file_rel
        if not p.exists():
            continue
        old = read_text_best_effort(p)
        ok, new = replace_links(old, current, proposal, link_type)
        if not ok:
            continue
        changed_files += 1
        rel_unix = file_rel.replace('\\', '/')
        plan_rows.append({
            'file': rel_unix,
            'link_type': link_type,
            'current': current,
            'proposal': proposal,
            'score': f"{score:.2f}"
        })
        diff = difflib.unified_diff(old.splitlines(True), new.splitlines(True), fromfile=f"a/{rel_unix}", tofile=f"b/{rel_unix}")
        diffs.extend(list(diff))
        if apply:
            p.write_text(new, encoding='utf-8', newline='\n')

    with DIFF.open('w', encoding='utf-8', newline='\n') as fp:
        for line in diffs:
            fp.write(line)
            if not line.endswith('\n'):
                fp.write('\n')
    with PLAN.open('w', newline='', encoding='utf-8') as fp:
        w = csv.DictWriter(fp, fieldnames=['file','link_type','current','proposal','score'])
        w.writeheader()
        w.writerows(plan_rows)
    mode = 'APPLIED' if apply else 'dry-run'
    print(f"Link auto-apply {mode}: files={changed_files}, diff={DIFF}, plan={PLAN}, threshold={th}, margin={margin}")


if __name__ == '__main__':
    main()
