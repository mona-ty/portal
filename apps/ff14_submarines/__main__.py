from __future__ import annotations

import argparse
import sys
from pathlib import Path
from typing import Optional

try:
    from PIL import Image  # type: ignore
except Exception:  # pragma: no cover
    Image = None  # type: ignore

try:
    import pytesseract  # type: ignore
except Exception:  # pragma: no cover
    pytesseract = None  # type: ignore


def _read_image_text(path: Path) -> str:
    if pytesseract is None or Image is None:
        raise RuntimeError("pytesseract/Pillow not available. Install requirements.")
    img = Image.open(path)
    # Japanese + English, single uniform block tends to work for lists
    return pytesseract.image_to_string(img, lang="jpn+eng")


def _maybe_toast(title: str, msg: str) -> None:  # pragma: no cover
    try:
        from win10toast import ToastNotifier  # type: ignore
        ToastNotifier().show_toast(title, msg, duration=5, threaded=True)
    except Exception:
        pass


def _maybe_discord(webhook: Optional[str], content: str) -> None:  # pragma: no cover
    if not webhook:
        return
    try:
        import requests  # type: ignore

        requests.post(webhook, json={"content": content}, timeout=5)
    except Exception:
        pass


def cmd_import(image_path: str, notify: bool = False, discord_webhook: Optional[str] = None) -> int:
    from ff14_submarines.ocr import extract_submarine_etas
    p = Path(image_path)
    if not p.exists():
        print(f"File not found: {p}", file=sys.stderr)
        return 2
    text = _read_image_text(p)
    etas = extract_submarine_etas(text)
    if not etas:
        print("No submarines detected.")
        return 1
    lines = []
    for e in etas:
        line = f"{e.name}: {e.remaining_minutes} min -> {e.eta.isoformat()}"
        print(line)
        lines.append(line)
    if notify:
        _maybe_toast("FF14 Submarines", lines[0])
        _maybe_discord(discord_webhook, "\n".join(lines))
    return 0


def cmd_watch(folder: str, pattern: str = "*.png", notify: bool = False, discord_webhook: Optional[str] = None) -> int:  # pragma: no cover
    try:
        from watchdog.observers import Observer  # type: ignore
        from watchdog.events import PatternMatchingEventHandler  # type: ignore
    except Exception:
        print("watchdog not available. Install requirements.", file=sys.stderr)
        return 2

    from ff14_submarines.ocr import extract_submarine_etas

    root = Path(folder)
    if not root.exists():
        print(f"Folder not found: {root}", file=sys.stderr)
        return 2

    class Handler(PatternMatchingEventHandler):
        def __init__(self) -> None:
            super().__init__(patterns=[pattern], ignore_directories=True)

        def on_created(self, event):
            try:
                text = _read_image_text(Path(event.src_path))
                etas = extract_submarine_etas(text)
                if etas:
                    print(f"[+] {Path(event.src_path).name}")
                    lines = []
                    for e in etas:
                        line = f"  {e.name}: {e.remaining_minutes} min -> {e.eta.isoformat()}"
                        print(line)
                        lines.append(line)
                    if notify:
                        _maybe_toast("FF14 Submarines", lines[0])
                        _maybe_discord(discord_webhook, "\n".join(lines))
                else:
                    print(f"[ ] {Path(event.src_path).name}: no entries")
            except Exception as ex:
                print(f"[!] failed to process {event.src_path}: {ex}", file=sys.stderr)

    observer = Observer()
    observer.schedule(Handler(), str(root), recursive=False)
    observer.start()
    print(f"Watching {root} for {pattern} ... (Ctrl+C to stop)")
    try:
        while True:
            observer.join(1)
    except KeyboardInterrupt:
        observer.stop()
    observer.join()
    return 0


def build_parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(prog="ff14_submarines", description="FF14 submarine OCR tools")
    sub = p.add_subparsers(dest="cmd", required=True)

    p_imp = sub.add_parser("import", help="OCR a still image and print ETAs")
    p_imp.add_argument("image", help="Path to image (png/jpg)")
    p_imp.add_argument("--notify", action="store_true", help="Show a Windows toast and/or Discord message")
    p_imp.add_argument("--discord-webhook", dest="discord_webhook", help="Discord webhook URL")

    p_watch = sub.add_parser("watch", help="Watch a folder for images and OCR them")
    p_watch.add_argument("folder", help="Folder to watch")
    p_watch.add_argument("--pattern", default="*.png", help="Glob pattern (default: *.png)")
    p_watch.add_argument("--notify", action="store_true", help="Show a Windows toast and/or Discord message")
    p_watch.add_argument("--discord-webhook", dest="discord_webhook", help="Discord webhook URL")

    return p


def main(argv: Optional[list[str]] = None) -> int:
    args = build_parser().parse_args(argv)
    if args.cmd == "import":
        return cmd_import(args.image, args.notify, args.discord_webhook)
    if args.cmd == "watch":
        return cmd_watch(args.folder, args.pattern, args.notify, args.discord_webhook)
    return 0


if __name__ == "__main__":  # pragma: no cover
    raise SystemExit(main())
