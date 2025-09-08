#!/usr/bin/env python3
import re
import os
import csv
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
NOTES_DIR = ROOT / 'notes'
REPORTS_DIR = ROOT / 'TASKS' / 'artifacts'
REPORT_CSV = REPORTS_DIR / 'notes_tag_dry_run.csv'


def read_text_best_effort(p: Path) -> str:
    for enc in ('utf-8', 'utf-8-sig', 'cp932', 'shift_jis'):
        try:
            return p.read_text(encoding=enc)
        except Exception:
            continue
    return p.read_text(errors='ignore')


def normalize_tag(tag: str) -> str:
    t = tag.strip()
    if t.startswith('#'):
        t = t[1:]
    # Replace whitespace with hyphen; trim brackets/punctuations around
    t = re.sub(r"\s+", "-", t)
    t = t.strip(".,;:'\"()[]{}<>")
    t = t.replace('—', '-').replace('–', '-')
    # Obsidian supports unicode; lowercase for ascii for consistency
    t = t.lower()
    return t


def extract_yaml_front_matter(text: str):
    if not text.startswith('---'):
        return None, None
    lines = text.splitlines()
    if len(lines) < 3:
        return None, None
    # find closing '---' on its own line
    end_idx = None
    for i in range(1, min(len(lines), 200)):
        if lines[i].strip() == '---':
            end_idx = i
            break
    if end_idx is None:
        return None, None
    yaml_block = "\n".join(lines[1:end_idx])
    body = "\n".join(lines[end_idx+1:])
    return yaml_block, body


def parse_tags_from_yaml(yaml_block: str):
    if not yaml_block:
        return []
    tags = []
    # simple line-based parse for tags, tag, keywords
    lines = yaml_block.splitlines()
    i = 0
    while i < len(lines):
        line = lines[i]
        m = re.match(r"^(tags?|keywords)\s*:\s*(.*)$", line.strip(), re.IGNORECASE)
        if m:
            rest = m.group(2).strip()
            if rest == '' or rest == '|' or rest == '>':
                # block style list expected following with - item
                j = i + 1
                while j < len(lines):
                    l = lines[j]
                    if re.match(r"^\s*-\s*", l):
                        item = re.sub(r"^\s*-\s*", "", l).strip()
                        if item:
                            tags.append(item)
                        j += 1
                        continue
                    # stop when next key or blank line without dash encountered
                    if re.match(r"^\s*[A-Za-z0-9_-]+\s*:\s*", l) or l.strip() == '':
                        break
                    j += 1
                i = j
                continue
            # inline list [a, b] or comma-separated
            if rest.startswith('[') and rest.endswith(']'):
                inner = rest[1:-1]
                parts = [p.strip() for p in inner.split(',') if p.strip()]
                tags.extend(parts)
            else:
                # comma separated or single
                parts = [p.strip() for p in rest.split(',') if p.strip()]
                tags.extend(parts)
        i += 1
    # normalize
    out = []
    seen = set()
    for t in tags:
        nt = normalize_tag(t)
        if nt and nt not in seen:
            out.append(nt)
            seen.add(nt)
    return out


def extract_inline_tags(text: str):
    # remove code fences to reduce noise
    cleaned = re.sub(r"```[\s\S]*?```", "\n", text)
    # Obsidian tags: #tag
    # Use a simple pattern: # until whitespace or another #
    tags = []
    for m in re.finditer(r"#([^\s#]+)", cleaned):
        tags.append(m.group(1))
    out = []
    seen = set()
    for t in tags:
        nt = normalize_tag(t)
        if nt and nt not in seen:
            out.append(nt)
            seen.add(nt)
    return out


def tags_from_path(p: Path):
    parts = []
    try:
        rel = p.relative_to(NOTES_DIR)
        parts = list(rel.parents)[:-1]  # exclude the file itself and the empty last parent
        parts = [d.name for d in parts][::-1]  # top-down
    except Exception:
        pass
    # favor deeper directories first
    parts = parts[-3:]
    out = []
    seen = set()
    for part in parts:
        nt = normalize_tag(part)
        if nt and nt not in seen:
            out.append(nt)
            seen.add(nt)
    return out


def propose_tags(existing_yaml, inline_tags, path_tags, limit=5):
    result = []
    seen = set()
    for src in (existing_yaml, inline_tags, path_tags):
        for t in src:
            if t not in seen:
                result.append(t)
                seen.add(t)
            if len(result) >= limit:
                return result
    return result


def main():
    if not NOTES_DIR.exists():
        print(f"notes directory not found: {NOTES_DIR}")
        return
    REPORTS_DIR.mkdir(parents=True, exist_ok=True)
    files = sorted(NOTES_DIR.rglob('*.md'))
    rows = []
    for f in files:
        try:
            text = read_text_best_effort(f)
        except Exception as e:
            rows.append({
                'file': str(f.relative_to(ROOT)),
                'status': f'read_error:{e}',
                'yaml_tags': '',
                'inline_tags': '',
                'path_tags': '',
                'proposed_tags': ''
            })
            continue
        yaml_block, body = extract_yaml_front_matter(text)
        yaml_tags = parse_tags_from_yaml(yaml_block) if yaml_block is not None else []
        inline_tags = extract_inline_tags(text)
        path_tags = tags_from_path(f)
        proposed = propose_tags(yaml_tags, inline_tags, path_tags, limit=5)
        rows.append({
            'file': str(f.relative_to(ROOT)),
            'status': 'ok',
            'yaml_tags': ' '.join(yaml_tags),
            'inline_tags': ' '.join(inline_tags),
            'path_tags': ' '.join(path_tags),
            'proposed_tags': ' '.join(proposed)
        })
    with REPORT_CSV.open('w', newline='', encoding='utf-8') as fp:
        writer = csv.DictWriter(fp, fieldnames=['file','status','yaml_tags','inline_tags','path_tags','proposed_tags'])
        writer.writeheader()
        writer.writerows(rows)
    print(f"Wrote dry-run report: {REPORT_CSV} ({len(rows)} files)")


if __name__ == '__main__':
    main()
