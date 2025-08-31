from __future__ import annotations

import json
from importlib.resources import files
from typing import Iterable, List, Dict, Any


def _normalize_list(lst: Iterable[Any]) -> List[Dict[str, Any]]:
    out = []
    for item in lst:
        if isinstance(item, str):
            out.append({"word": item, "weight": 1.0, "tags": []})
        elif isinstance(item, dict):
            out.append({
                "word": item.get("word", ""),
                "weight": float(item.get("weight", 1.0)),
                "tags": list(item.get("tags", [])),
                "onlyWith": list(item.get("onlyWith", [])),
                "notWith": list(item.get("notWith", [])),
            })
        else:
            raise TypeError(f"Unsupported item in data.json: {item!r}")
    return out


_RAW = json.loads(files(__package__).joinpath("data.json").read_text(encoding="utf-8"))
BANKS: Dict[str, List[Dict[str, Any]]] = {k: _normalize_list(v) for k, v in _RAW.items()}


def pick(name: str, rng, require_tags: Iterable[str] | None = None, exclude_tags: Iterable[str] | None = None) -> str:
    bank = BANKS[name]
    req = set(require_tags or [])
    exc = set(exclude_tags or [])
    candidates = [e for e in bank if req.issubset(e.get("tags", [])) and exc.isdisjoint(e.get("tags", []))]
    if not candidates:
        candidates = bank
    weights = [max(0.0, float(e.get("weight", 1.0))) for e in candidates]
    tot = sum(weights) or len(candidates)
    r = rng.random() * tot
    acc = 0.0
    for e, w in zip(candidates, weights):
        acc += (w if tot != len(candidates) else 1.0)
        if r <= acc:
            return e["word"]
    return candidates[-1]["word"]


# Backward-compat: plain word lists
def _words(key: str) -> List[str]:
    return [e["word"] for e in BANKS[key]]


TARGETS = _words("TARGETS")
ACTIONS = _words("ACTIONS")
SELF_PRONOUNS = _words("SELF_PRONOUNS")
ROLES = _words("ROLES")
POWERS = _words("POWERS")
RESULTS = _words("RESULTS")
GREETINGS = _words("GREETINGS")
SKILLS = _words("SKILLS")
WARNINGS = _words("WARNINGS")
ADVERBS = _words("ADVERBS")
CLAIMS = _words("CLAIMS")
FILLERS = _words("FILLERS")
IMPERATIVES = _words("IMPERATIVES")
POSTFIXES = _words("POSTFIXES")
EVALUATIONS = _words("EVALUATIONS")
CONTRASTS = _words("CONTRASTS")
BOASTS = _words("BOASTS")
ITEMS = _words("ITEMS")
RARITIES = _words("RARITIES")
VERBS = _words("VERBS")
EMOJIS = _words("EMOJIS")
ENGLISH = _words("ENGLISH")
KATAKANA = _words("KATAKANA")
EXCUSES = _words("EXCUSES")
POLITE = _words("POLITE")
GAME_TERMS = _words("GAME_TERMS")
OPINIONS = _words("OPINIONS")
DIFFERENTS = _words("DIFFERENTS")
TAILS = _words("TAILS")
ELLIPSIS = _words("ELLIPSIS")
