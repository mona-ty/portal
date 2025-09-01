from __future__ import annotations

import argparse
from .generator import generate


def main() -> None:
    p = argparse.ArgumentParser(description="ブロント語風の文を生成します")
    p.add_argument("-n", "--count", type=int, default=5, help="生成件数")
    p.add_argument("--seed", type=int, default=None, help="乱数シード")
    p.add_argument("--strength", type=float, default=0.6, help="口調のクセの強さ(0..1)")
    # トグル（デフォルトON）。Python3.9+なら BooleanOptionalAction が使えるが、互換のため --no-xxx を用意。
    p.add_argument("--no-tail", action="store_true", help="語尾付与を無効化")
    p.add_argument("--no-dots", action="store_true", help="三点リーダを無効化")
    p.add_argument("--no-polite", action="store_true", help="です/ます疑問化を無効化")
    args = p.parse_args()

    lines = generate(
        n=args.count,
        seed=args.seed,
        strength=args.strength,
        tail=not args.no_tail,
        dots=not args.no_dots,
        polite=not args.no_polite,
    )
    for line in lines:
        print(line)


if __name__ == "__main__":
    main()
