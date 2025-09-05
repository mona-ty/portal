# NEXT_XSR_PLAN.md — XIVSubmarinesReturn Next Tasks (for Codex)

Baseline:
- GoogleCalendarClient の骨子実装（Refresh/Upsert）あり、Asia/Tokyo 指定。ID は "xsr-" + SHA-256 hex。:contentReference[oaicite:12]{index=12}
- Discord 通知はテキスト複数投稿、429 リトライあり。
- AlarmScheduler で “Latest=最速” を採用。
- 抽出・整形・差分書出しは導通済み。

Goal of this sprint:
1) **GCal 完了**（id 仕様順守・冪等アップサート）  
2) **Discord 集約**（Embed 1投稿、429尊重）  
3) **UIの文言統一**（Latest only = Earliest）  
4) **デバッグロギング**（配布OFF/開発ON、UIトグル＋Open trace）  
5) **JP表記の残り時間強化**（「1日2時間30分15秒」「45秒」など）

---

## EPIC B2 — Google Calendar 完了（仕様準拠・冪等）
**Edits**: `Services/GoogleCalendarClient.cs`  
- **event.id を仕様適合**: Googleの規定は *base32hex*（小文字a–v/0–9、長さ5–1024）なので、  
  既存の `"xsr-" + hex` を **prefixなし** の **lowercase hex**（= a–f/0–9のみ、仕様集合の部分集合）に変更。  
  → 例: `id = HexSha256(World|Character|Name|EtaUnix)` （**ハイフン/- と x 含有を回避**）。 :contentReference[oaicite:16]{index=16}
- 401 時は **Refresh→一回だけ再送**。409（重複）時は **update(PATCH)** に切替。:contentReference[oaicite:17]{index=17}
- `start/end` は RFC3339 で `timeZone="Asia/Tokyo"` を明示。:contentReference[oaicite:18]{index=18}

**DoD**
- 同一艦・同一 ETA で重複イベントが増えない（再実行しても1件）。  
- UI の「Test Google」でトークン検証 OK → 実イベント作成も動作確認。

## EPIC B1+ — Discord 集約（Embed 1 POST）
**Edits**: `Services/DiscordNotifier.cs`, `Configuration.cs`, `Plugin.UI.cs`  
- `Configuration.DiscordUseEmbeds`（default true）を追加し UI トグル。
- Snapshot 通知は **1回の POST**：  
  - Embeds 有効時: `embeds[0].title="Submarines"`, `fields` で艦別（<=25）。  
  - 無効時: `content` に艦行を結合し 1 投稿。  
- 429 は `Retry-After` を尊重して 1 回再送（既存ロジック流用/拡張）。:contentReference[oaicite:20]{index=20}

**DoD**
- Snapshot 実行で **常に1メッセージ**。`Latest only` 有効時は **最速1件**のみ。

## EPIC B3 — UI 表記統一（Latest only = Earliest）
**Edits**: `Plugin.UI.cs`  
- Google/Discord どちらのセクションも **「Earliest only (ETA min) / 最新のみ=最速」** に統一。

## EPIC C2 — Debug Logging（配布OFF/開発ON）
**Edits**: `Configuration.cs`, `Plugin.UI.cs`, `XsrDebug.cs`  
- `Configuration.DebugLogging` を追加（既存UIから利用されているためビルド修正）。
- `XsrDebug.Log()` を全体で呼びやすいように補助関数を整備し、`bridge/xsr_debug.log` へ出力。
- UI に「Open trace」を残す。

**DoD**
- 例外が debug ログへ落ちる（配布時は OFF 推奨）。

## EPIC A4+ — JP表記の残り時間パース補強
**Edits**: `Extractors.cs`  
- 追加で **JP の “日/秒”** に対応（例: `1日2時間30分15秒` → 1510分、`45秒` → 1分）。  
- 既存の **秒→1分切り上げ** と一貫。

**DoD**
- `/xsr selftest` のフィクスチャで PASS（report 出力）。

---

## Housekeeping
- WriteIfChanged を**常に**呼ぶ実装側を基準にし、重複 `Plugin.cs` があれば統一。

