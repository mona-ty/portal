#!/usr/bin/env python3
import re
from pathlib import Path
import csv

ROOT = Path(__file__).resolve().parents[1]
NOTES_DIR = ROOT / 'notes'
REPORTS_DIR = ROOT / 'reports'
REPORT_CSV = REPORTS_DIR / 'notes_tag_apply.csv'


def read_text_best_effort(p: Path) -> str:
    for enc in ('utf-8', 'utf-8-sig', 'cp932', 'shift_jis'):
        try:
            return p.read_text(encoding=enc)
        except Exception:
            continue
    return p.read_text(errors='ignore')


def write_text_utf8(p: Path, text: str):
    p.write_text(text, encoding='utf-8', newline='\n')


def normalize_tag(tag: str) -> str:
    t = tag.strip()
    if t.startswith('#'):
        t = t[1:]
    t = re.sub(r"\s+", "-", t)
    t = t.strip(".,;:'\"()[]{}<>")
    t = t.replace('—', '-').replace('–', '-')
    t = t.lower()
    return t


def extract_yaml_front_matter(text: str):
    if not text.startswith('---'):
        return None, None
    lines = text.splitlines()
    if len(lines) < 3:
        return None, None
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
    lines = yaml_block.splitlines()
    i = 0
    while i < len(lines):
        line = lines[i]
        m = re.match(r"^(tags?|keywords)\s*:\s*(.*)$", line.strip(), re.IGNORECASE)
        if m:
            rest = m.group(2).strip()
            if rest == '' or rest == '|' or rest == '>':
                j = i + 1
                while j < len(lines):
                    l = lines[j]
                    if re.match(r"^\s*-\s*", l):
                        item = re.sub(r"^\s*-\s*", "", l).strip()
                        if item:
                            tags.append(item)
                        j += 1
                        continue
                    if re.match(r"^\s*[A-Za-z0-9_-]+\s*:\s*", l) or l.strip() == '':
                        break
                    j += 1
                i = j
                continue
            if rest.startswith('[') and rest.endswith(']'):
                inner = rest[1:-1]
                parts = [p.strip() for p in inner.split(',') if p.strip()]
                tags.extend(parts)
            else:
                parts = [p.strip() for p in rest.split(',') if p.strip()]
                tags.extend(parts)
        i += 1
    out = []
    seen = set()
    for t in tags:
        nt = normalize_tag(t)
        if nt and nt not in seen:
            out.append(nt)
            seen.add(nt)
    return out


def extract_inline_tags(text: str):
    cleaned = re.sub(r"```[\s\S]*?```", "\n", text)
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
        parts = list(rel.parents)[:-1]
        parts = [d.name for d in parts][::-1]
    except Exception:
        pass
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


def update_yaml_block_preserve(yaml_block: str, new_tags):
    lines = yaml_block.splitlines() if yaml_block else []
    out_lines = []
    skip_mode = False
    for i, line in enumerate(lines):
        if not skip_mode:
            m = re.match(r"^(\s*)(tags?|keywords)\s*:\s*(.*)$", line, re.IGNORECASE)
            if m:
                indent = m.group(1)
                # write our inline tags and enable skip mode to drop following list items if any
                out_lines.append(f"{indent}tags: [{', '.join(new_tags)}]")
                skip_mode = True
                continue
            else:
                out_lines.append(line)
        else:
            # skip list items under the previous tags key
            if re.match(r"^\s*-\s*", line):
                continue
            # if next mapping key or blank, stop skipping and process this line normally
            if re.match(r"^\s*[A-Za-z0-9_-]+\s*:\s*", line) or line.strip() == '':
                skip_mode = False
                out_lines.append(line)
            else:
                # conservative: stop skipping after one non-list line
                skip_mode = False
                out_lines.append(line)
    if not any(re.match(r"^\s*tags\s*:\s*", l, re.IGNORECASE) for l in out_lines):
        out_lines.append(f"tags: [{', '.join(new_tags)}]")
    return "\n".join(out_lines)


def apply_tags_to_text(text: str, tags):
    yaml_block, body = extract_yaml_front_matter(text)
    if yaml_block is None:
        # create new yaml front matter
        new_yaml = f"tags: [{', '.join(tags)}]"
        return f"---\n{new_yaml}\n---\n{body if text.startswith('---') else text}"
    else:
        new_yaml_block = update_yaml_block_preserve(yaml_block, tags)
        return f"---\n{new_yaml_block}\n---\n{body}"


def main():
    if not NOTES_DIR.exists():
        print(f"notes directory not found: {NOTES_DIR}")
        return
    REPORTS_DIR.mkdir(parents=True, exist_ok=True)
    files = sorted(NOTES_DIR.rglob('*.md'))
    rows = []
    changed = 0
    created_yaml = 0
    for f in files:
        text = read_text_best_effort(f)
        yaml_block, body = extract_yaml_front_matter(text)
        yaml_tags = parse_tags_from_yaml(yaml_block) if yaml_block is not None else []
        inline_tags = extract_inline_tags(text)
        path_tags = tags_from_path(f)
        proposed = propose_tags(yaml_tags, inline_tags, path_tags, limit=5)
        before = ' '.join(yaml_tags)
        after = ' '.join(proposed)
        # decide whether to write
        need_write = True
        if yaml_tags == proposed and yaml_block is not None:
            need_write = False
        if need_write:
            new_text = apply_tags_to_text(text, proposed)
            write_text_utf8(f, new_text)
            changed += 1
            if yaml_block is None:
                created_yaml += 1
            status = 'updated'
        else:
            status = 'unchanged'
        rows.append({
            'file': str(f.relative_to(ROOT)),
            'status': status,
            'before_yaml_tags': before,
            'inline_tags': ' '.join(inline_tags),
            'path_tags': ' '.join(path_tags),
            'after_tags': after
        })

    with REPORT_CSV.open('w', newline='', encoding='utf-8') as fp:
        writer = csv.DictWriter(fp, fieldnames=['file','status','before_yaml_tags','inline_tags','path_tags','after_tags'])
        writer.writeheader()
        writer.writerows(rows)

    print(f"Applied tags to {changed}/{len(files)} files (created YAML in {created_yaml}).")
    print(f"Report written: {REPORT_CSV}")


if __name__ == '__main__':
    main()

