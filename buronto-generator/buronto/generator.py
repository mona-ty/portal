from __future__ import annotations

import random
from dataclasses import dataclass
from typing import List, Callable

from . import data as D


@dataclass
class Config:
    seed: int | None = None
    strength: float = 0.6  # how aggressively to add quirks [0..1]


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
        # 文末に口調の「だよ？」等を付ける
        if self.maybe(0.5 * self.config.strength):
            s = s.rstrip("。.") + (" " if self.maybe(0.4) else "") + self.choice(D.TAILS)
        return s

    def dotdot(self, s: str) -> str:
        # 省略三点を付ける
        if self.maybe(0.5 * self.config.strength):
            s = s + self.choice(D.ELLIPSIS)
        return s

    def katakana_emphasis(self, word: str) -> str:
        if self.maybe(0.5 * self.config.strength):
            return f"《{word}》"
        return word

    def bronto_greeting(self) -> str:
        g = self.choice(D.GREETINGS)
        return g

    def post_process(self, s: str) -> str:
        # です/ますを疑問調にする等の微調整
        if self.maybe(0.35 * self.config.strength):
            s = s.replace("です。", "ですか？").replace("です", "ですか？")
        if self.maybe(0.25 * self.config.strength):
            s = s.replace("ます。", "ますか？").replace("ます", "ますか？")
        return s

    def pattern1(self) -> str:
        target = self.choice(D.TARGETS)
        action = self.choice(D.ACTIONS)
        selfp = self.choice(D.SELF_PRONOUNS)
        power = self.choice(D.POWERS)
        result = self.choice(D.RESULTS)
        s = f"{self.bronto_greeting()} {target}を{action}と思うけど？ まあ{selfp}の{power}があれば{result}だったけどな？"
        return s

    def pattern2(self) -> str:
        selfp = self.choice(D.SELF_PRONOUNS)
        role = self.katakana_emphasis(self.choice(D.ROLES))
        assertion = self.choice(D.CLAIMS)
        s = f"{selfp}は{role}だから{assertion}ってことで？"
        return self.dotdot(s)

    def pattern3(self) -> str:
        skill = self.choice(D.SKILLS)
        warn = self.choice(D.WARNINGS)
        s = f"{skill}使った{self.choice(D.SELF_PRONOUNS)}の判断は正しいよな？ {warn}"
        return s

    def pattern4(self) -> str:
        s = f"{self.choice(D.ADVERBS)} {self.choice(D.CLAIMS)}。{self.choice(D.FILLERS)}"
        return self.add_tail(s)

    def pattern5(self) -> str:
        s = f"{self.choice(D.IMPERATIVES)}。{self.choice(D.POSTFIXES)}"
        return s

    def pattern6(self) -> str:
        s = f"{self.choice(D.TARGETS)}は{self.choice(D.EVALUATIONS)}。{self.choice(D.CONTRASTS)} {self.choice(D.SELF_PRONOUNS)}なら{self.choice(D.BOASTS)}だが？"
        return s

    def pattern7(self) -> str:
        s = f"最近{self.choice(D.ITEMS)}の{self.choice(D.RARITIES)}を{self.choice(D.VERBS)}って聞いたけど？"
        return self.dotdot(s)

    def pattern8(self) -> str:
        s = f"{self.choice(D.EMOJIS)} {self.choice(D.ENGLISH)}の{self.choice(D.KATAKANA)}が決まった気がする？"
        return s

    def pattern9(self) -> str:
        s = f"ミスったのは{self.choice(D.EXCUSES)}。{self.choice(D.POLITE)}"
        return self.dotdot(s)

    def pattern10(self) -> str:
        s = f"次は{self.choice(D.GAME_TERMS)}が{self.choice(D.OPINIONS)}。つまり{self.choice(D.SELF_PRONOUNS)}は{self.choice(D.DIFFERENTS)}ってこと？"
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

