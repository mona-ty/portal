# 開発ハンドブック（Apps Lab Monorepo）

このドキュメントは、モノレポ環境で個人開発を行う際の手順・規約・運用の勘所をまとめたものです。常にここに立ち返れば、迷わず進められます。

## 0. 前提
- OS: Windows 10/11（PowerShell）
- Git / GitHub CLI: `git`, `gh`
- Python: 3.10+（仮想環境は `.venv` 推奨）
- iOS: Xcode 15+（必要時）

## 1. リポの基本構成
- `apps/…` 小アプリの集約（Python/iOS など）
- `scripts/` 共通スクリプト（新規アプリ生成、分割など）
- `templates/` 生成用テンプレート
- `tests/` 共有テスト（将来はアプリ近接へ移行）
- `docs/` ドキュメント
- `archived/` 退避した資材
- `.github/workflows/` CI（Python ユニットテスト実行）

## 2. ローカル初期化
```powershell
# 取得
git clone <this-repo>
cd <repo>

# （任意）仮想環境
python -m venv .venv
.venv\Scripts\activate

# 依存（例: ff14-submarines）
pip install -r apps\ff14-submarines\requirements.txt

# テスト
python -m unittest discover -v
```

## 3. 新規アプリ作成（自動化）
- スクリプト: `scripts/new-app.ps1`
- 詳細: `docs/new_app.md`

例：Python CLI を作成
```powershell
pwsh scripts/new-app.ps1 -Kind python-cli -Name "image-tool" -Description "画像処理CLI" -WithTests -Commit
```
- 生成先: `apps/image-tool/`
- 実行: `python -m image_tool --help`
- テスト: `python -m unittest apps/image-tool/tests -v`

例：iOS SwiftUI を作成
```powershell
pwsh scripts/new-app.ps1 -Kind ios-swiftui -Name "LiftMeter" -Description "リフト計測のiOSアプリ" -Commit
```
- 生成先: `apps/liftmeter/`
- 次手順: Xcode で新規プロジェクトを作成し、`apps/liftmeter/iOSApp/` を追加

## 4. 日々の開発フロー（推奨）
1) ブランチを切る: `git checkout -b feat/<topic>`
2) 変更を小さく積む（README と最小テストを同時に）
3) コミット規約（セマンティック）: `feat/fix/chore/docs/test/refactor`
4) PR → CI でテスト確認 → マージ
5) 不要になったアプリや資材は `archived/` へ移動

## 5. アプリの昇格（独立リポ化）
- スクリプト: `scripts/split_repos.ps1`
- 前提: `gh auth login` 済み、`repos` 定義で `apps/<name>` の `path` が正しいこと
- 実行例（dry-run 相当の確認を事前に）:
```powershell
# 実行前に repos 配列内容と path の存在を確認
pwsh scripts/split_repos.ps1 -Owner <github-user> -Visibility public
```
- 完了後: 新リポを目視確認、必要ならこのモノレポの README からリンク

## 6. ff14-submarines（例）
- 依存: `apps/ff14-submarines/requirements.txt`
- 実行（対話セットアップ）:
```powershell
python -m ff14_submarines --setup
```
- テスト: `python -m unittest tests/test_ocr.py -v`

## 7. iOS（SwiftUI）
- このモノレポにはコードスケルトンのみを格納
- Xcode プロジェクトは必要時に生成し、`apps/<app>/iOSApp/` を取り込む
- Localizations/Widgets/AppIntents は必要に応じてターゲットに追加

## 8. 外部ワークスペース（Option B）
- 例: `C:\codex-work\<repo-name>\`
- 推奨サブフォルダ: `sessions/`, `prompts/`, `snippets/`, `scratch/`, `exports/`
- 成果のみをリポへ反映。資格情報やトークンは置かない

## 9. 規約・運用の小技
- 文字コード: UTF-8（日本語 README は必ず UTF-8）
- 秘密情報: `.env.local` などに分離し `.gitignore`
- 依存更新: 年1回棚卸し、脆弱性対応は都度
- 命名: ディレクトリは `kebab-case`、Python パッケージは `snake_case`
- テスト方針: アプリ近接配置（将来）、ルートから `unittest discover`

## 10. よくあるエラー
- `ModuleNotFoundError`: 仮想環境が未有効 or 依存未導入 → `.venv` 有効化後 `pip install -r ...`
- 文字化け: VS Code のエンコードを `UTF-8` に切替 → 再保存
- Windows パス: PowerShell では `\` をエスケープ

---
必要に応じて、Lint/カバレッジ/複数 Python バージョンの CI 追加や、Node/Go のテンプレ拡張にも対応可能です。
