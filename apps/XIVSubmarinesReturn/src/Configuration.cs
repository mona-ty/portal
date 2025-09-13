using Dalamud.Configuration;
using Dalamud.Plugin;

namespace XIVSubmarinesReturn;


public enum RouteDisplayMode { Letters, ShortIds, Raw }

public enum UiDensity { Compact, Cozy }

public enum NotionKeyMode
{
    // 同一スロットで同一ページに更新（推奨）
    PerSlot = 0,
    // ルートも含めて分岐
    PerSlotRoute = 1,
    // 旧方式（便ごとに別ページ）
    PerVoyage = 2,
}

public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    // 設定スキーマの内部バージョン（プロファイル導入用）
    public int ConfigVersion { get; set; } = 1;

    // 複数キャラ対応：プロファイルとアクティブID
    public System.Collections.Generic.List<CharacterProfile> Profiles { get; set; } = new();
    public ulong? ActiveContentId { get; set; }

    // ログイン検出時に自動でプロファイルを作成するか（既定: false）
    public bool AutoCreateProfileOnLogin { get; set; } = false;

    public bool AutoCaptureOnWorkshopOpen { get; set; } = false;

    // 設定ウィンドウの自動リサイズ（縦横）。既定で有効。
    public bool AutoResizeWindow { get; set; } = true;

    // 取得対象のアドオン名（既定は推測名、必要に応じて変更してください）
    public string AddonName { get; set; } = "SelectString";

    // SelectString 専用抽出の ON/OFF（切り分け用）
    public bool UseSelectStringExtraction { get; set; } = true;

    // SelectString 詳細テキスト（残り時間/ランク 等）の解析を試みる
    public bool UseSelectStringDetailExtraction { get; set; } = true;

    // アドオン名のフォールバック候補を積極的に試す
    public bool AggressiveFallback { get; set; } = true;

    // UIが取れない時にメモリ直読でのフォールバックを試す
    public bool UseMemoryFallback { get; set; } = true;

    // メモリのみで取得（UIからの追補を無効化）
    public bool MemoryOnlyMode { get; set; } = true;
    // 構造体内スキャンで“計画航路”の復元を試みる
    public bool MemoryRouteScanEnabled { get; set; } = true;
    // スキャンで採用する最小トークン数（3 以上）
    public int MemoryRouteMinCount { get; set; } = 3;
    // スキャンで許容する最大トークン数（通常 5）
    public int MemoryRouteMaxCount { get; set; } = 5;
    // スキャン範囲（CurrentExplorationPoints 近傍の±バイト数）。0 で全域
    public int MemoryRouteScanWindowBytes { get; set; } = 0x120;

    // 取得ゲート（無駄スキャン抑止）
    public bool EnableTerritoryGate { get; set; } = true;
    public bool EnableAddonGate { get; set; } = true;

    // 採用ロジック（降格防止）
    public bool AdoptPreferLonger { get; set; } = true;
    public bool AdoptAllowDowngrade { get; set; } = false; // Full保持中は基本降格しない
    public int AdoptTtlHours { get; set; } = 12; // TTL=12h
    public bool AdoptCachePersist { get; set; } = true; // プロファイルに永続

    // ArrayData 優先（RequestedUpdate監視時の直読）
    public bool PreferArrayDataFirst { get; set; } = true;

    // スキャナ拡張トグル
    public bool MemoryRouteZeroTerminated { get; set; } = true; // 0終端前提
    public bool MemoryScanPhaseEnabled { get; set; } = true; // 位相拡張を許可

    // AddonLifecycle 連動（イベントドリブン）
    public bool EnableAddonLifecycleCapture { get; set; } = true;

    // スロット番号(1..4)に対する実艦名の学習結果を保存
    public string[] SlotAliases { get; set; } = new string[4];

    // 直読でデフォルト名(Submarine-<n>)の行を採用するか
    public bool AcceptDefaultNamesInMemory { get; set; } = true;

    // ルート名マッピング (Point ID -> 表示名)
    public System.Collections.Generic.Dictionary<byte, string> RouteNames { get; set; } = new();

    // ルート表示モード（学習済みレター > P番号 > 原文）
    public RouteDisplayMode RouteDisplay { get; set; } = RouteDisplayMode.Letters;

    // セクター解決用のMapヒント（例: Deep-sea Site = 溺没海）
    public string SectorMapHint { get; set; } = "Deep-sea Site";

    // UI 外観設定
    public UiDensity UiRowDensity { get; set; } = UiDensity.Compact;
    public float UiFontScale { get; set; } = 1.0f; // 0.9..1.2 推奨
    public int HighlightSoonMins { get; set; } = 10; // ETAがこの分数以下で強調
    public string AccentColor { get; set; } = "#1E90FF"; // 青(DodgerBlue)

    // テーブル状態（必要に応じて保存）
    public int TableSortField { get; set; } = 3; // 0=Name 1=Slot 2=Rank 3=ETA
    public bool TableSortAsc { get; set; } = true;
    public string TableFilterText { get; set; } = string.Empty;

    // Discord embeds (duplicated below for grouping – keep single definition)
    // public bool DiscordUseEmbeds { get; set; } = false;


    // Discord settings
    public bool DiscordEnabled { get; set; } = false;
    public string DiscordWebhookUrl { get; set; } = string.Empty;
    public bool DiscordLatestOnly { get; set; } = false;
    public bool DiscordUseEmbeds { get; set; } = true;
    // Snapshot 通知の最小間隔（分）。0で常に許可。
    public int DiscordMinIntervalMinutes { get; set; } = 10;

    // In-game alarm lead minutes
    public bool GameAlarmEnabled { get; set; } = true;
    public System.Collections.Generic.List<int> AlarmLeadMinutes { get; set; } = new() { 5, 0 };

    // Debug logging
    public bool DebugLogging { get; set; } = true;

    // ルートキャッシュ（メモリから得られるポイントが不足する場合の補完に使用。キー=スロット番号）
    public System.Collections.Generic.Dictionary<int, string> LastRouteBySlot { get; set; } = new();

    // Notion settings
    public bool NotionEnabled { get; set; } = false;
    public string NotionToken { get; set; } = string.Empty;
    public string NotionDatabaseId { get; set; } = string.Empty;
    // 自動セットアップ用: 親ページID（URL→IDで設定可能）。空ならワークスペース直下を試行
    public string NotionParentPageId { get; set; } = string.Empty;
    public bool NotionLatestOnly { get; set; } = false;
    public NotionKeyMode NotionKeyMode { get; set; } = NotionKeyMode.PerSlot;
    // Property names mapping (must exist in the target database)
    public string NotionPropName { get; set; } = "Name";       // title
    public string NotionPropSlot { get; set; } = "Slot";       // number
    public string NotionPropEta { get; set; } = "ETA";         // date
    public string NotionPropRoute { get; set; } = "Route";     // rich_text
    public string NotionPropRank { get; set; } = "Rank";       // number
    public string NotionPropExtId { get; set; } = "ExtId";     // rich_text (for upsert)
    // 余分な付加情報（World/Character/FC/Remaining）は廃止（DB簡素化）

    public void Save(IDalamudPluginInterface pi)
    {
        pi.SavePluginConfig(this);
    }
}
