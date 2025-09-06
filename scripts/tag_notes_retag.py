#!/usr/bin/env python3
import re
import unicodedata
from pathlib import Path
import csv

ROOT = Path(__file__).resolve().parents[1]
NOTES_DIR = ROOT / 'notes'
REPORTS_DIR = ROOT / 'reports'
REPORT_CSV = REPORTS_DIR / 'notes_tag_retag.csv'

STOP_TAGS = { 'mk' }
STOPWORDS_EN = set('''a an the and or for of to in on with without from by as is are was were be been being this that these those it its at into about over under above below out up down off so not no yes you your our their we they them i me my mine ourselves himself herself itself themselves if else when than then which who whom whose what where why how all any each few more most other some such only own same can will just don t should now here there very via etc com www http https md txt json yaml yml csv tsv pdf png jpg jpeg gif mp4 webm mov mkv ts html htm css js tag tags hashtag hashtags document documents user users file files folder folders title titles page pages link links post posts content contents draft drafts sample samples example examples todo todos today update updated updates version versions note notes'''.split())
STOPWORDS_JA = set('''これ それ あれ ここ そこ あそこ こちら どれ どこ そして しかし また ため ので から こと もの とき です ます でした でしたら では には が は に を へ と も の より や など ために ように ような における に対して について まで までに そして また さらに 等 等々 的 的な 的に のような のように できる できない する しない 使用 利用 参考 注意 例 例示 例として 概要 要約'''.split())


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
    t = unicodedata.normalize('NFKC', t)
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
        if nt and nt not in seen and nt not in STOP_TAGS:
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
        if nt and nt not in seen and nt not in STOP_TAGS:
            out.append(nt)
            seen.add(nt)
    return out


def extract_domains(text: str):
    # find hostnames from URLs
    hosts = []
    for m in re.finditer(r"https?://([^/\s]+)", text, flags=re.IGNORECASE):
        host = m.group(1).lower()
        # strip common prefixes
        host = re.sub(r"^(www\.)", "", host)
        # take root domain label
        parts = host.split('.')
        if len(parts) >= 2:
            root = parts[-2]
        else:
            root = parts[0]
        # map short domains
        mapping = {'x': 'twitter', 't': 'twitter'}
        root = mapping.get(root, root)
        if root and root not in {'com','net','org','co','jp','io'}:
            hosts.append(root)
    out = []
    seen = set()
    for h in hosts:
        nh = normalize_tag(h)
        if nh and nh not in seen and nh not in STOP_TAGS and nh not in STOPWORDS_EN:
            out.append(nh)
            seen.add(nh)
    return out


def extract_keywords(text: str):
    # remove code fences and URLs for general tokenization (we separately extract domains)
    no_code = re.sub(r"```[\s\S]*?```", "\n", text)
    no_urls = re.sub(r"https?://\S+", " ", no_code)
    # headings get extra weight later
    lines = no_urls.splitlines()
    headings = [re.sub(r"^#+\s*", "", ln.strip()) for ln in lines if ln.strip().startswith('#')]
    body_text = '\n'.join([ln for ln in lines if not ln.strip().startswith('#')])

    # English-like tokens
    en_tokens = re.findall(r"[A-Za-z][A-Za-z0-9\-]{2,}", no_urls)
    # Katakana words (common for loanwords/tech)
    kata_tokens = re.findall(r"[ァ-ヴー]{2,}", no_urls)
    # Optionally, compact Kanji/Hiragana sequences (2-12 chars) — cautious to reduce noise
    ja_tokens = re.findall(r"[一-龠々〆ヵヶぁ-んァ-ヴー]{2,12}", ''.join(headings))

    def clean_tokens(tokens, lang='en'):
        out = []
        for t in tokens:
            nt = normalize_tag(t)
            if not nt:
                continue
            if nt in STOP_TAGS:
                continue
            if lang == 'en' and nt in STOPWORDS_EN:
                continue
            if lang == 'ja' and nt in STOPWORDS_JA:
                continue
            # filter overly generic tokens
            if nt in {'note','notes','index','todo','draft','temp','test'}:
                continue
            out.append(nt)
        return out

    en_tokens = clean_tokens(en_tokens, 'en')
    kata_tokens = clean_tokens(kata_tokens, 'ja')
    ja_tokens = clean_tokens(ja_tokens, 'ja')

    # scoring
    score = {}
    def bump(tok, w=1.0):
        score[tok] = score.get(tok, 0.0) + w

    for t in en_tokens:
        bump(t, 1.0)
    for t in kata_tokens:
        bump(t, 1.0)
    for t in ja_tokens:
        bump(t, 1.0)

    # headings weight
    head_text = '\n'.join(headings).lower()
    for t in list(score.keys()):
        if t in head_text:
            bump(t, 2.0)

    # domain names from URLs
    for d in extract_domains(text):
        bump(d, 1.5)

    # pick top tokens
    top = sorted(score.items(), key=lambda kv: (-kv[1], kv[0]))
    candidates = [k for k, _ in top]
    return candidates


