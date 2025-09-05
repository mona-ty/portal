using Dalamud.Configuration;
using Dalamud.Plugin;

namespace XIVSubmarinesReturn;

public enum CalendarMode { All, Latest }

public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool AutoCaptureOnWorkshopOpen { get; set; } = false;

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

    // スロット番号(1..4)に対する実艦名の学習結果を保存
    public string[] SlotAliases { get; set; } = new string[4];

    // 直読でデフォルト名(Submarine-<n>)の行を採用するか
    public bool AcceptDefaultNamesInMemory { get; set; } = true;

    // ルート名マッピング (Point ID -> 表示名)
    public System.Collections.Generic.Dictionary<byte, string> RouteNames { get; set; } = new();

    // Discord embeds (duplicated below for grouping – keep single definition)
    // public bool DiscordUseEmbeds { get; set; } = false;

    // Google Calendar settings
    public bool GoogleEnabled { get; set; } = false;
    public CalendarMode GoogleEventMode { get; set; } = CalendarMode.All;
    public string GoogleRefreshToken { get; set; } = string.Empty;
    public string GoogleClientId { get; set; } = string.Empty;
    public string GoogleClientSecret { get; set; } = string.Empty;
    public string GoogleCalendarId { get; set; } = "primary";
    public System.Collections.Generic.List<int> GoogleReminderMinutes { get; set; } = new() { 0 };

    // Discord settings
    public bool DiscordEnabled { get; set; } = false;
    public string DiscordWebhookUrl { get; set; } = string.Empty;
    public bool DiscordLatestOnly { get; set; } = false;
    public bool DiscordUseEmbeds { get; set; } = true;

    // In-game alarm lead minutes
    public System.Collections.Generic.List<int> AlarmLeadMinutes { get; set; } = new() { 5, 0 };

    // Debug logging
    public bool DebugLogging { get; set; } = true;

    // Notion settings
    public bool NotionEnabled { get; set; } = false;
    public string NotionToken { get; set; } = string.Empty;
    public string NotionDatabaseId { get; set; } = string.Empty;
    public bool NotionLatestOnly { get; set; } = false;
    // Property names mapping (must exist in the target database)
    public string NotionPropName { get; set; } = "Name";       // title
    public string NotionPropSlot { get; set; } = "Slot";       // number
    public string NotionPropEta { get; set; } = "ETA";         // date
    public string NotionPropRoute { get; set; } = "Route";     // rich_text
    public string NotionPropRank { get; set; } = "Rank";       // number
    public string NotionPropExtId { get; set; } = "ExtId";     // rich_text (for upsert)

    public void Save(IDalamudPluginInterface pi)
    {
        pi.SavePluginConfig(this);
    }
}

