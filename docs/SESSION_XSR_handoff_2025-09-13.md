# セッション引き継ぎメモ — XSR 1.1.0 リリース・実装サマリ（2025-09-13）

## 概要（今回の到達点）
- 目的: UI文字列に依存しない「メモリ優先（Memory-First）」でのフル航路復元を安定化し、UI/通知運用を改善。
- 成果: ルート復元の堅牢化（位相/lenhdr/TTL/キャッシュ）、Debug/Alarm UIの使い勝手改善、Discordの重複通知抑止、ドキュメント整備、v1.1.0 リリース＋GitHub反映。

## 実装・変更点（詳細）

### 1) ルート復元（Memory-First）
- 採用ロジック統合（AdoptBest）
  - ルール: Complete > Partial > Tail（実装では長さ優先）、同長は新鮮さ。
  - TTL/Confidence 導入: Full/Array を TTL=12h 保持（降格禁止）。`Character|World|Slot` 単位で永続化。
  - 最終採用は mem/cache を一元比較、採用トレースを `adopt-trace` ログで出力。
- スキャナ拡張（構造体/Addon周辺）
  - stride={1,2,4} に加え、位相ずらし（要素内バイト位置）、lenhdr（先頭1byte=長さ 3..5）を追加。
  - `MemoryRouteZeroTerminated` トグル（0 終端を break/skip 切替）。
  - 候補ごとに `phase`（window/full/lenhdr/phaseS.P や array-window/array-lenhdr）を付与。
  - tail 包含（forward/reversed、連続部分列/末尾一致）を必須。
- Addon可視直後の遅延キャプチャ
  - 可視化 2F/10F 後に `TryAdoptFromAddonArrays`（AtkUnitBase 周辺8192Bを探索）→ 成功時は source=array 採用。
  - 文字列は取得せず、数値列に相当する領域のみスキャン。

### 2) 診断/運用コマンド（追加）
- `/xsr memscan [slot]` — 候補をフェーズ・スコア付きで表示（チャット要約＋ログ cand[]）
- `/xsr memdump <slot> [offHex] [len]` — 構造体近傍の安全Hexダンプを出力
- `/xsr setroute <slot> <chain>`／`/xsr getroute [slot]` — ルートキャッシュの手動シード/確認

### 3) UI 改善
- Debugタブ（apps/XIVSubmarinesReturn/src/UI/DebugTab.cs）
  - 固定幅2カラム（各 360px）＋固定高さChild（420px, スクロール）で縦横の伸びを抑制。
  - 右カラムの「詳細設定」は CollapsingHeader（開閉可能）。閉じてもレイアウトは崩れない。
- アラームタブ（apps/XIVSubmarinesReturn/src/UI/AlarmTab.cs）
  - 「外部通知」中間カードを削除し、Discord/Notion を個別カードに。
  - 各セクション見出しを淡いグレーのヘッダーバー（Widgets.SectionHeaderBar）で視覚的に区切り。

### 4) Discord 通知の重複抑止
- 新設定: `DiscordMinIntervalMinutes`（既定 10 分）。
- Snapshot 通知は「差分あり」かつ「前回送信から最小間隔経過」の場合のみ送信。

### 5) ログ・可観測性
- `route recovered: off=..., stride=..., reversed=..., memTail=[..], full=[..]`（復元ログ）
- `adopt-trace mem=[..], cache=[..], final=[..], adopted=... , reason=..., phase=..., off=0x.., stride=.., reversed=.., conf=.., ttl=..`
- memscan cand[] に `phase` を付与。array 採用時にもトレース出力。

## 追加/変更された設定キー（Configuration.cs）
- 取得/スキャン: `MemoryRouteScanWindowBytes`, `MemoryRouteZeroTerminated`, `MemoryScanPhaseEnabled`
- ゲート/イベント: `EnableTerritoryGate`, `EnableAddonGate`, `PreferArrayDataFirst`, `EnableAddonLifecycleCapture`
- 採用/TTL: `AdoptPreferLonger`, `AdoptAllowDowngrade`, `AdoptTtlHours(12)`, `AdoptCachePersist`
- Discord: `DiscordMinIntervalMinutes`