def apply_tags_to_text(text: str, tags):
    yaml_block, body = extract_yaml_front_matter(text)
    # rebuild yaml block
    new_yaml = f"tags: [{', '.join(tags)}]" if tags else ''
    if yaml_block is None:
        if not tags:
            return text  # nothing to write
        return f"---\n{new_yaml}\n---\n{text}"
    else:
        # replace or remove existing tags from yaml
        lines = yaml_block.splitlines() if yaml_block else []
        out_lines = []
        skip_mode = False
        replaced = False
        for i, line in enumerate(lines):
            if not skip_mode:
                m = re.match(r"^(\s*)(tags?|keywords)\s*:\s*(.*)$", line, re.IGNORECASE)
                if m:
                    if tags:
                        out_lines.append(f"{m.group(1)}tags: [{', '.join(tags)}]")
                    # if no tags, drop the key entirely
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
        if not replaced and tags:
            out_lines.append(f"tags: [{', '.join(tags)}]")
        new_yaml_block = "\n".join(out_lines)
        return f"---\n{new_yaml_block}\n---\n{body}"


def main():
    if not NOTES_DIR.exists():
        print(f"notes directory not found: {NOTES_DIR}")
        return
    REPORTS_DIR.mkdir(parents=True, exist_ok=True)
    files = sorted(NOTES_DIR.rglob('*.md'))
    rows = []
    changed = 0
    for f in files:
        text = read_text_best_effort(f)
        yaml_block, _ = extract_yaml_front_matter(text)
        existing_yaml_tags = parse_tags_from_yaml(yaml_block) if yaml_block is not None else []
        existing_yaml_tags = [t for t in existing_yaml_tags if t not in STOP_TAGS]
        inline_tags = extract_inline_tags(text)
        inline_tags = [t for t in inline_tags if t not in STOP_TAGS]
        keywords = extract_keywords(text)

        # Build final tags: inline first, then keywords, up to 5
        final = []
        seen = set()
        for src in (inline_tags, keywords):
            for t in src:
                if t not in seen and t not in STOP_TAGS:
                    final.append(t)
                    seen.add(t)
                if len(final) >= 5:
                    break
            if len(final) >= 5:
                break

        before = ' '.join(existing_yaml_tags)
        after = ' '.join(final)
        need_write = True
        if existing_yaml_tags == final and yaml_block is not None:
            need_write = False
        if need_write:
            new_text = apply_tags_to_text(text, final)
            if new_text != text:
                write_text_utf8(f, new_text)
                changed += 1
            status = 'updated'
        else:
            status = 'unchanged'

        rows.append({
            'file': str(f.relative_to(ROOT)),
            'status': status,
            'before_yaml_tags': before,
            'inline_tags': ' '.join(inline_tags),
            'keywords_top': ' '.join(keywords[:10]),
            'after_tags': after
        })

    with REPORT_CSV.open('w', newline='', encoding='utf-8') as fp:
        writer = csv.DictWriter(fp, fieldnames=['file','status','before_yaml_tags','inline_tags','keywords_top','after_tags'])
        writer.writeheader()
        writer.writerows(rows)

    print(f"Retagged {changed}/{len(files)} files (mk removed and content-based tags applied).")
    print(f"Report written: {REPORT_CSV}")


if __name__ == '__main__':
    main()
