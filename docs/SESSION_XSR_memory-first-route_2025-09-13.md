# セッション引き継ぎメモ — XIV Submarines Return（XSR）メモリ優先ルート復元対応 2025-09-13

## 背景 / 課題
- 現象: 航路が末尾2点（例: 15,10 → O>J）しか取得できず、フル航路にならない。
- 影響バージョン: 1.0.4 → 1.0.6（段階的なUI抽出強化後も再現）。
- エビデンス:
  - `xsr_debug.log`: `S# route bytes = 15,10` が継続。UI由来の航路採用ログなし。
  - `stage_dump.txt`: SelectString系画面で艦名/航路テキストが見えない（分割描画 or 非表示）。
- 仮説: UIは単一行テキストとして航路を露出しない／トークン分割。メモリ上の `CurrentExplorationPoints` は末尾のみ。

## 方針決定（本セッション）
- UI依存は避け、メモリのみで完結する「計画航路」復元を優先（Memory-First）。
- 既存のキャッシュ採用（suffix/連続部分列）を継続利用し、初回復元後は安定化。
- UI追補（パネル走査）は既定で無効化（必要時のみ opt-in）。

## 変更概要（実装）
- Memory Only モードの導入（既定 ON）
  - `/xsr dump`・自動取得はメモリ直読を優先し、UI追補/事後リペアは抑止（`MemoryOnlyMode=false` で従来動作）。
- 構造体内スキャンで“計画航路”を復元
  - `HousingWorkshopSubmersibleSubData` の範囲内のみ走査し、0終端・値域1..255の連続配列（長さ3..5）を候補化。
  - 末尾列（例: [15,10]）を必ず含む候補のみ採用対象（順/逆・連続部分列/末尾一致）。
  - スコア: 末尾一致の強さ＋長さ＋既知フィールド近傍で加点。最良を採用しログ出力（`route recovered ...`）。
- 採用ロジックの一本化
  - `TryCaptureFromMemory`: `routeFromMem`（復元反映済み）を `ParseRouteNumbers`→キャッシュ（suffix/連続部分列）と統合し最終採用。
- RouteShort の可読性向上
  - 解決順: SectorResolver（Mapヒント＋Alias）→学習名（`Config/RouteNames`）→A..Zフォールバック（1..26）→`P<n>`。
  - Mapヒント/別名がなくても 1..26 はレター表示（例: 13,18,15,10,26 → M>R>O>J>Z）。

## 追加/変更された設定（apps/XIVSubmarinesReturn/src/Configuration.cs）
- `MemoryOnlyMode`（bool, 既定=true）: UI追補を無効化しメモリのみで取得。
- `MemoryRouteScanEnabled`（bool, 既定=true）: 構造体内スキャンの有効/無効。
- `MemoryRouteMinCount`（int, 既定=3）, `MemoryRouteMaxCount`（int, 既定=5）: スキャン候補の長さ制御。

## 変更ファイル（主な差分）
- `apps/XIVSubmarinesReturn/src/Plugin.cs`
  - `OnCmdDump`: `MemoryOnlyMode` 有効時は最初から `TryCaptureFromMemory` を使用。
  - `OnFrameworkUpdate`: 自動取得も `MemoryOnlyMode` を尊重（UI追補抑止）。
  - 追加 `TryRecoverFullRouteFromMemory(...)`: 計画航路の復元（末尾必須, 安全スキャン）。
  - `TryCaptureFromMemory`: 復元→キャッシュ統合→UI補完ガード→採用ログ強化。
  - `BuildRouteShortFromNumbers`: A..Z フォールバックを追加（1..26）。
- `apps/XIVSubmarinesReturn/src/Configuration.cs`
  - 上記設定キーを追加。
- `apps/XIVSubmarinesReturn/XIVSubmarinesReturn.csproj`
  - Dalamud参照の `HintPath` を `/` 区切りに統一（WSL等を考慮）。
- ドキュメント
  - 追加: `docs/XSR_memory_route_fix_2025-09-13.md`（本対応の技術メモ）。

## ビルド/配置（本セッションの手順）
- .NET 9 SDK をユーザーディレクトリに導入（`~/.dotnet/dotnet`）。
- Dalamud dev ライブラリを `goatcorp.github.io/dalamud-distrib/latest.zip` から取得し展開。
- `apps/XIVSubmarinesReturn/Local.props` を作成（未コミット）し `DalamudLibPath` を展開先に設定。
- ビルド: `dotnet restore` → `dotnet build -c Release -p:Platform=x64 -p:SkipPack=true`。
- 成果物: `apps/XIVSubmarinesReturn/bin/x64/Release/net9.0-windows/XIVSubmarinesReturn.dll`。
- Dalamud の Dev Plugins に配置して再読込→`/xsr dump` で検証。

## 検証ログ（提供ログの読み取り）
- 変更前: `route bytes = 15,10` のみ、UI採用なし。
- 初回復元時: 末尾必須ガード前は `[108,51,36,1,80]` 等の誤候補採用が発生（今回修正で抑止）。
- 末尾必須化後: `route recovered ... full=[...,15,10,...]` の形に限定。
- JSON: `RouteKey` は数値列、`RouteShort` は Mapヒント/別名がない場合でも 1..26 は A..Z にフォールバック表示。

## 受け入れ条件（合否基準）
- UIを開かず `/xsr dump` で `RouteKey` が3点以上に復元。
- `xsr_debug.log` に `route recovered` 行が出力され、復元配列が既知末尾（例: [15,10]）を含む。
- `RouteShort` が可読レター（A..Z）または別名に解決される。
- UI由来の追補は `MemoryOnlyMode=false` に設定した場合にのみ作動。

## 残課題 / 次セッションへの提案
- デバッグ補助: `/xsr memdump [slot]` を追加（構造体近傍の bounded hex dump 出力、境界/例外安全）。
- 手動シード: `/xsr setroute <slot> <chain>` `/xsr getroute [slot]` の実装（キャッシュの初期化・上書き）。
- スコア精度向上: Luminaの SubmarineExploration シートから実在セクターID集合を構築し、未知IDを減点して候補の精度向上。
- UI/設定: `MemoryOnlyMode`/`MemoryRouteScanEnabled` のトグルを設定UI/コマンドに露出。
- ドキュメント: ユーザー向け README/HELP にメモリ優先の運用ガイド、Mapヒント/別名の整備手順（`/sv import-alias mogship`）を追記。

## 参照
- 仕様/現状分析: `docs/XSR_route_extraction_review_2025-09-13.md`
- 本対応の技術メモ: `docs/XSR_memory_route_fix_2025-09-13.md`
- 提供ログ: `apps/XIVSubmarinesReturn/log/`（`xsr_debug.log`, `submarines.json`, `stage_dump.txt` など）
- 主なコード: `apps/XIVSubmarinesReturn/src/Plugin.cs`, `Configuration.cs`, `Sectors/*`, `Plugin.Stage.cs`

