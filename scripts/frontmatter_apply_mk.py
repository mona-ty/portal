#!/usr/bin/env python3
import difflib
import re
from datetime import datetime
from pathlib import Path
import csv

ROOT = Path(__file__).resolve().parents[1]
MK = ROOT / 'notes' / 'MK'
TASKS = ROOT / 'tasks'
AUDIT_CSV = TASKS / 'mk_frontmatter_dry_run.csv'
DIFF_PATH = TASKS / 'mk_frontmatter_apply_dry.diff'
PLAN_CSV = TASKS / 'mk_frontmatter_apply_plan.csv'


def read_text_best_effort(p: Path) -> str:
    for enc in ('utf-8', 'utf-8-sig', 'cp932', 'shift_jis'):
        try:
            return p.read_text(encoding=enc)
        except Exception:
            continue
    return p.read_text(errors='ignore')


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


def get_h1_title(text: str):
    for line in text.splitlines():
        if line.strip().startswith('#'):
            t = re.sub(r"^#+\s*", "", line.strip())
            return t.strip()
    return ''


def parse_scalar(yaml: str, key: str):
    pat = re.compile(rf"(?im)^\s*{re.escape(key)}\s*:\s*(.+?)\s*$")
    m = pat.search(yaml)
    if m:
        val = m.group(1).strip()
        if (val.startswith('"') and val.endswith('"')) or (val.startswith("'") and val.endswith("'")):
            val = val[1:-1]
        return val
    return ''


def parse_list(yaml: str, key: str):
    m = re.search(rf"(?im)^\s*{re.escape(key)}\s*:\s*(\[.*?\])\s*$", yaml)
    if m:
        inner = m.group(1).strip().strip('[]')
        return [x.strip().strip('"\'') for x in inner.split(',') if x.strip()]
    lines = yaml.splitlines()
    out = []
    start = None
    for i, line in enumerate(lines):
        if re.match(rf"(?i)^\s*{re.escape(key)}\s*:\s*$", line):
            start = i + 1
            break
    if start is not None:
        for j in range(start, len(lines)):
            l = lines[j]
            if re.match(r"^\s*-\s+", l):
                out.append(re.sub(r"^\s*-\s+", "", l).strip())
            else:
                if l.strip() == '':
                    continue
                break
    return out


def file_times(p: Path):
    try:
        stat = p.stat()
        created = datetime.fromtimestamp(getattr(stat, 'st_ctime', stat.st_mtime)).strftime('%Y-%m-%d')
        updated = datetime.fromtimestamp(stat.st_mtime).strftime('%Y-%m-%d')
        return created, updated
    except Exception:
        today = datetime.today().strftime('%Y-%m-%d')
        return today, today


def build_new_text(p: Path, text: str):
    yaml_block, body = extract_yaml(text)
    created_fs, updated_fs = file_times(p)
    fields_to_add = []
    title_current = parse_scalar(yaml_block or '', 'title') if yaml_block is not None else ''
    tags_list = parse_list(yaml_block or '', 'tags') if yaml_block is not None else []
    status_current = parse_scalar(yaml_block or '', 'status') if yaml_block is not None else ''
    created_current = parse_scalar(yaml_block or '', 'created') if yaml_block is not None else ''
    updated_current = parse_scalar(yaml_block or '', 'updated') if yaml_block is not None else ''

    h1 = get_h1_title(body if yaml_block is not None else text)

    if not title_current:
        fields_to_add.append(('title', h1 or p.stem))
    if not tags_list:
        fields_to_add.append(('tags', []))
    if not status_current:
        fields_to_add.append(('status', 'draft'))
    if not created_current:
        fields_to_add.append(('created', created_fs))
    if not updated_current:
        fields_to_add.append(('updated', updated_fs))

    if not fields_to_add:
        return None, []  # no change

    if yaml_block is None:
        # create new front matter with only missing fields (effectively minimal set)
        lines = ["---"]
        for k, v in fields_to_add:
            if k == 'tags':
                lines.append("tags: []")
            else:
                lines.append(f"{k}: {v}")
        lines.append("---")
        new_text = "\n".join(lines) + "\n" + text
        return new_text, fields_to_add
    else:
        # append missing fields at end of yaml block
        ylines = yaml_block.splitlines()
        for k, v in fields_to_add:
            if k == 'tags':
                ylines.append("tags: []")
            else:
                ylines.append(f"{k}: {v}")
        new_yaml = "\n".join(ylines)
        new_text = f"---\n{new_yaml}\n---\n{body}"
        return new_text, fields_to_add


def main():
    apply = False
    import sys
    if '--apply' in sys.argv:
        apply = True
    TASKS.mkdir(parents=True, exist_ok=True)
    plan_rows = []
    diffs = []
    count = 0
    for p in sorted(MK.rglob('*.md')):
        low = str(p).lower()
        if any(seg in low for seg in ['70_templates', '60_attachments', '90_archive', '.obsidian\\', '/.obsidian/']):
            continue
        old = read_text_best_effort(p)
        new, added = build_new_text(p, old)
        if new is None:
            continue
        count += 1
        rel = str(p.relative_to(ROOT)).replace('\\', '/')
        plan_rows.append({'file': rel, 'added_fields': ' '.join([k for k, _ in added])})
        diff = difflib.unified_diff(
            old.splitlines(True), new.splitlines(True),
            fromfile=f"a/{rel}", tofile=f"b/{rel}", lineterm=""
        )
        diffs.extend(list(diff))
        if apply:
            p.write_text(new, encoding='utf-8', newline='\n')

    with DIFF_PATH.open('w', encoding='utf-8', newline='\n') as fp:
        for line in diffs:
            fp.write(line)
            fp.write('\n')
    with PLAN_CSV.open('w', newline='', encoding='utf-8') as fp:
        w = csv.DictWriter(fp, fieldnames=['file','added_fields'])
        w.writeheader()
        w.writerows(plan_rows)
    mode = 'APPLIED' if apply else 'dry-run'
    print(f"Frontmatter apply {mode}: files={count}, diff={DIFF_PATH}, plan={PLAN_CSV}")


if __name__ == '__main__':
    main()
