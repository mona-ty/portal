using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Memory;
using System.Runtime.InteropServices;
using System.Text;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.UI;
using XIVSubmarinesReturn.Services;
using System.Net.Http;
using XIVSubmarinesReturn.Sectors;

namespace XIVSubmarinesReturn;

public sealed partial class Plugin : IDalamudPlugin
{
    public string Name => "XIV Submarines Return";

    private readonly IDalamudPluginInterface _pi;
    private readonly ICommandManager _cmd;
    private readonly IChatGui _chat;
    private readonly IFramework _framework;
    private readonly IGameGui _gameGui;
    private readonly IPluginLog _log;
    private readonly IToastGui _toast;

    public Configuration Config { get; private set; }

    private const string CmdDump = "/subdump";
    private const string CmdConfig = "/subcfg";
    private const string CmdOpen = "/subopen";
    private const string CmdSetAddon = "/subaddon";
    private const string CmdRoot = "/xivsubmarinesreturn";
    private const string CmdShort = "/xsr";

    private DateTime _lastAutoCaptureUtc = DateTime.MinValue;
    private bool _wasAddonVisible;
    private DateTime _lastTickUtc;

    private AlarmScheduler? _alarm;
    private DiscordNotifier? _discord;
    private GoogleCalendarClient? _gcal;
    private NotionClient? _notion;
    private SectorResolver? _sectorResolver;
    private Commands.SectorCommands? _sectorCommands;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IChatGui chat,
        IFramework framework,
        IGameGui gameGui,
        IPluginLog log,
        Dalamud.Plugin.Services.IDataManager data,
        IToastGui toast)
    {
        _pi = pluginInterface;
        _cmd = commandManager;
        _chat = chat;
        _framework = framework;
        _gameGui = gameGui;
        _log = log;
        _toast = toast;

        Config = _pi.GetPluginConfig() as Configuration ?? new Configuration();

        _cmd.AddHandler(CmdDump, new CommandInfo(OnCmdDump)
        {
            HelpMessage = "Submarines: 画面から収集しJSONを書き出し"
        });
        _cmd.AddHandler(CmdConfig, new CommandInfo(OnCmdConfigOpen)
        {
            HelpMessage = "Submarines: 設定を表示（簡易）"
        });
        _cmd.AddHandler(CmdOpen, new CommandInfo(OnCmdOpen)
        {
            HelpMessage = "Submarines: 出力フォルダを開く"
        });
        _cmd.AddHandler(CmdSetAddon, new CommandInfo(OnCmdSetAddon)
        {
            HelpMessage = "Submarines: set addon name (/subaddon <name>)"
        });
        _cmd.AddHandler(CmdRoot, new CommandInfo(OnCmdRoot)
        {
            HelpMessage = "XIVSubmarinesReturn root command. Try: /xsr help"
        });
        _cmd.AddHandler(CmdShort, new CommandInfo(OnCmdRoot)
        {
            HelpMessage = "XIVSubmarinesReturn short command. Try: /xsr help"
        });

        _framework.Update += OnFrameworkUpdate;
        InitUI();

        try
        {
            var http = new HttpClient();
            try
            {
                var ver = typeof(Plugin).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
                http.DefaultRequestHeaders.UserAgent.Clear();
                http.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("XIVSubmarinesReturn", ver));
            }
            catch { }

            _discord = new DiscordNotifier(Config, _log, http);
            _notion = new NotionClient(Config, _log, http);
            // Sector Resolver (Excel + Alias JSON)
            try
            {
                var aliasPath = System.IO.Path.Combine(_pi.ConfigDirectory?.FullName ?? string.Empty, "AliasIndex.json");
                // ensure default file exists
                try
                {
                    var idx = AliasIndex.LoadOrDefault(aliasPath);
                    if (!System.IO.File.Exists(aliasPath)) idx.Save(aliasPath);
                }
                catch { }
                
                _sectorResolver = new SectorResolver(data, aliasPath, _log);
                // Register commands (pass HttpClient/Log for importer)
                _sectorCommands = new Commands.SectorCommands(_pi, _cmd, _chat, _sectorResolver, http, _log);
            }
            catch { }

#if XSR_FEAT_GCAL
            _gcal = new GoogleCalendarClient(Config, _log, http);
            _alarm = new AlarmScheduler(Config, _chat, _toast, _log, _discord, _gcal, _notion);
#else
            _alarm = new AlarmScheduler(Config, _chat, _toast, _log, _discord, _notion);
#endif
        }
        catch { }
    }

    public void Dispose()
    {
        _framework.Update -= OnFrameworkUpdate;
        try { _sectorCommands?.Dispose(); } catch { }
        _cmd.RemoveHandler(CmdDump);
        _cmd.RemoveHandler(CmdConfig);
        _cmd.RemoveHandler(CmdOpen);
        _cmd.RemoveHandler(CmdSetAddon);
        _cmd.RemoveHandler(CmdRoot);
        _cmd.RemoveHandler(CmdShort);
    }

    private void OnCmdDump(string cmd, string args)
    {
        try
        {
            if (!TryCaptureFromConfiguredAddon(out var snap))
            {
                if (Config.UseMemoryFallback && TryCaptureFromMemory(out snap)) { }
                else {
                _log.Warning("Direct capture failed; no JSON written.");
                _chat.Print("[Submarines] 取得に失敗しました。工房の潜水艦一覧（右パネルに残り時間が出る画面）を開いて /xsr dump を実行してください。");
                return;
                }
            }
            // Attach identity once (if available)
            try { TrySetIdentity(snap); } catch { }
            try { EtaFormatter.Enrich(snap); } catch { }
            try { TryAdoptPreviousRoutes(snap); } catch { }
            try { _alarm?.UpdateSnapshot(snap); } catch { }
            BridgeWriter.WriteIfChanged(snap);
            _chat.Print( $"[Submarines] JSONを書き出しました: {BridgeWriter.CurrentFilePath()}"); 
        }
        catch (Exception ex)
        {
            _log.Error(ex, "OnCmdDump failed");
            _chat.PrintError( $"[Submarines] エラー: {ex.Message}"); 
        }
    }

    private void OnCmdConfig(string cmd, string args)
    {
        _chat.Print("[Submarines] 設定UIは未実装です。/xsr addon <name> で対象アドオンを設定可能です。");
    }

    private void OnCmdCfg(string args)
    {
        var a = (args ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(a))
        {
            _chat.Print($"[Submarines] cfg: mem={(Config.UseMemoryFallback ? "on" : "off")}");
            return;
        }
        var parts = a.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && string.Equals(parts[0], "mem", StringComparison.OrdinalIgnoreCase))
        {
            var v = parts[1].ToLowerInvariant();
            if (v is "on" or "off")
            {
                Config.UseMemoryFallback = v == "on";
                SaveConfig();
                _chat.Print($"[Submarines] cfg mem -> {(Config.UseMemoryFallback ? "on" : "off")}");
                return;
            }
        }
        _chat.Print("[Submarines] cfg usage: /xsr cfg mem on|off");
    }

    private void OnCmdOpen(string cmd, string args)
    {
        OpenBridgeFolder();
    }

    private void OnCmdConfigOpen(string cmd, string args)
    {
        try
        {
            _showUI = true;
            _chat.Print("[Submarines] 設定ウィンドウを開きました。");
        }
        catch
        {
            _chat.Print("[Submarines] 設定UIを開けませんでした。/xsr ui をお試しください。");
        }
    }

    // メモリ直読: HousingManager -> WorkshopTerritory -> Submersible[]（A/B安全読み）
    private unsafe void CmdDumpFromMemory()
    {
        try
        {
            if (!TryCaptureFromMemory(out var snap))
            {
                _chat.Print("[Submarines] メモリ直読に失敗しました。工房に入り直してからお試しください。");
                return;
            }
            try { EtaFormatter.Enrich(snap); } catch { }
            try { TryAdoptPreviousRoutes(snap); } catch { }
            try { TryAdoptFromLogs(snap); } catch { }
            try { _alarm?.UpdateSnapshot(snap); } catch { }
            BridgeWriter.WriteIfChanged(snap);
            _chat.Print( $"[Submarines] JSONを書き出しました: {BridgeWriter.CurrentFilePath()}"); 
        }
        catch (Exception ex)
        {
            _log.Error(ex, "CmdDumpFromMemory failed");
            _chat.PrintError($"[Submarines] エラー: {ex.Message}");
        }
    }

    private unsafe void CmdLearnNames()
    {
        try
        {
            // SelectString/SelectIconString のどちらかを探す
            var unit = ResolveAddonPtr("SelectString");
            if (unit == null)
                unit = ResolveAddonPtr("SelectIconString");
            if (unit == null)
            {
                _chat.Print("[Submarines] SelectString を開いてから実行してください。");
                return;
            }
            List<string> learned = new();
            if (TryCaptureFromSelectStringFast(unit, out var items) && items.Count > 0)
            {
                // メニュー見出し等を除外（艦名のみを狙う）
                learned = items
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(NormalizeItemText)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Where(s => !s.Equals("やめる", StringComparison.Ordinal))
                    .Where(s => !s.Contains("報告", StringComparison.Ordinal))
                    .Where(s => !s.Contains("中止", StringComparison.Ordinal))
                    .Where(s => !s.Contains("海底図", StringComparison.Ordinal))
                    .Where(s => !s.EndsWith("の確認", StringComparison.Ordinal))
                    .Where(s => !s.StartsWith("前回の", StringComparison.Ordinal))
                    .Where(s => !System.Text.RegularExpressions.Regex.IsMatch(s, @"^Submarine-\d+$"))
                    .Take(4)
                    .ToList();
            }

            // SelectString から学習できない場合、工房パネルから学習（CompanyCraftSubmersibleList 等）
            if (learned.Count == 0)
            {
                var candidates = new[] { "CompanyCraftSubmersibleList", "FreeCompanyWorkshopSubmersible", "CompanyCraftSubmersible", "CompanyCraftList" };
                var lines = new List<string>();
                foreach (var n in candidates)
                {
                    var u = ResolveAddonPtr(n);
                    if (u == null) continue;
                    CollectTextLines(u, lines);
                }
                if (lines.Count > 0)
                {
                    learned = InferNamesFromLines(lines);
                }
            }

            if (learned.Count == 0)
            {
                _chat.Print("[Submarines] 艦名の学習に失敗しました。艦名が表示されている画面で再実行してください。");
                return;
            }

            // 先頭4件を保存（スロット1..4）
            for (int i = 0; i < Math.Min(4, learned.Count); i++)
                Config.SlotAliases[i] = learned[i];

            SaveConfig();
            var names = string.Join(", ", (Config.SlotAliases ?? Array.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)));
            _chat.Print($"[Submarines] 名前を学習しました: {names}");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "CmdLearnNames failed");
            _chat.PrintError($"[Submarines] エラー: {ex.Message}");
        }
    }

    #if false
    private unsafe bool TryCaptureFromMemory(out SubmarineSnapshot snapshot)
    {
        snapshot = new SubmarineSnapshot
        {
            PluginVersion = typeof(Plugin).Assembly.GetName().Version?.ToString(3) ?? "0.0.0",
            Note = "captured from memory (WorkshopTerritory.Submersible)",
            Items = new List<SubmarineRecord>()
        };
        try
        {
            var hm = HousingManager.Instance();
            if (hm == null)
                return false;
            var wt = hm->WorkshopTerritory;
            if (wt == null)
                return false;

            var subsBase = (HousingWorkshopSubmersibleSubData*)(&wt->Submersible);
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            for (int i = 0; i < 4; i++)
            {
                var s = subsBase + i;

                // name: offset 0x22, 20 bytes (UTF-8想定)
                string name = string.Empty;
                try
                {
                    byte* namePtr = (byte*)s + 0x22;
                    int len = 20;
                    for (int k = 0; k < 20; k++) { if (namePtr[k] == 0) { len = k; break; } }
                    if (len > 0)
                        name = Encoding.UTF8.GetString(new ReadOnlySpan<byte>(namePtr, len)).Trim();
                }
                catch { }
                name = NormalizeItemText(name ?? string.Empty);
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                bool wasDefault = System.Text.RegularExpressions.Regex.IsMatch(name ?? string.Empty, @"^Submarine-\d+$");
                var rec = new SubmarineRecord { Name = name, Slot = i + 1, IsDefaultName = wasDefault };
                // 学習済みの実艦名があれば置き換え（スロット1..4 -> 配列0..3）
                try
                {
                    var aliases = Config.SlotAliases;
                    if (aliases != null && i >= 0 && i < aliases.Length)
                    {
                        var alias = aliases[i];
                        if (!string.IsNullOrWhiteSpace(alias) && wasDefault)
                            rec.Name = alias.Trim();
                    }
                }
                catch { }

                // rank: RankId (byte)
                try
                {
                    int rank = s->RankId;
                    if (rank > 0) rec.Rank = rank;
                }
                catch { }

                // eta: ReturnTime (epoch sec)
                try
                {
                    uint rt = s->ReturnTime;
                    if (rt > now)
                    {
                        var minutes = (int)((rt - now) / 60);
                        if (minutes >= 0 && minutes <= 60 * 72) rec.DurationMinutes = minutes;
                        rec.EtaUnix = rt;
                    }
                }
                catch { }

                // route key: CurrentExplorationPoints[0..] を連結（例: Point-15 - Point-20）
        {
            _chat.Print($"[Submarines] Version: {typeof(Plugin).Assembly.GetName().Version?.ToString(3) ?? "0.0.0"}");
            _chat.Print("[Submarines] Commands: dump | dumpmem | learnnames | ui | cfg mem on|off | open | addon <name> | version");
            _chat.Print("  /xsr dump  -> same as /subdump");
            _chat.Print("  /xsr open  -> same as /subopen");
            _chat.Print("  /xsr addon <name> -> same as /subaddon <name>");
            _chat.Print("  /xsr version -> print plugin version");
            _chat.Print("  /xsr probe -> check common addon names");
            _chat.Print("  /xsr dumpstage -> scan many addons on screen");
            return;
        }

        var parts = a.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var sub = parts[0].ToLowerInvariant();
        var rest = parts.Length > 1 ? parts[1] : string.Empty;
            switch (sub)
            {
                case "dump":
                    OnCmdDump(CmdDump, rest);
                    break;
                case "dumpmem":
                    CmdDumpFromMemory();
                    break;
                case "version":
                    _chat.Print($"[Submarines] Version: {typeof(Plugin).Assembly.GetName().Version?.ToString(3) ?? "0.0.0"}");
                    break;
                case "ui":
                    _showUI = true;
                    break;
            case "learnnames":
                CmdLearnNames();
                break;
            case "leannames": // タイポ別名
                CmdLearnNames();
                break;
            case "open":
                OnCmdOpen(CmdOpen, rest);
                break;
            case "cfg":
            case "config":
                OnCmdCfg(rest);
                break;
            case "addon":
                OnCmdSetAddon(CmdSetAddon, rest);
                break;
            case "probe":
                CmdProbe();
                break;
            case "dumpstage":
                CmdDumpStage();
                break;
            // removed: selftest command
            // case "selftest":
            //     CmdSelfTest();
            //     break;
            default:
                _chat.Print("[Submarines] Unknown subcommand. Try: /xsr help");
                break;
        }
    }

    // SelectString: EntryNames 優先で取り、プレースホルダ/ノイズは除外
    #endif

    // Fixed implementation
    private unsafe bool TryCaptureFromMemory(out SubmarineSnapshot snapshot)
    {
        snapshot = new SubmarineSnapshot
        {
            PluginVersion = typeof(Plugin).Assembly.GetName().Version?.ToString(3) ?? "0.0.0",
            Note = "captured from memory (WorkshopTerritory.Submersible)",
            Items = new List<SubmarineRecord>()
        };
        try
        {
            var hm = HousingManager.Instance();
            if (hm == null) return false;
            var wt = hm->WorkshopTerritory;
            if (wt == null) return false;

            // Prefer DataPointers; fallback to contiguous memory cast
            HousingWorkshopSubmersibleSubData* GetSubPtr(int idx)
            {
                try
                {
                    var p = wt->Submersible.DataPointers[idx].Value;
                    if ((nint)p != 0)
                        return (HousingWorkshopSubmersibleSubData*)p;
                }
                catch { }
                try
                {
                    var basePtr = (HousingWorkshopSubmersibleSubData*)(&wt->Submersible);
                    return basePtr + idx;
                }
                catch { }
                return null;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            for (int i = 0; i < 4; i++)
            {
                var s = GetSubPtr(i);
                if (s == null) continue;

                string name = string.Empty;
                try
                {
                    byte* namePtr = (byte*)s + 0x22;
                    int len = 20;
                    for (int k = 0; k < 20; k++) { if (namePtr[k] == 0) { len = k; break; } }
                    if (len > 0)
                        name = Encoding.UTF8.GetString(new ReadOnlySpan<byte>(namePtr, len)).Trim();
                }
                catch { }
                name = NormalizeItemText(name ?? string.Empty);
                if (string.IsNullOrWhiteSpace(name)) continue;

                bool wasDefault = System.Text.RegularExpressions.Regex.IsMatch(name ?? string.Empty, @"^Submarine-\d+$");
                var rec = new SubmarineRecord { Name = name, Slot = i + 1, IsDefaultName = wasDefault };

                try
                {
                    var aliases = Config.SlotAliases;
                    if (aliases != null && i >= 0 && i < aliases.Length)
                    {
                        var alias = aliases[i];
                        if (!string.IsNullOrWhiteSpace(alias) && wasDefault)
                            rec.Name = alias.Trim();
                    }
                }
                catch { }

                try
                {
                    int rank = s->RankId;
                    if (rank > 0) rec.Rank = rank;
                }
                catch { }

                try
                {
                    uint rt = s->ReturnTime;
                    if (rt > now)
                    {
                        var minutes = (int)((rt - now) / 60);
                        if (minutes >= 0 && minutes <= 60 * 72) rec.DurationMinutes = minutes;
                        rec.EtaUnix = rt;
                    }
                }
                catch { }

                // Optional: route key from CurrentExplorationPoints
                try
                {
                    var pts = new System.Collections.Generic.List<byte>(5);
                    for (int j = 0; j < 5; j++)
                    {
                        try
                        {
                            byte v = s->CurrentExplorationPoints[j];
                            if (v >= 1 && v <= 255) pts.Add(v);
                        }
                        catch { break; }
                    }

                    int fieldOff = -1;
                    try { unsafe { fixed (byte* rp = s->CurrentExplorationPoints) { fieldOff = (int)((byte*)rp - (byte*)s); } } } catch { }

                    string routeFromMem = pts.Count > 0 ? FormatRouteKey(pts) : string.Empty;

                    // ルート補完: メモリ値が1～2点しかない場合、キャッシュ（前回フルルート）と突き合わせて補う
                    try
                    {
                        int slot = i + 1;
                        if (pts.Count >= 3)
                        {
                            rec.RouteKey = routeFromMem;
                            try { Config.LastRouteBySlot[slot] = rec.RouteKey; SaveConfig(); } catch { }
                        }
                        else if (pts.Count > 0)
                        {
                            rec.RouteKey = routeFromMem;
                            if (Config.LastRouteBySlot != null && Config.LastRouteBySlot.TryGetValue(slot, out var prev) && !string.IsNullOrWhiteSpace(prev))
                            {
                                var prevNums = ParseRouteNumbers(prev);
                                var curNums = pts.ConvertAll(b => (int)b);
                                if (IsSuffix(prevNums, curNums))
                                    rec.RouteKey = prev; // 末尾一致なら過去のフルルートを採用
                            }
                        }
                        else
                        {
                            if (Config.LastRouteBySlot != null && Config.LastRouteBySlot.TryGetValue(slot, out var prev) && !string.IsNullOrWhiteSpace(prev))
                                rec.RouteKey = prev;
                        }
                    }
                    catch { rec.RouteKey = routeFromMem; }

                    try { Services.XsrDebug.Log(Config, $"S{i + 1} route bytes = {(pts.Count>0?string.Join(",", pts):"(none)")}"); } catch { }
                    try { TryWriteExtractTrace(new System.Collections.Generic.List<string> { $"route: off=0x{fieldOff:X},stride=1 -> [{string.Join(",", pts)}]" }); } catch { }

                    // Re-evaluate route using cache + contiguous-subsequence logic (review plan)
                    try
                    {
                        var memNums = new System.Collections.Generic.List<int>(pts.Count);
                        foreach (var b in pts) memNums.Add((int)b);
                        var cached = TryGetCachedNumbers(i + 1);
                        System.Collections.Generic.List<int> routeNumbers;
                        bool adoptedCache = false; string reason = "mem";
                        if (memNums.Count >= 3)
                        {
                            routeNumbers = memNums; SaveCache(i + 1, routeNumbers);
                        }
                        else if (memNums.Count > 0 && cached != null && cached.Count >= 3)
                        {
                            var memR = ReverseCopy(memNums);
                            if (ContainsContiguousSubsequence(cached, memNums)) { routeNumbers = cached; adoptedCache = true; reason = "subseq"; }
                            else if (ContainsContiguousSubsequence(cached, memR)) { routeNumbers = cached; adoptedCache = true; reason = "reverse-subseq"; }
                            else { routeNumbers = memNums; }
                        }
                        else if (memNums.Count == 0 && cached != null && cached.Count >= 3)
                        {
                            routeNumbers = cached; adoptedCache = true; reason = "none";
                        }
                        else
                        {
                            routeNumbers = memNums;
                        }

                        rec.RouteKey = BuildRouteKeyFromNumbers(routeNumbers);
                        if (rec.Extra == null) rec.Extra = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal);
                        rec.Extra["RouteShort"] = BuildRouteShortFromNumbers(routeNumbers);
                        // UIパネルからの補完（SelectString限定から、複数アドオン走査へ拡張）
                        try
                        {
                            if (routeNumbers.Count < 3)
                            {
                                var addonNames = new[] {
                                    "SelectString","SelectIconString",
                                    "CompanyCraftSubmersibleList","FreeCompanyWorkshopSubmersible","CompanyCraftSubmersible","CompanyCraftList",
                                    "SubmersibleExploration","SubmarineExploration","SubmersibleVoyage","ExplorationResult",
                                };
                                foreach (var an in addonNames)
                                {
                                    if (TryCaptureFromAddon(an, out var snapUi) && snapUi != null && snapUi.Items != null)
                                    {
                                        var match = snapUi.Items.FirstOrDefault(x => string.Equals((x.Name ?? string.Empty).Trim(), (rec.Name ?? string.Empty).Trim(), System.StringComparison.Ordinal));
                                        var k = match?.RouteKey;
                                        var numsUi = string.IsNullOrWhiteSpace(k) ? null : ParseRouteNumbers(k);
                                        if (numsUi != null && numsUi.Count >= 3)
                                        {
                                            rec.RouteKey = BuildRouteKeyFromNumbers(numsUi);
                                            if (rec.Extra == null) rec.Extra = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal);
                                            rec.Extra["RouteShort"] = BuildRouteShortFromNumbers(numsUi);
                                            SaveCache(i + 1, numsUi);
                                            routeNumbers = numsUi; adoptedCache = true; reason = "ui-panels";
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        catch { }

                        var memLog = string.Join(",", memNums);
                        var cacheLog = cached == null ? "-" : string.Join(",", cached);
                        var finalLog = string.Join(",", routeNumbers);
                        Services.XsrDebug.Log(Config, $"S{i + 1} route mem=[{memLog}], cache=[{cacheLog}], adopted={(adoptedCache?"cache":"mem")}, reason={reason}, final=[{finalLog}]");
                    }
                    catch { }
                }
                catch { }

                snapshot.Items.Add(rec);
            }
            // 事後リペア: スナップショット内に短いルートが残っている場合、可視パネルから一括補完
            try
            {
                bool NeedsRepair(string? rk) => ParseRouteNumbers(rk ?? string.Empty).Count < 3;
                if (snapshot.Items.Any(it => NeedsRepair(it.RouteKey)))
                {
                    var addonNames = new[] {
                        "CompanyCraftSubmersibleList","FreeCompanyWorkshopSubmersible","CompanyCraftSubmersible","CompanyCraftList",
                        "SubmersibleExploration","SubmarineExploration","SubmersibleVoyage","ExplorationResult",
                        "SelectString","SelectIconString",
                    };
                    foreach (var an in addonNames)
                    {
                        if (!snapshot.Items.Any(it => NeedsRepair(it.RouteKey))) break;
                        if (!TryCaptureFromAddon(an, out var snap2) || snap2?.Items == null) continue;
                        foreach (var rec in snapshot.Items)
                        {
                            if (!NeedsRepair(rec.RouteKey)) continue;
                            var m = snap2.Items.FirstOrDefault(x => string.Equals((x.Name ?? string.Empty).Trim(), (rec.Name ?? string.Empty).Trim(), System.StringComparison.Ordinal));
                            var k = m?.RouteKey; if (string.IsNullOrWhiteSpace(k)) continue;
                            var nums = ParseRouteNumbers(k);
                            if (nums.Count >= 3)
                            {
                                rec.RouteKey = BuildRouteKeyFromNumbers(nums);
                                if (rec.Extra == null) rec.Extra = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal);
                                rec.Extra["RouteShort"] = BuildRouteShortFromNumbers(nums);
                                SaveCache(rec.Slot ?? 0, nums);
                                Services.XsrDebug.Log(Config, $"S{rec.Slot} route repaired via ui-panels -> [{string.Join(",", nums)}]");
                            }
                        }
                    }
                }
            }
            catch { }

            return snapshot.Items.Count > 0;
        }
        catch { return false; }
    }

    private unsafe bool TryCaptureFromSelectStringFast(AtkUnitBase* unit, out List<string> items)
    {
        items = new List<string>();
        try
        {
            var sel = (AddonSelectString*)unit;
            if (sel == null) return false;

            ref var popup = ref sel->PopupMenu.PopupMenu;
            var cnt = popup.EntryCount;
            var names = popup.EntryNames;
            if (cnt > 0 && cnt <= 64 && names != null)
            {
                for (int i = 0; i < cnt; i++)
                {
                    try
                    {
                        var ptr = (nint)names[i].Value;
                        if (ptr != 0)
                        {
                            var ss = MemoryHelper.ReadSeStringNullTerminated(ptr);
                            var txt = ss?.TextValue ?? ss?.ToString() ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(txt))
                                items.Add(NormalizeItemText(txt));
                        }
                    }
                    catch { }
                }
            }

            // ノイズ除去
            try
            {
                items.RemoveAll(s =>
                {
                    var t = NormalizeItemText(s ?? string.Empty);
                    if (string.IsNullOrWhiteSpace(t)) return true;
                    if (System.Text.RegularExpressions.Regex.IsMatch(t, @"^Submarine-\d+$")) return true;
                    if (t.Equals("やめる", StringComparison.Ordinal)) return true;
                    if (t.StartsWith("潜水艦を選択してください", StringComparison.Ordinal)) return true;
                    if (t.StartsWith("探索機体数", StringComparison.Ordinal)) return true;
                    if (t.StartsWith("保有燃料数", StringComparison.Ordinal)) return true;
                    return false;
                });
            }
            catch { }
            return items.Count > 0;
        }
        catch { return false; }
    }

    // 可視レンダラ保険（簡易）
    private unsafe List<string> CollectSelectStringItems(AtkUnitBase* atk)
    {
        var results = new List<string>();
        if (atk == null) return results;
        try
        {
            var sel = (AddonSelectString*)atk;
            var list = sel->PopupMenu.PopupMenu.List;
            if (list == null) return results;
            int len = list->ListLength;
            for (int i = 0; i < len; i++)
            {
                try
                {
                    var renderer = list->GetItemRenderer(i);
                    if (renderer == null) continue;
                    var uld = renderer->UldManager;
                    int c = uld.NodeListCount;
                    for (int j = 0; j < c; j++)
                    {
                        var node = uld.NodeList[j];
                        if (node == null || node->Type != NodeType.Text) continue;
                        var t = ((AtkTextNode*)node)->NodeText.ToString();
                        if (!string.IsNullOrWhiteSpace(t)) { results.Add(NormalizeItemText(t)); break; }
                    }
                }
                catch { }
            }
        }
        catch { }
        return results;
    }

    private static string NormalizeItemText(string s)
    {
        var t = (s ?? string.Empty).Trim();
        var br = t.IndexOf('[');
        if (br > 1) t = t.Substring(0, br).Trim();
        return t;
    }

    private void OnCmdRoot(string cmd, string args)
    {
        try
        {
            var a = (args ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(a))
            {
                _chat.Print($"[Submarines] Version: {typeof(Plugin).Assembly.GetName().Version?.ToString(3) ?? "0.0.0"}");
                _chat.Print("[Submarines] Commands: dump | dumpmem | learnnames | ui | cfg mem on|off | open | addon <name> | version");
                _chat.Print("  /xsr dump  -> same as /subdump");
                _chat.Print("  /xsr open  -> same as /subopen");
                _chat.Print("  /xsr addon <name> -> same as /subaddon <name>");
                _chat.Print("  /xsr version -> print plugin version");
                _chat.Print("  /xsr probe -> check common addon names");
                _chat.Print("  /xsr dumpstage -> scan many addons on screen");
                return;
            }

            var parts = a.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var sub = parts[0].ToLowerInvariant();
            var rest = parts.Length > 1 ? parts[1] : string.Empty;
            switch (sub)
            {
                case "dump":
                    OnCmdDump(CmdDump, rest);
                    break;
                case "dumpmem":
                    CmdDumpFromMemory();
                    break;
                case "version":
                    _chat.Print($"[Submarines] Version: {typeof(Plugin).Assembly.GetName().Version?.ToString(3) ?? "0.0.0"}");
                    break;
                case "ui":
                    _showUI = true;
                    break;
                case "learnnames":
                case "leannames":
                    CmdLearnNames();
                    break;
                case "open":
                    OnCmdOpen(CmdOpen, rest);
                    break;
                case "cfg":
                case "config":
                    OnCmdCfg(rest);
                    break;
                case "addon":
                    OnCmdSetAddon(CmdSetAddon, rest);
                    break;
                case "probe":
                    CmdProbe();
                    break;
                case "dumpstage":
                    CmdDumpStage();
                    break;
            // removed: selftest command
            // case "selftest":
            //     CmdSelfTest();
            //     break;
                default:
                    _chat.Print("[Submarines] Unknown subcommand. Try: /xsr help");
                    break;
            }
        }
        catch (Exception ex)
        {
            _chat.PrintError($"[Submarines] �G���[: {ex.Message}");
        }
    }

    private void OnCmdSetAddon(string cmd, string args)
    {
        var name = (args ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            _chat.Print($"[Submarines] 現在のアドオン名: {Config.AddonName}");
            return;
        }
        Config.AddonName = name;
        SaveConfig();
        _chat.Print($"[Submarines] アドオン名を更新しました: {name}");
    }

    public void SaveConfig() => Config.Save(_pi);

    private unsafe AtkUnitBase* ResolveAddonPtr(string name)
    {
        try
        {
            for (int i = 0; i < 32; i++)
            {
                var u = ToPtr(_gameGui.GetAddonByName(name, i));
                if (u != null && u->IsVisible) return u;
            }
            for (int i = 0; i < 32; i++)
            {
                var u = ToPtr(_gameGui.GetAddonByName(name, i));
                if (u != null) return u;
            }
            return null;
        }
        catch { return null; }
    }

    private unsafe AtkUnitBase* ToPtr(object obj)
    {
        try
        {
            if (obj is nint ni && ni != 0) return (AtkUnitBase*)ni;
            if (obj is IntPtr ip && ip != IntPtr.Zero) return (AtkUnitBase*)ip;
            dynamic d = obj;
            try { nint a = (nint)d.Address; if (a != 0) return (AtkUnitBase*)a; } catch { }
            try { nint v = (nint)d.Value; if (v != 0) return (AtkUnitBase*)v; } catch { }
            try { nint s = (nint)d; if (s != 0) return (AtkUnitBase*)s; } catch { }
        }
        catch { }
        return null;
    }

    private unsafe void CmdProbe()
    {
        var candidates = new[]
        {
            Config.AddonName,
            "SelectString", "SelectIconString",
            "CompanyCraftSubmersibleList", "FreeCompanyWorkshopSubmersible", "CompanyCraftSubmersible", "CompanyCraftList",
            "SubmersibleExploration", "SubmarineExploration", "SubmersibleVoyage", "ExplorationResult",
        };
        _chat.Print("[Submarines] Probing addon availability...");
        foreach (var n in candidates.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.Ordinal))
        {
            bool shown = false, exists = false; int foundIdx = -1;
            for (int i = 0; i < 16; i++)
            {
                var u = ToPtr(_gameGui.GetAddonByName(n, i));
                if (u != null)
                {
                    exists = true;
                    if (u->IsVisible) { shown = true; foundIdx = i; break; }
                }
            }
            _chat.Print($"  {n}: " + (shown ? $"visible (idx={foundIdx})" : (exists ? "exists (hidden)" : "not found")));
        }
    }

    private unsafe void CmdDumpPlus2()
    {
#if !DEBUG
        _chat.Print("[Submarines] dumpplus2 is disabled in release build.");
        return;
#else
        _chat.Print("[Submarines] debug only");
#endif
    }

    private unsafe void CmdDumpPlus()
    {
#if !DEBUG
        _chat.Print("[Submarines] dumpplus is disabled in release build.");
        return;
#else
        _chat.Print("[Submarines] debug only");
#endif
    }

    private void TryWriteExtractTrace(List<string> trace)
    {
        try
        {
            if (trace == null || trace.Count == 0) return;
            var dir = System.IO.Path.GetDirectoryName(BridgeWriter.CurrentFilePath()) ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var log = System.IO.Path.Combine(dir, "extract_log.txt");
            System.IO.File.WriteAllLines(log, trace);
        }
        catch { }
    }

    // ルート名マップ（必要に応じて拡張）
    private string FormatRouteKey(System.Collections.Generic.List<byte> pts)
    {
        var names = new System.Collections.Generic.List<string>(pts.Count);
        foreach (var p in pts)
        {
            // Always store numeric route key (Point-<id>) for stable display conversion
            names.Add($"Point-{p}");
            
            
        }
        return string.Join(" - ", names);
    }

    private static System.Collections.Generic.List<int> ParseRouteNumbers(string routeKey)
    {
        var nums = new System.Collections.Generic.List<int>();
        try
        {
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(routeKey ?? string.Empty, @"(\d+)"))
                if (int.TryParse(m.Groups[1].Value, out var v)) nums.Add(v);
        }
        catch { }
        return nums;
    }

    // 数値配列 → RouteKey（Point-xx - ...）
    private static string BuildRouteKeyFromNumbers(System.Collections.Generic.List<int> nums)
    {
        try { return string.Join(" - ", nums.ConvertAll(n => $"Point-{n}")); }
        catch { return string.Empty; }
    }

    // (duplicate removed; see IsSuffix below)

    private static System.Collections.Generic.List<int> ReverseCopy(System.Collections.Generic.List<int> v)
    {
        try { var c = new System.Collections.Generic.List<int>(v); c.Reverse(); return c; } catch { return v; }
    }

    private static bool ContainsContiguousSubsequence(System.Collections.Generic.List<int> sup, System.Collections.Generic.List<int> sub)
    {
        try
        {
            if (sup == null || sub == null) return false;
            if (sub.Count == 0 || sup.Count < sub.Count) return false;
            for (int i = 0; i <= sup.Count - sub.Count; i++)
            {
                bool ok = true;
                for (int j = 0; j < sub.Count; j++)
                {
                    if (sup[i + j] != sub[j]) { ok = false; break; }
                }
                if (ok) return true;
            }
            return false;
        }
        catch { return false; }
    }

    private System.Collections.Generic.List<int>? TryGetCachedNumbers(int slot)
    {
        try
        {
            if (Config.LastRouteBySlot != null && Config.LastRouteBySlot.TryGetValue(slot, out var s) && !string.IsNullOrWhiteSpace(s))
                return ParseRouteNumbers(s);
        }
        catch { }
        return null;
    }

    private void SaveCache(int slot, System.Collections.Generic.List<int> numbers)
    {
        try
        {
            if (numbers == null || numbers.Count < 3) return; // フルのみ保存
            if (Config.LastRouteBySlot == null) Config.LastRouteBySlot = new System.Collections.Generic.Dictionary<int, string>();
            Config.LastRouteBySlot[slot] = BuildRouteKeyFromNumbers(numbers);
            SaveConfig();
        }
        catch { }
    }

    private string BuildRouteShortFromNumbers(System.Collections.Generic.List<int> nums)
    {
        try
        {
            var parts = new System.Collections.Generic.List<string>(nums.Count);
            var hint = Config.SectorMapHint;
            foreach (var n in nums)
            {
                string? letter = null;
                try { letter = _sectorResolver?.GetAliasForSector((uint)n, hint); } catch { }
                if (string.IsNullOrWhiteSpace(letter) && Config.RouteNames != null && Config.RouteNames.TryGetValue((byte)n, out var nm) && !string.IsNullOrWhiteSpace(nm))
                    letter = nm;
                parts.Add(string.IsNullOrWhiteSpace(letter) ? $"P{n}" : letter!);
            }
            return string.Join('>', parts);
        }
        catch { return string.Empty; }
    }

    private static bool IsSuffix(System.Collections.Generic.List<int> full, System.Collections.Generic.List<int> tail)
    {
        try
        {
            if (full == null || tail == null) return false;
            if (tail.Count == 0 || full.Count < tail.Count) return false;
            for (int i = 0; i < tail.Count; i++)
            {
                if (full[full.Count - tail.Count + i] != tail[i]) return false;
            }
            return true;
        }
        catch { return false; }
    }

    private static System.Collections.Generic.List<string> InferNamesFromLines(System.Collections.Generic.List<string> lines)
    {
        var result = new System.Collections.Generic.List<string>(4);
        var seen = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
        foreach (var raw in lines)
        {
            var nm = NormalizeItemText(raw ?? string.Empty);
            if (string.IsNullOrWhiteSpace(nm)) continue;
            if (System.Text.RegularExpressions.Regex.IsMatch(nm, @"^Submarine-\d+$")) continue;
            if (System.Text.RegularExpressions.Regex.IsMatch(nm, @"(残り時間|探索|補給|rank|ランク|航路|行き先|目的地|出発|出航|燃料|機体|選択してください|報告|中止|海底図|確認)", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                continue;
            if (nm.Length < 2 || nm.Length > 32) continue;
            if (seen.Add(nm)) result.Add(nm);
            if (result.Count >= 4) break;
        }
        return result;
    }

    private bool TryCaptureFromConfiguredAddon(out SubmarineSnapshot snapshot)
    {
        var name = Config.AddonName;
        if (!string.IsNullOrWhiteSpace(name) && TryCaptureFromAddon(name, out snapshot))
            return true;

        var fallbacks = new[] { "SelectString", "SelectIconString", "SelectYesno" };
        foreach (var fb in fallbacks)
            if (TryCaptureFromAddon(fb, out snapshot))
                return true;

        if (Config.AggressiveFallback)
        {
            var fb2 = new[]
            {
                "CompanyCraftSubmersibleList",
                "FreeCompanyWorkshopSubmersible",
                "CompanyCraftSubmersible",
                "CompanyCraftList",
                "SubmersibleExploration",
                "SubmarineExploration",
                "SubmersibleVoyage",
                "ExplorationResult",
            };
            foreach (var n in fb2)
                if (TryCaptureFromAddon(n, out snapshot))
                    return true;
        }
        snapshot = default!;
        return false;
    }

    private unsafe bool TryCaptureFromAddon(string addonName, out SubmarineSnapshot snapshot)
    {
        snapshot = new SubmarineSnapshot
        {
            PluginVersion = typeof(Plugin).Assembly.GetName().Version?.ToString(3) ?? "0.0.0",
            Note = $"captured from addon '{addonName}'",
            Items = new List<SubmarineRecord>()
        };

        try
        {
            var unit = ResolveAddonPtr(addonName);
            if (unit == null) return false;
            bool selectLike = string.Equals(addonName, "SelectString", StringComparison.Ordinal) || string.Equals(addonName, "SelectIconString", StringComparison.Ordinal);

            var lines = new List<string>();
            if (Config.UseSelectStringExtraction && selectLike)
            {
                try
                {
                    var items1 = CollectSelectStringItems(unit);
                    if (items1 != null && items1.Count > 0)
                        lines.AddRange(items1);
                }
                catch { }
                if (TryCaptureFromSelectStringFast(unit, out var itemTexts) && itemTexts.Count > 0)
                {
                    try
                    {
                        var filtered = itemTexts
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .Select(NormalizeItemText)
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .Where(s => !s.Equals("やめる", StringComparison.Ordinal))
                            .Where(s => !s.StartsWith("潜水艦を選択してください", StringComparison.Ordinal))
                            .Where(s => !s.StartsWith("探索機体数", StringComparison.Ordinal))
                            .Where(s => !s.StartsWith("保有燃料数", StringComparison.Ordinal))
                            .ToList();
                        if (filtered.Count > 0) lines.AddRange(filtered);
                    }
                    catch { lines.AddRange(itemTexts); }
                }
            }

            if (!selectLike || Config.UseSelectStringDetailExtraction)
            {
                try
                {
                    var count = unit->UldManager.NodeListCount;
                    for (var i = 0; i < count; i++)
                    {
                        var node = unit->UldManager.NodeList[i];
                        if (node == null) continue;
                        if (node->Type == NodeType.Text)
                        {
                            var text = ((AtkTextNode*)node)->NodeText.ToString();
                            if (!string.IsNullOrWhiteSpace(text))
                                lines.Add(text.Trim());
                        }
                    }

                    var visited = new HashSet<nint>();
                    void Dfs(AtkResNode* n, int depth)
                    {
                        if (n == null || depth > 2048) return;
                        var key = (nint)n; if (!visited.Add(key)) return;
                        if (n->Type == NodeType.Text)
                        {
                            var t = ((AtkTextNode*)n)->NodeText.ToString();
                            if (!string.IsNullOrWhiteSpace(t)) lines.Add(t.Trim());
                        }
                        else if (n->Type == NodeType.Component)
                        {
                            try
                            {
                                var comp = ((AtkComponentNode*)n)->Component;
                                if (comp != null)
                                {
                                    var root = comp->UldManager.RootNode; if (root != null) Dfs(root, depth + 1);
                                    var cnt = comp->UldManager.NodeListCount;
                                    for (var j = 0; j < cnt; j++)
                                    {
                                        var cn = comp->UldManager.NodeList[j]; if (cn != null) Dfs(cn, depth + 1);
                                    }
                                }
                            }
                            catch { }
                        }
                        if (n->ChildNode != null) Dfs(n->ChildNode, depth + 1);
                        if (n->NextSiblingNode != null) Dfs(n->NextSiblingNode, depth);
                    }
                    try { if (unit->RootNode != null) Dfs(unit->RootNode, 0); } catch { }
                }
                catch (Exception ex)
                {
                    _log.Warning(ex, "Traverse addon nodes failed");
                }
            }

            try { lines.RemoveAll(raw => System.Text.RegularExpressions.Regex.IsMatch(NormalizeItemText(raw ?? string.Empty), @"^Submarine-\d+$")); } catch { }
            if (lines.Count == 0)
                return false;

            if (Extractors.ExtractFromLines(lines, snapshot, out var trace))
            {
                try { trace.Insert(0, $"addon={addonName} lines={lines.Count} items={snapshot.Items.Count}"); } catch { }
                try { EnrichFromWorkshopPanels(snapshot); } catch { }
                try
                {
                    snapshot.Items = snapshot.Items
                        .OrderBy(x => x.DurationMinutes.HasValue ? 0 : 1)
                        .ThenBy(x => x.DurationMinutes ?? int.MaxValue)
                        .ThenBy(x => x.Name, StringComparer.Ordinal)
                        .Take(4)
                        .ToList();
                }
                catch { }
                TryWriteExtractTrace(trace);
                return true;
            }

            if (selectLike)
            {
                try
                {
                    if (EnrichFromWorkshopPanels(snapshot) && snapshot.Items.Count > 0)
                    {
                        try
                        {
                            snapshot.Items = snapshot.Items
                                .OrderBy(x => x.DurationMinutes.HasValue ? 0 : 1)
                                .ThenBy(x => x.DurationMinutes ?? int.MaxValue)
                                .ThenBy(x => x.Name, StringComparer.Ordinal)
                                .Take(4)
                                .ToList();
                        }
                        catch { }
                        return true;
                    }
                }
                catch { }
                _chat.Print("[Submarines] この画面はプレースホルダです。工房の潜水艦一覧（右パネルに残り時間が出る画面）を開いて /xsr dump を実行してください。");
            }
            return false;
        }
        catch (Exception ex)
        {
            _log.Error(ex, $"TryCaptureFromAddon failed for '{addonName}'");
            return false;
        }
    }

    // 工房パネルからの“追い取り”で不足項目（時間/ランク/航路）を補完
    private unsafe bool EnrichFromWorkshopPanels(SubmarineSnapshot snapshot)
    {
        try
        {
            var candidates = new[]
            {
                "CompanyCraftSubmersibleList",
                "FreeCompanyWorkshopSubmersible",
                "CompanyCraftSubmersible",
                "CompanyCraftList",
                "SubmersibleExploration",
                "SubmarineExploration",
                "SubmersibleVoyage",
                "ExplorationResult",
            };

            var extraLines = new List<string>();
            foreach (var name in candidates)
            {
                for (int idx = 0; idx < 32; idx++)
                {
                    var unit = ToPtr(_gameGui.GetAddonByName(name, idx));
                    if (unit == null) continue;
                    try { CollectTextLines(unit, extraLines); } catch { }
                }
            }
            if (extraLines.Count == 0)
                return false;

            var tmp = new SubmarineSnapshot
            {
                PluginVersion = typeof(Plugin).Assembly.GetName().Version?.ToString(3) ?? "0.0.0",
                Note = "enriched from workshop panels",
                Items = new List<SubmarineRecord>()
            };
            if (!Extractors.ExtractFromLines(extraLines, tmp, out var _))
                return false;

            foreach (var rec in tmp.Items)
            {
                var name = NormalizeItemText(rec.Name ?? string.Empty);
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (System.Text.RegularExpressions.Regex.IsMatch(name, @"^Submarine-\d+$"))
                    continue; // プレースホルダは追加しない
                var existing = snapshot.Items.FirstOrDefault(x => x.Name.Equals(name, StringComparison.Ordinal));
                if (existing == null)
                {
                    snapshot.Items.Add(new SubmarineRecord
                    {
                        Name = name,
                        DurationMinutes = rec.DurationMinutes,
                        Rank = rec.Rank,
                        RouteKey = rec.RouteKey,
                    });
                    continue;
                }
                if (!existing.DurationMinutes.HasValue && rec.DurationMinutes.HasValue)
                    existing.DurationMinutes = rec.DurationMinutes;
                if (!existing.Rank.HasValue && rec.Rank.HasValue)
                    existing.Rank = rec.Rank;
                if (string.IsNullOrEmpty(existing.RouteKey) && !string.IsNullOrEmpty(rec.RouteKey))
                    existing.RouteKey = rec.RouteKey;
            }
            return snapshot.Items.Count > 0;
        }
        catch { return false; }
    }

    private unsafe void CollectTextLines(AtkUnitBase* unit, List<string> lines)
    {
        if (unit == null) return;
        try
        {
            var count = unit->UldManager.NodeListCount;
            for (var i = 0; i < count; i++)
            {
                var node = unit->UldManager.NodeList[i];
                if (node == null) continue;
                if (node->Type == NodeType.Text)
                {
                    var text = ((AtkTextNode*)node)->NodeText.ToString();
                    if (!string.IsNullOrWhiteSpace(text)) lines.Add(text.Trim());
                }
            }
            var visited = new HashSet<nint>();
            void Dfs(AtkResNode* n, int depth)
            {
                if (n == null || depth > 2048) return;
                var key = (nint)n; if (!visited.Add(key)) return;
                if (n->Type == NodeType.Text)
                {
                    try
                    {
                        var t = ((AtkTextNode*)n)->NodeText.ToString();
                        if (!string.IsNullOrWhiteSpace(t)) lines.Add(t.Trim());
                    }
                    catch { }
                }
                else if (n->Type == NodeType.Component)
                {
                    try
                    {
                        var comp = ((AtkComponentNode*)n)->Component;
                        if (comp != null)
                        {
                            var root = comp->UldManager.RootNode; if (root != null) Dfs(root, depth + 1);
                            var cnt = comp->UldManager.NodeListCount;
                            for (var j = 0; j < cnt; j++)
                            {
                                var cn = comp->UldManager.NodeList[j]; if (cn != null) Dfs(cn, depth + 1);
                            }
                        }
                    }
                    catch { }
                }
                if (n->ChildNode != null) Dfs(n->ChildNode, depth + 1);
                if (n->NextSiblingNode != null) Dfs(n->NextSiblingNode, depth);
            }
            try { if (unit->RootNode != null) Dfs(unit->RootNode, 0); } catch { }
        }
        catch { }
    }
    public void OpenBridgeFolder()
    {
        try
        {
            var path = System.IO.Path.GetDirectoryName(BridgeWriter.CurrentFilePath());
            if (!string.IsNullOrEmpty(path))
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch { }
    }

    public void CaptureNow()
    {
        var snap = new SubmarineSnapshot
        {
            PluginVersion = typeof(Plugin).Assembly.GetName().Version?.ToString(3) ?? "0.0.0",
            Note = "sample (replace with real capture)",
            Items = new List<SubmarineRecord>
            {
                new() { Name = "Submarine-1", RouteKey = "R-Alpha", DurationMinutes = 180 },
                new() { Name = "Submarine-2", RouteKey = "R-Beta", DurationMinutes = 210 }
            }
        };
        try { EtaFormatter.Enrich(snap); } catch { }
        BridgeWriter.WriteIfChanged(snap);
            _chat.Print( $"[Submarines] JSONを書き出しました: {BridgeWriter.CurrentFilePath()}"); 
    }

    private bool _notifiedThisVisibility;

    // Removed: self-test entry point (kept for historical reference, now unused)
    private void XSR_Removed_CmdSelfTest()
    {
        try
        {
            var cases = new[]
            {
                "1d 2h 30m 15s",
                "45m",
                "00:07:05",
                "1:30",
                "16時間57分",
                "1時間2分"
            };
            var lines = new List<string>();
            lines.Add("[XSR] Duration parser self-test");
            foreach (var s in cases)
            {
                try
                {
                    var mi = typeof(Extractors).GetMethod("TryParseDurationEx", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    var t = mi?.Invoke(null, new object[] { s });
                    lines.Add($"{s} => {t ?? "null"}");
                }
                catch (Exception ex) { lines.Add($"{s} => error: {ex.Message}"); }
            }
            var dir = System.IO.Path.GetDirectoryName(BridgeWriter.CurrentFilePath()) ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var path = System.IO.Path.Combine(dir, "selftest_report.txt");
            System.IO.File.WriteAllLines(path, lines);
            _chat.Print($"[Submarines] selftest written: {path}");
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "CmdSelfTest failed");
        }
    }
    private void TrySetIdentity(SubmarineSnapshot snap)
    {
        try { /* placeholder for future identity injection */ }
        catch { }
    }

    private void TryAdoptPreviousRoutes(SubmarineSnapshot snap)
    {
        try
        {
            var path = BridgeWriter.CurrentFilePath();
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path)) return;
            var prevJson = System.IO.File.ReadAllText(path);
            var prev = System.Text.Json.JsonSerializer.Deserialize<SubmarineSnapshot>(prevJson);
            if (prev?.Items == null || prev.Items.Count == 0) return;

            int CountNums(string? rk) => ParseRouteNumbers(rk ?? string.Empty).Count;
            foreach (var it in snap.Items)
            {
                if (it == null) continue;
                var curCnt = CountNums(it.RouteKey);
                if (curCnt >= 3) continue; // 既に十分

                SubmarineRecord? match = null;
                // 名前一致を優先、なければスロット一致
                if (!string.IsNullOrWhiteSpace(it.Name))
                    match = prev.Items.FirstOrDefault(x => string.Equals((x.Name ?? string.Empty).Trim(), (it.Name ?? string.Empty).Trim(), StringComparison.Ordinal));
                if (match == null && it.Slot.HasValue)
                    match = prev.Items.FirstOrDefault(x => x.Slot == it.Slot);
                if (match == null) continue;

                var newCnt = CountNums(match.RouteKey);
                if (newCnt >= 3)
                {
                    it.RouteKey = match.RouteKey;
                    if (it.Extra == null) it.Extra = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);
                    it.Extra["RouteShort"] = BuildRouteShortFromNumbers(ParseRouteNumbers(match.RouteKey));
                    try { SaveCache(it.Slot ?? 0, ParseRouteNumbers(match.RouteKey)); } catch { }
                }
            }
        }
        catch { }
    }

    // Fallback seeding: parse xsr_debug.log to adopt last known full routes per slot
    private void TryAdoptFromLogs(SubmarineSnapshot snap)
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(BridgeWriter.CurrentFilePath()) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(dir)) return;
            var logPath = System.IO.Path.Combine(dir, "xsr_debug.log");
            if (!System.IO.File.Exists(logPath)) return;
            var lines = System.IO.File.ReadAllLines(logPath);
            var map = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<int>>();
            foreach (var raw in lines)
            {
                try
                {
                    // Patterns:
                    // "S1 route bytes = 13,18,15,10,26"
                    var s = raw?.Trim() ?? string.Empty;
                    if (s.Length == 0) continue;
                    int idxS = s.IndexOf("S"); if (idxS < 0) continue;
                    int idxSpace = s.IndexOf(' ', idxS+1); if (idxSpace < 0) continue;
                    var slotStr = s.Substring(idxS+1, idxSpace-(idxS+1));
                    if (!int.TryParse(slotStr, out var slot)) continue;
                    var p = s.IndexOf("route bytes ="); if (p < 0) continue;
                    var listStr = s.Substring(p + "route bytes =".Length).Trim();
                    var nums = new System.Collections.Generic.List<int>();
                    foreach (var t in listStr.Split(new[]{',',' '}, System.StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (int.TryParse(t, out var v)) nums.Add(v);
                    }
                    if (nums.Count >= 3) map[slot] = nums; // always keep last occurrence
                }
                catch { }
            }
            if (map.Count == 0) return;

            foreach (var it in snap.Items)
            {
                try
                {
                    var curCnt = ParseRouteNumbers(it.RouteKey ?? string.Empty).Count;
                    if (curCnt >= 3) continue;
                    var slot = it.Slot ?? 0; if (slot <= 0) continue;
                    if (!map.TryGetValue(slot, out var nums) || nums == null || nums.Count < 3) continue;
                    it.RouteKey = BuildRouteKeyFromNumbers(nums);
                    if (it.Extra == null) it.Extra = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal);
                    it.Extra["RouteShort"] = BuildRouteShortFromNumbers(nums);
                    try { SaveCache(slot, nums); } catch { }
                }
                catch { }
            }
        }
        catch { }
    }

    private unsafe void OnFrameworkUpdate(IFramework _)
    {
        // 1秒間隔でアラームTick
        try
        {
            var nowUtc = DateTime.UtcNow;
            if ((nowUtc - _lastTickUtc) >= TimeSpan.FromSeconds(1))
            {
                _lastTickUtc = nowUtc;
                try { _alarm?.Tick(DateTimeOffset.UtcNow); } catch { }
            }
        }
        catch { }

        if (!Config.AutoCaptureOnWorkshopOpen) return;
        // Addon名が未設定でも、自動検出して可視状態を確認する
        var name = Config.AddonName;
        if (string.IsNullOrWhiteSpace(name))
        {
            try
            {
                var candidates = new[]
                {
                    "CompanyCraftSubmersibleList", "FreeCompanyWorkshopSubmersible", "CompanyCraftSubmersible", "CompanyCraftList",
                    "SubmersibleExploration", "SubmarineExploration", "SubmersibleVoyage", "ExplorationResult"
                };
                foreach (var n in candidates)
                {
                    unsafe { var u = ResolveAddonPtr(n); if (u != null && u->IsVisible) { name = n; break; } }
                }
                if (!string.IsNullOrWhiteSpace(name)) { Config.AddonName = name; SaveConfig(); }
            }
            catch { }
            if (string.IsNullOrWhiteSpace(name)) return;
        }
        try
        {
            var unit = ResolveAddonPtr(name);
            bool visible = unit != null && unit->IsVisible;
            if (visible && (!_wasAddonVisible || (DateTime.UtcNow - _lastAutoCaptureUtc) > TimeSpan.FromSeconds(10)))
            {
                if (!_notifiedThisVisibility)
                {
                    SubmarineSnapshot? snap;
                    bool ok = false;
                    try { ok = TryCaptureFromAddon(name, out snap); } catch { snap = null; }
                    if (!ok)
                    {
                        try { ok = TryCaptureFromMemory(out snap); } catch { snap = null; }
                    }
                    if (ok && snap != null)
                    {
                        try { EtaFormatter.Enrich(snap); } catch { }
                        try { TryAdoptPreviousRoutes(snap); } catch { }
                        try { _alarm?.UpdateSnapshot(snap); } catch { }
                        BridgeWriter.WriteIfChanged(snap);
                        _chat.Print("[Submarines] 自動取得しJSONを書き出しました。");
                        _log.Info("Auto-captured and wrote JSON");
                        _notifiedThisVisibility = true; // 可視セッション中は一度だけ通知
                    }
                }
                _lastAutoCaptureUtc = DateTime.UtcNow;
            }
            _wasAddonVisible = visible;
            if (!visible) _notifiedThisVisibility = false; // 非可視に戻ったらリセット
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "OnFrameworkUpdate auto-capture error");
        }
    }
}


