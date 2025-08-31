from __future__ import annotations

import re
from buronto.generator import generate, Generator, Config


def test_determinism_same_seed():
    a = generate(n=10, seed=123, strength=0.8)
    b = generate(n=10, seed=123, strength=0.8)
    assert a == b


def test_determinism_diff_seed():
    a = generate(n=10, seed=1, strength=0.8)
    b = generate(n=10, seed=2, strength=0.8)
    assert a != b  # different RNG should differ most of the time


def test_flags_disable_quirks():
    # With flags off, outputs must not contain ellipsis symbols nor common tails
    lines = generate(n=50, seed=42, strength=1.0, tail=False, dots=False, polite=False)
    joined = "\n".join(lines)
    assert "…" not in joined
    # common tails in our data.json
    for t in ["だよ？", "だな？", "だろ？", "ってこと？"]:
        assert t not in joined


def test_post_process_only_when_enabled():
    g = Generator(Config(seed=0, strength=1.0, polite=True, dots=False, tail=False))
    s = g.post_process("これはです。ます。")
    assert "ですか？" in s and "ますか？" in s
    g2 = Generator(Config(seed=0, strength=1.0, polite=False, dots=False, tail=False))
    s2 = g2.post_process("これはです。ます。")
    assert "ですか？" not in s2 and "ますか？" not in s2

