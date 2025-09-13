# Repository Guidelines

## プロジェクト構成 & モジュール
- `apps/` 各アプリ（例: `pomodoro-cli/`, `liftlog-ios/`, `ff14-submarines/`）
- `scripts/` 開発補助（PowerShell/Python/Shell）、`tools/` ユーティリティ
- `tests/` Python `unittest`、ドキュメント/素材は `docs/`, `templates/`, `samples/`, `notes/`, `archive/`
- 生成物: `bin/`, `obj/`, `.vs/`、Python 仮想環境: `.venv`
- Windows 起点ランチャー: `scripts/win/Start-WSLAndCodex.ps1` → WSL `scripts/wsl/codex-dev.sh`

## ビルド・実行・テスト
- 依存関係: `pnpm install`（`package.json` を参照）
- Python 仮想環境: `python -m venv .venv && . .venv/bin/activate`（Win: ` .\\.venv\\Scripts\\activate`）
- テスト実行: `python -m unittest -v`（`tests/test_*.py` を検出）
- Dalamud プラグイン雛形: `just new <Name>`
- リリース補助: `scripts/release_xsr.sh` など。各アプリの README/コメントに従う

## コーディング規約・命名
- 改行/文字コードは `.editorconfig` に従う（CRLF, UTF‑8 BOM）
- Python: PEP 8、4スペース、`snake_case`（モジュール/関数）
- JS/TS: 2スペース、末尾セミコロン、`camelCase`/`PascalCase`
- C#: .NET 規約（型/メソッド `PascalCase`、変数 `camelCase`）
- ファイル/ディレクトリ: 原則 `kebab-case`、テストは `test_*.py`

## テスト指針
- 主要分岐とエラー経路をカバー。外部 API（例: OpenAI）はモック/スタブ化
- 秘密鍵は環境変数や `.env` で注入。コミット禁止
- 例: `python -m unittest tests/test_example.py -v`

## コミット & PR ガイド
- Conventional Commits: `feat:`, `fix:`, `docs:`, `ci:`, `chore:`
- 1コミット1論点。要約は簡潔に、本文で背景/影響範囲を説明
- PR: 目的/変更点/テスト方法/影響範囲/関連Issue。UI変更はスクリーンショット
- マージ条件: テスト緑、セルフレビュー済、不要ファイルなし

## セキュリティ & 設定 / エージェント向け
- 秘密情報はコミット禁止。`.gitignore` を遵守。破壊的変更は `README`/`CHANGELOG` に移行手順を追記
- 設定は環境変数を優先し、必要に応じて `config.toml` を利用
- エージェントは破壊的変更の前に確認し、要点先出し・箇条書きで報告。詳細ポリシーは [docs/AGENT_POLICY.md](docs/AGENT_POLICY.md) に従う

## エージェント方針（必須遵守）
- 言語/トーン: 日本語で簡潔・丁寧。要点先出し・箇条書き。不要な思考過程は出力しない。
- あいまいさ: 不明点は短く確認してから実行。
- 進め方: 単純作業は即対応。複雑作業は短い計画を提示し、進捗を簡潔に更新。
- 破壊的変更: 事前に理由と代替案を提示して同意を得るまで実行しない。
- ツール/ネットワーク: 必要最小限のみ利用。秘密情報は外部へ送信しない。
- 推論/表示: 内部では高精度で検討し、思考過程は非表示。
- オプション: 可能なら MCP `context7` を補助に使用。長時間処理後は Windows 通知音を任意で使用可（例: `powershell -NoProfile -Command "[System.Media.SystemSounds]::Exclamation.Play()"`）。
- スコープ: 本 AGENTS.md はリポジトリ全体に適用。詳細は原典の [docs/AGENT_POLICY.md](docs/AGENT_POLICY.md) を参照（本節は必須遵守の要約）。
