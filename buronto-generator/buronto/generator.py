from __future__ import annotations

import random
from dataclasses import dataclass
from typing import List, Callable

from . import data as D


@dataclass
class Config:
    seed: int | None = None
    strength: float = 0.6  # how aggressively to add ブロント語風味 [0..1]


class Generator:
    def __init__(self, config: Config | None = None):
        self.config = config or Config()
        if self.config.seed is not None:
            random.seed(self.config.seed)

    def choice(self, xs: List[str]) -> str:
        return random.choice(xs)

    def maybe(self, p: float) -> bool:
        return random.random() < p

    def add_tail(self, s: str) -> str:
        # Randomly append ブロント語尾
        if self.maybe(0.5 * self.config.strength):
            s = s.rstrip("。") + (" " if self.maybe(0.4) else "") + self.choice(D.TAILS)
        return s

    def dotdot(self, s: str) -> str:
        # Sprinkle ellipsis
        if self.maybe(0.5 * self.config.strength):
            s = s + self.choice(D.ELLIPSIS)
        return s

    def katakana_emphasis(self, word: str) -> str:
        if self.maybe(0.5 * self.config.strength):
            return f"{word}（）"  # プロ（） 風
        return word

    def bronto_greeting(self) -> str:
        g = self.choice(D.GREETINGS)
        if g.startswith("おい") and self.maybe(0.7):
            g = g.replace("い", "ィ")  # おいィ
        return g

    def post_process(self, s: str) -> str:
        # Random lightweight quirks
        if self.maybe(0.35 * self.config.strength):
            s = s.replace("です", "ですが?")
        if self.maybe(0.25 * self.config.strength):
            s = s.replace("ます", "ますが?")
        return s

    def pattern1(self) -> str:
        target = self.choice(D.TARGETS)
        action = self.choice(D.ACTIONS)
        selfp = self.choice(D.SELF_PRONOUNS)
        power = self.choice(D.POWERS)
        result = self.choice(D.RESULTS)
        s = f"{self.bronto_greeting()} {target}で{action}とか正気か? まあ{selfp}が{power}すれば{result}なんだが?"
        return s

    def pattern2(self) -> str:
        selfp = self.choice(D.SELF_PRONOUNS)
        role = self.katakana_emphasis(self.choice(D.ROLES))
        assertion = self.choice(D.CLAIMS)
        s = f"{selfp}は{role}だから{assertion}だが?"
        return self.dotdot(s)

    def pattern3(self) -> str:
        skill = self.choice(D.SKILLS)
        warn = self.choice(D.WARNINGS)
        s = f"{skill}使える{self.choice(D.SELF_PRONOUNS)}の動き見せるが? {warn}"
        return s

    def pattern4(self) -> str:
        s = f"{self.choice(D.ADVERBS)} {self.choice(D.CLAIMS)}。{self.choice(D.FILLERS)}"
        return self.add_tail(s)

    def pattern5(self) -> str:
        s = f"{self.choice(D.IMPERATIVES)}。{self.choice(D.POSTFIXES)}"
        return s

    def pattern6(self) -> str:
        s = f"{self.choice(D.TARGETS)}は{self.choice(D.EVALUATIONS)}。{self.choice(D.CONTRASTS)} {self.choice(D.SELF_PRONOUNS)}なら{self.choice(D.BOASTS)}だが?"
        return s

    def pattern7(self) -> str:
        s = f"この{self.choice(D.ITEMS)}は{self.choice(D.RARITIES)}だから{self.choice(D.VERBS)}しておいたが?"
        return self.dotdot(s)

    def pattern8(self) -> str:
        s = f"{self.choice(D.EMOJIS)} {self.choice(D.ENGLISH)}の{self.choice(D.KATAKANA)}理解してから来いよな?"
        return s

    def pattern9(self) -> str:
        s = f"ミスったら{self.choice(D.EXCUSES)}。{self.choice(D.POLITE)}"
        return self.dotdot(s)

    def pattern10(self) -> str:
        s = f"正直{self.choice(D.GAME_TERMS)}は{self.choice(D.OPINIONS)}。でも{self.choice(D.SELF_PRONOUNS)}は{self.choice(D.DIFFERENTS)}だが?"
        return s

    def generate_one(self) -> str:
        patterns: List[Callable[[], str]] = [
            self.pattern1, self.pattern2, self.pattern3, self.pattern4, self.pattern5,
            self.pattern6, self.pattern7, self.pattern8, self.pattern9, self.pattern10,
        ]
        s = random.choice(patterns)()
        return self.post_process(s)


def generate(n: int = 5, seed: int | None = None, strength: float = 0.6) -> List[str]:
    gen = Generator(Config(seed=seed, strength=strength))
    return [gen.generate_one() for _ in range(n)]

