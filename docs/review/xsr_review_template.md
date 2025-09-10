XIV Submarines Return — コードレビュー依頼テンプレート（特化版・汎用ベース）

注意: 本テンプレは汎用版（docs/review/ai_code_review_template.md）をベースに、XSR向けに前提/コマンド/ログ/出力先を具体化しています。

使い方（前提）
- 対象: `apps/XIVSubmarinesReturn`（C# / .NET 9 / Dalamud プラグイン）
- 開発/レビュー: Codex で実装、GPT‑5 Pro でレビュー（個人開発: チケット無し）
- 必須添付: 実装方針・コード情報・ログ抜粋（機微情報は必ずマスク）
- 収集: 汎用 `scripts/collect_review_bundle.ps1` または特化 `scripts/collect_xsr_review.ps1`

ショート版（素早い依頼）
- 概要: <何を・なぜ・どの画面/機能に（1–3行）>
- 変更対象: <コミットID/ブランチ/主要ファイル（相対パス）>
- 実装方針（必須）: <UI抽出/フォールバック/Update頻度/外部連携など主要判断>
- 再現/実行:
  - ビルド: `dotnet build apps\XIVSubmarinesReturn\XIVSubmarinesReturn.csproj -c Release -p:Platform=x64`
  - 配置: `%AppData%\XIVLauncher\devPlugins\XIVSubmarinesReturn` に DLL（manifest 埋込）
  - 操作: `/xsr help | dump | open | addon <name> | version | ui`
- ログ抜粋（必須 10–30行）: `apps/XIVSubmarinesReturn/log/*.txt` 抜粋 + `%AppData%\XIVSubmarinesReturn\bridge\submarines.json`
- 着目点: <バグ/性能/UI抽出精度/セキュリティ/観測性 の優先順>
- 質問（最大3件）: <具体的に確認したい点>

フル版（詳細テンプレート）
- 依頼サマリ
  - 目的/背景: <狙い・関連するゲーム内操作/アドオン>
  - 成果物/受入条件: <期待するUI/ログ/JSON出力の状態>
  - 重要度/期日: <高/中/低> / <YYYY‑MM‑DD>

- 変更範囲
  - コミット/ブランチ: <ID/名前>
  - 種別: <新規/改修/リファクタ/バグ修正/実験フラグ>
  - 影響領域: <UI(Overview/Alarm/Debug)/Extractor/Bridge/Services(Discord/Notion)/Commands>

- 実装方針・設計（必須）
  - 方針: <ImGui UI構成、IFramework.Updateのポーリング間隔、抽出アルゴリズム、Fallback条件>
  - 代替検討: <不採用案と理由>
  - トレードオフ: <性能(毎フレーム処理) vs 可読性、抽出精度 vs 安定性>
  - 失敗時処理/例外: <タイムアウト/リトライ/UI未検出時の動作>

- コード情報（必須）
  - 言語/ランタイム: C# / net9.0-windows（Dalamud ApiLevel=13）
  - フレームワーク/依存: Dalamud, Dalamud.Bindings.ImGui, FFXIVClientStructs, ImGuiScene, DalamudPackager(13.1.0)
  - 主要ファイル:
    - `src/Plugin.cs`（エントリ/ライフサイクル）
    - `src/Plugin.UI.cs`（ImGui UI）
    - `src/Extractors.cs`（UI/SelectString抽出）
    - `src/BridgeWriter.cs`（JSON出力: submarines.json）
    - `src/Services/*`（AlarmScheduler/Discord/Notion）
  - 設定/フラグ: `Local.props`（`DalamudLibPath`）、プラグイン設定（メモリ/フォールバック等）

- 差分ハイライト
  - 重要変更1: <ファイル/関数/ロジックの要約>
  - 重要変更2: <…>
  - 互換性: <破壊的/非破壊的、移行の有無>

- 実行・再現手順（ゲーム内含む）
  - セットアップ: `Local.props` 設定 → 上記ビルド → devPlugins 配置
  - 起動/検証: `/xsr help`, `/xsr dump`, `/xsr open`, `/xsr addon <name>`, `/xsr version`, `/xsr ui`
  - 手動確認: <対象UIを開く→抽出→JSON出力確認までの手順>

