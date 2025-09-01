FF14 潜水艦 返還時刻キャプチャ & Googleカレンダー自動登録ツール

概要
- 画面OCRで潜水艦の「帰還までの残り時間」を読み取り、Googleカレンダーに予定を自動作成・更新します。
- 普段どおりプレイしていて、潜水艦リストを一瞬開いたタイミングで自動キャリブレーション（領域検出）できます。

主な機能
- 画面キャプチャ（mss）→ OCR（Tesseract）→ テキスト解析（残り時間）
- Googleカレンダー予定の作成/更新（タイトル一致＋時間近傍）
- 自動セットアップ（放置検出）とグローバルホットキー（Ctrl+Alt+S）による即時再キャリブレーション

対応環境
- OS: Windows 推奨（ホットキー、ネイティブヘルパーはWindows専用）
- Python: 3.10 以上
- 画面表示: ボーダーレス/ウィンドウ表示を推奨（独占フルスクリーンはキャプチャ不可のことがあります）

前提ソフトウェア
1) Tesseract OCR 本体 + 日本語データ（jpn）
   - Windowsインストーラ: https://github.com/UB-Mannheim/tesseract/wiki
   - 既定パス例: `C:\\Program Files\\Tesseract-OCR\\tesseract.exe`
2) Google Calendar API の認可設定（OAuth クライアントID）
   - Cloud Console で API有効化 → 認証情報作成 → `credentials.json` を本プロジェクト直下に配置

インストール
```
python -m venv .venv
.venv\\Scripts\\activate
pip install -r requirements.txt
```

初期設定（どちらか）
- 自動セットアップ（ビルド不要のPythonフォールバック内蔵）
```
python -m ff14_submarines --auto-setup
```
  - 最大5分間バックグラウンドで検出（20秒間隔）。潜水艦リストが一瞬でも表示されれば矩形を推定し `data/config.json` に保存します。
  - 一度保存後は `python -m ff14_submarines` で通常運用できます。

- 手動セットアップ（オーバーレイで矩形選択）
```
python -m ff14_submarines --setup
```
  - 画面全体に半透明オーバーレイと赤い矩形が出ます。ドラッグ/リサイズで範囲を調整し、Enterで確定（Escで中止）。

運用（監視の開始）
```
python -m ff14_submarines
```
- 既定では5分ごとに指定範囲をキャプチャ→OCR→残り時間を解析し、予定を作成/更新します。
- ログ: `data/logs/app.log`

グローバルホットキー（任意）
```
python -m ff14_submarines --hotkey
```
- 監視中でも Ctrl+Alt+S で即時の自動検出を実行します。
- ネイティブヘルパー（後述）があればそれを優先、無ければPythonフォールバックで約60秒監視して検出します。

（オプション）ネイティブCヘルパーのビルド（Windows）
- MSVC（Developer Command Prompt）
```
cl /O2 native/auto_setup.c /Fe:native/auto_setup.exe
```
- MinGW
```
gcc -municode -O2 native/auto_setup.c -o native/auto_setup.exe -lgdi32 -luser32
```
- ビルド済みの場合、`--auto-setup` や `--hotkey` の検出が高速・確実になります。

設定ファイル `data/config.json`（主な項目）
- `region`: 画面キャプチャ領域（x,y,width,height）
- `tesseract_path`: Tesseract実行ファイルのパス（未設定でもPATHに通っていればOK）
- `tesseract_lang`: 既定は `jpn`
- `capture_interval_sec`: キャプチャ間隔（既定: 300秒）
- `calendar_id`: 予定登録するカレンダー（既定: `primary`）
- `reminder_minutes`: 通知（分）
- `event_duration_minutes`: 予定の長さ（分）
- `min_minutes_threshold_update`: 前回との差がこの分以上なら更新（既定: 2）

Google 認可/トークン
- 初回の予定作成時にブラウザで認可フローが開きます。
- トークンは `data/token.json` に保存され、次回以降は自動更新されます。

トラブルシューティング
- OCRが不安定: キャプチャ範囲を少し狭め、文字だけが含まれるよう調整。`tesseract_lang` に `jpn+eng` を指定すると “Rank” が拾いやすい場合があります。
- Tesseractが見つからない: `data/config.json` の `tesseract_path` を実環境のパスに設定。
- Google認可に失敗: `credentials.json` の配置を確認。権限スコープは `https://www.googleapis.com/auth/calendar` を使用。
- フルスクリーンでキャプチャ不可: ボーダーレス/ウィンドウ表示へ変更。
- ホットキーが効かない: Windows以外では未対応。Windowsでも権限や他ツールの競合で登録に失敗する場合があります（ログ参照）。

ライセンス/注意
- 本ツールは非公式です。ゲームの利用規約に反しない範囲で自己責任にてご利用ください。

