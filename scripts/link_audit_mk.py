#!/usr/bin/env python3
import csv
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MK = ROOT / 'notes' / 'MK'
TASKS = ROOT / 'tasks'
OUT = TASKS / 'mk_link_audit.csv'


def read_text_best_effort(p: Path) -> str:
    for enc in ('utf-8', 'utf-8-sig', 'cp932', 'shift_jis'):
        try:
            return p.read_text(encoding=enc)
        except Exception:
            continue
    return p.read_text(errors='ignore')


def all_md_files():
    files = [p for p in MK.rglob('*.md')]
    out = []
    for f in files:
        low = str(f).lower()
        if any(seg in low for seg in ['70_templates', '60_attachments', '90_archive', '.obsidian\\', '/.obsidian/']):
            continue
        out.append(f)
    return out


def build_name_index(files):
    idx = {}
    for f in files:
        name = f.stem.lower()
        idx.setdefault(name, []).append(f)
    return idx


def relpath(from_path: Path, to_path: Path) -> str:
    try:
        return str(to_path.relative_to(from_path.parent))
    except Exception:
        # fallback to os.path.relpath semantics
        import os
        return os.path.relpath(str(to_path), str(from_path.parent))


def audit_file(p: Path, name_index, all_files_set):
    rows = []
    text = read_text_best_effort(p)
    # Wiki links: [[target|alias]] or [[target]] or ![[...]]
    for m in re.finditer(r"!??\[\[([^\]]+)\]\]", text):
        raw = m.group(1)
        target = raw.split('|', 1)[0]
        target = target.split('#', 1)[0]
        typ = 'embed-wikilink' if text[m.start():m.start()+3] == '![[' else 'wikilink'
        if '/' in target or '\\' in target:
            # path-specified wikilink: check existence (respect explicit extension)
            pt = Path(target)
            if pt.suffix.lower() == '':
                cand = (p.parent / target).with_suffix('.md')
            else:
                cand = (p.parent / target)
            if cand.exists():
                rows.append({'file': str(p.relative_to(ROOT)), 'link_type': typ, 'current': target, 'status': 'ok', 'proposal': ''})
            else:
                # try by name for md targets only
                if pt.suffix.lower() in ('', '.md'):
                    name = pt.stem.lower()
                    hits = name_index.get(name, [])
                    if len(hits) == 1:
                        new_rel = relpath(p, hits[0])
                        rows.append({'file': str(p.relative_to(ROOT)), 'link_type': typ, 'current': target, 'status': 'broken', 'proposal': new_rel})
                    elif len(hits) > 1:
                        rows.append({'file': str(p.relative_to(ROOT)), 'link_type': typ, 'current': target, 'status': 'ambiguous', 'proposal': ''})
                    else:
                        rows.append({'file': str(p.relative_to(ROOT)), 'link_type': typ, 'current': target, 'status': 'not_found', 'proposal': ''})
                else:
                    rows.append({'file': str(p.relative_to(ROOT)), 'link_type': typ, 'current': target, 'status': 'not_found', 'proposal': ''})
        else:
            # name-only wikilink (accept both bare and with .md)
            name = Path(target).stem.lower()
            hits = name_index.get(name, [])
            if len(hits) >= 1:
                rows.append({'file': str(p.relative_to(ROOT)), 'link_type': typ, 'current': target, 'status': 'ok', 'proposal': ''})
            else:
                rows.append({'file': str(p.relative_to(ROOT)), 'link_type': typ, 'current': target, 'status': 'not_found', 'proposal': ''})

    # Markdown links: [text](path)
    for m in re.finditer(r"!??\[[^\]]*\]\(([^)\s]+)\)", text):
        url = m.group(1)
        typ = 'embed-md' if text[m.start()] == '!' else 'md'
        if re.match(r"^[a-z]+://", url, flags=re.IGNORECASE):
            rows.append({'file': str(p.relative_to(ROOT)), 'link_type': typ, 'current': url, 'status': 'external', 'proposal': ''})
            continue
        # normalize anchors
        path_part = url.split('#', 1)[0]
        # handle spaces and url-encoding basic
        path_part = path_part.replace('%20', ' ')
        target_path = (p.parent / path_part)
        # if missing extension and likely note, add .md for existence check
        if target_path.suffix == '':
            md_try = target_path.with_suffix('.md')
        else:
            md_try = target_path
        if md_try.exists():
            rows.append({'file': str(p.relative_to(ROOT)), 'link_type': typ, 'current': url, 'status': 'ok', 'proposal': ''})
        else:
            # try by name lookup
            name = Path(path_part).stem.lower()
            hits = name_index.get(name, [])
            if len(hits) == 1:
                new_rel = relpath(p, hits[0])
                # preserve original anchor if present
                if '#' in url:
                    new_rel = new_rel + '#' + url.split('#', 1)[1]
                rows.append({'file': str(p.relative_to(ROOT)), 'link_type': typ, 'current': url, 'status': 'broken', 'proposal': new_rel})
            elif len(hits) > 1:
                rows.append({'file': str(p.relative_to(ROOT)), 'link_type': typ, 'current': url, 'status': 'ambiguous', 'proposal': ''})
            else:
                rows.append({'file': str(p.relative_to(ROOT)), 'link_type': typ, 'current': url, 'status': 'not_found', 'proposal': ''})

    return rows


def main():
    TASKS.mkdir(parents=True, exist_ok=True)
    files = all_md_files()
    name_index = build_name_index(files)
    all_set = set(files)
    rows = []
    for f in files:
        rows.extend(audit_file(f, name_index, all_set))
    with OUT.open('w', newline='', encoding='utf-8') as fp:
        w = csv.DictWriter(fp, fieldnames=['file','link_type','current','status','proposal'])
        w.writeheader()
        w.writerows(rows)
    # Basic summary
    total = len(rows)
    broken = sum(1 for r in rows if r['status'] == 'broken')
    ambiguous = sum(1 for r in rows if r['status'] == 'ambiguous')
    not_found = sum(1 for r in rows if r['status'] == 'not_found')
    external = sum(1 for r in rows if r['status'] == 'external')
    print(f"Link audit written: {OUT} rows={total} broken={broken} ambiguous={ambiguous} not_found={not_found} external={external}")


if __name__ == '__main__':
    main()
