# XSR — メモリ優先フル航路復元 改修計画（実装方針）

## 目的 / スコープ
- 目的: UI文字列に依存せず、メモリのみでフル航路（3–5点）を安定復元。短い値（例: 15,10）への降格を防止。
- スコープ: 取得トリガー最適化、採用ロジックの一元化（AdoptBest）、スキャナ堅牢化、デバッグ/運用コマンド、設定/ログ整備。
- 非スコープ: UI文字列の解析。UIはタイミング検出のみ使用。

## 既定 / 合意事項
- UI文字列禁止の範囲: ルート取得のみ（UIライフサイクルはトリガーに使用可）。
- TTL既定: 12h（Full 取得後はTTL内の降格を禁止）。
- 取得タイミング: AddonLifecycle（PostSetup/Refresh/ReceiveEvent）＋1–2フレーム遅延で実行。
- メモリアンカー: IGameGui(Agent/Addon)＋SigScanner で安定化可。

## 改修方針（全体像）
1) 採用ロジックの安全化（AdoptBest）
- ルール: Complete > Partial > Tail、長い方優先、同長は新鮮さ優先。
- TTL/Confidenceを導入（Full=12h保護、TTL内は降格不可）。
- mem/cache/UI（UIは無効でも将来互換）を共通スコアで採用決定。

2) 取得タイミングの最適化（UI非依存）
- AddonLifecycle購読（対象パネルの PostSetup/Refresh/ReceiveEvent）→ 1–2F遅延で1回限りキャプチャ。
- IGameGui.GetAddonByName/AgentById でスロット/構造体への到達性を向上（文字列は読まない）。
- SigScanner/Resolverで Manager/Agent の基点を解決し、パッチ差分に耐性を持たせる。

3) スキャナの堅牢化
- 現行 stride(1/2/4)に加え、位相（要素内バイト位置: 2byte高位、4byte第2/第3バイト等）を試行。
- 0終端前提のトグル化（固定長/長さヘッダ/区切り子推定を段階的に試行）。
- 近傍±Window→全域→位相→最小長2（可視化）と段階的フォールバック。Top-N候補・重複抑止。

4) デバッグ/運用コマンド
- `/xsr memdump [slot] [off] [len]`：境界安全Hexダンプ（既定: tail近傍±0x200）。
- `/xsr setroute <slot> <chain>`／`/xsr getroute [slot]`：手動シード/確認で運用安定化。

5) 設定 / ログ
- 設定追加: EnableAddonLifecycleCapture, AdoptTtlHours(12), AdoptPreferLonger, AdoptAllowDowngrade(false),
  MemoryRouteZeroTerminated(true), MemoryScanPhaseEnabled(true)。
- ログ強化: 採用時に `adopted, reason, score, len, confidence, ttl, phase` を出力。memscanのフェーズ（window/full/min2/phaseN）も記録。

## 追記（再レビュー反映）

### ゲート条件（無駄スキャン抑止）
- EnableTerritoryGate=true: 工房テリトリー内（`IClientState.TerritoryType` 判定）のみ取得処理を実行。
- EnableAddonGate=true: 対象アドオンがロード済みかつ可視のときのみキャプチャフェーズを起動（候補名は複数・部分一致許容）。

### RequestedUpdate/ArrayData 優先
- AddonLifecycle の Pre/PostRequestedUpdate を監視し、ArrayData（数値配列）が取得できた場合は第一候補として採用し、構造体スキャンをスキップ。
- ログの source を `array` として明示。

### reversed の必須化／スコア明文化
- Tail 包含条件は forward または reversed の連続部分列/末尾一致を必須。
- Score = Tail一致×W1 + 長さ×W2 + 近傍性×W3 + 妥当値(rank/ETA)×W4。
- 段階フォールバック順: array → ±Window → full-struct → 位相（要素内バイト）→ minLen=2（可視化）。

### 降格防止の永続化（TTL 12h）
- LastGoodRouteStore を導入し、Character|World|Slot をキーにディスク保存。Full は TTL(12h) 内は降格不可（再起動後も保持）。
- 設定追加: AdoptCachePersist=true, AdoptCacheTtlHours=12。

### ログ/診断の強化
- memscan: `phase=array|window|full|phaseN`, `reversed=true/false` を出力。
- 採用ログ: `adopted=cache/mem/array, reason=score(ttl/confidence), len, reversed, off, stride, phase` を出力。
- memdump はアドオン可視時のみ許可（事故防止）。

### 追加設定キー（一括）
- EnableTerritoryGate=true, EnableAddonGate=true
- AdoptCachePersist=true, AdoptCacheTtlHours=12
- PreferArrayDataFirst=true

## 受け入れ条件（追記）
- 工房テリトリー内 ＆ 対象アドオン可視のときのみ各フェーズを実行。
- RequestedUpdate(ArrayData) 直読で復元が成功した場合は source=array で採用し、スキャンは省略。
- 永続キャッシュが有効で、 TTL(12h) 内は降格しない（再起動後も保持）。

## 変更点（ファイル別）
- `apps/XIVSubmarinesReturn/src/Plugin.cs`
  - 取得/採用フロー再構成（TryCaptureFromMemory → AdoptBest → TTL更新 → JSON）。
  - AddonLifecycle購読と遅延キャプチャの導入、Agent/Addonアンカー参照。
  - スキャナ拡張（位相/固定長/ゼロ終端トグル、段階フォールバック、Top-N重複抑止）。
  - 新コマンド: memdump, setroute, getroute。ログ詳細出力。
- `apps/XIVSubmarinesReturn/src/Configuration.cs`
  - 追加キー（上記設定）。
- `apps/XIVSubmarinesReturn/src/Services/XsrDebug.cs`
  - 構造化キー出力の補助（タグ/phase/score等）。
- `apps/XIVSubmarinesReturn/src/Plugin.UI.cs`（任意）
  - 設定トグル/TTLの露出、「メモリから取得（推奨）」表記、診断ガイド。

## 実装ステップ（順序）
1. AdoptBest＋TTL/Confidence 実装（降格防止の即効対策）
2. AddonLifecycle 連動（1–2F遅延キャプチャ）とアンカー整備
3. スキャナ位相拡張＋段階フォールバック強化
4. コマンド拡充（memdump/setroute/getroute）
5. 設定/UI/ログ整備と微調整

## 受け入れ条件
- `/xsr dump`（Memory Only）で、Full獲得後はTTL(12h)内に降格しない（tailのみでも保持）。
- Addon表示直後のキャプチャで `route recovered ... full=[...]` 出力率が向上。
- `/xsr memscan` でフェーズ表示と上位候補が安定出力（候補0でもmin2/phase可視化が機能）。
- `/xsr setroute` により手動フルルートを注入→TTL内は降格しない。
- UI文字列はルート取得に未使用（ライフサイクル検出のみ）。

## リスク / 対策
- 誤検出: tail必須・位相/近傍加点・Top-N制限・rank/eta妥当性で抑制。
- パッチ耐性: SigScanner/Resolver＋段階フォールバックで確保。
- パフォーマンス: イベント駆動で最小化（フレーム常時スキャンはしない）。

## 参考 / 関連資料
- 設計・レビュー詳細: `docs/review/XSR_memory_first_ai_review_2025-09-13.md`
- 既存実装要約: `docs/XSR_memory_route_fix_2025-09-13.md`
