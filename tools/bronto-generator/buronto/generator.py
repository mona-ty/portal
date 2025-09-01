from __future__ import annotations

import random
from dataclasses import dataclass
from typing import List, Callable

from . import data as D


@dataclass
class Config:
    seed: int | None = None
    strength: float = 0.6  # how aggressively to add quirks [0..1]
    tail: bool = True
    dots: bool = True
    polite: bool = True


class Generator:
    def __init__(self, config: Config | None = None):
        self.config = config or Config()
        self.rng = random.Random(self.config.seed)
        self._style_tags: set[str] = set()

    def choice(self, xs: List[str]) -> str:
        return self.rng.choice(xs)

    def maybe(self, p: float) -> bool:
        return self.rng.random() < p

    def add_tail(self, s: str) -> str:
        if self.config.tail and self.maybe(0.5 * self.config.strength):
            req = ["polite"] if "polite" in self._style_tags else []
            exc = ["polite"] if "casual" in self._style_tags else []
            tail = D.pick("TAILS", self.rng, require_tags=req, exclude_tags=exc)
            s = s.rstrip("。.") + (" " if self.maybe(0.4) else "") + tail
        return s

    def dotdot(self, s: str) -> str:
        if self.config.dots and self.maybe(0.5 * self.config.strength):
            s = s + D.pick("ELLIPSIS", self.rng)
        return s

    def katakana_emphasis(self, word: str) -> str:
        if self.maybe(0.5 * self.config.strength):
            return f"《{word}》"
        return word

    def bronto_greeting(self) -> str:
        g = D.pick("GREETINGS", self.rng)
        self._style_tags.update(D.get_tags("GREETINGS", g))
        return g

    def post_process(self, s: str) -> str:
        if self.config.polite:
            if self.maybe(0.35 * self.config.strength):
                s = s.replace("です。", "ですか？").replace("です", "ですか？")
            if self.maybe(0.25 * self.config.strength):
                s = s.replace("ます。", "ますか？").replace("ます", "ますか？")
        s = (
            s.replace("?", "？").replace("!", "！")
            .replace("。。", "。")
            .replace("、、", "、")
            .replace("？？", "？")
            .replace("！！", "！")
        )
        while "………" in s:
            s = s.replace("………", "……")
        s = s.replace(" ？", "？").replace(" ！", "！").replace(" 。", "。").replace(" 、", "、")
        s = " ".join(s.split())
        if not any(s.endswith(t) for t in D.TAILS) and not s.endswith(("？", "！", "。")):
            s += "。"
        return s

    def acceptable(self, s: str) -> bool:
        if not (12 <= len(s) <= 80):
            return False
        banned = ["。。", "、、", "！？！？", "？？？"]
        if any(b in s for b in banned):
            return False
        total = sum(c.isalnum() for c in s)
        if total:
            kata = sum("ァ" <= c <= "ン" or c == "ー" for c in s)
            if kata / max(1, total) > 0.75:
                return False
        return True

    def pattern1(self) -> str:
        target = D.pick("TARGETS", self.rng)
        action = D.pick("ACTIONS", self.rng)
        selfp = D.pick("SELF_PRONOUNS", self.rng)
        self._style_tags.update(D.get_tags("SELF_PRONOUNS", selfp))
        power = D.pick("POWERS", self.rng)
        result = D.pick("RESULTS", self.rng)
        s = f"{self.bronto_greeting()} {target}を{action}と思うけど？ まあ{selfp}の{power}があれば{result}だったけどな？"
        return s

    def pattern2(self) -> str:
        selfp = D.pick("SELF_PRONOUNS", self.rng)
        self._style_tags.update(D.get_tags("SELF_PRONOUNS", selfp))
        role = self.katakana_emphasis(D.pick("ROLES", self.rng))
        assertion = D.pick("CLAIMS", self.rng)
        s = f"{selfp}は{role}だから{assertion}ってことで？"
        return self.dotdot(s)

    def pattern3(self) -> str:
        skill = D.pick("SKILLS", self.rng)
        warn = D.pick("WARNINGS", self.rng)
        sp = D.pick('SELF_PRONOUNS', self.rng)
        self._style_tags.update(D.get_tags("SELF_PRONOUNS", sp))
        s = f"{skill}使った{sp}の判断は正しいよな？ {warn}"
        return s

    def pattern4(self) -> str:
        s = f"{D.pick('ADVERBS', self.rng)} {D.pick('CLAIMS', self.rng)}。{D.pick('FILLERS', self.rng)}"
        return self.add_tail(s)

    def pattern5(self) -> str:
        req = ["polite"] if "polite" in self._style_tags else []
        exc = ["polite"] if "casual" in self._style_tags else []
        pf = D.pick('POSTFIXES', self.rng, require_tags=req, exclude_tags=exc)
        s = f"{D.pick('IMPERATIVES', self.rng)}。{pf}"
        return s

    def pattern6(self) -> str:
        s = f"{D.pick('TARGETS', self.rng)}は{D.pick('EVALUATIONS', self.rng)}。{D.pick('CONTRASTS', self.rng)} {D.pick('SELF_PRONOUNS', self.rng)}なら{D.pick('BOASTS', self.rng)}だが？"
        return s

    def pattern7(self) -> str:
        s = f"最近{D.pick('ITEMS', self.rng)}の{D.pick('RARITIES', self.rng)}を{D.pick('VERBS', self.rng)}って聞いたけど？"
        return self.dotdot(s)

    def pattern8(self) -> str:
        s = f"{D.pick('EMOJIS', self.rng)} {D.pick('ENGLISH', self.rng)}の{D.pick('KATAKANA', self.rng)}が決まった気がする？"
        return s

    def pattern9(self) -> str:
        s = f"ミスったのは{D.pick('EXCUSES', self.rng)}。{D.pick('POLITE', self.rng)}"
        return self.dotdot(s)

    def pattern10(self) -> str:
        s = f"次は{D.pick('GAME_TERMS', self.rng)}が{D.pick('OPINIONS', self.rng)}。つまり{D.pick('SELF_PRONOUNS', self.rng)}は{D.pick('DIFFERENTS', self.rng)}ってこと？"
        return s

    def generate_one(self) -> str:
        patterns: List[Callable[[], str]] = [
            self.pattern1, self.pattern2, self.pattern3, self.pattern4, self.pattern5,
            self.pattern6, self.pattern7, self.pattern8, self.pattern9, self.pattern10,
        ]
        self._style_tags = set()
        s = self.rng.choice(patterns)()
        return self.post_process(s)

    def generate_one_filtered(self, attempts: int = 7) -> str:
        last = ""
        for _ in range(max(1, attempts)):
            last = self.generate_one()
            if self.acceptable(last):
                return last
        return last


def generate(
    n: int = 5,
    seed: int | None = None,
    strength: float = 0.6,
    *,
    tail: bool = True,
    dots: bool = True,
    polite: bool = True,
) -> List[str]:
    gen = Generator(Config(seed=seed, strength=strength, tail=tail, dots=dots, polite=polite))
    out: List[str] = []
    seen = set()
    while len(out) < n:
        s = gen.generate_one_filtered()
        if s in seen:
            s = gen.generate_one_filtered()
        out.append(s)
        seen.add(s)
    return out