## ドキュメント/バージョン/リリース
- CHANGELOG: apps/XIVSubmarinesReturn/CHANGELOG.md に [1.1.0] を追記（今回の変更点を要約）
- README: コマンド/設定/動作要点を Memory-First 方針に合わせて更新
- バージョン: csproj/manifest/repo.json を 1.1.0 に更新（repo.json の LastUpdate も最新化）

## リポジトリ反映
- モノレポ main へマージ済み。タグ v1.1.0 作成。
- サブツリー push（apps/XIVSubmarinesReturn → 単独リポジトリ）
  - 反映先: https://github.com/mona-ty/XIVSubmarinesReturn （main）
  - コミット: 34db9c4435e234be4bdb5d6fe7aa9c4746d0ba78（タグ v1.1.0）

## 影響ファイル（主要）
- ルート復元/採用: `src/Plugin.cs`, `src/Models.cs`, `src/CharacterProfile.cs`
- UI: `src/UI/DebugTab.cs`, `src/UI/AlarmTab.cs`, `src/UI/Widgets.cs`, `src/UI/Theme.cs`
- サービス: `src/Services/AlarmScheduler.cs`, `src/Services/DiscordNotifier.cs`
- 設定/メタ: `src/Configuration.cs`, `manifest.json`, `repo.json`, `XIVSubmarinesReturn.csproj`
- ドキュメント: `docs/XSR_memory-first_fix_plan_2025-09-13.md`, `docs/review/XSR_memory_first_ai_review_2025-09-13.md`, `apps/.../CHANGELOG.md`, `apps/.../README.md`

## 検証手順（抜粋）
1) 工房で `/xsr dump` 実行 → `submarines.json` に 3–5点の `RouteKey`、ログに `route recovered` / `adopt-trace` が出力されること。
2) `/xsr memscan` 実行 → チャットに `best phase=...` 表示、ログの cand[] に phase/score/offset が記録されること。
3) `/xsr setroute <slot> ...` → TTL 12h 内は降格しない（`adopt-trace` の reason=ttl を確認）。
4) Discord: スナップショット差分がある場合のみ送信され、`DiscordMinIntervalMinutes` 未満の連続変化では送信されないこと。
5) UI: Debugタブが2カラム固定/スクロールで表示され、アラームタブのセクション見出しがグレー帯で区切られていること。

## 既知の注意点/今後（提案）
- AddonLifecycle の正式購読
  - v13 の IAddonLifecycle による `RegisterListener(PostSetup/Refresh/Pre/PostRequestedUpdate)` を導入し、ArrayData 採用をイベント直下で実行（現状は可視化遅延キャプチャで代替）。
- スキャナの固定長レイアウト派生の精緻化
  - lenhdr 以外のプレフィックス形式（例: 先頭2byte=長さ/種別）への対応を検討。
- ログ構造の最適化
  - `adopt-trace` を JSON Lines 出力にし、解析/可視化を容易化。
- さらにUI圧縮（必要に応じて）
  - Debugタブの列幅/Child高さを設定化（例: 320px/380px）またはラベル短縮版トグルを追加。
- ビルド/配布（DalamudPackager）
  - `-p:MakeZip=true` で `latest.zip` を作成し、XSR 単独リポジトリの GitHub Releases に添付。

## メモ（参考ログ抜粋）
- `route recovered: off=0x42, stride=1, reversed=False, memTail=[13,18,15,10,26], full=[13,18,15,10,26]`
- `adopt-trace mem=[..], cache=[..], final=[..], adopted=cache, reason=ttl, phase=window, off=0x42, stride=1, reversed=False, conf=Full, ttl=active`

---
更新者: 自動化エージェント（Codex CLI）
