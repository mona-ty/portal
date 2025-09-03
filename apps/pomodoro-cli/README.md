# Pomodoro CLI (簡易版)

シンプルなPomodoroタイマー。作業/休憩/サイクル数を指定できます。

## 使い方

Python 3.10+ を想定。

```bash
# 例: 作業25分 休憩5分 4サイクル
python -m pomodoro_cli --work 25 --break 5 --cycles 4

# 高速モード(分を秒として扱う)での手早い確認
python -m pomodoro_cli --work 3 --break 2 --cycles 1 --fast
```

## オプション
- `--work`: 作業時間(分)。既定 25
- `--break`: 休憩時間(分)。既定 5
- `--cycles`: サイクル数。既定 4
- `--fast`: 分単位の値を秒として扱う高速モード

## 備考
- コンソールに秒単位で残り時間を表示します。
- 区切り時にベル音(\a)を鳴らします（環境により無音の場合あり）。


