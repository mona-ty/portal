# 新規アプリ作成ワークフロー（自動化）

このモノレポに新しいアプリを追加するためのスキャフォールドを用意しています。

## スクリプト
- `scripts/new-app.ps1`
  - 種別ごとのテンプレートから `apps/<app-slug>/` を生成します
  - プレースホルダ（名前/説明/パッケージ名など）を埋め込みます

## テンプレート
- Python CLI: `templates/python-cli/`
  - `package/`（後に `__PKG_NAME__` にリネーム）
  - `README.md.tmpl`, `pyproject.toml.tmpl`, `tests/test_smoke.py.tmpl`
- iOS SwiftUI: `templates/ios-swiftui/`
  - `iOSApp/`（空フォルダの雛形）、`README.md.tmpl`

## 使い方（例）

Python CLI の雛形:
```powershell
pwsh scripts/new-app.ps1 -Kind python-cli -Name "image-tool" -Description "画像処理CLI" -WithTests
```
- 生成: `apps/image-tool/`
- パッケージ名: `image_tool`（自動）
- 実行: `python -m image_tool --help`
- テスト: `python -m unittest apps/image-tool/tests -v`

iOS SwiftUI の雛形:
```powershell
pwsh scripts/new-app.ps1 -Kind ios-swiftui -Name "LiftMeter" -Description "リフト計測のiOSアプリ"
```
- 生成: `apps/liftmeter/`
- 次手順: Xcode で新規プロジェクトを作成し、`apps/liftmeter/iOSApp/` を追加

## オプション
- `-Package`: Python のパッケージ名（省略時は `app-slug` を `_` に）
- `-WithTests`: Python のテンプレに最小テストを同梱
- `-Commit`: 生成後に `git add` とコミットを実施
- `-DryRun`: 実際には生成せず、作成対象のみ表示

## 運用のヒント
- README は生成直後に最小情報（要件/セットアップ/使い方）を埋める
- Python は将来的に `pyproject.toml` を使って配布可能に（テンプレ同梱）
- iOS は Xcode でターゲット/Capabilities を必要に応じて追加
- テストは各アプリ配下に近接配置し、CI はルートから `unittest discover` で検出します

