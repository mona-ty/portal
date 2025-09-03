# Pomodoro CLI（ポモドーロ・タイマー）

シンプルなポモドーロ・タイマーの CLI ツールです。作業/休憩/サイクルを指定して、端末上でタイマーを回します。

## 要件
- Python 3.10+

## 使い方
```powershell
# 例: 作業25分 休憩5分 4サイクル
python -m pomodoro_cli --work 25 --break 5 --cycles 4

# 動作確認（短時間）
python -m pomodoro_cli --work 3 --break 2 --cycles 1 --fast
```

## オプション
- `--work`   作業時間（分） 既定: 25
- `--break`  休憩時間（分） 既定: 5
- `--cycles` サイクル数      既定: 4
- `--fast`   1秒=1分の高速モード（確認用）

## 備考
- コンソールに進捗を表示し、区切りでベル音（\a）を鳴らします（環境により無音の場合あり）。
