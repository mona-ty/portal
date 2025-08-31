FF14 潜水艦 帰還時間キャプチャ & Googleカレンダー登録ツール

概要
- 画面OCRで潜水艦の残り時間を読み取り、帰還予定をGoogleカレンダーに自動登録します。
- ローカルで常駐。初回のみ設定（キャプチャ領域の指定とGoogle認証）が必要です。
- 潜水艦は最大4隻を想定。重複登録を避け、変更があればイベントを更新します。

主な機能
- オーバーレイでキャプチャ領域を指定（マウスでウィンドウを配置しEnter）。
- 指定領域を定期キャプチャ→Tesseractで日本語OCR→テキスト解析。
- 帰還予定時刻をGoogleカレンダーへ作成/更新（プライマリカレンダー）。
- ログ/キャッシュを`data/`に保存（領域設定`config.json`、Googleトークン`token.json`）。

前提条件
1) Python 3.10+（Windowsを想定）
2) Tesseract OCR 本体 + 日本語学習データ（`jpn.traineddata`）
   - Windows インストーラ: https://github.com/UB-Mannheim/tesseract/wiki
   - 既定パス例: `C:\\Program Files\\Tesseract-OCR\\tesseract.exe`
3) GoogleカレンダーAPIの認可情報（OAuth クライアントID、デスクトップ）
   - Cloud ConsoleでAPI有効化 → 認証情報作成 → `credentials.json` を本プロジェクト直下へ配置

インストール
```
python -m venv .venv
.venv\\Scripts\\activate
pip install -r requirements.txt
```

初回セットアップ手順
1) `credentials.json` を配置
2) Tesseract のパスを確認（必要なら `config.json` の `tesseract_path` を編集）
3) 起動してキャプチャ領域を設定
```
python -m ff14_submarines --setup
```
   - 透明なオーバーレイが出ます。潜水艦一覧パネルに重ねてサイズ/位置を調整し、Enterで確定。
4) 常駐実行
```
python -m ff14_submarines
```
   - 初回のみブラウザが開いてGoogle認証を求められます。

使い方のヒント
- キャプチャ間隔は既定5分。`config.json`の`capture_interval_sec`で調整。
- OCRが不安定な場合は領域を狭め、文字部分だけを含むように調整してください。
- 重複登録防止: タイトルと開始時刻が近いイベントは更新します（拡張プロパティにもID付与）。

トラブルシュート
- OCRで日本語が読めない → Tesseractの日本語データ（`jpn`）が入っているか確認。
- Tesseractが見つからない → `config.json` の `tesseract_path` を実インストールパスへ。
- Google認証が失敗 → `credentials.json` のクライアント種別が「デスクトップ」であるか確認。
- イベントが作成されない → `data/logs/app.log` を確認。

開発メモ
- 最低依存: `mss`, `pillow`, `pytesseract`, `google-api-python-client`, `google-auth-httplib2`, `google-auth-oauthlib`。
- Windows前提。Mac/Linuxも`mss`とTesseractがあれば動作見込み。

