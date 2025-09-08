# FF14 Submarines: CLI 拡張（試作）

依存（要 Tesseract + 日本語データ `jpn`）:
- `apps/ff14-submarines/requirements.txt` をインストール

## 使い方
- 静止画OCR:
  - `python -m ff14_submarines import <image.png> [--notify] [--discord-webhook <URL>]`
- フォルダ監視:
  - `python -m ff14_submarines watch <folder> [--pattern *.png] [--notify] [--discord-webhook <URL>]`

## 通知
- `--notify`: Windows トースト通知（win10toast）を使用
- `--discord-webhook`: URL を指定すると Discord に出力

## メモ
- OCR テキストの解析は全角数値・コロン・ブラケットを半角に正規化し、
  「1時間 49分」「49分」「３：０５」の各形式に対応。重複は同名の最短のみ。
- 返却は最短順で最大 4 件。
