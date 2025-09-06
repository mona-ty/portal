#!/usr/bin/env python3
import csv
import re
import os
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MK = ROOT / 'notes' / 'MK'
TASKS = ROOT / 'TASKS'
OUT = TASKS / 'mk_frontmatter_dry_run.csv'


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
        # strip quotes
        if (val.startswith('"') and val.endswith('"')) or (val.startswith("'") and val.endswith("'")):
            val = val[1:-1]
        return val
    return ''


def parse_list(yaml: str, key: str):
    # support inline [a, b] or block list under key:
    m = re.search(rf"(?im)^\s*{re.escape(key)}\s*:\s*(\[.*?\])\s*$", yaml)
    if m:
        inner = m.group(1).strip()
        inner = inner.strip('[]')
        return [x.strip().strip('"\'') for x in inner.split(',') if x.strip()]
    # block list
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


def main():
    TASKS.mkdir(parents=True, exist_ok=True)
    rows = []
    for f in sorted(MK.rglob('*.md')):
        low = str(f).lower()
        if any(seg in low for seg in ['70_templates', '60_attachments', '90_archive', '.obsidian\\', '/.obsidian/']):
            continue
        text = read_text_best_effort(f)
        yaml_block, body = extract_yaml(text)
        has_yaml = 'yes' if yaml_block is not None else 'no'
        h1 = get_h1_title(body if yaml_block else text)
        title = parse_scalar(yaml_block or '', 'title')
        tags_list = parse_list(yaml_block or '', 'tags')
        status = parse_scalar(yaml_block or '', 'status')
        created = parse_scalar(yaml_block or '', 'created')
        updated = parse_scalar(yaml_block or '', 'updated')
        aliases_list = parse_list(yaml_block or '', 'aliases')
        source_list = parse_list(yaml_block or '', 'source')

        missing = []
        proposed = {}

        if not title:
            missing.append('title')
            # prefer H1, else filename stem
            proposed['title'] = h1 or f.stem
        if not tags_list:
            missing.append('tags')
            proposed['tags'] = []
        if not status:
            missing.append('status')
            proposed['status'] = 'draft'
        if not created or not re.match(r"^\d{4}-\d{2}-\d{2}$", created):
            missing.append('created')
            proposed['created'] = file_times(f)[0]
        if not updated or not re.match(r"^\d{4}-\d{2}-\d{2}$", updated):
            missing.append('updated')
            proposed['updated'] = file_times(f)[1]

        issues = []
        if len(tags_list) > 5:
            issues.append(f"tags_over_limit:{len(tags_list)}")
        # basic checks
        if yaml_block is None:
            issues.append('no_yaml')

        rows.append({
            'file': str(f.relative_to(ROOT)),
            'has_yaml': has_yaml,
            'missing': ' '.join(missing),
            'title_current': title,
            'title_h1': h1,
            'title_proposed': proposed.get('title', ''),
            'tags_count': str(len(tags_list)),
            'tags_over_limit': 'yes' if len(tags_list) > 5 else 'no',
            'status_current': status,
            'status_proposed': proposed.get('status', ''),
            'created_current': created,
            'created_proposed': proposed.get('created', ''),
            'updated_current': updated,
            'updated_proposed': proposed.get('updated', ''),
            'aliases_count': str(len(aliases_list)),
            'source_count': str(len(source_list)),
            'issues': ' '.join(issues),
        })

    with OUT.open('w', newline='', encoding='utf-8') as fp:
        w = csv.DictWriter(fp, fieldnames=[
            'file','has_yaml','missing','title_current','title_h1','title_proposed',
            'tags_count','tags_over_limit','status_current','status_proposed',
            'created_current','created_proposed','updated_current','updated_proposed',
            'aliases_count','source_count','issues'
        ])
        w.writeheader()
        w.writerows(rows)
    print(f"Frontmatter dry-run written: {OUT} ({len(rows)} files)")


if __name__ == '__main__':
    main()
