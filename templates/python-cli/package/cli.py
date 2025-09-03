"""
Simple CLI entrypoint for __APP_NAME__.
"""
import argparse


def build_parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(prog="__APP_SLUG__", description="__DESCRIPTION__")
    p.add_argument("--name", default="world", help="name to greet")
    return p


def main(argv=None) -> int:
    args = build_parser().parse_args(argv)
    print(f"Hello, {args.name}!")
    return 0

