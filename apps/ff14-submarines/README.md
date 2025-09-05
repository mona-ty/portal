# FF14 サブマリン 帰還予定 OCR + Googleカレンダー登録ツール

FF14 の潜水艦（サブマリン）の帰還予定を画面から OCR で読み取り、Google カレンダーに予定を作成/更新するツールです。Windows 前提（画面キャプチャ/ホットキー実装のため）。

## 前提
- OS: Windows 10/11
- Python: 3.10+
- 依存ツール: Tesseract OCR（日本語データ含む）
- Google API: Calendar API 有効化 + OAuth クライアント（`credentials.json`）

## セットアップ
1) Tesseract をインストール
   - 推奨: https://github.com/UB-Mannheim/tesseract/wiki
   - 既定パス例: `C:\\Program Files\\Tesseract-OCR\\tesseract.exe`
2) Google Calendar API を有効化し、OAuth クライアントを作成
   - `credentials.json` を本プロジェクト直下に配置（初回認可で `data/token.json` が作成されます）
3) 依存をインストール
```powershell
python -m venv .venv
.venv\\Scripts\\activate
pip install -r requirements.txt
```

## 使い方
- 自動セットアップ（推奨: 初回）
```powershell
python -m ff14_submarines --auto-setup
```
  - 数分かけて画面キャプチャ/OCR の疎通と暫定設定を行い、`data/config.json` を生成します。

- 対話セットアップ（微調整）
```powershell
python -m ff14_submarines --setup
```
  - 画面上のキャプチャ領域・サイズ・閾値などを対話で調整できます。

- 通常起動（定期 OCR → カレンダー反映）
```powershell
python -m ff14_submarines
```
  - 既定では 5 分毎に OCR → 最短の ETA を Google カレンダーへ作成/更新します。

- ホットキー（Windows 専用: Ctrl+Alt+S）
```powershell
python -m ff14_submarines --hotkey
```
  - ホットキー押下時に 1 回だけ OCR→反映します。

## 設定 `data/config.json`
- `region`: 画面上のキャプチャ領域（`x,y,width,height`）
- `tesseract_path`: `tesseract.exe` のフルパス（PATH 済みなら省略可）
- `tesseract_lang`: 例 `jpn`（精度が必要なら `jpn+eng`）
- `capture_interval_sec`: ポーリング間隔（既定 300）
- `calendar_id`: 反映先カレンダー ID（例 `primary`）
- `reminder_minutes`, `event_duration_minutes`, `min_minutes_threshold_update`
- OCR 前処理: `enable_preprocess`, `preprocess_scale`, `preprocess_threshold`, `preprocess_sharpen`
- Tesseract 詳細: `tesseract_psm`, `tesseract_oem`

## トラブルシューティング
- OCR が弱い: `tesseract_lang` を `jpn+eng`、前処理の閾値やスケールを調整
- Tesseract が見つからない: `tesseract_path` を正しいパスへ
- 低 DPI/フォント問題: FF14/Windows のスケーリング設定を見直す
- 認可に失敗: `data/token.json` を削除して再認可

## 免責
本ツールは個人利用向けの実験的実装です。利用は自己責任でお願いします。
