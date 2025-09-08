#!/usr/bin/env python3
import csv
import difflib
import re
import unicodedata
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MK = ROOT / 'notes' / 'MK'
ART = ROOT / 'TASKS' / 'artifacts'
PLAN_IN = ART / 'mk_retag_supplement_dry_run.csv'
PLAN_OUT = ART / 'mk_retag_supplement_apply_plan.csv'
DIFF = ART / 'mk_retag_supplement_apply_dry.diff'


def read_text_best_effort(p: Path) -> str:
    for enc in ('utf-8', 'utf-8-sig', 'cp932', 'shift_jis'):
        try:
            return p.read_text(encoding=enc)
        except Exception:
            continue
    return p.read_text(errors='ignore')


def write_text_utf8(p: Path, text: str):
    p.write_text(text, encoding='utf-8', newline='\n')


def extract_yaml(text: str):
    if not text.startswith('---'):
        return None, text
    lines = text.splitlines()
    end = None
    for i in range(1, min(300, len(lines))):
        if lines[i].strip() == '---':
            end = i
            break
    if end is None:
        return None, text
    yaml_block = "\n".join(lines[1:end])
    body = "\n".join(lines[end+1:])
    return yaml_block, body


def parse_tags_from_yaml(yaml_block: str):
    if not yaml_block:
        return []
    tags = []
    for line in yaml_block.splitlines():
        m = re.match(r"^(tags?|keywords)\s*:\s*(.*)$", line.strip(), re.IGNORECASE)
        if not m:
            continue
        rest = m.group(2).strip()
        if rest.startswith('[') and rest.endswith(']'):
            inner = rest[1:-1]
            parts = [p.strip() for p in inner.split(',') if p.strip()]
            tags.extend(parts)
        elif rest:
            parts = [p.strip() for p in rest.split(',') if p.strip()]
            tags.extend(parts)
    out = []
    seen = set()
    for t in tags:
        nt = t.strip()
        if nt and nt not in seen:
            out.append(nt)
            seen.add(nt)
    return out


def update_yaml_with_tags(yaml_block: str, new_tags):
    lines = yaml_block.splitlines() if yaml_block else []
    out_lines = []
    skip_mode = False
    replaced = False
    for i, line in enumerate(lines):
        if not skip_mode:
            m = re.match(r"^(\s*)(tags?|keywords)\s*:\s*(.*)$", line, re.IGNORECASE)
            if m:
                indent = m.group(1)
                out_lines.append(f"{indent}tags: [{', '.join(new_tags)}]")
                skip_mode = True
                replaced = True
                continue
            else:
                out_lines.append(line)
        else:
            if re.match(r"^\s*-\s*", line):
                continue
            if re.match(r"^\s*[A-Za-z0-9_-]+\s*:\s*", line) or line.strip() == '':
                skip_mode = False
                out_lines.append(line)
            else:
                skip_mode = False
                out_lines.append(line)
    if not replaced:
        out_lines.append(f"tags: [{', '.join(new_tags)}]")
    return "\n".join(out_lines)


def apply_supplement(p: Path, additions):
    text = read_text_best_effort(p)
    yaml_block, body = extract_yaml(text)
    current = parse_tags_from_yaml(yaml_block or '') if yaml_block is not None else []
    # merge and cap 5
    merged = []
    seen = set()
    for t in current + additions:
        if t and t not in seen:
            merged.append(t)
            seen.add(t)
        if len(merged) >= 5:
            break
    if merged == current:
        return None, current, merged
    new_yaml = update_yaml_with_tags(yaml_block or '', merged)
    new_text = f"---\n{new_yaml}\n---\n{body}" if yaml_block is not None else f"---\n{new_yaml}\n---\n{text}"
    return new_text, current, merged


def main():
    apply = False
    import sys
    if '--apply' in sys.argv:
        apply = True
    if not PLAN_IN.exists():
        print(f"Supplement plan not found: {PLAN_IN}")
        return
    rows = list(csv.DictReader(PLAN_IN.open(encoding='utf-8')))
    plan_rows = []
    diffs = []
    changed = 0
    for r in rows:
        additions = [t for t in (r.get('proposed_additions','').split()) if t]
        if not additions:
            continue
        p = ROOT / r['file']
        if not p.exists():
            continue
        new_text, before, after = apply_supplement(p, additions)
        if new_text is None:
            continue
        changed += 1
        rel = r['file'].replace('\\','/')
        plan_rows.append({'file': rel, 'before': ' '.join(before), 'added': ' '.join([t for t in after if t not in before]), 'after': ' '.join(after)})
        diff = difflib.unified_diff(read_text_best_effort(p).splitlines(True), new_text.splitlines(True), fromfile=f"a/{rel}", tofile=f"b/{rel}")
        diffs.extend(list(diff))
        if apply:
            write_text_utf8(p, new_text)

    with PLAN_OUT.open('w', newline='', encoding='utf-8') as fp:
        w = csv.DictWriter(fp, fieldnames=['file','before','added','after'])
        w.writeheader()
        w.writerows(plan_rows)
    with DIFF.open('w', encoding='utf-8', newline='\n') as fp:
        for line in diffs:
            fp.write(line)
            if not line.endswith('\n'):
                fp.write('\n')
    mode = 'APPLIED' if apply else 'dry-run'
    print(f"Retag supplement {mode}: files_changed={changed}, plan={PLAN_OUT}, diff={DIFF}")


if __name__ == '__main__':
    main()
