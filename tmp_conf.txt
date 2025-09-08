using Dalamud.Configuration;
using Dalamud.Plugin;

namespace XIVSubmarinesReturn;

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

    public void Save(IDalamudPluginInterface pi)
    {
        pi.SavePluginConfig(this);
    }
}

