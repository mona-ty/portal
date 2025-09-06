#!/usr/bin/env python3
import csv
import re
import unicodedata
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MK = ROOT / 'notes' / 'MK'
TASKS = ROOT / 'tasks' / 'artifacts'
OUT = TASKS / 'mk_retag_dry_run.csv'

STOP_TAGS = {'mk'}
STOPWORDS_EN = set('''a an the and or for of to in on with without from by as is are was were be been being this that these those it its at into about over under above below out up down off so not no yes you your our their we they them i me my mine ourselves himself herself itself themselves if else when than then which who whom whose what where why how all any each few more most other some such only own same can will just don t should now here there very via etc com www http https md txt json yaml yml csv tsv pdf png jpg jpeg gif mp4 webm mov mkv ts html htm css js tag tags hashtag hashtags document documents user users file files folder folders title titles page pages link links post posts content contents draft drafts sample samples example examples todo todos today update updated updates version versions note notes'''.split())
STOPWORDS_JA = set('''これ それ あれ ここ そこ あそこ こちら どれ どこ そして しかし また ため ので から こと もの とき です ます でした でしたら では には が は に を へ と も の より や など ために ように ような における に対して について まで までに そして また さらに 等 等々 的 的な 的に のような のように できる できない する しない 使用 利用 参考 注意 例 例示 例として 概要 要約'''.split())


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
    hosts = []
    for m in re.finditer(r"https?://([^/\s]+)", text, flags=re.IGNORECASE):
        host = m.group(1).lower()
        host = re.sub(r"^(www\.)", "", host)
        parts = host.split('.')
        root = parts[-2] if len(parts) >= 2 else parts[0]
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
    no_code = re.sub(r"```[\s\S]*?```", "\n", text)
    no_urls = re.sub(r"https?://\S+", " ", no_code)
    lines = no_urls.splitlines()
    headings = [re.sub(r"^#+\s*", "", ln.strip()) for ln in lines if ln.strip().startswith('#')]
    en_tokens = re.findall(r"[A-Za-z][A-Za-z0-9\-]{2,}", no_urls)
    kata_tokens = re.findall(r"[ァ-ヴー]{2,}", no_urls)
    ja_tokens = re.findall(r"[一-龠々〆ヵヶぁ-んァ-ヴー]{2,12}", ''.join(headings))

    def clean(tokens, lang='en'):
        out = []
        for t in tokens:
            nt = normalize_tag(t)
            if not nt: continue
            if nt in STOP_TAGS: continue
            if lang == 'en' and nt in STOPWORDS_EN: continue
            if lang == 'ja' and nt in STOPWORDS_JA: continue
            if nt in {'note','notes','index','todo','draft','temp','test'}: continue
            out.append(nt)
        return out

    en_tokens = clean(en_tokens, 'en')
    kata_tokens = clean(kata_tokens, 'ja')
    ja_tokens = clean(ja_tokens, 'ja')

    score = {}
    def bump(tok, w=1.0):
        score[tok] = score.get(tok, 0.0) + w

    for t in en_tokens: bump(t, 1.0)
    for t in kata_tokens: bump(t, 1.0)
    for t in ja_tokens: bump(t, 1.0)

    head_text = '\n'.join(headings).lower()
    for t in list(score.keys()):
        if t in head_text: bump(t, 2.0)
    for d in extract_domains(text):
        bump(d, 1.5)

    top = sorted(score.items(), key=lambda kv: (-kv[1], kv[0]))
    return [k for k, _ in top]


def main():
    TASKS.mkdir(parents=True, exist_ok=True)
    rows = []
    for f in sorted(MK.rglob('*.md')):
        low = str(f).lower()
        if any(seg in low for seg in ['70_templates', '60_attachments', '90_archive', '.obsidian\\', '/.obsidian/']):
            continue
        text = read_text_best_effort(f)
        yaml_block, body = extract_yaml(text)
        yaml_tags = parse_tags_from_yaml(yaml_block) if yaml_block is not None else []
        inline_tags = extract_inline_tags(text)
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

        rows.append({
            'file': str(f.relative_to(ROOT)),
            'current_yaml_tags': ' '.join(yaml_tags),
            'inline_tags': ' '.join(inline_tags),
            'keywords_top': ' '.join(keywords[:10]),
            'proposed_tags': ' '.join(final)
        })

    with OUT.open('w', newline='', encoding='utf-8') as fp:
        w = csv.DictWriter(fp, fieldnames=['file','current_yaml_tags','inline_tags','keywords_top','proposed_tags'])
        w.writeheader()
        w.writerows(rows)
    print(f"Retag MK dry-run written: {OUT} ({len(rows)} files)")


if __name__ == '__main__':
    main()

