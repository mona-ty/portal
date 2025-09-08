#!/usr/bin/env python3
import csv
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MK = ROOT / 'notes' / 'MK'
TASKS = ROOT / 'TASKS'
OUT_CSV = TASKS / 'mk_classification_proposals.csv'


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


def parse_tags(yaml_block: str):
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
    norm = []
    seen = set()
    for t in tags:
        nt = t.lower().strip().lstrip('#')
        if nt and nt not in seen:
            seen.add(nt)
            norm.append(nt)
    return norm


def score_classification(path: Path, text: str, yaml_block: str):
    # Scores for each target folder
    scores = {
        '10_research': 0.0,
        '20_notes': 0.0,
        '30_projects': 0.0,
        '40_prompts': 0.0,
        '50_code': 0.0,
    }
    reasons = []

    # Counts
    code_fences = len(re.findall(r"```", text)) // 2
    urls = len(re.findall(r"https?://", text, flags=re.IGNORECASE))
    headings = len(re.findall(r"^#+\s+", text, flags=re.MULTILINE))

    # YAML tags influence
    tags = parse_tags(yaml_block or '')
    tagset = set(tags)

    # Prompt-like signals
    if any(t in tagset for t in ['prompt', 'prompts']):
        scores['40_prompts'] += 3
        reasons.append('tag:prompt')
    if re.search(r"(?im)^##?\s*(Task|Model|System|Few-?Shots|Input|Output)\b", text):
        scores['40_prompts'] += 2.5
        reasons.append('sections:prompt-like')

    # Code-like signals
    if any(t in tagset for t in ['code', 'snippet', 'dev']):
        scores['50_code'] += 2.5
        reasons.append('tag:code')
    if code_fences >= 2:
        scores['50_code'] += 2.5
        reasons.append('codeblocks>=2')
    if re.search(r"(?i)\b(import|class|def|function|const|var|let|SELECT\s+|curl\s+-|pip install|npm install|yarn add|dotnet |powershell )", text):
        scores['50_code'] += 1.5
        reasons.append('dev-terms')

    # Research-like signals
    if any(t in tagset for t in ['research', 'paper', 'reference']):
        scores['10_research'] += 2.0
        reasons.append('tag:research')
    if urls >= 3:
        scores['10_research'] += 2.0
        reasons.append('urls>=3')
    if re.search(r"(?im)^##?\s*(Summary|Quotes|Excerpts|Source|Links)\b", text):
        scores['10_research'] += 1.5
        reasons.append('sections:research-like')

    # Project-like signals
    name_l = path.name.lower()
    if name_l == 'readme.md' or path.parent.name.lower() == '30_projects':
        scores['30_projects'] += 3.0
        reasons.append('readme/projects-folder')
    if re.search(r"(?im)^##?\s*(Overview|Goals|Scope|Timeline|Milestones|Tasks)\b", text):
        scores['30_projects'] += 1.5
        reasons.append('sections:project-like')
    if re.search(r"(?im)^status:\s*active\b", yaml_block or '') or re.search(r"(?im)^project:\s*\S+", yaml_block or ''):
        scores['30_projects'] += 1.0
        reasons.append('yaml:project/status')

    # Notes default
    if re.search(r"(?im)^##?\s*(TL;?DR|Details|Why|Key Points)\b", text):
        scores['20_notes'] += 1.5
        reasons.append('sections:notes-like')
    if headings >= 3 and urls < 3 and code_fences <= 1:
        scores['20_notes'] += 1.0
        reasons.append('essayish')

    # Path-based hints
    pstr = str(path).lower()
    if '/prompts/' in pstr or '\\prompts\\' in pstr or 'pronmpts' in pstr:
        scores['40_prompts'] += 1.5
        reasons.append('path:prompts')
    if '/deepresearch/' in pstr or '\\deepresearch\\' in pstr:
        scores['10_research'] += 1.0
        reasons.append('path:deepresearch')
    if '/ideas/' in pstr or '\\ideas\\' in pstr or '/ai-idea/' in pstr:
        scores['20_notes'] += 0.8
        reasons.append('path:ideas')
    if '/asmr/' in pstr:
        # ASMRは媒体タグ相当。研究 or notes に寄せる
        if urls >= 3:
            scores['10_research'] += 0.6
        else:
            scores['20_notes'] += 0.6
        reasons.append('path:asmr')

    # Decide best
    best = max(scores.items(), key=lambda kv: kv[1])
    target = best[0]
    confidence = best[1]
    return target, confidence, ','.join(reasons)


def main():
    TASKS.mkdir(parents=True, exist_ok=True)
    if not MK.exists():
        print(f"MK not found: {MK}")
        return
    files = sorted(MK.rglob('*.md'))
    rows = []
    for f in files:
        rel = f.relative_to(ROOT)
        # Skip templates, attachments, archive
        low = str(rel).lower()
        if any(seg in low for seg in ['70_templates/', '60_attachments/', '90_archive/', '70_templates\\', '60_attachments\\', '90_archive\\']):
            continue
        # Skip dashboard and plugin data
        if '/.obsidian/' in low or '\\.obsidian\\' in low:
            continue
        text = read_text_best_effort(f)
        yaml_block, _ = extract_yaml(text)
        target, conf, reason = score_classification(f, text, yaml_block)

        # Already in a target folder?
        cur_folder = f.parent
        cur_key = None
        for key in ['00_inbox','10_research','20_notes','30_projects','40_prompts','50_code']:
            if cur_folder.parts[-1] == key:
                cur_key = key
                break
        action = 'move'
        if cur_key and cur_key == target:
            action = 'keep'

        rows.append({
            'file': str(rel),
            'current_folder': cur_key or '',
            'proposed_folder': target,
            'action': action,
            'confidence': f"{conf:.2f}",
            'reasons': reason,
        })

    with OUT_CSV.open('w', newline='', encoding='utf-8') as fp:
        writer = csv.DictWriter(fp, fieldnames=['file','current_folder','proposed_folder','action','confidence','reasons'])
        writer.writeheader()
        writer.writerows(rows)
    print(f"Wrote proposals: {OUT_CSV} ({len(rows)} files)")


if __name__ == '__main__':
    main()