- ログ（必須）
  - 事象ログ抜粋: `apps/XIVSubmarinesReturn/log/*.txt` から10–50行（保存先リンクも）
  - 出力JSON: `%AppData%\XIVSubmarinesReturn\bridge\submarines.json`（または `%AppData%\ff14_submarines_act\bridge\submarines.json`）
  - 追加: `dumpstage`/`dumptree` 出力、`code_review.txt`/`codexlog.txt` 抜粋

- テスト
  - 追加/変更テスト（手動）: <UIパターン/境界条件/SelectString多言語>
  - カバレッジ（重要経路）: <抽出→書き出し→外部連携>
  - 未検証: <ゲーム状態依存/言語依存 等>

- パフォーマンス
  - 指標: <フレーム時間影響/Update当たり処理量/ログ量>
  - 測定条件: <艦数/ページ数/連打操作/高頻度更新>
  - 結果と閾値: <例: 1ms/Update 以内 など>
  - 退行リスク: <Update内重処理/文字列探索のオーダー>

- セキュリティ・コンプライアンス
  - 入力検証/サニタイズ: <UIテキスト/外部入力>
  - 機微情報: Discord/Webhook, Notion トークン（必ずマスク）
  - 依存スキャン/ライセンス: <該当なし/結果>

- API/外部連携
  - Discord/Notion: <使用有無/タイムアウト/リトライ/失敗時のUI通知>
  - JSONブリッジ: フォーマット/互換性/移行の有無

- リリース計画
  - フラグ/段階展開: <設定ON/OFF, 安全装置>
  - ロールバック: <前版DLLに戻す手順>
  - 監視: <ログ/エラーアラート/JSON整合チェック>

- 既知の懸念・未対応
  - <例: 特定言語環境でSelectString未検出/高負荷時の取りこぼし>

- レビュー観点の優先順位（XSR特化）
  - 正当性/境界: UI抽出の網羅性、空/重複/多言語
  - 耐障害性: Update内例外の抑止、失敗時の再試行
  - 性能: 毎フレーム処理の負荷、文字列探索の効率
  - 可読性: 抽出/整形/出力の関心分離、命名/責務
  - 観測性: ログ粒度、デバッグコマンド、出力検証手段

- Codex開発コンテキスト（推奨）
  - 実装プロンプト/方針: <Codexへ与えた要件・制約>
  - 変更差分: `git diff` 要点（抜粋可）
  - 実装時の制約: <時間/再現環境/既知制約>

- 具体的な質問（例）
  - Q1: `IFramework.Update` 内の抽出頻度と負荷は適切？
  - Q2: `SelectString` 抽出の境界条件（ソート/フィルタ/言語差）に漏れは？
  - Q3: `BridgeWriter` のJSONスキーマは将来互換を保てる？
  - Q4: 外部通知（Discord/Notion）の失敗時リトライ/バックオフ設計は適切？

送付前チェックリスト
- 機微情報（トークン/ID）はマスクした
- 再現手順が1回で通る
- 代表ログとフルログの導線がある
- 重要差分と設計判断を1–2分で把握できる
- 質問が具体的でレビュー観点が明確

付録: よく使うコマンド
- ビルド: `dotnet build apps\XIVSubmarinesReturn\XIVSubmarinesReturn.csproj -c Release -p:Platform=x64`
- 配置: `%AppData%\XIVLauncher\devPlugins\XIVSubmarinesReturn`
- 実行（ゲーム内）: `/xsr help`, `/xsr dump`, `/xsr open`, `/xsr addon <name>`, `/xsr version`, `/xsr ui`
- 収集（汎用）: `pwsh scripts/collect_review_bundle.ps1 -ProjectRoot 'apps/XIVSubmarinesReturn' -AppName 'XIVSubmarinesReturn' -LogPaths 'apps/XIVSubmarinesReturn/log',"$env:AppData\XIVSubmarinesReturn\bridge"`
- 収集（特化）: `pwsh scripts/collect_xsr_review.ps1`
