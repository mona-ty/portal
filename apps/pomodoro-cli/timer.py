import argparse
import sys
import time
from dataclasses import dataclass


@dataclass
class PomodoroConfig:
    work_minutes: float
    break_minutes: float
    cycles: int
    fast_mode: bool


def format_mm_ss(seconds: int) -> str:
    minutes = seconds // 60
    secs = seconds % 60
    return f"{int(minutes):02d}:{int(secs):02d}"


def countdown(total_seconds: int, label: str) -> None:
    start_time = time.time()
    end_time = start_time + total_seconds
    last_display = None
    try:
        while True:
            now = time.time()
            remaining = int(max(0, round(end_time - now)))
            if remaining != last_display:
                print(f"\r{label} 残り {format_mm_ss(remaining)}", end="", flush=True)
                last_display = remaining
            if now >= end_time:
                break
            time.sleep(0.1)
    finally:
        print()  # newline


def beep(times: int = 2) -> None:
    for _ in range(times):
        # Console bell (may be disabled in some terminals)
        print("\a", end="", flush=True)
        time.sleep(0.15)


def run_pomodoro(config: PomodoroConfig) -> int:
    scale = 1 if not config.fast_mode else 1  # interpret minutes as seconds in fast mode below

    for cycle_index in range(1, config.cycles + 1):
        print(f"=== サイクル {cycle_index}/{config.cycles} ===")

        work_seconds = int(config.work_minutes * (60 if not config.fast_mode else 1) * scale)
        countdown(work_seconds, "作業")
        beep(2)

        # 最終サイクルでは休憩をスキップ
        if cycle_index == config.cycles:
            break

        break_seconds = int(config.break_minutes * (60 if not config.fast_mode else 1) * scale)
        countdown(break_seconds, "休憩")
        beep(1)

    print("完了！おつかれさまです。")
    return 0


def parse_args(argv: list[str]) -> PomodoroConfig:
    parser = argparse.ArgumentParser(description="シンプルなPomodoroタイマー")
    parser.add_argument("--work", type=float, default=25, help="作業時間(分)")
    parser.add_argument("--break", dest="break_", type=float, default=5, help="休憩時間(分)")
    parser.add_argument("--cycles", type=int, default=4, help="サイクル数")
    parser.add_argument("--fast", action="store_true", help="高速モード(分を秒として扱う)")
    args = parser.parse_args(argv)
    return PomodoroConfig(
        work_minutes=args.work,
        break_minutes=args.break_,
        cycles=args.cycles,
        fast_mode=args.fast,
    )


def main(argv: list[str]) -> int:
    config = parse_args(argv)
    return run_pomodoro(config)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))




