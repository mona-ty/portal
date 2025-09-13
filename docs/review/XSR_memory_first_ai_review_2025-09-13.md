# XSR — メモリ優先（Memory-First）航路復元 実装レビュー（AI向け詳細） 2025-09-13

## 目的
- UI文字列に依存せず、メモリのみから潜水艦の「計画航路」（3–5点）を安定的に復元する。
- 末尾2点（例: 15,10）しか取得できない環境においても、フル航路を導出して JSON に反映する。

## 実装概要（現状）
- 主要ファイル
  - `apps/XIVSubmarinesReturn/src/Plugin.cs`
  - `apps/XIVSubmarinesReturn/src/Configuration.cs`
  - `apps/XIVSubmarinesReturn/src/Services/XsrDebug.cs`
- コンフィグ（Configuration.cs）
  - `MemoryOnlyMode`（bool, 既定=true）: UI追補を無効化しメモリのみで取得。
  - `MemoryRouteScanEnabled`（bool, 既定=true）: 構造体内スキャンで計画航路を復元。
  - `MemoryRouteMinCount`（int, 既定=3）/`MemoryRouteMaxCount`（int, 既定=5）: 候補の長さ制御。
  - `MemoryRouteScanWindowBytes`（int, 既定=0x120）: `CurrentExplorationPoints` 近傍の±バイト範囲。0で全域。
- メモリ取得（Plugin.cs）
  - `TryCaptureFromMemory(...)`
    - `HousingManager.Instance()->WorkshopTerritory` から `HousingWorkshopSubmersibleSubData` を各スロットで参照（`DataPointers[idx]` 優先、失敗時は構造体配列計算）。
    - 艦名（0x22付近, UTF-8想定）、ランク（`RankId`）、ETA（`ReturnTime`）を取得。
    - `CurrentExplorationPoints[0..4]` を「末尾列（tail）」として収集（例: [15,10]）。
    - `TryRecoverFullRouteFromMemory(...)` で計画航路の復元を試行。
    - 復元に成功すれば `RouteKey` に数値列（`Point-xx` 連結）を採用、失敗時は tail またはキャッシュを参照。
    - 採用ログ（`XsrDebug.Log`）を出力（mem/cache/final, route recovered など）。
  - `TryRecoverFullRouteFromMemory(...)`
    - `sizeof(HousingWorkshopSubmersibleSubData)` 範囲内を、stride={1,2,4} でスキャン。
    - 読み取りルール: `byte v = *(basePtr + off + k*stride)` を 0終端まで収集。1..255 以外は破棄。
    - 候補受理条件: 最小長（min=3）以上、tail（順/逆）が「連続部分列または末尾一致」で含まれること（tailが非空の場合）。
    - スコア: tail一致強度 + 長さ + 既知フィールド近傍（`CurrentExplorationPoints` からの距離による加点）。
    - 最良候補を採用し、`route recovered: off=0x.., stride=.., reversed=.., memTail=[..], full=[..]` を出力。
  - ルート可読化
    - `BuildRouteShortFromNumbers(...)` で、Alias/Mapヒント不在時も 1..26 → A..Z へフォールバック表示。
- 診断コマンド
  - `/xsr memscan [slot]`
    - tail を元に候補を走査し、上位3件をログ（`xsr_debug.log`）へ、最良候補をチャットへ要約出力。
    - 候補0件時: 全域スキャンに自動フォールバック。なお 0件継続時は min=2 での可視化スキャンを試行（チャット表記に`(min2)` を付記）。
- ビルド・配置
  - `Local.props` の `DalamudLibPath`/`DevPluginsDir` を利用しローカル環境でビルド→Dev Plugins へ自動コピー。
  - `/xsr version`/`/xsr help` のバージョン表記にビルド日時を付与。

## 検証ログ（要点）
- 取得航路
  - 多数の実行で `route bytes = 15,10` のみ（tail）。
  - 復元前のログでは誤検出とみられる配列（例: `[108,51,36,1,80]`）を採用していたが、末尾必須ガードにより抑止。
- memscan
  - 工房内で `/xsr memscan` 実行。
  - 結果: `candidates=0`（全域スキャンも0）、min=2 の可視化スキャンも 0 のケースあり。
  - 例（ユーザー提供）:
    - `[20:25:06] S1..S4 memscan: tail=[15,10] candidates=0 (full-scan also empty)`
- 出力（要パス）
  - JSON/ログ: `%AppData%\XIVSubmarinesReturn\bridge\submarines.json` / `xsr_debug.log`

## 問題点（現時点のギャップ）
- 構造体内の「連続バイト列（1..255, 0終端, 長さ3..5）」という仮定で、tail=[15,10] を包含する候補が検出できない。
- stride={1,2,4} でも不検出であり、単純な 8/16/32bit 低位バイト列ではない可能性が高い。
- 末尾必須・近傍加点のスコアリングは堅牢だが、母集合に目的データが載っていない（もしくは表現形式が異なる）場合は検出不能。

## 根本原因の仮説
- 表現形式の相違
  - 計画航路が `SubData` 内に「連続配列」として格納されていない（別領域/別構造体/圧縮/テーブル間接参照）。
  - 値が 1..255 の ID ではなく、0起算/別テーブル index/ビットフィールド/暗号化/圧縮などの表現。
  - 0 終端ではなく固定長/長さフィールド付き/区切り子（0 以外）を使用。
