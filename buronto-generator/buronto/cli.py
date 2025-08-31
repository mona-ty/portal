from __future__ import annotations

import argparse
from .generator import generate


def main() -> None:
    p = argparse.ArgumentParser(description="ブロント語風の文を生成します")
    p.add_argument("-n", "--count", type=int, default=5, help="生成件数")
    p.add_argument("--seed", type=int, default=None, help="乱数シード")
    p.add_argument("--strength", type=float, default=0.6, help="口調のクセの強さ(0..1)")
    args = p.parse_args()

    lines = generate(n=args.count, seed=args.seed, strength=args.strength)
    for line in lines:
        print(line)


if __name__ == "__main__":
    main()
