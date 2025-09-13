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
    private readonly object? _addonLifecycle; // AddonLifecycle (via reflection when available)
    private readonly IClientState? _client;

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
    private int _deferredCaptureFrames; // Addon可視化後の遅延キャプチャカウンタ
    private int _deferredCaptureFrames2; // 追加の再試行
    private string _deferredAddonName = string.Empty;

    private AlarmScheduler? _alarm;
    private DiscordNotifier? _discord;
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
        IClientState client,
        IToastGui toast)
    {
        _pi = pluginInterface;
        _cmd = commandManager;
        _chat = chat;
        _framework = framework;
        _gameGui = gameGui;
        _log = log;
        _toast = toast;
        _client = client;
        _addonLifecycle = null;

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
        try { SetupAddonLifecycleListeners(); } catch { }
        InitUI();

        // 設定移行（V1 -> V2: プロファイル導入）
        try { MigrateToProfilesV2IfNeeded(); } catch { }

        // ログイン中での自動作成はオプション
        try { if (Config.AutoCreateProfileOnLogin) EnsureActiveProfileFromClient(); } catch { }

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

            _alarm = new AlarmScheduler(Config, _chat, _toast, _log, _discord, _notion);
        }
        catch { }
    }

    private unsafe void SetupAddonLifecycleListeners()
    {
        if (!Config.EnableAddonLifecycleCapture) return;
        if (_addonLifecycle == null)
        {
            try { Services.XsrDebug.Log(Config, "AddonLifecycle not available; using deferred captures"); } catch { }
            return;
        }
        var names = new[]
        {
            Config.AddonName,
            "CompanyCraftSubmersibleList", "FreeCompanyWorkshopSubmersible", "CompanyCraftSubmersible", "CompanyCraftList",
            "SubmersibleExploration", "SubmarineExploration", "SubmersibleVoyage", "ExplorationResult",
        };
        // NOTE: Formal registration via Dalamud IAddonLifecycle will be attempted in a future build
    }


    private void TryPersistActiveProfileSnapshot(SubmarineSnapshot snap)
    {
        try
        {
            // 優先: 現在ログインしているキャラクターの CID へ保存（UI選択に依存しない）
            try
            {
                if (_client != null && _client.IsLoggedIn)
                {
                    ulong id = _client.LocalContentId;
                    if (id != 0)
                    {
                        var prof = GetOrCreateProfileById(id);
                        if (prof != null)
                        {
                            prof.LastSnapshot = snap;
                            SaveConfig();
                            try { Services.XsrDebug.Log(Config, $"PersistSnapshot: to client cid=0x{id:X}, items={(snap?.Items?.Count ?? 0)}"); } catch { }
                            return;
                        }
                    }
                }
            }
            catch { }

            // フォールバック: 現在のアクティブプロファイルに保存
            var p = GetActiveProfile();
            if (p == null) return;
            p.LastSnapshot = snap;
            SaveConfig();
            try { Services.XsrDebug.Log(Config, $"PersistSnapshot: to active cid=0x{(Config.ActiveContentId?.ToString("X") ?? "null")}, items={(snap?.Items?.Count ?? 0)}"); } catch { }
        }
        catch { }
    }

    // ログイン中のCID用プロフィールを作成/取得（UIの ActiveContentId は変更しない）
    private CharacterProfile? GetOrCreateProfileById(ulong id)
    {
        try
        {
            var list = Config.Profiles ?? new System.Collections.Generic.List<CharacterProfile>();
            var prof = list.FirstOrDefault(x => x.ContentId == id);
            if (prof == null)
            {
                prof = new CharacterProfile { ContentId = id };
                // 旧プレースホルダ（CID=0）からの引き継ぎ
                try
                {
                    var ph = list.FirstOrDefault(x => x.ContentId == 0);
                    if (ph != null)
                    {
                        try { if (prof.SlotAliases == null || prof.SlotAliases.Length == 0) prof.SlotAliases = ph.SlotAliases ?? new string[4]; } catch { }
                        try { if (prof.LastRouteBySlot == null || prof.LastRouteBySlot.Count == 0) prof.LastRouteBySlot = ph.LastRouteBySlot ?? new System.Collections.Generic.Dictionary<int, string>(); } catch { }
                        try { if (prof.RouteNames == null || prof.RouteNames.Count == 0) prof.RouteNames = ph.RouteNames ?? new System.Collections.Generic.Dictionary<byte, string>(); } catch { }
                        try { list.Remove(ph); } catch { }
                    }
                }
                catch { }
                list.Add(prof);
                Config.Profiles = list;
            }
            // メタ情報（可能なら更新）
            try { prof.LastSeenUtc = DateTimeOffset.UtcNow; } catch { }
            try
            {
                var lp = _client?.LocalPlayer;
                if (lp != null)
                {
                    try { prof.CharacterName = lp.Name?.TextValue ?? prof.CharacterName; } catch { }
                }
            }
            catch { }
            return prof;
        }
        catch { return null; }
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

    // ===== プロファイル補助 =====
    private CharacterProfile? GetActiveProfile()
    {
        try
        {
            var list = Config.Profiles ?? new System.Collections.Generic.List<CharacterProfile>();
            if (list.Count == 0) return null;
            if (Config.ActiveContentId.HasValue)
            {
                var p = list.FirstOrDefault(x => x.ContentId == Config.ActiveContentId.Value);
                if (p != null) return p;
            }
            return list[0];
        }
        catch { return null; }
    }

    private void EnsureActiveProfileFromClient()
    {
        try
        {
            if (_client == null) return;
            if (!_client.IsLoggedIn) return;
            ulong id = _client.LocalContentId;
            if (id == 0) return;

            try { Services.XsrDebug.Log(Config, $"EnsureActiveProfileFromClient: begin client=0x{id:X}, active=0x{Config.ActiveContentId?.ToString("X") ?? "null"}"); } catch { }
            var list = Config.Profiles ?? new System.Collections.Generic.List<CharacterProfile>();
            var prof = list.FirstOrDefault(x => x.ContentId == id);
            if (prof == null)
            {
                prof = new CharacterProfile { ContentId = id };
                // 旧プレースホルダ（CID=0）があれば統合してから差し替え
                try
                {
                    var ph = list.FirstOrDefault(x => x.ContentId == 0);
                    if (ph != null)
                    {
                        try { if (prof.SlotAliases == null || prof.SlotAliases.Length == 0) prof.SlotAliases = ph.SlotAliases ?? new string[4]; } catch { }
                        try { if (prof.LastRouteBySlot == null || prof.LastRouteBySlot.Count == 0) prof.LastRouteBySlot = ph.LastRouteBySlot ?? new System.Collections.Generic.Dictionary<int, string>(); } catch { }
                        try { if (prof.RouteNames == null || prof.RouteNames.Count == 0) prof.RouteNames = ph.RouteNames ?? new System.Collections.Generic.Dictionary<byte, string>(); } catch { }
                        try { list.Remove(ph); } catch { }
                    }
                }
                catch { }
                list.Add(prof);
                Config.Profiles = list;
                try { Services.XsrDebug.Log(Config, $"EnsureActiveProfileFromClient: created profile for 0x{id:X}"); } catch { }
            }

            // メタ情報更新（取得できる範囲で）
            try { prof.LastSeenUtc = DateTimeOffset.UtcNow; } catch { }
            try
            {
                var lp = _client.LocalPlayer;
                if (lp != null)
                {
                    try { prof.CharacterName = lp.Name?.TextValue ?? prof.CharacterName; } catch { }
                    // HomeWorld の名称取得は Lumina の型に依存しており環境差があるため安全にスキップ
                    // 必要なら後続の別経路で補完する
                }
            }
            catch { }

            if (Config.ActiveContentId != id) { Config.ActiveContentId = id; }
            SaveConfig();
            try { Services.XsrDebug.Log(Config, $"EnsureActiveProfileFromClient: end active=0x{Config.ActiveContentId?.ToString("X") ?? "null"}, profiles={(Config.Profiles?.Count ?? 0)}"); } catch { }
        }
        catch { }
    }

    private void MigrateToProfilesV2IfNeeded()
    {
        try
        {
            if (Config.ConfigVersion >= 2) return;
            var hasAny = false;
            try
            {
                if (Config.SlotAliases != null && Config.SlotAliases.Any(s => !string.IsNullOrWhiteSpace(s))) hasAny = true;
                if (!hasAny && Config.LastRouteBySlot != null && Config.LastRouteBySlot.Count > 0) hasAny = true;
                if (!hasAny && Config.RouteNames != null && Config.RouteNames.Count > 0) hasAny = true;
            }
            catch { }

            // 旧データを初回のみ1件のプロファイルに包む（元のフィールドは互換のため残す）
            if ((Config.Profiles?.Count ?? 0) == 0 && hasAny)
            {
                var p = new CharacterProfile
                {
                    ContentId = 0,
                    CharacterName = string.Empty,
                    WorldName = string.Empty,
                    FreeCompanyName = string.Empty,
                    LastSeenUtc = DateTimeOffset.UtcNow,
                    SlotAliases = Config.SlotAliases ?? new string[4],
                    LastRouteBySlot = Config.LastRouteBySlot ?? new System.Collections.Generic.Dictionary<int, string>(),
                    RouteNames = Config.RouteNames ?? new System.Collections.Generic.Dictionary<byte, string>(),
                };
                Config.Profiles = new System.Collections.Generic.List<CharacterProfile> { p };
                if (!Config.ActiveContentId.HasValue) Config.ActiveContentId = p.ContentId;
            }
            Config.ConfigVersion = 2;
            SaveConfig();
        }
        catch { }
    }

    private string[] GetSlotAliases()
    {
        try { return GetActiveProfile()?.SlotAliases ?? (Config.SlotAliases ?? new string[4]); } catch { return Config.SlotAliases ?? new string[4]; }
    }

    private System.Collections.Generic.Dictionary<int, string> GetLastRouteMap()
    {
        try { return GetActiveProfile()?.LastRouteBySlot ?? (Config.LastRouteBySlot ?? new System.Collections.Generic.Dictionary<int, string>()); } catch { return Config.LastRouteBySlot ?? new System.Collections.Generic.Dictionary<int, string>(); }
    }

    private System.Collections.Generic.Dictionary<byte, string> GetRouteNameMap()
    {
        try { return GetActiveProfile()?.RouteNames ?? (Config.RouteNames ?? new System.Collections.Generic.Dictionary<byte, string>()); } catch { return Config.RouteNames ?? new System.Collections.Generic.Dictionary<byte, string>(); }
    }

    // ===== LastGoodRoute 永続キャッシュ（降格防止 & TTL） =====
    private LastGoodRoute? GetLastGoodRoute(int slot)
    {
        try
        {
            var p = GetActiveProfile();
            if (p?.LastGoodRouteBySlot != null && p.LastGoodRouteBySlot.TryGetValue(slot, out var lg)) return lg;
        }
        catch { }
        return null;
    }

    private void SaveLastGoodRoute(int slot, System.Collections.Generic.List<int> numbers, RouteConfidence conf, string source)
    {
        try
        {
            if (!Config.AdoptCachePersist) return;
            var p = GetActiveProfile(); if (p == null) return;
            if (p.LastGoodRouteBySlot == null) p.LastGoodRouteBySlot = new System.Collections.Generic.Dictionary<int, LastGoodRoute>();
            var key = BuildRouteKeyFromNumbers(numbers);
            p.LastGoodRouteBySlot[slot] = new LastGoodRoute
            {
                RouteKey = key,
                Confidence = conf,
                CapturedAtUtc = DateTimeOffset.UtcNow,
                Source = source
            };
            SaveConfig();
        }
        catch { }
    }

    private static int ConfidenceScore(RouteConfidence c) => c switch
    {
        RouteConfidence.None => 0,
        RouteConfidence.Tail => 1,
        RouteConfidence.Partial => 2,
        RouteConfidence.Full => 3,
        RouteConfidence.Array => 4,
        _ => 0,
    };

    private void OnCmdDump(string cmd, string args)
    {
        try
        {
            SubmarineSnapshot? snap = null;
            if (Config.MemoryOnlyMode)
            {
                if (!TryCaptureFromMemory(out snap))
                {
                    _log.Warning("Memory-only capture failed; no JSON written.");
                    _chat.Print("[Submarines] メモリからの取得に失敗しました。工房に入り直してから /xsr dump をお試しください。");
                    return;
                }
            }
            else
            {
                if (!TryCaptureFromConfiguredAddon(out snap))
                {
                    if (Config.UseMemoryFallback && TryCaptureFromMemory(out snap)) { }
                    else {
                        _log.Warning("Direct capture failed; no JSON written.");
                        _chat.Print("[Submarines] 取得に失敗しました。工房の潜水艦一覧（右パネルに残り時間が出る画面）を開いて /xsr dump を実行してください。");
                        return;
                    }
                }
            }
            try { TrySetIdentity(snap); } catch { }
            try { TrySetIdentity(snap); } catch { }
            try { EtaFormatter.Enrich(snap); } catch { }
            try { TryAdoptPreviousRoutes(snap); } catch { }
            try { _alarm?.UpdateSnapshot(snap); } catch { }
            BridgeWriter.WriteIfChanged(snap);
            try { TryPersistActiveProfileSnapshot(snap); } catch { }
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
        // 互換: 旧経路から呼ばれた場合も設定UIを開く
        OnCmdConfigOpen(cmd, args);
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
            try { TryPersistActiveProfileSnapshot(snap); } catch { }
            _chat.Print( $"[Submarines] JSONを書き出しました: {BridgeWriter.CurrentFilePath()}"); 
            // 選択中プロファイルの表示を更新
            try
            {
                var p = GetActiveProfile();
                if (p?.LastSnapshot?.Items != null && p.LastSnapshot.Items.Count > 0)
                {
                    _uiSnapshot = p.LastSnapshot; _uiLastReadUtc = DateTime.UtcNow; _uiStatus = "メモリ取得";
                }
                else
                {
                    _uiSnapshot = null; _uiLastReadUtc = DateTime.MinValue; _uiStatus = "メモリ取得: 保存データなし";
                }
            }
            catch { }
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
            var aliases = GetSlotAliases();
            for (int i = 0; i < Math.Min(4, learned.Count); i++)
            {
                try { aliases[i] = learned[i]; } catch { }
                try { if (Config.SlotAliases != null) Config.SlotAliases[i] = learned[i]; } catch { }
            }

            SaveConfig();
            var names = string.Join(", ", (aliases ?? Array.Empty<string>()).Where(s => !string.IsNullOrWhiteSpace(s)));
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
                var rec = new SubmarineRecord { Name = name ?? string.Empty, Slot = i + 1, IsDefaultName = wasDefault };
                // 学習済みの実艦名があれば置き換え（スロット1..4 -> 配列0..3）
                try
                {
                    var aliases = GetSlotAliases();
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
            _chat.Print($"[Submarines] Version: {GetVersionWithBuild()}");
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
                case "memscan":
                    CmdMemScan(rest);
                    break;
                case "version":
                    _chat.Print($"[Submarines] Version: {GetVersionWithBuild()}");
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
            case "selftest":
                CmdSelfTest();
                break;
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
                var rec = new SubmarineRecord { Name = name ?? string.Empty, Slot = i + 1, IsDefaultName = wasDefault };

                try
                {
                    var aliases = GetSlotAliases();
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

                // Optional: route key from CurrentExplorationPoints (+構造体内スキャンでの復元)
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
                    // 構造体内をスキャンし、3..5点の候補が見つかれば採用
                    try
                    {
                        if (Config.MemoryRouteScanEnabled)
                        {
                            if (TryRecoverFullRouteFromMemory(s, pts, fieldOff, out var recovered, out var recOff, out var recStride, out var recReversed))
                            {
                                if (recovered != null && recovered.Count >= Math.Max(3, Config.MemoryRouteMinCount))
                                {
                                    routeFromMem = BuildRouteKeyFromNumbers(recovered);
                                    try { Services.XsrDebug.Log(Config, $"S{i + 1} route recovered: off=0x{recOff:X}, stride={recStride}, reversed={recReversed}, memTail=[{string.Join(",", pts)}], full=[{string.Join(",", recovered)}]"); } catch { }
                                }
                            }
                        }
                    }
                    catch { }

                    // 採用: TTL/Confidence を考慮した一元採用（AdoptBest）
                    try
                    {
                        int slot = i + 1;
                        var memNums0 = ParseRouteNumbers(routeFromMem);
                        var cachedNums0 = TryGetCachedNumbers(slot);
                        bool adoptedCacheLocal = false; string reasonLocal = "mem";
                        var best = AdoptBest(memNums0, cachedNums0, slot, out adoptedCacheLocal, out reasonLocal);
                        if (best != null && best.Count > 0)
                        {
                            rec.RouteKey = BuildRouteKeyFromNumbers(best);
                            if (rec.Extra == null) rec.Extra = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal);
                            rec.Extra["RouteShort"] = BuildRouteShortFromNumbers(best);
                            if (best.Count >= 3) { try { SaveCache(slot, best); } catch { } }
                            try { Services.XsrDebug.Log(Config, $"S{slot} adopt: reason={reasonLocal}, len={best.Count}"); } catch { }
                        }
                    }
                    catch { rec.RouteKey = routeFromMem; }

                    try { Services.XsrDebug.Log(Config, $"S{i + 1} route bytes = {(pts.Count>0?string.Join(",", pts):"(none)")}"); } catch { }
                    try { TryWriteExtractTrace(new System.Collections.Generic.List<string> { $"route: off=0x{fieldOff:X},stride=1 -> [{string.Join(",", pts)}]" }); } catch { }

                    // 採用のトレース（mem/cache/final）
                    try
                    {
                        var memNums = ParseRouteNumbers(routeFromMem);
                        var cached = TryGetCachedNumbers(i + 1);
                        var routeNumbers = ParseRouteNumbers(rec.RouteKey ?? string.Empty);
                        bool adoptedCache = cached != null && routeNumbers.SequenceEqual(cached);
                        string reason = adoptedCache ? "cache" : "mem-or-ttl";
                        LogRouteAdoption(i + 1, memNums, cached, routeNumbers, adoptedCache, reason);
                        // MemoryOnly の場合、UIパネルからの補完は行わない
                        if (!Config.MemoryOnlyMode)
                        {
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
                        }

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
            // 事後リペア: MemoryOnly の場合は UI からの補完を行わない
            try
            {
                if (!Config.MemoryOnlyMode)
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
            var a = args ?? string.Empty;
            var m = System.Text.RegularExpressions.Regex.Match(a, "^\\s*(\\S+)\\s*(.*)$");
            if (!m.Success || string.IsNullOrWhiteSpace(m.Groups[1].Value))
            {
                PrintHelp();
                return;
            }
            var sub = (m.Groups[1].Value ?? string.Empty).ToLowerInvariant();
            var rest = m.Groups[2].Value ?? string.Empty;
            switch (sub)
            {
                case "dump":
                    OnCmdDump(CmdDump, rest);
                    break;
                case "dumpmem":
                    CmdDumpFromMemory();
                    break;
                case "memscan":
                    CmdMemScan(rest);
                    break;
                case "memdump":
                    CmdMemDump(rest);
                    break;
                case "setroute":
                    CmdSetRoute(rest);
                    break;
                case "getroute":
                    CmdGetRoute(rest);
                    break;
                case "help":
                    PrintHelp();
                    break;
                case "version":
            _chat.Print($"[Submarines] Version: {GetVersionWithBuild()}");
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
                case "selftest":
                    CmdSelfTest();
                    break;
                default:
                    _chat.Print("[Submarines] Unknown subcommand. Try: /xsr help");
                    break;
            }
        }
        catch (Exception ex)
        {
            _chat.PrintError($"[Submarines] エラー: {ex.Message}");
        }
    }

    private void PrintHelp()
    {
        try
        {
            _chat.Print($"[Submarines] Version: {GetVersionWithBuild()}");
            _chat.Print("[Submarines] Commands: help | dump | dumpmem | memscan [slot] | learnnames | ui | open | addon <name> | cfg mem on|off | version | probe | dumpstage");
            _chat.Print("  /xsr dump       -> JSONを書き出し（/subdump と同等）");
            _chat.Print("  /xsr dumpmem    -> メモリ直読で取得");
            _chat.Print("  /xsr memscan    -> メモリ内の計画航路候補を診断（チャット要約＋ログ詳細）");
            _chat.Print("  /xsr memdump    -> 指定スロットの構造体近傍をHexダンプ（診断用）");
            _chat.Print("  /xsr setroute <slot> <chain> -> 手動でフルルートをセット（キャッシュ初期化）");
            _chat.Print("  /xsr getroute [slot] -> 最後に良かったルートを表示");
            _chat.Print("  /xsr ui         -> 設定ウィンドウを開く（/subcfg でも開く）");
            _chat.Print("  /xsr open       -> 出力フォルダを開く（/subopen と同等）");
            _chat.Print("  /xsr addon <n>  -> 取得対象のアドオン名を設定（/subaddon と同等）");
            _chat.Print("  /xsr cfg mem on|off -> UI失敗時にメモリ直読するか");
            _chat.Print("  /xsr probe      -> よく使うアドオン名の存在/可視を確認");
            _chat.Print("  /xsr dumpstage  -> 画面上の多数アドオンからテキスト走査");
        }
        catch { }
    }

    private string GetVersionWithBuild()
    {
        try
        {
            var ver = typeof(Plugin).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
            string ts = string.Empty;
            try
            {
                // 1) Prefer this assembly's on-disk timestamp
                string? loc = null;
                try { loc = typeof(Plugin).Assembly.Location; } catch { }
                if (!string.IsNullOrWhiteSpace(loc) && System.IO.File.Exists(loc))
                {
                    var t = System.IO.File.GetLastWriteTime(loc);
                    ts = t.ToString("yyyy-MM-dd HH:mm");
                }
                // 2) Fallback: Dev Plugins path (XIVLauncher)
                if (string.IsNullOrWhiteSpace(ts))
                {
                    var app = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    var devDll = System.IO.Path.Combine(app, "XIVLauncher", "devPlugins", "XIVSubmarinesReturn", "XIVSubmarinesReturn.dll");
                    var devMan = System.IO.Path.Combine(app, "XIVLauncher", "devPlugins", "XIVSubmarinesReturn", "manifest.json");
                    if (System.IO.File.Exists(devDll)) ts = System.IO.File.GetLastWriteTime(devDll).ToString("yyyy-MM-dd HH:mm");
                    else if (System.IO.File.Exists(devMan)) ts = System.IO.File.GetLastWriteTime(devMan).ToString("yyyy-MM-dd HH:mm");
                }
                // 3) Fallback: embedded manifest directory (if available via Location)
                if (string.IsNullOrWhiteSpace(ts) && !string.IsNullOrWhiteSpace(loc))
                {
                    var dir = System.IO.Path.GetDirectoryName(loc) ?? string.Empty;
                    var man = System.IO.Path.Combine(dir, "manifest.json");
                    if (System.IO.File.Exists(man)) ts = System.IO.File.GetLastWriteTime(man).ToString("yyyy-MM-dd HH:mm");
                }
            }
            catch { }
            return string.IsNullOrWhiteSpace(ts) ? ver : $"{ver} ({ts})";
        }
        catch { return "0.0.0"; }
    }

    private unsafe void CmdMemScan(string args)
    {
        try
        {
            int? targetSlot = null;
            var sarg = (args ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(sarg) && int.TryParse(sarg, out var n) && n >= 1 && n <= 4)
                targetSlot = n;

            var hm = HousingManager.Instance();
            if (hm == null) { _chat.Print("[Submarines] memscan: 工房外です"); return; }
            var wt = hm->WorkshopTerritory;
            if (wt == null) { _chat.Print("[Submarines] memscan: 工房外です"); return; }

            HousingWorkshopSubmersibleSubData* GetSubPtr(int idx)
            {
                try
                {
                    var p = wt->Submersible.DataPointers[idx].Value;
                    if ((nint)p != 0) return (HousingWorkshopSubmersibleSubData*)p;
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

            int start = targetSlot ?? 1;
            int end = targetSlot ?? 4;
            for (int si = start; si <= end; si++)
            {
                var s = GetSubPtr(si - 1);
                if (s == null) { _chat.Print($"[Submarines] S{si} memscan: pointer null"); continue; }

                var tail = new System.Collections.Generic.List<byte>(5);
                try
                {
                    for (int j = 0; j < 5; j++)
                    {
                        byte v = s->CurrentExplorationPoints[j];
                        if (v == 0) break;
                        if (v >= 1 && v <= 255) tail.Add(v);
                    }
                }
                catch { }

                int fieldOff = -1;
                try { fixed (byte* rp = s->CurrentExplorationPoints) { fieldOff = (int)((byte*)rp - (byte*)s); } } catch { }

                var cands = ScanRouteCandidates(s, tail, fieldOff, 3);
                if (cands.Count == 0)
                {
                    // Fallback: full-struct scan (ignore window)
                    try
                    {
                        int win = Config.MemoryRouteScanWindowBytes;
                        Config.MemoryRouteScanWindowBytes = 0;
                        try { cands = ScanRouteCandidates(s, tail, fieldOff, 3); }
                        finally { Config.MemoryRouteScanWindowBytes = win; }
                    }
                    catch { }
                    if (cands.Count == 0)
                    {
                        // Fallback 2: allow minCount=2 (visual probe)
                        int savedMin = Config.MemoryRouteMinCount;
                        try
                        {
                            Config.MemoryRouteMinCount = 2;
                            cands = ScanRouteCandidates(s, tail, fieldOff, 3);
                        }
                        catch { }
                        finally { Config.MemoryRouteMinCount = savedMin; }
                        if (cands.Count == 0)
                        {
                            _chat.Print($"[Submarines] S{si} memscan: candidates=0, tail=[{string.Join(",", tail)}]");
                            try { Services.XsrDebug.Log(Config, $"S{si} memscan: tail=[{string.Join(",", tail)}] candidates=0 (full-scan & min2 also empty)"); } catch { }
                            continue;
                        }
                        else
                        {
                            _chat.Print($"[Submarines] S{si} memscan: (min2) best off=0x{cands[0].Offset:X}, stride={cands[0].Stride}, score={cands[0].Score}, seq=[{string.Join(",", cands[0].Sequence)}], tail=[{string.Join(",", tail)}], total={cands.Count}");
                        }
                    }
                }

                int shown = Math.Min(3, cands.Count);
                for (int k = 0; k < shown; k++)
                {
                    var c = cands[k];
                    try { Services.XsrDebug.Log(Config, $"S{si} cand[{k}] phase={c.Phase}, off=0x{c.Offset:X}, stride={c.Stride}, score={c.Score}, reversed={c.Reversed}, seq=[{string.Join(",", c.Sequence)}]"); } catch { }
                }
                var best = cands[0];
                _chat.Print($"[Submarines] S{si} memscan: best phase={best.Phase}, off=0x{best.Offset:X}, stride={best.Stride}, score={best.Score}, seq=[{string.Join(",", best.Sequence)}], tail=[{string.Join(",", tail)}], total={cands.Count}");
            }
        }
        catch (Exception ex)
        {
            _chat.PrintError($"[Submarines] memscan エラー: {ex.Message}");
        }
    }

    private unsafe void CmdMemDump(string args)
    {
        try
        {
            var hm = HousingManager.Instance();
            if (hm == null) { _chat.Print("[Submarines] memdump: 工房外です"); return; }
            var wt = hm->WorkshopTerritory; if (wt == null) { _chat.Print("[Submarines] memdump: 工房外です"); return; }
            var parts = (args ?? string.Empty).Trim().Split(new[]{' '}, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || !int.TryParse(parts[0], out var slot) || slot < 1 || slot > 4)
            {
                _chat.Print("[Submarines] memdump 使用法: /xsr memdump <slot 1..4> [offHex] [len]");
                return;
            }
            int offReq = -1; int lenReq = 0x200;
            if (parts.Length >= 2)
            {
                var t = parts[1].Trim();
                if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) t = t.Substring(2);
                if (int.TryParse(t, System.Globalization.NumberStyles.HexNumber, null, out var ov)) offReq = ov;
            }
            if (parts.Length >= 3) int.TryParse(parts[2], out lenReq);

            HousingWorkshopSubmersibleSubData* GetSubPtr(int idx)
            {
                try { var p = wt->Submersible.DataPointers[idx].Value; if ((nint)p != 0) return (HousingWorkshopSubmersibleSubData*)p; } catch { }
                try { var basePtr = (HousingWorkshopSubmersibleSubData*)(&wt->Submersible); return basePtr + idx; } catch { }
                return null;
            }
            var s = GetSubPtr(slot - 1); if (s == null) { _chat.Print($"[Submarines] S{slot} memdump: pointer null"); return; }
            int structSize = sizeof(FFXIVClientStructs.FFXIV.Client.Game.HousingWorkshopSubmersibleSubData);
            byte* basePtr = (byte*)s;

            // 既定オフセット: CurrentExplorationPoints の位置
            int fieldOff = -1; try { fixed (byte* rp = s->CurrentExplorationPoints) { fieldOff = (int)((byte*)rp - (byte*)s); } } catch { }
            int center = offReq >= 0 ? offReq : (fieldOff >= 0 ? fieldOff : 0);
            int radius = Math.Max(16, lenReq);
            int start = Math.Max(0, center - radius);
            int end = Math.Min(structSize - 1, center + radius);

            var dir = System.IO.Path.GetDirectoryName(BridgeWriter.CurrentFilePath()) ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var path = System.IO.Path.Combine(dir, $"memdump_S{slot}_0x{start:X}-0x{end:X}.hex");
            using var sw = new System.IO.StreamWriter(path, false, System.Text.Encoding.ASCII);
            for (int off = start; off <= end; off += 16)
            {
                var line = new System.Text.StringBuilder();
                line.Append($"{off,4:X}: ");
                for (int k = 0; k < 16 && off + k <= end; k++)
                {
                    byte b = *(basePtr + off + k);
                    line.Append(b.ToString("X2")).Append(' ');
                }
                sw.WriteLine(line.ToString());
            }
            sw.Flush();
            _chat.Print($"[Submarines] S{slot} memdump: {path}");
        }
        catch (Exception ex) { _chat.PrintError($"[Submarines] memdump エラー: {ex.Message}"); }
    }

    private void CmdSetRoute(string args)
    {
        try
        {
            var a = (args ?? string.Empty).Trim();
            var parts = a.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !int.TryParse(parts[0], out var slot) || slot < 1 || slot > 4)
            {
                _chat.Print("[Submarines] setroute 使用法: /xsr setroute <slot 1..4> <chain>"); return;
            }
            var nums = ParseRouteNumbers(parts[1]);
            if (nums.Count < 3) { _chat.Print("[Submarines] setroute: 3点以上で指定してください"); return; }
            SaveCache(slot, nums);
            SaveLastGoodRoute(slot, nums, RouteConfidence.Full, "manual");
            _chat.Print($"[Submarines] S{slot} setroute: [{string.Join(",", nums)}]");
        }
        catch (Exception ex) { _chat.PrintError($"[Submarines] setroute エラー: {ex.Message}"); }
    }

    private void CmdGetRoute(string args)
    {
        try
        {
            int? slot = null;
            var s = (args ?? string.Empty).Trim();
            if (int.TryParse(s, out var n) && n >= 1 && n <= 4) slot = n;
            if (slot.HasValue)
            {
                var lg = GetLastGoodRoute(slot.Value);
                if (lg == null || string.IsNullOrWhiteSpace(lg.RouteKey)) { _chat.Print($"[Submarines] S{slot} getroute: (none)"); return; }
                _chat.Print($"[Submarines] S{slot} getroute: key='{lg.RouteKey}', conf={lg.Confidence}, at={lg.CapturedAtUtc:yyyy-MM-dd HH:mm}Z");
            }
            else
            {
                for (int i = 1; i <= 4; i++)
                {
                    var lg = GetLastGoodRoute(i);
                    if (lg == null || string.IsNullOrWhiteSpace(lg.RouteKey)) { _chat.Print($"[Submarines] S{i} getroute: (none)"); }
                    else _chat.Print($"[Submarines] S{i} getroute: key='{lg.RouteKey}', conf={lg.Confidence}, at={lg.CapturedAtUtc:yyyy-MM-dd HH:mm}Z");
                }
            }
        }
        catch (Exception ex) { _chat.PrintError($"[Submarines] getroute エラー: {ex.Message}"); }
    }

    private sealed class RouteCandidate
    {
        public int Offset { get; set; }
        public int Stride { get; set; }
        public bool Reversed { get; set; }
        public int Score { get; set; }
        public System.Collections.Generic.List<int> Sequence { get; set; } = new();
        public string Phase { get; set; } = "window"; // window|full|lenhdr|phaseN
    }

    private unsafe System.Collections.Generic.List<RouteCandidate> ScanRouteCandidates(FFXIVClientStructs.FFXIV.Client.Game.HousingWorkshopSubmersibleSubData* s,
        System.Collections.Generic.List<byte> tailBytes,
        int currentFieldOffset,
        int maxReturn)
    {
        var results = new System.Collections.Generic.List<RouteCandidate>();
        try
        {
            if (!Config.MemoryRouteScanEnabled || s == null) return results;

            int minCnt = Math.Max(3, Config.MemoryRouteMinCount);
            int maxCnt = Math.Max(minCnt, Config.MemoryRouteMaxCount);
            int structSize = sizeof(FFXIVClientStructs.FFXIV.Client.Game.HousingWorkshopSubmersibleSubData);
            if (structSize <= 0 || structSize > 4096) structSize = 512;

            var tail = new System.Collections.Generic.List<int>(tailBytes?.Count ?? 0);
            if (tailBytes != null) foreach (var b in tailBytes) tail.Add((int)b);
            var tailR = ReverseCopy(tail);

            byte* basePtr = (byte*)s;
            int curOff = currentFieldOffset;
            int window = Config.MemoryRouteScanWindowBytes;
            int offStart = 0, offEnd = structSize - 1;
            if (window > 0 && curOff >= 0)
            {
                offStart = Math.Max(0, curOff - window);
                offEnd = Math.Min(structSize - 1, curOff + window);
            }

            int[] strides = new[] { 1, 2, 4 };
            for (int si = 0; si < strides.Length; si++)
            {
                int stride = strides[si];
                for (int off = offStart; off <= offEnd; off++)
                {
                    // Pass A: 0終端の連続読み（従来）
                    var cand = new System.Collections.Generic.List<int>(maxCnt);
                    for (int k = 0; k < maxCnt; k++)
                    {
                        int pos = off + k * stride;
                        if (pos >= structSize) break;
                        byte v = *(basePtr + pos);
                        if (v == 0)
                        {
                            if (Config.MemoryRouteZeroTerminated) break; else continue;
                        }
                        if (v < 1 || v > 255) { cand.Clear(); break; }
                        cand.Add(v);
                    }
                    if (cand.Count < minCnt) continue;

                    int score = 0; bool rev = false; bool hasTail = false;
                    if (tail.Count > 0)
                    {
                        if (ContainsContiguousSubsequence(cand, tail)) { score += 20 + 3 * tail.Count; hasTail = true; }
                        if (IsSuffix(cand, tail)) { score += 40 + 4 * tail.Count; hasTail = true; }
                        if (ContainsContiguousSubsequence(cand, tailR)) { score += 18 + 2 * tail.Count; hasTail = true; rev = true; }
                        if (IsSuffix(cand, tailR)) { score += 36 + 3 * tail.Count; hasTail = true; rev = true; }
                    }
                    else { hasTail = true; }
                    if (!hasTail) continue;

                    score += cand.Count;
                    if (curOff >= 0)
                    {
                        int d = Math.Abs(off - curOff);
                        score += Math.Max(0, 24 - d / 4);
                    }

                    results.Add(new RouteCandidate { Offset = off, Stride = stride, Reversed = rev, Score = score, Sequence = cand, Phase = (window > 0 ? "window" : "full") });
                }

                // Pass B: 長さヘッダ（lenhdr）モード（先頭1byte=長さ 3..5）
                if (Config.MemoryScanPhaseEnabled)
                {
                    for (int off = offStart; off <= offEnd; off++)
                    {
                        int posLen = off;
                        if (posLen >= structSize) break;
                        byte len = *(basePtr + posLen);
                        if (len < minCnt || len > maxCnt) continue;
                        var cand = new System.Collections.Generic.List<int>(len);
                        bool bad = false;
                        for (int k = 0; k < len; k++)
                        {
                            int pos = off + (k + 1) * stride;
                            if (pos >= structSize) { bad = true; break; }
                            byte v = *(basePtr + pos);
                            if (v < 1 || v > 255) { bad = true; break; }
                            cand.Add(v);
                        }
                        if (bad || cand.Count < minCnt) continue;

                        int score = 0; bool rev = false; bool hasTail = false;
                        if (tail.Count > 0)
                        {
                            if (ContainsContiguousSubsequence(cand, tail)) { score += 20 + 3 * tail.Count; hasTail = true; }
                            if (IsSuffix(cand, tail)) { score += 40 + 4 * tail.Count; hasTail = true; }
                            if (ContainsContiguousSubsequence(cand, tailR)) { score += 18 + 2 * tail.Count; hasTail = true; rev = true; }
                            if (IsSuffix(cand, tailR)) { score += 36 + 3 * tail.Count; hasTail = true; rev = true; }
                        }
                        else { hasTail = true; }
                        if (!hasTail) continue;
                        score += cand.Count;
                        if (curOff >= 0)
                        {
                            int d = Math.Abs(off - curOff);
                            score += Math.Max(0, 24 - d / 4);
                        }
                        results.Add(new RouteCandidate { Offset = off, Stride = stride, Reversed = rev, Score = score, Sequence = cand, Phase = "lenhdr" });
                    }
                    // Pass C: 位相ずらし（stride内の別バイト位置を使用）
                    for (int bytePhase = 1; bytePhase < stride; bytePhase++)
                    {
                        for (int off = offStart; off <= offEnd; off++)
                        {
                            var cand = new System.Collections.Generic.List<int>(maxCnt);
                            for (int k = 0; k < maxCnt; k++)
                            {
                                int pos = off + k * stride + bytePhase;
                                if (pos >= structSize) break;
                                byte v = *(basePtr + pos);
                                if (v == 0)
                                {
                                    if (Config.MemoryRouteZeroTerminated) break; else continue;
                                }
                                if (v < 1 || v > 255) { cand.Clear(); break; }
                                cand.Add(v);
                            }
                            if (cand.Count < minCnt) continue;
                            int score = 0; bool rev = false; bool hasTail = false;
                            if (tail.Count > 0)
                            {
                                if (ContainsContiguousSubsequence(cand, tail)) { score += 20 + 3 * tail.Count; hasTail = true; }
                                if (IsSuffix(cand, tail)) { score += 40 + 4 * tail.Count; hasTail = true; }
                                if (ContainsContiguousSubsequence(cand, tailR)) { score += 18 + 2 * tail.Count; hasTail = true; rev = true; }
                                if (IsSuffix(cand, tailR)) { score += 36 + 3 * tail.Count; hasTail = true; rev = true; }
                            }
                            else { hasTail = true; }
                            if (!hasTail) continue;
                            score += cand.Count;
                            if (curOff >= 0)
                            {
                                int d = Math.Abs(off - curOff);
                                score += Math.Max(0, 24 - d / 4);
                            }
                            results.Add(new RouteCandidate { Offset = off, Stride = stride, Reversed = rev, Score = score, Sequence = cand, Phase = $"phase{stride}.{bytePhase}" });
                        }
                    }
                }
            }

            results.Sort((a, b) => b.Score.CompareTo(a.Score));
            if (maxReturn > 0 && results.Count > maxReturn) results.RemoveRange(maxReturn, results.Count - maxReturn);
            return results;
        }
        catch { return results; }
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

    // HousingWorkshopSubmersibleSubData 内部の連続バイト列から 3..5 点の「計画航路」を推定復元
    private unsafe bool TryRecoverFullRouteFromMemory(FFXIVClientStructs.FFXIV.Client.Game.HousingWorkshopSubmersibleSubData* s,
        System.Collections.Generic.List<byte> tailBytes,
        int currentFieldOffset,
        out System.Collections.Generic.List<int> recovered,
        out int bestOff,
        out int bestStride,
        out bool reversed)
    {
        recovered = new System.Collections.Generic.List<int>();
        bestOff = -1; bestStride = 1; reversed = false;
        try
        {
            if (!Config.MemoryRouteScanEnabled || s == null) return false;
            int minCnt = Math.Max(3, Config.MemoryRouteMinCount);
            int maxCnt = Math.Max(minCnt, Config.MemoryRouteMaxCount);

            int structSize = sizeof(FFXIVClientStructs.FFXIV.Client.Game.HousingWorkshopSubmersibleSubData);
            if (structSize <= 0 || structSize > 4096) structSize = 512;

            var tail = new System.Collections.Generic.List<int>(tailBytes?.Count ?? 0);
            if (tailBytes != null) foreach (var b in tailBytes) tail.Add((int)b);
            var tailR = ReverseCopy(tail);

            byte* basePtr = (byte*)s;
            int bestScore = 0; System.Collections.Generic.List<int>? best = null; bool bestRev = false; int offBest = -1; int strideBest = 1;
            int curOff = currentFieldOffset;
            int window = Config.MemoryRouteScanWindowBytes;
            int offStart = 0, offEnd = structSize - 1;
            if (window > 0 && curOff >= 0)
            {
                offStart = Math.Max(0, curOff - window);
                offEnd = Math.Min(structSize - 1, curOff + window);
            }

            bool anyWithTail = false;
            int[] strides = new[] { 1, 2, 4 };
            for (int si = 0; si < strides.Length; si++)
            {
                int stride = strides[si];
                for (int off = offStart; off <= offEnd; off++)
                {
                    var cand = new System.Collections.Generic.List<int>(maxCnt);
                    for (int k = 0; k < maxCnt; k++)
                    {
                        int pos = off + k * stride;
                        if (pos >= structSize) break;
                        byte v = *(basePtr + pos);
                        if (v == 0) break;
                        if (v < 1 || v > 255) { cand.Clear(); break; }
                        cand.Add(v);
                    }
                    if (cand.Count < minCnt) continue;

                    int score = 0; bool rev = false; bool hasTail = false;
                    if (tail.Count > 0)
                    {
                        if (ContainsContiguousSubsequence(cand, tail)) { score += 20 + 3 * tail.Count; hasTail = true; }
                        if (IsSuffix(cand, tail)) { score += 40 + 4 * tail.Count; hasTail = true; }
                        if (ContainsContiguousSubsequence(cand, tailR)) { score += 18 + 2 * tail.Count; hasTail = true; rev = true; }
                        if (IsSuffix(cand, tailR)) { score += 36 + 3 * tail.Count; hasTail = true; rev = true; }
                    }
                    else { hasTail = true; }
                    if (!hasTail) continue;
                    anyWithTail = true;
                    score += cand.Count;
                    if (curOff >= 0)
                    {
                        int d = Math.Abs(off - curOff);
                        score += Math.Max(0, 24 - d / 4);
                    }

                    if (score > bestScore)
                    {
                        bestScore = score; best = cand; bestRev = rev; offBest = off; strideBest = stride;
                    }
                }
            }

            if (best != null && best.Count >= minCnt && bestScore > 0 && (anyWithTail || tail.Count == 0))
            {
                recovered = best; bestOff = offBest; bestStride = strideBest; reversed = bestRev; return true;
            }
            return false;
        }
        catch { return false; }
    }

    private System.Collections.Generic.List<int>? TryGetCachedNumbers(int slot)
    {
        try
        {
            var map = GetLastRouteMap();
            if (map != null && map.TryGetValue(slot, out var s) && !string.IsNullOrWhiteSpace(s))
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
            var p = GetActiveProfile();
            var key = BuildRouteKeyFromNumbers(numbers);
            if (p != null)
            {
                if (p.LastRouteBySlot == null) p.LastRouteBySlot = new System.Collections.Generic.Dictionary<int, string>();
                p.LastRouteBySlot[slot] = key;
            }
            if (Config.LastRouteBySlot == null) Config.LastRouteBySlot = new System.Collections.Generic.Dictionary<int, string>();
            Config.LastRouteBySlot[slot] = key;
            SaveConfig();
        }
        catch { }
    }

    // 採用ロジック（Complete > Partial > Tail, 長さ優先, TTL/Confidence守る）
    private System.Collections.Generic.List<int> AdoptBest(System.Collections.Generic.List<int> memNums, System.Collections.Generic.List<int>? cached, int slot, out bool adoptedCache, out string reason)
    {
        adoptedCache = false; reason = "mem";
        var memLen = memNums?.Count ?? 0;
        var cacheLen = cached?.Count ?? 0;

        // TTL保護：Full/Array は TTL 内なら無条件保持
        try
        {
            var lg = GetLastGoodRoute(slot);
            if (lg != null && !string.IsNullOrWhiteSpace(lg.RouteKey))
            {
                var ttl = TimeSpan.FromHours(Math.Max(1, Config.AdoptTtlHours));
                if (DateTimeOffset.UtcNow - lg.CapturedAtUtc <= ttl && ConfidenceScore(lg.Confidence) >= ConfidenceScore(RouteConfidence.Full))
                {
                    var nums = ParseRouteNumbers(lg.RouteKey);
                    adoptedCache = true; reason = "ttl";
                    return nums;
                }
            }
        }
        catch { }

        // 比較: 長い方優先（Complete > Partial > Tail の代替として長さで序数化）
        if (Config.AdoptPreferLonger)
        {
            if (cacheLen >= 3 && memLen < cacheLen)
            {
                // Suffix/包含の場合はキャッシュ維持
                if (IsSuffix(cached!, memNums) || ContainsContiguousSubsequence(cached!, memNums))
                {
                    adoptedCache = true; reason = "cache-longer";
                    return cached!;
                }
            }
        }

        // デフォルト: memNums を採用（3点以上なら保存）
        if (memLen >= 3)
        {
            SaveLastGoodRoute(slot, memNums, RouteConfidence.Full, "mem");
            return memNums;
        }

        // mem が短い場合、キャッシュに頼る
        if (cacheLen >= 3)
        {
            adoptedCache = true; reason = "cache";
            return cached!;
        }

        // どちらも短い → そのまま（mem）
        return memNums ?? new System.Collections.Generic.List<int>();
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
                var rmap = GetRouteNameMap();
                if (string.IsNullOrWhiteSpace(letter) && rmap != null && rmap.TryGetValue((byte)n, out var nm) && !string.IsNullOrWhiteSpace(nm))
                    letter = nm;
                // 追加フォールバック: 1..26 は A..Z として表記（Mapヒント/別名が未整備でも一般的表記に合わせる）
                if (string.IsNullOrWhiteSpace(letter) && n >= 1 && n <= 26)
                {
                    letter = ((char)('A' + (n - 1))).ToString();
                }
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

    private void LogRouteAdoption(int slot,
        System.Collections.Generic.List<int> memNums,
        System.Collections.Generic.List<int>? cacheNums,
        System.Collections.Generic.List<int> finalNums,
        bool adoptedCache,
        string reason,
        string? phase = null,
        int? off = null,
        int? stride = null,
        bool? reversed = null)
    {
        try
        {
            var lg = GetLastGoodRoute(slot);
            string conf = lg?.Confidence.ToString() ?? "None";
            string ttl = "none";
            if (lg != null)
            {
                var active = (DateTimeOffset.UtcNow - lg.CapturedAtUtc) <= TimeSpan.FromHours(Math.Max(1, Config.AdoptTtlHours));
                ttl = active ? "active" : "expired";
            }
            string memLog = string.Join(",", memNums ?? new System.Collections.Generic.List<int>());
            string cacheLog = cacheNums == null ? "-" : string.Join(",", cacheNums);
            string finalLog = string.Join(",", finalNums ?? new System.Collections.Generic.List<int>());
            string offStr = off.HasValue ? ($"0x{off.Value:X}") : "-";
            string strideStr = stride.HasValue ? stride.Value.ToString() : "-";
            string revStr = reversed.HasValue ? (reversed.Value ? "true" : "false") : "-";
            string phaseStr = string.IsNullOrWhiteSpace(phase) ? "-" : phase;
            Services.XsrDebug.Log(Config,
                $"S{slot} adopt-trace mem=[{memLog}], cache=[{cacheLog}], final=[{finalLog}], adopted={(adoptedCache ? "cache" : "mem")}, reason={reason}, phase={phaseStr}, off={offStr}, stride={strideStr}, reversed={revStr}, conf={conf}, ttl={ttl}");
        }
        catch { }
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

                // Route merge policy:
                // - If UI route has >=3 points (numeric) and current route has fewer, adopt UI route (normalize to Point-xx)
                // - If current route is empty and UI route exists, adopt UI route
                // - If UI route is textual (letters like M>R>O>J>Z) and provides more tokens than current, prefer it for display (RouteShort)
                if (!string.IsNullOrWhiteSpace(rec.RouteKey))
                {
                    try
                    {
                        var curKey = existing.RouteKey ?? string.Empty;
                        var curNums = ParseRouteNumbers(curKey);
                        var uiNums = ParseRouteNumbers(rec.RouteKey);

                        bool adopted = false; string reason = string.Empty;

                        if (uiNums.Count >= 3 && uiNums.Count > curNums.Count)
                        {
                            // Prefer fuller UI route (numeric) and normalize
                            existing.RouteKey = BuildRouteKeyFromNumbers(uiNums);
                            if (existing.Extra == null) existing.Extra = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal);
                            existing.Extra["RouteShort"] = BuildRouteShortFromNumbers(uiNums);
                            try { SaveCache(existing.Slot ?? 0, uiNums); } catch { }
                            adopted = true; reason = $"ui-numeric({uiNums.Count})>cur({curNums.Count})";
                        }
                        else if (string.IsNullOrWhiteSpace(curKey))
                        {
                            // No current route: adopt UI as-is
                            existing.RouteKey = rec.RouteKey;
                            adopted = true; reason = "ui-override-empty";
                        }

                        // Update RouteShort if UI textual route seems more informative (more tokens)
                        try
                        {
                            var uiTokens = (rec.RouteKey ?? string.Empty).Split('>').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                            var curShort = (existing.Extra != null && existing.Extra.TryGetValue("RouteShort", out var rs)) ? rs : curKey;
                            var curTokens = (curShort ?? string.Empty).Split('>').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                            if (uiNums.Count == 0 && uiTokens.Count > curTokens.Count)
                            {
                                if (existing.Extra == null) existing.Extra = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal);
                                existing.Extra["RouteShort"] = rec.RouteKey;
                                reason = adopted ? ($"{reason}+ui-text-short({uiTokens.Count})") : $"ui-text-short({uiTokens.Count})";
                                adopted = true;
                            }
                        }
                        catch { }

                        try { Services.XsrDebug.Log(Config, adopted ? $"Adopted UI route: slot={existing.Slot ?? 0}, reason={reason}, ui='{rec.RouteKey}', cur='{curKey}'" : $"UI route ignored: ui='{rec.RouteKey}', cur='{curKey}'"); } catch { }
                    }
                    catch { }
                }
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

    private void CmdSelfTest()
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
        // Territory gate: 工房内のみ自動取得
        try
        {
            if (Config.EnableTerritoryGate)
            {
                unsafe { if (FFXIVClientStructs.FFXIV.Client.Game.HousingManager.Instance()->WorkshopTerritory == null) return; }
            }
        }
        catch { }
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
            if (Config.EnableAddonGate && !visible) { _wasAddonVisible = false; _deferredCaptureFrames = 0; _deferredCaptureFrames2 = 0; return; }
            if (visible && (!_wasAddonVisible || (DateTime.UtcNow - _lastAutoCaptureUtc) > TimeSpan.FromSeconds(10)))
            {
                if (!_notifiedThisVisibility)
                {
                    // Addon 可視化直後: 1～2フレーム遅延でメモリキャプチャ（ArrayData/内部構造が安定するタイミングを狙う）
                    _deferredAddonName = name;
                    _deferredCaptureFrames = 2;
                    _deferredCaptureFrames2 = 10; // 追加の再試行
                    SubmarineSnapshot? snap;
                    bool ok = false;
                    if (Config.MemoryOnlyMode)
                    {
                        try { ok = TryCaptureFromMemory(out snap); } catch { snap = null; }
                    }
                    else
                    {
                        try { ok = TryCaptureFromAddon(name, out snap); } catch { snap = null; }
                        if (!ok)
                        {
                            try { ok = TryCaptureFromMemory(out snap); } catch { snap = null; }
                        }
                    }
                    if (ok && snap != null)
                    {
                        try { EtaFormatter.Enrich(snap); } catch { }
                        // MemoryOnly の場合は UI 追補を行わない
                        if (!Config.MemoryOnlyMode)
                        {
                            // Try to enrich from visible workshop panels even after memory capture
                            try
                            {
                                if (EnrichFromWorkshopPanels(snap))
                                {
                                    try
                                    {
                                        snap.Items = snap.Items
                                            .OrderBy(x => x.DurationMinutes.HasValue ? 0 : 1)
                                            .ThenBy(x => x.DurationMinutes ?? int.MaxValue)
                                            .ThenBy(x => x.Name, StringComparer.Ordinal)
                                            .Take(4)
                                            .ToList();
                                    }
                                    catch { }
                                    try { Services.XsrDebug.Log(Config, "Enriched snapshot from workshop panels after capture"); } catch { }
                                }
                            }
                            catch { }
                        }
                        try { TryAdoptPreviousRoutes(snap); } catch { }
                        try { _alarm?.UpdateSnapshot(snap); } catch { }
                        BridgeWriter.WriteIfChanged(snap);
                        try { TryPersistActiveProfileSnapshot(snap); } catch { }
                        _chat.Print("[Submarines] 自動取得しJSONを書き出しました。");
                        _log.Info("Auto-captured and wrote JSON");
                        _notifiedThisVisibility = true; // 可視セッション中は一度だけ通知
                    }
                }
                _lastAutoCaptureUtc = DateTime.UtcNow;
            }
            _wasAddonVisible = visible;
            if (!visible) _notifiedThisVisibility = false; // 非可視に戻ったらリセット

            // 遅延キャプチャの実行
            try
            {
                if (visible && _deferredCaptureFrames > 0)
                {
                    _deferredCaptureFrames--;
                    if (_deferredCaptureFrames == 0)
                    {
                        bool changed = false;
                        if (Config.PreferArrayDataFirst)
                        {
                            try { if (TryAdoptFromAddonArrays(unit)) changed = true; } catch { }
                        }
                        if (!changed && TryCaptureFromMemory(out var snapDefer) && snapDefer != null)
                        {
                            try { EtaFormatter.Enrich(snapDefer); } catch { }
                            BridgeWriter.WriteIfChanged(snapDefer);
                            try { Services.XsrDebug.Log(Config, $"DeferredCapture phase=event-defer2 addon={_deferredAddonName}"); } catch { }
                        }
                    }
                }
                if (visible && _deferredCaptureFrames2 > 0)
                {
                    _deferredCaptureFrames2--;
                    if (_deferredCaptureFrames2 == 0)
                    {
                        bool changed = false;
                        if (Config.PreferArrayDataFirst)
                        {
                            try { if (TryAdoptFromAddonArrays(unit)) changed = true; } catch { }
                        }
                        if (!changed && TryCaptureFromMemory(out var snapDefer2) && snapDefer2 != null)
                        {
                            try { EtaFormatter.Enrich(snapDefer2); } catch { }
                            BridgeWriter.WriteIfChanged(snapDefer2);
                            try { Services.XsrDebug.Log(Config, $"DeferredCapture phase=event-defer10 addon={_deferredAddonName}"); } catch { }
                        }
                    }
                }
            }
            catch { }
        }
        catch (Exception ex)
        {
            _log.Warning(ex, "OnFrameworkUpdate auto-capture error");
        }
    }

    // Addonのメモリ（ArrayData等）を起点に、tail を含む 3..5 連続値を探索し、採用（UI文字列は不使用）
    private unsafe bool TryAdoptFromAddonArrays(FFXIVClientStructs.FFXIV.Component.GUI.AtkUnitBase* unit)
    {
        try
        {
            if (unit == null) return false;
            var hm = HousingManager.Instance(); if (hm == null) return false;
            var wt = hm->WorkshopTerritory; if (wt == null) return false;

            // まず通常メモリキャプチャを行い、足りないスロットだけを補う
            if (!TryCaptureFromMemory(out var snap) || snap == null || snap.Items == null) return false;

            byte* basePtr = (byte*)unit;
            int scanSize = 8192; // Addon周辺の限定スキャン

            bool anyChanged = false;
            foreach (var rec in snap.Items)
            {
                try
                {
                    int slot = rec.Slot ?? 0; if (slot <= 0 || slot > 4) continue;

                    // tail 取得（CurrentExplorationPoints）
                    var subsBase = (HousingWorkshopSubmersibleSubData*)(&wt->Submersible);
                    var s = wt->Submersible.DataPointers[slot - 1].Value;
                    if ((nint)s == 0) s = (FFXIVClientStructs.FFXIV.Client.Game.HousingWorkshopSubmersibleSubData*)(subsBase + (slot - 1));
                    if ((nint)s == 0) continue;
                    var tail = new System.Collections.Generic.List<byte>(5);
                    for (int j = 0; j < 5; j++)
                    {
                        byte v = s->CurrentExplorationPoints[j];
                        if (v == 0) break; if (v >= 1 && v <= 255) tail.Add(v);
                    }
                    var existing = ParseRouteNumbers(rec.RouteKey ?? string.Empty);
                    if (existing.Count >= 3) continue; // 既にフル

                    // Addonメモリ領域をスキャン（位相/lenhdr対応）
                    var cands = ScanRouteCandidatesFromRaw(basePtr, scanSize, tail, maxReturn: 3);
                    if (cands.Count == 0) continue;
                    var best = cands[0];

                    var nums = best.Sequence;
                    if (nums != null && nums.Count >= Math.Max(3, Config.MemoryRouteMinCount))
                    {
                        rec.RouteKey = BuildRouteKeyFromNumbers(nums);
                        if (rec.Extra == null) rec.Extra = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal);
                        rec.Extra["RouteShort"] = BuildRouteShortFromNumbers(nums);
                        try { SaveCache(slot, nums); } catch { }
                        try { SaveLastGoodRoute(slot, nums, RouteConfidence.Array, "array"); } catch { }
                        anyChanged = true;
                        try { Services.XsrDebug.Log(Config, $"S{slot} array adopt: phase={best.Phase}, off=0x{best.Offset:X}, stride={best.Stride}, seq=[{string.Join(",", nums)}]"); } catch { }
                        try
                        {
                            var cached = TryGetCachedNumbers(slot);
                            LogRouteAdoption(slot, new System.Collections.Generic.List<int>(), cached ?? new System.Collections.Generic.List<int>(), nums, cached != null && cached.SequenceEqual(nums), "array", best.Phase, best.Offset, best.Stride, best.Reversed);
                        }
                        catch { }
                    }
                }
                catch { }
            }

            if (anyChanged)
            {
                try { EtaFormatter.Enrich(snap); } catch { }
                BridgeWriter.WriteIfChanged(snap);
            }
            return anyChanged;
        }
        catch { return false; }
    }

    private unsafe System.Collections.Generic.List<RouteCandidate> ScanRouteCandidatesFromRaw(byte* basePtr, int structSize, System.Collections.Generic.List<byte> tailBytes, int maxReturn)
    {
        var results = new System.Collections.Generic.List<RouteCandidate>();
        try
        {
            int minCnt = Math.Max(3, Config.MemoryRouteMinCount);
            int maxCnt = Math.Max(minCnt, Config.MemoryRouteMaxCount);
            var tail = new System.Collections.Generic.List<int>(tailBytes?.Count ?? 0);
            if (tailBytes != null) foreach (var b in tailBytes) tail.Add((int)b);
            var tailR = ReverseCopy(tail);
            int[] strides = new[] { 1, 2, 4 };
            // Pass A/C: 0終端 or 位相ずらし
            for (int si = 0; si < strides.Length; si++)
            {
                int stride = strides[si];
                for (int off = 0; off < structSize; off++)
                {
                    var cand = new System.Collections.Generic.List<int>(maxCnt);
                    for (int k = 0; k < maxCnt; k++)
                    {
                        int pos = off + k * stride; if (pos >= structSize) break;
                        byte v = *(basePtr + pos);
                        if (v == 0) { if (Config.MemoryRouteZeroTerminated) break; else continue; }
                        if (v < 1 || v > 255) { cand.Clear(); break; }
                        cand.Add(v);
                    }
                    if (cand.Count < minCnt) continue;
                    int score = 0; bool rev = false; bool hasTail = false;
                    if (tail.Count > 0)
                    {
                        if (ContainsContiguousSubsequence(cand, tail)) { score += 20 + 3 * tail.Count; hasTail = true; }
                        if (IsSuffix(cand, tail)) { score += 40 + 4 * tail.Count; hasTail = true; }
                        if (ContainsContiguousSubsequence(cand, tailR)) { score += 18 + 2 * tail.Count; hasTail = true; rev = true; }
                        if (IsSuffix(cand, tailR)) { score += 36 + 3 * tail.Count; hasTail = true; rev = true; }
                    }
                    else { hasTail = true; }
                    if (!hasTail) continue;
                    score += cand.Count;
                    results.Add(new RouteCandidate { Offset = off, Stride = stride, Reversed = rev, Score = score, Sequence = cand, Phase = "array-window" });
                }
                // 位相
                if (Config.MemoryScanPhaseEnabled)
                {
                    for (int bytePhase = 1; bytePhase < stride; bytePhase++)
                    {
                        for (int off = 0; off < structSize; off++)
                        {
                            var cand = new System.Collections.Generic.List<int>(maxCnt);
                            for (int k = 0; k < maxCnt; k++)
                            {
                                int pos = off + k * stride + bytePhase; if (pos >= structSize) break;
                                byte v = *(basePtr + pos);
                                if (v == 0) { if (Config.MemoryRouteZeroTerminated) break; else continue; }
                                if (v < 1 || v > 255) { cand.Clear(); break; }
                                cand.Add(v);
                            }
                            if (cand.Count < minCnt) continue;
                            int score = 0; bool rev = false; bool hasTail = false;
                            if (tail.Count > 0)
                            {
                                if (ContainsContiguousSubsequence(cand, tail)) { score += 20 + 3 * tail.Count; hasTail = true; }
                                if (IsSuffix(cand, tail)) { score += 40 + 4 * tail.Count; hasTail = true; }
                                if (ContainsContiguousSubsequence(cand, tailR)) { score += 18 + 2 * tail.Count; hasTail = true; rev = true; }
                                if (IsSuffix(cand, tailR)) { score += 36 + 3 * tail.Count; hasTail = true; rev = true; }
                            }
                            else { hasTail = true; }
                            if (!hasTail) continue;
                            score += cand.Count;
                            results.Add(new RouteCandidate { Offset = off, Stride = stride, Reversed = rev, Score = score, Sequence = cand, Phase = $"array-phase{stride}.{bytePhase}" });
                        }
                    }
                }
                // 長さヘッダ
                if (Config.MemoryScanPhaseEnabled)
                {
                    for (int off = 0; off < structSize; off++)
                    {
                        int posLen = off; if (posLen >= structSize) break;
                        byte len = *(basePtr + posLen);
                        if (len < minCnt || len > maxCnt) continue;
                        var cand = new System.Collections.Generic.List<int>(len);
                        bool bad = false;
                        for (int k = 0; k < len; k++)
                        {
                            int pos = off + (k + 1) * stride; if (pos >= structSize) { bad = true; break; }
                            byte v = *(basePtr + pos);
                            if (v < 1 || v > 255) { bad = true; break; }
                            cand.Add(v);
                        }
                        if (bad || cand.Count < minCnt) continue;
                        int score = 0; bool rev = false; bool hasTail = false;
                        if (tail.Count > 0)
                        {
                            if (ContainsContiguousSubsequence(cand, tail)) { score += 20 + 3 * tail.Count; hasTail = true; }
                            if (IsSuffix(cand, tail)) { score += 40 + 4 * tail.Count; hasTail = true; }
                            if (ContainsContiguousSubsequence(cand, tailR)) { score += 18 + 2 * tail.Count; hasTail = true; rev = true; }
                            if (IsSuffix(cand, tailR)) { score += 36 + 3 * tail.Count; hasTail = true; rev = true; }
                        }
                        else { hasTail = true; }
                        if (!hasTail) continue;
                        score += cand.Count;
                        results.Add(new RouteCandidate { Offset = off, Stride = stride, Reversed = rev, Score = score, Sequence = cand, Phase = "array-lenhdr" });
                    }
                }
            }
            results.Sort((a, b) => b.Score.CompareTo(a.Score));
            if (maxReturn > 0 && results.Count > maxReturn) results.RemoveRange(maxReturn, results.Count - maxReturn);
            return results;
        }
        catch { return results; }
    }
}