- 参照先の相違
  - `CurrentExplorationPoints` は「現在航路の尾部」を露出するが、計画航路は WorkshopTerritory 直下ではなく、別のマネージャ/ログ構造を参照している。
  - `DataPointers[idx]` が有効でも、計画航路はそこから更にポインタ先の別領域に存在。
- バージョン差分
  - クライアント/パッチにより構造が変化し、従来の近傍走査では範囲外。

## 修正方針（見直し案）
- 方向性A: メモリマップの再特定（構造的アプローチ）
  - `WorkshopTerritory` と近傍の関連構造（`CompanyCraft*`, `SubmersibleExploration*`）のフィールド相関を広域にダンプし、フィンガープリントを構築。
  - 値表現の仮説を多重化（0起算/±1/±128 マスク/16bitリトルエンディアン/インターリーブ等）し、tail の写像（f(x)=15,10）を含む候補を探索。
  - 「0終端前提」を外し、固定長/長さヘッダ/区切り子推定を導入（ただしスコア閾値と近傍加点でノイズ抑制）。
- 方向性B: 参照テーブル前提の解読（意味論的アプローチ）
  - 値列ではなく、参照テーブル（例: `ushort* routeTable` としての index 列）＋別領域に ID 実体、という想定で走査。
  - stride では「要素サイズ」と「要素中の有効バイト位置」を位相ずらしで試す（例: 2/4/8 byte要素 × 低位/高位バイト）。
- 方向性C: 代替復元（グラフ探索アプローチ）
  - Lumina の `SubmarineExploration` シートから到達可能グラフを生成し、tail（末尾2点）＋艦のランク/パーツ性能/ETAなどから、整合する5点列を探索（複数一致時は優先規則で一意化）。
  - 完全なメモリ直読ではないが、UI文字列を使わず静的データ＋動的値（ETA/ランク）で復元可能性を高める。
- 方向性D: 運用補助
  - `/xsr setroute <slot> <chain>` `/xsr getroute [slot]` を実装し、正しいフルルートを手動シード可能に（キャッシュ初期化）。
  - `/xsr memdump [slot]` を追加し、指定オフセット±範囲の境界安全なHexダンプを取得（フィールド特定の時短）。

## 推奨プラン（段階的）
- Phase 1（デバッグ機能強化）
  - `/xsr memdump [slot] [off hex] [len]` 実装（既定: `CurrentExplorationPoints` 近傍±0x200, 行折り/ASCII可視化）。
  - memscan の探索空間を「要素サイズ × 位相」へ拡張（例: 2byte要素の高位バイト読み、4byte要素の第2/第3バイト読みなど）。
  - 0終端ルールのトグル化（`MemoryRouteZeroTerminated`）と候補抽出の安定化（同候補の重複抑止）。
- Phase 2（意味論的復元）
  - 候補写像 `g(b) = b + k`（k∈{-1,0,1}）や `g16(lo,hi)=lo|hi<<8` を導入し、tail の一致を多様化。
  - 参照テーブル（小さな ID 配列）と値配列の二段構え探索（先にインデックス列→後から実値列の対応を検証）。
- Phase 3（代替復元）
  - Lumina グラフを用いた候補列の列挙→ETA/Rank/機体性能によるスコアリング→一意候補の採用。
  - メモリで得た tail は強制制約として利用（整合しない列は破棄）。

## リスクと方針整理
- メモリ表現の多様性により誤検出のリスクが増す → tail必須・近傍加点・候補上限・ログ監査で抑制。
- UI文字列は絶対不可 → 本レビューでは一切の文字列解析を除外。UI Addon 構造のメモリポインタ読み取りは「文字列解析なし」を条件に要検討（将来検討）。
- 代替復元（グラフ）は「完全メモリ直読」ではないが、UI依存ゼロで実現可能。静的データ依存のため再現性は高い。

## 追加で必要な入力（検証短縮のため）
- `memdump` による `WorkshopTerritory` 近傍の実ダンプ（機密が混入しない範囲）。
- 艦の Rank/パーツ構成/ETA（メモリから取得済のため JSON とログで足りる想定）。

## 付録A: 関連関数（抜粋・概要）
- `TryCaptureFromMemory`: SubData各スロットを走査し、tail取得→復元→キャッシュ統合→JSON化。
- `TryRecoverFullRouteFromMemory`: stride={1,2,4}×ウィンドウで 0終端の1..255配列（3..5点）を探索、tail必須でスコア採用。
- `ScanRouteCandidates`: `/xsr memscan` 用の候補収集（上位3件ログ/チャット要約）。全域/最小長2へのフォールバックを実装。
- `BuildRouteShortFromNumbers`: Alias/Mapヒント不在時、1..26→A..Z。その他は `P<n>`。
- `XsrDebug.Log`: `%AppData%\XIVSubmarinesReturn\bridge\xsr_debug.log` へ追記。

## 付録B: 主な設定キー
- `MemoryOnlyMode`（bool, default true）
- `MemoryRouteScanEnabled`（bool, default true）
- `MemoryRouteMinCount`（int, default 3）
- `MemoryRouteMaxCount`（int, default 5）
- `MemoryRouteScanWindowBytes`（int, default 0x120）

## 付録C: 既知のログ例（要点）
- `route bytes = 15,10`（tailのみ）
- `/xsr memscan`: `candidates=0`（全域/最小長2でも 0 のケースあり）
- 旧ログに存在した誤候補: `[108,51,36,1,80]`（現在は末尾必須で抑止）

