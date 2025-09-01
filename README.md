Monorepo Overview

このリポジトリは複数の小プロジェクトをひとつにまとめたモノレポ構成です。用途ごとに `apps/` と `tools/` に再配置しました。

構成
- apps/ff14-submarines: FF14 潜水艦の帰還時間キャプチャ & Googleカレンダー登録ツール（Python）。従来のルートREADMEと `requirements.txt` はここへ移動しました。
- apps/todo-app: シンプルなToDoアプリ（HTML/JS/CSS）。
- tools/bronto-generator: 文体（句読点や表記ゆれ）の整形・生成ツール（Python + Web UI）。テストは `tests/` にあります。
- docs: 共有ドキュメント・サイト用ファイル。

クイックスタート
- FF14 ツール: `apps/ff14-submarines/README.md` を参照。セットアップと実行方法を記載しています。
- ToDo アプリ: `apps/todo-app/index.html` をブラウザで開くと動作します。
- bronto-generator: `pytest` でテスト実行。CLI や Web の使い方は `tools/bronto-generator/README.md` を参照。

開発のヒント
- Python 依存は各アプリ配下に分離しています。仮想環境はアプリごとに作成してください。
- CI はリポジトリ直下から `pytest` を実行しても `tools/bronto-generator/tests` を検出します。

