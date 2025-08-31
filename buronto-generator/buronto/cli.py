from __future__ import annotations

import argparse
from .generator import generate


def main() -> None:
    p = argparse.ArgumentParser(description="Generate ブロント語風の架空発言")
    p.add_argument("-n", "--count", type=int, default=5, help="生成する行数")
    p.add_argument("--seed", type=int, default=None, help="乱数シード")
    p.add_argument("--strength", type=float, default=0.6, help="ブロント語風味(0..1)")
    args = p.parse_args()

    lines = generate(n=args.count, seed=args.seed, strength=args.strength)
    for line in lines:
        print(line)


if __name__ == "__main__":
    main()

