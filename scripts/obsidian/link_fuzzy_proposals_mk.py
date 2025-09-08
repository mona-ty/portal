#!/usr/bin/env python3
import csv
import os
import re
from difflib import SequenceMatcher
import unicodedata
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MK = ROOT / 'notes' / 'MK'
TASKS = ROOT / 'TASKS'
AUDIT = TASKS / 'mk_link_audit.csv'
AUDIT_ALT = (ROOT / 'TASKS' / 'artifacts' / 'mk_link_audit.csv')
OUT = TASKS / 'artifacts' / 'mk_link_fuzzy_proposals.csv'


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


def extract_h1(text: str) -> str:
    for line in text.splitlines():
        s = line.strip()
        if s.startswith('#'):
            s = re.sub(r"^#+\s*", "", s)
            if s:
                return s
    return ''


def build_name_index(files):
    idx = {}
    for f in files:
        names = set()
        names.add(f.stem)
        try:
            txt = read_text_best_effort(f)
            # remove yaml
            if txt.startswith('---'):
                parts = txt.split('\n')
                for i in range(1, min(300, len(parts))):
                    if parts[i].strip() == '---':
                        txt_body = '\n'.join(parts[i+1:])
                        break
                else:
                    txt_body = txt
            else:
                txt_body = txt
            h1 = extract_h1(txt_body)
            if h1:
                names.add(h1)
        except Exception:
            pass
        # normalized variants
        for n in list(names):
            nf = unicodedata.normalize('NFKC', n)
            nf = nf.replace('　', ' ')
            names.add(nf)
        for n in names:
            key = norm_name(n)
            if not key:
                continue
            idx.setdefault(key, []).append(f)
    return idx


def norm_name(s: str) -> str:
    s = s.strip().lower()
    s = s.replace('%20', ' ')
    s = s.replace('_', ' ')
    s = re.sub(r"\s+", " ", s)
    s = s.strip()
    return s


def relpath(from_path: Path, to_path: Path) -> str:
    try:
        return str(to_path.relative_to(from_path.parent)).replace('\\','/')
    except Exception:
        return os.path.relpath(str(to_path), str(from_path.parent)).replace('\\','/')


def main():
    # optional arg: --min-score <float>
    min_score = 0.5
    import sys
    args = sys.argv[1:]
    for i, a in enumerate(args):
        if a == '--min-score' and i + 1 < len(args):
            try:
                min_score = float(args[i + 1])
            except Exception:
                pass

    audit_path = AUDIT if AUDIT.exists() else AUDIT_ALT
    if not audit_path.exists():
        print(f"Audit not found: {AUDIT} or {AUDIT_ALT}")
        return
    files = all_md_files()
    name_index = build_name_index(files)
    rows = list(csv.DictReader(audit_path.open(encoding='utf-8')))
    out_rows = []
    not_found_rows = [r for r in rows if r.get('status') == 'not_found']
    for r in not_found_rows:
        rel = r['file']
        src = ROOT / rel
        current = r['current']
        link_type = r['link_type']
        # extract base name from current
        base = current
        if link_type and 'wikilink' in link_type:
            base = current.split('|',1)[0].split('#',1)[0]
        else:
            base = current.split('#',1)[0]
        base_name = Path(base).stem
        q = norm_name(unicodedata.normalize('NFKC', base_name))
        # compute scores against known names
        scored = []
        for name, plist in name_index.items():
            score = SequenceMatcher(None, q, name).ratio()
            if score >= min_score:
                for p in plist:
                    scored.append((score, p))
        scored.sort(key=lambda x: (-x[0], str(x[1])))
        # take top 5
        top = scored[:5]
        if not top:
            out_rows.append({'file': rel, 'link_type': link_type, 'current': current, 'suggestions': ''})
            continue
        sug = []
        for score, p in top:
            rp = relpath(ROOT / rel, p)
            sug.append(f"{rp}|{p.stem}|{score:.2f}")
        out_rows.append({
            'file': rel,
            'link_type': link_type,
            'current': current,
            'suggestions': '; '.join(sug)
        })

    with OUT.open('w', newline='', encoding='utf-8') as fp:
        w = csv.DictWriter(fp, fieldnames=['file','link_type','current','suggestions'])
        w.writeheader()
        w.writerows(out_rows)
    print(f"Fuzzy proposals written: {OUT} (not_found={len(not_found_rows)} with suggestions={sum(1 for r in out_rows if r['suggestions'])}, min_score={min_score})")


if __name__ == '__main__':
    main()
