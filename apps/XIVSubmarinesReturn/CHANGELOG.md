# Changelog — XIVSubmarinesReturn (XSR)

このプロジェクトの重要な変更は本ファイルで管理します。
- 形式: Keep a Changelog 準拠（簡略）
- バージョニング: Semantic Versioning に準拠

## [Unreleased]
- Added:
- Changed:
- Fixed:
- Docs:

## [1.1.0] - 2025-09-13
### Added
- Memory-first route recovery 強化（位相/lenhdr/zero-terminatedトグル、ArrayData採用の基盤）
- 採用ロジックに TTL/Confidence を導入（Fullは12h保持）
- 診断コマンド: `/xsr memscan`, `/xsr memdump`, `/xsr setroute`, `/xsr getroute`
- Discord 通知の最小間隔（`DiscordMinIntervalMinutes`、既定10分）

### Changed
- Debugタブを固定幅2カラム＋固定高さに刷新（スクロール化で縦伸びを抑制）
- アラームタブのセクション見出しを淡いグレーのバーに変更
- 「外部通知」中間カードを削除し、Discord/Notionを個別に明示

### Fixed
- 連続取得でのDiscord重複通知の抑制（差分検出＋最小間隔）

### Docs
- メモリ優先の実装計画・レビューを docs/ に追加
