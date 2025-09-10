# XIV Submarines Return — AIレビュー資料（詳細）
作成時刻: 2025-09-07 15:22:00

## 目的 / 範囲
- 目的: メモリ経路の本線化、ルート解決/ログ出力の拡充、UI/テーブルの安定化、秘匿入力の改善。
- 範囲: apps/XIVSubmarinesReturn（C#/.NET9/Dalamud）。

## 実施フェーズ（実装済）
- フェーズ1: メモリ経路の是正（DataPointers本線化）
  - WorkshopTerritory->Submersible.DataPointers[i].Value から HousingWorkshopSubmersibleSubData* を取得（4隻）。
  - CurrentExplorationPoints[0..4] を 1..255 の範囲で採用（最大5点）。
  - FormatRouteKey(List<byte>) で RouteKey="Point-<id> - ..." を生成。RouteShort は Enrich で Pxx>... を付与。
  - 旧ヒューリスティック（base+0x42等）に依存しない。
- フェーズ2: ログ/トレース拡充
  - xsr_debug.log: S{n} route bytes = 13,18,15,10,26 を出力（未取得は (none)）。
  - extract_log.txt: oute: off=0x{相対オフセット},stride=1 -> [13,18,15,10,26] を出力（stride=1）。
- フェーズ3: テーブル/ETAの安定化
  - SnapshotTable の強調判定を UTC 基準（DateTimeOffset.UtcNow）に統一。閾値 HighlightSoonMins は据え置き。
  - SortSpecs によるヘッダクリックソートの反映/永続化は現行のとおり。
- フェーズ4: MaskedInput の日本語化/改善
  - 非表示時は ImGuiInputTextFlags.Password を使用（伏字でも編集可能）。
  - 目アイコンのツールチップを日本語化（「表示/非表示」）。
- フェーズ5: UI分離・一本化
  - 概要タブの手動取得は常にメモリ（Manual (Memory)）。
  - Debugタブの「UIから取得」ボタンは非表示（削除）。
  - Ui_DumpUi() は内部的にメモリ取得へリダイレクト（保険）。

## 受入観点（要点）
- submarines.json に 4隻×最大5点の RouteKey（Point-xx - ...）が出力される。
- xsr_debug.log に route bytes、extract_log.txt に off/stride/配列が出力される。
- テーブル: ヘッダクリックでソート、ETA強調はUTC基準（しきい値据え置き）。
- 秘匿入力: 表示/非表示の切替（日本語ツールチップ）、非表示時でも編集/保存可能。
- UI: 「UIから取得」は表示されず、手動取得はメモリ経路のみ。

## 環境/操作
- ビルド: dotnet build apps\\XIVSubmarinesReturn\\XIVSubmarinesReturn.csproj -c Release -p:Platform=x64 -p:SkipPack=true
- 配置: %AppData%\\XIVLauncher\\devPlugins\\XIVSubmarinesReturn
- 主なコマンド: /xsr help, /xsr dumpmem, /xsr dump, /xsr open, /xsr addon <name>, /xsr version, /xsr ui

## エビデンス（ログ/JSON）
### xsr_debug.log
`	ext[2025-09-07 00:27:56] Notion send failed: 404 
[2025-09-07 00:27:57] Notion send failed: 404 
[2025-09-07 00:27:57] Notion send failed: 404 
[2025-09-07 00:27:58] Notion send failed: 404 
[2025-09-07 00:31:38] Notion send failed: 404 
[2025-09-07 00:31:38] Notion send failed: 404 
[2025-09-07 00:31:38] Notion send failed: 404 
[2025-09-07 00:31:39] Notion send failed: 404 
[2025-09-07 00:33:22] Notion send failed: 404 
[2025-09-07 00:33:22] Notion send failed: 404 
[2025-09-07 00:33:23] Notion send failed: 404 
[2025-09-07 00:33:23] Notion send failed: 404 
[2025-09-07 00:34:44] Notion send failed: 404 
[2025-09-07 00:34:44] Notion send failed: 404 
[2025-09-07 00:34:45] Notion send failed: 404 
[2025-09-07 00:34:48] Notion send failed: 404 
[2025-09-07 01:10:56] Notion send failed: 404 
[2025-09-07 01:10:56] Notion send failed: 404 
[2025-09-07 01:10:57] Notion send failed: 404 
[2025-09-07 01:10:58] Notion send failed: 404 
[2025-09-07 01:11:02] Notion send failed: 404 
[2025-09-07 01:11:03] Notion send failed: 404 
[2025-09-07 01:11:06] Notion send failed: 404 
[2025-09-07 01:11:07] Notion send failed: 404 
[2025-09-07 13:38:39] S1 route bytes = 15,10 
[2025-09-07 13:38:39] S2 route bytes = 15,10 
[2025-09-07 13:38:39] S3 route bytes = 15,10 
[2025-09-07 13:38:39] S4 route bytes = 15,10 
[2025-09-07 13:38:50] S1 route bytes = 15,10 
[2025-09-07 13:38:50] S2 route bytes = 15,10 
[2025-09-07 13:38:50] S3 route bytes = 15,10 
[2025-09-07 13:38:50] S4 route bytes = 15,10 
[2025-09-07 13:39:39] S1 route bytes = 15,10 
[2025-09-07 13:39:39] S2 route bytes = 15,10 
[2025-09-07 13:39:39] S3 route bytes = 15,10 
[2025-09-07 13:39:39] S4 route bytes = 15,10 
[2025-09-07 14:06:28] S1 route bytes = 13,18,15,10,26 
[2025-09-07 14:06:28] S2 route bytes = 13,18,15,10,26 
[2025-09-07 14:06:28] S3 route bytes = 13,18,15,10,26 
[2025-09-07 14:06:28] S4 route bytes = 13,18,15,10,26 
[2025-09-07 14:07:02] S1 route bytes = 13,18,15,10,26 
[2025-09-07 14:07:02] S2 route bytes = 13,18,15,10,26 
[2025-09-07 14:07:02] S3 route bytes = 13,18,15,10,26 
[2025-09-07 14:07:02] S4 route bytes = 13,18,15,10,26 
[2025-09-07 14:07:14] S1 route bytes = 13,18,15,10,26 
[2025-09-07 14:07:14] S2 route bytes = 13,18,15,10,26 
[2025-09-07 14:07:14] S3 route bytes = 13,18,15,10,26 
[2025-09-07 14:07:14] S4 route bytes = 13,18,15,10,26 
[2025-09-07 14:11:51] S1 route bytes = 13,18,15,10,26 
[2025-09-07 14:11:51] S2 route bytes = 13,18,15,10,26 
[2025-09-07 14:11:51] S3 route bytes = 13,18,15,10,26 
[2025-09-07 14:11:51] S4 route bytes = 13,18,15,10,26 
`

### extract_log.txt
`	extroute: off=0x42,stride=1 -> [13,18,15,10,26]
`

### submarines.json
`json{
  "SchemaVersion": 2,
  "PluginVersion": "0.1.1",
  "CapturedAt": "2025-09-07T05:11:51.1638375+00:00",
  "Items": [
    {
      "Name": "Bonfire",
      "RouteKey": "Point-13 - Point-18 - Point-15 - Point-10 - Point-26",
      "DurationMinutes": 2416,
      "Rank": 131,
      "Slot": 1,
      "IsDefaultName": false,
      "EtaUnix": 1757366876,
      "Extra": {
        "EtaLocal": "06:27",
        "RemainingText": "40\u6642\u959316\u5206",
        "RouteShort": "P13\u003E18\u003EP15\u003EP10\u003EP26"
      }
    },
    {
      "Name": "Siipi",
      "RouteKey": "Point-13 - Point-18 - Point-15 - Point-10 - Point-26",
      "DurationMinutes": 2416,
      "Rank": 131,
      "Slot": 2,
      "IsDefaultName": false,
      "EtaUnix": 1757366885,
      "Extra": {
        "EtaLocal": "06:28",
        "RemainingText": "40\u6642\u959316\u5206",
        "RouteShort": "P13\u003E18\u003EP15\u003EP10\u003EP26"
      }
    },
    {
      "Name": "Pilvi",
      "RouteKey": "Point-13 - Point-18 - Point-15 - Point-10 - Point-26",
      "DurationMinutes": 2416,
      "Rank": 131,
      "Slot": 3,
      "IsDefaultName": false,
      "EtaUnix": 1757366894,
      "Extra": {
        "EtaLocal": "06:28",
        "RemainingText": "40\u6642\u959316\u5206",
        "RouteShort": "P13\u003E18\u003EP15\u003EP10\u003EP26"
      }
    },
    {
      "Name": "Kukka",
      "RouteKey": "Point-13 - Point-18 - Point-15 - Point-10 - Point-26",
      "DurationMinutes": 2416,
      "Rank": 131,
      "Slot": 4,
      "IsDefaultName": false,
      "EtaUnix": 1757366915,
      "Extra": {
        "EtaLocal": "06:28",
        "RemainingText": "40\u6642\u959317\u5206",
        "RouteShort": "P13\u003E18\u003EP15\u003EP10\u003EP26"
      }
    }
  ],
  "Source": "dalamud",
  "Note": "captured from memory (WorkshopTerritory.Submersible)",
  "Character": null,
  "World": null,
  "FreeCompany": null
}`

## 主要変更コード（全文）
### apps/XIVSubmarinesReturn/src/Plugin.cs
`csharpusing System;
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
        Dalamud.Plugin.Services.IDataManager data)
    {
        _pi = pluginInterface;
        _cmd = commandManager;
        _chat = chat;
        _framework = framework;
        _gameGui = gameGui;
        _log = log;

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

            _alarm = new AlarmScheduler(Config, _chat, _log, _discord, _notion);
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
            try { TrySetIdentity(snap); } catch { }
            try { TrySetIdentity(snap); } catch { }
            try { EtaFormatter.Enrich(snap); } catch { }
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

                    if (pts.Count > 0)
                    {
                        rec.RouteKey = FormatRouteKey(pts);
                        try { Services.XsrDebug.Log(Config, $"S{i + 1} route bytes = {string.Join(",", pts)}"); } catch { }
                        try { TryWriteExtractTrace(new System.Collections.Generic.List<string> { $"route: off=0x{fieldOff:X},stride=1 -> [{string.Join(",", pts)}]" }); } catch { }
                    }
                    else
                    {
                        try { Services.XsrDebug.Log(Config, $"S{i + 1} route bytes = (none)"); } catch { }
                        try { TryWriteExtractTrace(new System.Collections.Generic.List<string> { $"route: off=0x{fieldOff:X},stride=1 -> []" }); } catch { }
                    }
                }
                catch { }

                snapshot.Items.Add(rec);
            }

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
        var name = Config.AddonName;
        if (string.IsNullOrWhiteSpace(name)) return;
        try
        {
            var unit = ResolveAddonPtr(name);
            bool visible = unit != null && unit->IsVisible;
            if (visible && (!_wasAddonVisible || (DateTime.UtcNow - _lastAutoCaptureUtc) > TimeSpan.FromSeconds(10)))
            {
                if (!_notifiedThisVisibility && TryCaptureFromAddon(name, out var snap))
                {
                    try { EtaFormatter.Enrich(snap); } catch { }
                    try { _alarm?.UpdateSnapshot(snap); } catch { }
                    BridgeWriter.WriteIfChanged(snap);
                    _chat.Print("[Submarines] 自動取得しJSONを書き出しました。");
                    _log.Info("Auto-captured and wrote JSON");
                    _notifiedThisVisibility = true; // 可視セッション中は一度だけ通知
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


`

### apps/XIVSubmarinesReturn/src/Plugin.UI.cs
`csharpusing System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ImGui = Dalamud.Bindings.ImGui.ImGui;
using ImGuiTableFlags = Dalamud.Bindings.ImGui.ImGuiTableFlags;
using ImGuiWindowFlags = Dalamud.Bindings.ImGui.ImGuiWindowFlags;
using ImGuiCond = Dalamud.Bindings.ImGui.ImGuiCond;
using ImGuiConfigFlags = Dalamud.Bindings.ImGui.ImGuiConfigFlags;
using XIVSubmarinesReturn.UI;
using Dalamud.Interface.Components;
using Dalamud.Interface;
using System.Numerics;

namespace XIVSubmarinesReturn;

public sealed partial class Plugin
{
    private bool _showUI;
    private string _uiStatus = string.Empty;
    private SubmarineSnapshot? _uiSnapshot;
    private SubmarineSnapshot? _uiPrevSnapshot;
    private DateTime _uiLastReadUtc = DateTime.MinValue;
    private string _probeText = string.Empty;
    private int _routeEditId;
    private string _routeEditName = string.Empty;
    private string _alarmLeadText = string.Empty;
    private string _routeLearnLetters = string.Empty; // e.g., "M>R>O>J>Z"
    private string _filterText = string.Empty;
    private int _sortField = 3; // 0=Name 1=Slot 2=Rank 3=ETA
    private bool _sortAsc = true;
    private XIVSubmarinesReturn.UI.SnapshotTable _snapTable = new XIVSubmarinesReturn.UI.SnapshotTable();    // reveal toggles for masked inputs
    private bool _revealDiscordWebhook;
    private bool _showLegacyUi = false;
    private int _mogshipLastMaps;
    private int _mogshipLastAliases;
    public void Ui_ReloadSnapshot() { try { _uiLastReadUtc = DateTime.MinValue; _uiStatus = "再読込"; } catch { } }
    public void Ui_ImportFromMogship() { try { TryImportFromMogship(); } catch (Exception ex) { _uiStatus = $"Mogship取込失敗: {ex.Message}"; } }
    public void Ui_OpenBridgeFolder() { try { TryOpenFolder(System.IO.Path.GetDirectoryName(BridgeWriter.CurrentFilePath())); } catch { } }
    public void Ui_OpenConfigFolder() { try { TryOpenFolder(_pi.ConfigDirectory?.FullName); } catch { } }
    public string Ui_GetUiStatus() => _uiStatus ?? string.Empty;
    public void Ui_SetUiStatus(string s) { _uiStatus = s ?? string.Empty; }
    public string Ui_GetProbeText() => _probeText ?? string.Empty;
    public void Ui_ClearProbeText() { _probeText = string.Empty; }
    public void Ui_DrawSnapshotTable() { try { DrawSnapshotTable2(); } catch { } }
    public void Ui_OpenTrace()
    {
        try
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(BridgeWriter.CurrentFilePath()) ?? string.Empty, "xsr_debug.log");
            if (!string.IsNullOrWhiteSpace(path))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch { }
    }
    public void Ui_LearnNames() { try { CmdLearnNames(); _uiStatus = "Learn triggered"; } catch (Exception ex) { _uiStatus = $"Learn failed: {ex.Message}"; } }
    // UIからの取得は無効化: 常にメモリ経由で手動取得
    public void Ui_DumpUi() { try { CmdDumpFromMemory(); _uiStatus = "Capture(Memory) triggered"; } catch (Exception ex) { _uiStatus = $"Capture(Memory) failed: {ex.Message}"; } }
    public void Ui_DumpMemory() { try { CmdDumpFromMemory(); _uiStatus = "Capture(Memory) triggered"; } catch (Exception ex) { _uiStatus = $"Capture(Memory) failed: {ex.Message}"; } }
    public void Ui_Probe() { try { _probeText = ProbeToText(); _uiStatus = "Probe done"; } catch (Exception ex) { _uiStatus = $"Probe failed: {ex.Message}"; } }
    private bool _revealNotionToken;
    private bool _revealGcalRefresh;
    private bool _revealGcalSecret;

    private void InitUI()
    {
        try
        {
            _pi.UiBuilder.Draw += DrawUI;
            _pi.UiBuilder.OpenConfigUi += () => _showUI = true;
        }
        catch { }
    }

    private void DrawUI()
    {
        if (!_showUI) return;
        try
        {
            try { var io = ImGui.GetIO(); io.ConfigFlags |= ImGuiConfigFlags.DockingEnable; } catch { }
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(760, 640), ImGuiCond.Appearing);
            ImGui.SetNextWindowSizeConstraints(new System.Numerics.Vector2(520, 320), new System.Numerics.Vector2(2000, 2000));
            if (!ImGui.Begin("XIV Submarines Return", ref _showUI, ImGuiWindowFlags.None))
            {
                ImGui.End();
                return;
            }
            try { ImGui.SetWindowFontScale(Math.Clamp(Config.UiFontScale, 0.8f, 1.5f)); } catch { }

            // Tabs（概要 / アラーム / デバッグ / スナップショット(旧)）
            if (ImGui.BeginTabBar("xsr_tabs"))
            {
                // 概要
                if (ImGui.BeginTabItem("概要"))
                {
                    try { XIVSubmarinesReturn.UI.OverviewTab.Draw(this); } catch { }
                    ImGui.Separator(); if (!_showLegacyUi) goto __OV_END;
                    ImGui.Separator(); ImGui.TextDisabled("(旧UI)");
                    // 概要: 最小構成（自動取得 + Addon名）
                    bool autoCap = Config.AutoCaptureOnWorkshopOpen;
                    if (ImGui.Checkbox("工房を開いたら自動取得", ref autoCap))
                    {
                        Config.AutoCaptureOnWorkshopOpen = autoCap;
                        SaveConfig();
                    }

                    // Top bar (Reload / Mogship import / Open folders)
                    try
                    {
                        if (Widgets.IconButton(FontAwesomeIcon.Sync, "再読込")) { _uiLastReadUtc = DateTime.MinValue; _uiStatus = "再読込"; }
                        ImGui.SameLine();
                        if (Widgets.IconButton(FontAwesomeIcon.CloudDownloadAlt, "Mogship取込")) { TryImportFromMogship(); }
                        ImGui.SameLine();
                        if (Widgets.IconButton(FontAwesomeIcon.FolderOpen, "Bridgeフォルダ")) { TryOpenFolder(System.IO.Path.GetDirectoryName(BridgeWriter.CurrentFilePath())); }
                        ImGui.SameLine();
                        if (Widgets.IconButton(FontAwesomeIcon.Cog, "Configフォルダ")) { TryOpenFolder(_pi.ConfigDirectory?.FullName); }
                        if (!string.IsNullOrEmpty(_uiStatus)) { ImGui.SameLine(); ImGui.TextDisabled(_uiStatus); }
                    }
                    catch { }

                    // Addon name input
                    var addonName = Config.AddonName ?? string.Empty;
                    if (ImGui.InputText("Addon名", ref addonName, 64))
                    {
                        Config.AddonName = addonName;
                        SaveConfig();
                    }

                    ImGui.Separator();
                    // 外観設定（密度/フォント/ETA強調）
                    int dens = Config.UiRowDensity == UiDensity.Compact ? 0 : 1;
                    if (ImGui.RadioButton("Compact", dens == 0)) { Config.UiRowDensity = UiDensity.Compact; SaveConfig(); dens = 0; }
                    ImGui.SameLine();
                    if (ImGui.RadioButton("Cozy", dens == 1)) { Config.UiRowDensity = UiDensity.Cozy; SaveConfig(); dens = 1; }
                    var fscale = Config.UiFontScale;
                    if (ImGui.SliderFloat("FontScale", ref fscale, 0.9f, 1.2f)) { Config.UiFontScale = fscale; SaveConfig(); }
                    var accStr = Config.AccentColor ?? "#1E90FF";
                    if (ImGui.InputText("Accent(#RRGGBB)", ref accStr, 16)) { Config.AccentColor = accStr; SaveConfig(); }
                    try
                    {
                        var acc = Theme.ParseColor(Config.AccentColor, new Vector4(0.12f, 0.55f, 0.96f, 1f));
                        ImGui.SameLine(); ImGui.ColorButton("acc_prev", acc);
                    }
                    catch { }
                    int soon = Config.HighlightSoonMins;
                    if (ImGui.SliderInt("ETA強調(分)", ref soon, 0, 60)) { Config.HighlightSoonMins = soon; SaveConfig(); }
                    ImGui.TextDisabled("詳細設定はデバッグタブへ移動しました。");

                    // 手動取得（UIは削除し、メモリ取得のみ残す）
                    if (ImGui.Button("メモリから取得"))
                    {
                        try { CmdDumpFromMemory(); _uiStatus = "メモリから取得を実行しました"; }
                        catch (Exception ex) { _uiStatus = $"メモリからの取得に失敗: {ex.Message}"; }
                    }
                    ImGui.SameLine(); ImGui.Text("UIが読めない場合のフォールバック");
                    if (!string.IsNullOrEmpty(_uiStatus)) { ImGui.SameLine(); ImGui.Text(_uiStatus); }

                    ImGui.Separator();
                    // 取得サマリ（スナップショット統合）
                    if (ImGui.Button("再読込")) { _uiLastReadUtc = DateTime.MinValue; }
                    ImGui.SameLine(); ImGui.Text("最新スナップショット");
                    // 表示設定（ルート表示モード）
                    try
                    {
                        int rMode = Config.RouteDisplay switch
                        {
                            RouteDisplayMode.Letters => 0,
                            RouteDisplayMode.ShortIds => 1,
                            RouteDisplayMode.Raw => 2,
                            _ => 0
                        };
                        ImGui.Text("ルート表示"); ImGui.SameLine();
                        if (ImGui.RadioButton("レター", rMode == 0)) { Config.RouteDisplay = RouteDisplayMode.Letters; SaveConfig(); rMode = 0; }
                        ImGui.SameLine();
                        if (ImGui.RadioButton("P番号", rMode == 1)) { Config.RouteDisplay = RouteDisplayMode.ShortIds; SaveConfig(); rMode = 1; }
                        ImGui.SameLine();
                        if (ImGui.RadioButton("原文", rMode == 2)) { Config.RouteDisplay = RouteDisplayMode.Raw; SaveConfig(); rMode = 2; }
                        // Mapヒント + Alias再読込
                        var mapHint = Config.SectorMapHint ?? string.Empty;
                        if (ImGui.InputText("Mapヒント", ref mapHint, 64)) { Config.SectorMapHint = mapHint; SaveConfig(); }
                        ImGui.SameLine();
                        if (ImGui.Button("Alias JSON再読込"))
                        {
                            try { _sectorResolver?.ReloadAliasIndex(); _uiStatus = "Alias reloaded"; }
                            catch (Exception ex) { _uiStatus = $"Alias reload failed: {ex.Message}"; }
                        }
                    }
                    catch { }

                    try { DrawSnapshotTable2(); } catch { }

                    // (Google Calendar 機能は廃止済み)

                    __OV_END: ImGui.EndTabItem();
                }

                // アラーム
                if (ImGui.BeginTabItem("アラーム"))
                {
                    try { XIVSubmarinesReturn.UI.AlarmTab.Draw(this); } catch { }
                    ImGui.Separator(); if (!_showLegacyUi) goto __AL_END;
                    ImGui.Separator(); ImGui.TextDisabled("(旧UI)");
                    // Discord
                    if (ImGui.CollapsingHeader("Discord"))
                    {
                        bool dEnable = Config.DiscordEnabled;
                        if (ImGui.Checkbox("有効", ref dEnable)) { Config.DiscordEnabled = dEnable; SaveConfig(); }
                        var wh = Config.DiscordWebhookUrl ?? string.Empty;
                        if (Widgets.MaskedInput("Webhook URL", ref wh, 512, ref _revealDiscordWebhook)) { Config.DiscordWebhookUrl = wh; SaveConfig(); }
                        bool latestOnly = Config.DiscordLatestOnly;
                        if (ImGui.Checkbox("最早(ETA最小)のみ", ref latestOnly)) { Config.DiscordLatestOnly = latestOnly; SaveConfig(); }
                        bool useEmbeds = Config.DiscordUseEmbeds;
                        if (ImGui.Checkbox("埋め込み(リッチ表示)を使用", ref useEmbeds)) { Config.DiscordUseEmbeds = useEmbeds; SaveConfig(); }
                        if (ImGui.Button("Discordテスト送信"))
                        {
                            try
                            {
                                var snap = _uiSnapshot ?? new SubmarineSnapshot
                                {
                                    PluginVersion = typeof(Plugin).Assembly.GetName().Version?.ToString(3) ?? "0.0.0",
                                    Items = new System.Collections.Generic.List<SubmarineRecord>
                                    {
                                        new() { Name = "Submarine-1", Slot = 1, DurationMinutes = 10, RouteKey = "Point-1 - Point-2" }
                                    }
                                };
                                try { Services.EtaFormatter.Enrich(snap); } catch { }
                                if (_discord != null)
                                {
                                    _discord.NotifySnapshotAsync(snap, Config.DiscordLatestOnly).GetAwaiter().GetResult();
                                    _uiStatus = "Discord sent";
                                }
                                else _uiStatus = "Discord not init";
                            }
                            catch (System.Exception ex) { _uiStatus = $"Discord test failed: {ex.Message}"; }
                        }
                    }

                    // ゲーム内アラーム
                    if (ImGui.CollapsingHeader("ゲーム内アラーム"))
                    {
                        bool aEnable = Config.GameAlarmEnabled;
                        if (ImGui.Checkbox("有効", ref aEnable)) { Config.GameAlarmEnabled = aEnable; SaveConfig(); }
                        if (string.IsNullOrEmpty(_alarmLeadText))
                        {
                            try
                            {
                                var ls = (Config.AlarmLeadMinutes ?? new System.Collections.Generic.List<int>()).ToArray();
                                _alarmLeadText = string.Join(",", ls);
                            }
                            catch { _alarmLeadText = "5,0"; }
                        }
                        var tmp = _alarmLeadText;
                        if (ImGui.InputText("リード分(カンマ区切り)", ref tmp, 64))
                        {
                            _alarmLeadText = tmp;
                        }
                        ImGui.SameLine();
                        if (ImGui.Button("保存"))
                        {
                            try
                            {
                                var parts = (tmp ?? string.Empty).Split(new[] { ',', ' ', ';' }, System.StringSplitOptions.RemoveEmptyEntries);
                                var list = new System.Collections.Generic.List<int>(parts.Length);
                                foreach (var p in parts)
                                {
                                    if (int.TryParse(p.Trim(), out var v)) list.Add(v);
                                }
                                Config.AlarmLeadMinutes = list;
                                SaveConfig();
                                _uiStatus = "Alarm leads saved";
                            }
                            catch (System.Exception ex) { _uiStatus = $"Alarm save failed: {ex.Message}"; }
                        }
                    }

                    // Notion
                    if (ImGui.CollapsingHeader("Notion"))
                    {
                        bool nEnable = Config.NotionEnabled;
                        if (ImGui.Checkbox("有効", ref nEnable)) { Config.NotionEnabled = nEnable; SaveConfig(); }
                        var tok = Config.NotionToken ?? string.Empty;
                        if (Widgets.MaskedInput("Integration Token", ref tok, 256, ref _revealNotionToken)) { Config.NotionToken = tok; SaveConfig(); }
                        var db = Config.NotionDatabaseId ?? string.Empty;
                        if (ImGui.InputText("Database ID", ref db, 256)) { Config.NotionDatabaseId = db; SaveConfig(); }
                        bool nLatest = Config.NotionLatestOnly;
                        if (ImGui.Checkbox("最早(ETA最小)のみ", ref nLatest)) { Config.NotionLatestOnly = nLatest; SaveConfig(); }

                        // Upsert Key Mode
                        var modeVal = (int)Config.NotionKeyMode;
                        ImGui.Text("Upsertキー方式");
                        if (ImGui.RadioButton("スロット単位", modeVal == 0)) { Config.NotionKeyMode = NotionKeyMode.PerSlot; SaveConfig(); modeVal = 0; }
                        ImGui.SameLine();
                        if (ImGui.RadioButton("スロット+ルート", modeVal == 1)) { Config.NotionKeyMode = NotionKeyMode.PerSlotRoute; SaveConfig(); modeVal = 1; }
                        ImGui.SameLine();
                        if (ImGui.RadioButton("航海毎(レガシー)", modeVal == 2)) { Config.NotionKeyMode = NotionKeyMode.PerVoyage; SaveConfig(); modeVal = 2; }

                        // Property names
                        var pn = Config.NotionPropName ?? "Name";
                        if (ImGui.InputText("Prop: Name (title)", ref pn, 64)) { Config.NotionPropName = pn; SaveConfig(); }
                        var ps = Config.NotionPropSlot ?? "Slot";
                        if (ImGui.InputText("Prop: Slot (number)", ref ps, 64)) { Config.NotionPropSlot = ps; SaveConfig(); }
                        var pe = Config.NotionPropEta ?? "ETA";
                        if (ImGui.InputText("Prop: ETA (date)", ref pe, 64)) { Config.NotionPropEta = pe; SaveConfig(); }
                        var pr = Config.NotionPropRoute ?? "Route";
                        if (ImGui.InputText("Prop: Route (rich_text)", ref pr, 64)) { Config.NotionPropRoute = pr; SaveConfig(); }
                        var prk = Config.NotionPropRank ?? "Rank";
                        if (ImGui.InputText("Prop: Rank (number)", ref prk, 64)) { Config.NotionPropRank = prk; SaveConfig(); }
                        var px = Config.NotionPropExtId ?? "ExtId";
                        if (ImGui.InputText("Prop: ExtId (rich_text)", ref px, 64)) { Config.NotionPropExtId = px; SaveConfig(); }
                        var prem = Config.NotionPropRemaining ?? "Remaining";
                        if (ImGui.InputText("Prop: Remaining (rich_text)", ref prem, 64)) { Config.NotionPropRemaining = prem; SaveConfig(); }
                        var pw = Config.NotionPropWorld ?? "World";
                        if (ImGui.InputText("Prop: World (rich_text)", ref pw, 64)) { Config.NotionPropWorld = pw; SaveConfig(); }
                        var pc = Config.NotionPropCharacter ?? "Character";
                        if (ImGui.InputText("Prop: Character (rich_text)", ref pc, 64)) { Config.NotionPropCharacter = pc; SaveConfig(); }
                        var pfc = Config.NotionPropFC ?? "FC";
                        if (ImGui.InputText("Prop: FC (rich_text)", ref pfc, 64)) { Config.NotionPropFC = pfc; SaveConfig(); }

                        if (ImGui.Button("プロパティ検証"))
                        {
                            try
                            {
                                if (_notion != null)
                                {
                                    var msg = _notion.EnsureDatabasePropsAsync().GetAwaiter().GetResult();
                                    _uiStatus = msg;
                                }
                                else _uiStatus = "Notion client not ready";
                            }
                            catch (System.Exception ex) { _uiStatus = $"Notion validate failed: {ex.Message}"; }
                        }

                        if (ImGui.Button("Notionテスト送信"))
                        {
                            try
                            {
                                var snap = _uiSnapshot ?? new SubmarineSnapshot
                                {
                                    PluginVersion = typeof(Plugin).Assembly.GetName().Version?.ToString(3) ?? "0.0.0",
                                    Items = new System.Collections.Generic.List<SubmarineRecord>
                                    {
                                        new() { Name = "Submarine-1", Slot = 1, DurationMinutes = 10, RouteKey = "Point-1 - Point-2", Rank = 10, EtaUnix = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds() }
                                    }
                                };
                                try { Services.EtaFormatter.Enrich(snap); } catch { }
                                if (_alarm != null)
                                {
                                    // reuse scheduler path
                                    _alarm.UpdateSnapshot(snap);
                                    _uiStatus = "Notion test enqueued";
                                }
                            }
                            catch (System.Exception ex) { _uiStatus = $"Notion test failed: {ex.Message}"; }
                        }
                    }

                    __AL_END: ImGui.EndTabItem();
                }

                // デバッグ
                if (ImGui.BeginTabItem("デバッグ"))
                {
                    // デバッグ設定
                    try { XIVSubmarinesReturn.UI.DebugTab.Draw(this); } catch { }
                    ImGui.Separator(); if (!_showLegacyUi) goto __DBG_END;
                    ImGui.Separator(); ImGui.TextDisabled("(旧UI)");
                    if (ImGui.CollapsingHeader("デバッグ"))
                    {
                        bool dbg = Config.DebugLogging;
                        if (ImGui.Checkbox("デバッグログを有効化", ref dbg)) { Config.DebugLogging = dbg; SaveConfig(); }
                        if (ImGui.Button("トレースを開く"))
                        {
                            try
                            {
                                var path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(BridgeWriter.CurrentFilePath()) ?? string.Empty, "xsr_debug.log");
                                if (!string.IsNullOrWhiteSpace(path))
                                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
                            }
                            catch (System.Exception ex) { _uiStatus = $"Open trace failed: {ex.Message}"; }
                        }
                        ImGui.SameLine();
                        if (ImGui.Button("セルフテスト実行"))
                        {
                            try { CmdSelfTest(); _uiStatus = "Self-test executed"; }
                            catch (System.Exception ex) { _uiStatus = $"Self-test failed: {ex.Message}"; }
                        }
                    }

                    // ツール群（1行=ボタン+説明）
                    // UIからの取得ボタンは非表示（機能無効化）

                    // UIからの取得ボタンは非表示（機能無効化）

                    if (ImGui.Button("メモリから取得"))
                    {
                        try { CmdDumpFromMemory(); _uiStatus = "Capture(Memory) triggered"; }
                        catch (Exception ex) { _uiStatus = $"Capture(Memory) failed: {ex.Message}"; }
                    }
                    ImGui.SameLine(); ImGui.Text("UIが読めない場合のフォールバック");

                    if (ImGui.Button("アドオン探索"))
                    {
                        try { _probeText = ProbeToText(); _uiStatus = "Probe done"; }
                        catch (Exception ex) { _uiStatus = $"Probe failed: {ex.Message}"; }
                    }
                    ImGui.SameLine(); ImGui.Text("候補アドオンの存在/表示状態を確認します");

                    if (ImGui.Button("フォルダを開く"))
                    {
                        try { OnCmdOpen("/subopen", string.Empty); _uiStatus = "Folder opened"; }
                        catch (Exception ex) { _uiStatus = $"Open failed: {ex.Message}"; }
                    }
                    ImGui.SameLine(); ImGui.Text("出力フォルダ(bridge)を開きます");
                    if (!string.IsNullOrEmpty(_uiStatus)) { ImGui.SameLine(); ImGui.Text(_uiStatus); }

                    ImGui.Separator();
                    if (!string.IsNullOrEmpty(_probeText) && ImGui.Button("結果をクリア")) { _probeText = string.Empty; }
                    if (!string.IsNullOrEmpty(_probeText))
                    {
                        ImGui.Separator();
                        ImGui.Text("探索結果:");
                        ImGui.BeginChild("probe", new Vector2(480, 120), true, ImGuiWindowFlags.None);
                        ImGui.TextWrapped(_probeText);
                        ImGui.EndChild();
                    }

                    // Advanced settings (moved from Overview)
                    ImGui.Separator();
                    if (ImGui.CollapsingHeader("詳細設定"))
                    {
                        bool memFb = Config.UseMemoryFallback;
                        if (ImGui.Checkbox("UI取得に失敗したらメモリから取得", ref memFb))
                        {
                            Config.UseMemoryFallback = memFb;
                            SaveConfig();
                        }

                        bool selExt = Config.UseSelectStringExtraction;
                        if (ImGui.Checkbox("SelectString抽出を使用", ref selExt))
                        {
                            Config.UseSelectStringExtraction = selExt;
                            SaveConfig();
                        }
                        bool selDet = Config.UseSelectStringDetailExtraction;
                        if (ImGui.Checkbox("SelectString詳細抽出を使用", ref selDet))
                        {
                            Config.UseSelectStringDetailExtraction = selDet;
                            SaveConfig();
                        }
                        bool aggr = Config.AggressiveFallback;
                        if (ImGui.Checkbox("テキスト行が少ない場合に強力フォールバック", ref aggr))
                        {
                            Config.AggressiveFallback = aggr;
                            SaveConfig();
                        }
                        bool acceptDefaults = Config.AcceptDefaultNamesInMemory;
                        if (ImGui.Checkbox("メモリ名が既定(Submarine-<n>)でも許可", ref acceptDefaults))
                        {
                            Config.AcceptDefaultNamesInMemory = acceptDefaults;
                            SaveConfig();
                        }

                        ImGui.Separator();
                        ImGui.Text("スロット別名 (1..4)");
                        for (int i = 0; i < 4; i++)
                        {
                            var val = Config.SlotAliases != null && i < Config.SlotAliases.Length ? (Config.SlotAliases[i] ?? string.Empty) : string.Empty;
                            var tmp = val;
                            ImGui.PushID(i);
                            if (ImGui.InputText("##alias", ref tmp, 64))
                            {
                                if (Config.SlotAliases == null || Config.SlotAliases.Length < 4)
                                    Config.SlotAliases = new string[4];
                                Config.SlotAliases[i] = tmp;
                                SaveConfig();
                            }
                            ImGui.SameLine();
                            ImGui.Text($"Slot {i + 1}");
                            ImGui.PopID();
                        }

                        // ルート名エディタ（任意）
                        ImGui.Separator();
                        ImGui.Text("ルート名 (ID -> 表示名)");
                        ImGui.InputInt("ID", ref _routeEditId);
                        ImGui.InputText("名前", ref _routeEditName, 64);
                        if (ImGui.Button("追加/更新"))
                        {
                            try
                            {
                                if (_routeEditId >= 0 && _routeEditId <= 255)
                                {
                                    Config.RouteNames[(byte)_routeEditId] = _routeEditName ?? string.Empty;
                                    SaveConfig();
                                    _uiStatus = "Route name saved";
                                }
                            }
                            catch (Exception ex) { _uiStatus = $"Route save failed: {ex.Message}"; }
                        }
                        if (Config.RouteNames != null && Config.RouteNames.Count > 0)
                        {
                            if (ImGui.BeginTable("routes", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                            {
                                ImGui.TableSetupColumn("ID");
                                ImGui.TableSetupColumn("名前");
                                ImGui.TableHeadersRow();
                                foreach (var kv in Config.RouteNames.OrderBy(k => k.Key))
                                {
                                    ImGui.TableNextRow();
                                    ImGui.TableSetColumnIndex(0); ImGui.Text(kv.Key.ToString());
                                    ImGui.TableSetColumnIndex(1); ImGui.Text(kv.Value ?? string.Empty);
                                }
                                ImGui.EndTable();
                            }
                        }

                        // スナップショットからレター表記を学習
                        ImGui.Separator();
                        ImGui.Text("スナップショットからレター表記を学習 (例: M>R>O>J>Z)");
                        ImGui.InputText("レター並び", ref _routeLearnLetters, 64);
                        ImGui.SameLine();
                        if (ImGui.Button("対応付けを保存"))
                        {
                            try
                            {
                                var snap = _uiSnapshot;
                                var first = snap?.Items?.FirstOrDefault(x =>
                                    (x.Extra != null && x.Extra.TryGetValue("RouteShort", out var rs) && !string.IsNullOrWhiteSpace(rs))
                                    || !string.IsNullOrWhiteSpace(x.RouteKey));
                                if (first == null) { _uiStatus = "スナップショットに航路がありません"; }
                                else
                                {
                                    // P番号列を取得
                                    var pText = string.Empty;
                                    if (first.Extra != null && first.Extra.TryGetValue("RouteShort", out var rs0) && !string.IsNullOrWhiteSpace(rs0)) pText = rs0!;
                                    else pText = first.RouteKey ?? string.Empty;
                                    var nums = new System.Collections.Generic.List<int>();
                                    try
                                    {
                                        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(pText ?? string.Empty, @"P?(\d+)") )
                                        {
                                            if (int.TryParse(m.Groups[1].Value, out var v)) nums.Add(v);
                                        }
                                    } catch { }

                                    // レター列を取得
                                    var letters = (_routeLearnLetters ?? string.Empty)
                                        .Split(new[]{'>','-',' ','/','\t'}, System.StringSplitOptions.RemoveEmptyEntries)
                                        .Select(s => s.Trim())
                                        .ToList();

                                    if (nums.Count == 0 || letters.Count == 0 || nums.Count != letters.Count)
                                    {
                                        _uiStatus = $"学習失敗: P数={nums.Count} レター数={letters.Count} (一致している必要があります)";
                                    }
                                    else
                                    {
                                        if (Config.RouteNames == null) Config.RouteNames = new System.Collections.Generic.Dictionary<byte,string>();
                                        for (int i = 0; i < nums.Count; i++)
                                        {
                                            var id = nums[i];
                                            var nm = letters[i];
                                            if (id >= 0 && id <= 255 && !string.IsNullOrWhiteSpace(nm))
                                                Config.RouteNames[(byte)id] = nm;
                                        }
                                        SaveConfig();
                                        _uiStatus = "レター対応を保存しました";
                                    }
                                }
                            }
                            catch (System.Exception ex) { _uiStatus = $"学習エラー: {ex.Message}"; }
                        }
                    }

                    __DBG_END: ImGui.EndTabItem();
                }

                ImGui.EndTabBar();
            }
            // (old Overview block removed)

            // (Google Calendar 機能は廃止済み)

            // Discord (moved under Alarm tab)
            if (false && ImGui.CollapsingHeader("Discord"))
            {
                bool dEnable = Config.DiscordEnabled;
                if (ImGui.Checkbox("Enable", ref dEnable)) { Config.DiscordEnabled = dEnable; SaveConfig(); }
                var wh = Config.DiscordWebhookUrl ?? string.Empty;
                if (Widgets.MaskedInput("Webhook URL", ref wh, 512, ref _revealDiscordWebhook)) { Config.DiscordWebhookUrl = wh; SaveConfig(); }
                bool latestOnly = Config.DiscordLatestOnly;
                if (ImGui.Checkbox("Earliest only (ETA min) / \u6700\u65e9(ETA\u6700\u5c0f)", ref latestOnly)) { Config.DiscordLatestOnly = latestOnly; SaveConfig(); }
                bool useEmbeds = Config.DiscordUseEmbeds;
                if (ImGui.Checkbox("Use embeds", ref useEmbeds)) { Config.DiscordUseEmbeds = useEmbeds; SaveConfig(); }
                if (ImGui.Button("Test Discord"))
                {
                    try
                    {
                        var snap = _uiSnapshot ?? new SubmarineSnapshot
                        {
                            PluginVersion = typeof(Plugin).Assembly.GetName().Version?.ToString(3) ?? "0.0.0",
                            Items = new System.Collections.Generic.List<SubmarineRecord>
                            {
                                new() { Name = "Submarine-1", Slot = 1, DurationMinutes = 10, RouteKey = "Point-1 - Point-2" }
                            }
                        };
                        try { Services.EtaFormatter.Enrich(snap); } catch { }
                        if (_discord != null)
                        {
                            _discord.NotifySnapshotAsync(snap, Config.DiscordLatestOnly).GetAwaiter().GetResult();
                            _uiStatus = "Discord sent";
                        }
                        else _uiStatus = "Discord not init";
                    }
                    catch (System.Exception ex) { _uiStatus = $"Discord test failed: {ex.Message}"; }
                }
            }

            // Debug (own tab)
            if (false && ImGui.CollapsingHeader("Debug"))
            {
                bool dbg = Config.DebugLogging;
                if (ImGui.Checkbox("Enable debug logging", ref dbg)) { Config.DebugLogging = dbg; SaveConfig(); }
                if (ImGui.Button("Open trace"))
                {
                    try
                    {
                        var path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(BridgeWriter.CurrentFilePath()) ?? string.Empty, "xsr_debug.log");
                        if (!string.IsNullOrWhiteSpace(path))
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = path, UseShellExecute = true });
                    }
                    catch (System.Exception ex) { _uiStatus = $"Open trace failed: {ex.Message}"; }
                }
                ImGui.SameLine();
                if (ImGui.Button("Run self-test"))
                {
                    try { CmdSelfTest(); _uiStatus = "Self-test executed"; }
                    catch (System.Exception ex) { _uiStatus = $"Self-test failed: {ex.Message}"; }
                }
            }

            // Alarm (GameAlarm) under Alarm tab
            if (false && ImGui.CollapsingHeader("Alarm"))
            {
                if (string.IsNullOrEmpty(_alarmLeadText))
                {
                    try
                    {
                        var ls = (Config.AlarmLeadMinutes ?? new System.Collections.Generic.List<int>()).ToArray();
                        _alarmLeadText = string.Join(",", ls);
                    }
                    catch { _alarmLeadText = "5,0"; }
                }
                var tmp = _alarmLeadText;
                if (ImGui.InputText("Lead minutes (comma)", ref tmp, 64))
                {
                    _alarmLeadText = tmp;
                }
                ImGui.SameLine();
                if (ImGui.Button("Save Alarm"))
                {
                    try
                    {
                        var parts = (tmp ?? string.Empty).Split(new[] { ',', ' ', ';' }, System.StringSplitOptions.RemoveEmptyEntries);
                        var list = new System.Collections.Generic.List<int>(parts.Length);
                        foreach (var p in parts)
                        {
                            if (int.TryParse(p.Trim(), out var v)) list.Add(v);
                        }
                        Config.AlarmLeadMinutes = list;
                        SaveConfig();
                        _uiStatus = "Alarm leads saved";
                    }
                    catch (System.Exception ex) { _uiStatus = $"Alarm save failed: {ex.Message}"; }
                }
            }

            // Notion under Alarm tab (Notion upsert/validate lives here)
            if (false && ImGui.CollapsingHeader("Notion"))
            {
                bool nEnable = Config.NotionEnabled;
                if (ImGui.Checkbox("Enable", ref nEnable)) { Config.NotionEnabled = nEnable; SaveConfig(); }
                var tok = Config.NotionToken ?? string.Empty;
                if (Widgets.MaskedInput("Integration Token", ref tok, 256, ref _revealNotionToken)) { Config.NotionToken = tok; SaveConfig(); }
                var db = Config.NotionDatabaseId ?? string.Empty;
                if (ImGui.InputText("Database ID", ref db, 256)) { Config.NotionDatabaseId = db; SaveConfig(); }
                bool nLatest = Config.NotionLatestOnly;
                if (ImGui.Checkbox("Earliest only (ETA min)", ref nLatest)) { Config.NotionLatestOnly = nLatest; SaveConfig(); }

                // Upsert Key Mode
                var modeVal = (int)Config.NotionKeyMode;
                ImGui.Text("Upsert key mode");
                if (ImGui.RadioButton("Per Slot", modeVal == 0)) { Config.NotionKeyMode = NotionKeyMode.PerSlot; SaveConfig(); modeVal = 0; }
                ImGui.SameLine();
                if (ImGui.RadioButton("Per Slot + Route", modeVal == 1)) { Config.NotionKeyMode = NotionKeyMode.PerSlotRoute; SaveConfig(); modeVal = 1; }
                ImGui.SameLine();
                if (ImGui.RadioButton("Per Voyage (legacy)", modeVal == 2)) { Config.NotionKeyMode = NotionKeyMode.PerVoyage; SaveConfig(); modeVal = 2; }

                // Property names
                var pn = Config.NotionPropName ?? "Name";
                if (ImGui.InputText("Prop: Name (title)", ref pn, 64)) { Config.NotionPropName = pn; SaveConfig(); }
                var ps = Config.NotionPropSlot ?? "Slot";
                if (ImGui.InputText("Prop: Slot (number)", ref ps, 64)) { Config.NotionPropSlot = ps; SaveConfig(); }
                var pe = Config.NotionPropEta ?? "ETA";
                if (ImGui.InputText("Prop: ETA (date)", ref pe, 64)) { Config.NotionPropEta = pe; SaveConfig(); }
                var pr = Config.NotionPropRoute ?? "Route";
                if (ImGui.InputText("Prop: Route (rich_text)", ref pr, 64)) { Config.NotionPropRoute = pr; SaveConfig(); }
                var prk = Config.NotionPropRank ?? "Rank";
                if (ImGui.InputText("Prop: Rank (number)", ref prk, 64)) { Config.NotionPropRank = prk; SaveConfig(); }
                var px = Config.NotionPropExtId ?? "ExtId";
                if (ImGui.InputText("Prop: ExtId (rich_text)", ref px, 64)) { Config.NotionPropExtId = px; SaveConfig(); }
                var prem = Config.NotionPropRemaining ?? "Remaining";
                if (ImGui.InputText("Prop: Remaining (rich_text)", ref prem, 64)) { Config.NotionPropRemaining = prem; SaveConfig(); }
                var pw = Config.NotionPropWorld ?? "World";
                if (ImGui.InputText("Prop: World (rich_text)", ref pw, 64)) { Config.NotionPropWorld = pw; SaveConfig(); }
                var pc = Config.NotionPropCharacter ?? "Character";
                if (ImGui.InputText("Prop: Character (rich_text)", ref pc, 64)) { Config.NotionPropCharacter = pc; SaveConfig(); }
                var pfc = Config.NotionPropFC ?? "FC";
                if (ImGui.InputText("Prop: FC (rich_text)", ref pfc, 64)) { Config.NotionPropFC = pfc; SaveConfig(); }

                if (ImGui.Button("Validate properties"))
                {
                    try
                    {
                        if (_notion != null)
                        {
                            var msg = _notion.EnsureDatabasePropsAsync().GetAwaiter().GetResult();
                            _uiStatus = msg;
                        }
                        else _uiStatus = "Notion client not ready";
                    }
                    catch (System.Exception ex) { _uiStatus = $"Notion validate failed: {ex.Message}"; }
                }

                if (ImGui.Button("Test Notion"))
                {
                    try
                    {
                        var snap = _uiSnapshot ?? new SubmarineSnapshot
                        {
                            PluginVersion = typeof(Plugin).Assembly.GetName().Version?.ToString(3) ?? "0.0.0",
                            Items = new System.Collections.Generic.List<SubmarineRecord>
                            {
                                new() { Name = "Submarine-1", Slot = 1, DurationMinutes = 10, RouteKey = "Point-1 - Point-2", Rank = 10, EtaUnix = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds() }
                            }
                        };
                        try { Services.EtaFormatter.Enrich(snap); } catch { }
                        if (_alarm != null)
                        {
                            // reuse scheduler path
                            _alarm.UpdateSnapshot(snap);
                            _uiStatus = "Notion test enqueued";
                        }
                    }
                    catch (System.Exception ex) { _uiStatus = $"Notion test failed: {ex.Message}"; }
                }
            }

            // (old Debug tools block removed)

            ImGui.End();
        }
        catch { try { ImGui.End(); } catch { } }
    }

    private void DrawSnapshotTable()
    {
        try
        {
            // Reload snapshot if file changed or last read too old (5s)
            var path = BridgeWriter.CurrentFilePath();
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                var lastWrite = File.GetLastWriteTimeUtc(path);
                if (_uiSnapshot == null || lastWrite > _uiLastReadUtc || (DateTime.UtcNow - _uiLastReadUtc) > TimeSpan.FromSeconds(5))
                {
                    try
                    {
                        _uiPrevSnapshot = _uiSnapshot;
                        var json = File.ReadAllText(path);
                        _uiSnapshot = JsonSerializer.Deserialize<SubmarineSnapshot>(json);
                        _uiLastReadUtc = lastWrite;
                    }
                    catch (Exception ex)
                    {
                        _uiStatus = $"Read json failed: {ex.Message}";
                    }
                }
            }

            ImGui.Text($"スナップショット: {( _uiSnapshot?.Items?.Count ?? 0)} 件");
            if (_uiSnapshot?.Items == null || _uiSnapshot.Items.Count == 0)
            {
                ImGui.TextDisabled("データがありません。取得を実行してください。");
                return;
            }

            // 列: スロット / 名前 / ランク / ETA/残り / ルート（5列に統合して右端の余計な列を抑制）
            if (ImGui.BeginTable("subs", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
            {
                ImGui.TableSetupColumn("スロット");
                ImGui.TableSetupColumn("名前");
                ImGui.TableSetupColumn("ランク");
                ImGui.TableSetupColumn("ETA/残り");
                ImGui.TableSetupColumn("ルート");
                ImGui.TableHeadersRow();

                foreach (var it in _uiSnapshot.Items)
                {
                    bool changed = IsChanged(it);
                    ImGui.TableNextRow();
                    // Slot
                    ImGui.TableSetColumnIndex(0);
                    ImGui.Text(it.Slot.HasValue ? $"S{it.Slot.Value}" : "");
                    // Name
                    ImGui.TableSetColumnIndex(1);
                    if (changed) { ImGui.Text($"* {it.Name}"); }
                    else ImGui.Text(it.Name ?? string.Empty);
                    // Rank
                    ImGui.TableSetColumnIndex(2);
                    ImGui.Text(it.Rank?.ToString() ?? "");
                    // ETA/残り（1列に統合）
                    ImGui.TableSetColumnIndex(3);
                    try
                    {
                        var etaLoc = (it.Extra != null && it.Extra.TryGetValue("EtaLocal", out var t)) ? t : string.Empty;
                        var rem = (it.Extra != null && it.Extra.TryGetValue("RemainingText", out var r)) ? r : string.Empty;
                        var txt = string.IsNullOrWhiteSpace(rem) ? (etaLoc ?? string.Empty) : $"{etaLoc} / {rem}";
                        bool highlight = false;
                        try
                        {
                            int minsLeft = int.MaxValue;
                            if (it.EtaUnix.HasValue && it.EtaUnix.Value > 0)
                            {
                                var eta = DateTimeOffset.FromUnixTimeSeconds(it.EtaUnix.Value);
                                minsLeft = (int)Math.Round((eta - DateTimeOffset.Now).TotalMinutes);
                            }
                            else if (!string.IsNullOrWhiteSpace(rem))
                            {
                                var m = System.Text.RegularExpressions.Regex.Match(rem, @"(?:(?<h>\d+)\s*時間)?\s*(?<m>\d+)\s*分");
                                if (m.Success)
                                {
                                    int h = m.Groups["h"].Success ? int.Parse(m.Groups["h"].Value) : 0;
                                    int mm = m.Groups["m"].Success ? int.Parse(m.Groups["m"].Value) : 0;
                                    minsLeft = Math.Max(0, h * 60 + mm);
                                }
                            }
                            if (minsLeft <= Config.HighlightSoonMins) highlight = true;
                        }
                        catch { }
                        if (highlight)
                        {
                            var acc = Theme.ParseColor(Config.AccentColor, new Vector4(0.12f, 0.55f, 0.96f, 1f));
                            ImGui.PushStyleColor(Dalamud.Bindings.ImGui.ImGuiCol.Text, acc);
                            ImGui.Text(txt ?? string.Empty);
                            ImGui.PopStyleColor();
                        }
                        else ImGui.Text(txt ?? string.Empty);
                    }
                    catch { ImGui.Text(""); }

                    // Route -> 表示モードに応じて表示（レター優先）
                    ImGui.TableSetColumnIndex(4);
                    try
                    {
                        var show = BuildRouteDisplay(it);
                        if (ImGui.Selectable(show ?? string.Empty, false))
                        {
                            try { ImGui.SetClipboardText(show ?? string.Empty); } catch { }
                            _uiStatus = "ルートをコピーしました";
                        }
                    }
                    catch { ImGui.Text(it.RouteKey ?? string.Empty); }
                }

                ImGui.EndTable();
            }
        }
        catch (Exception ex)
        {
            _uiStatus = $"Table error: {ex.Message}";
        }
    }

    // New table drawer (encapsulated SnapshotTable)
    private void DrawSnapshotTable2()
    {
        try
        {
            // Reload snapshot if file changed or last read too old (5s)
            var path = BridgeWriter.CurrentFilePath();
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                var lastWrite = File.GetLastWriteTimeUtc(path);
                if (_uiSnapshot == null || lastWrite > _uiLastReadUtc || (DateTime.UtcNow - _uiLastReadUtc) > TimeSpan.FromSeconds(5))
                {
                    try
                    {
                        _uiPrevSnapshot = _uiSnapshot;
                        var json = File.ReadAllText(path);
                        _uiSnapshot = JsonSerializer.Deserialize<SubmarineSnapshot>(json);
                        _uiLastReadUtc = lastWrite;
                    }
                    catch (Exception ex)
                    {
                        _uiStatus = $"Read json failed: {ex.Message}";
                    }
                }
            }

            ImGui.Text($"スナップショット: {( _uiSnapshot?.Items?.Count ?? 0)} 件");
            if (_uiSnapshot?.Items == null || _uiSnapshot.Items.Count == 0)
            {
                ImGui.TextDisabled("データがありません。取得を実行してください。");
                return;
            }

            _snapTable.Draw(_uiSnapshot.Items,
                Config,
                (id) =>
                {
                    try { return _sectorResolver?.GetAliasForSector((uint)id, Config.SectorMapHint); } catch { return null; }
                },
                (msg) => { _uiStatus = msg; },
                () => { try { SaveConfig(); } catch { } }
            );
        }
        catch (Exception ex)
        {
            _uiStatus = $"Table error: {ex.Message}";
        }
    }

    private string BuildRouteDisplay(SubmarineRecord it)
    {
        try
        {
            var baseRoute = (it.Extra != null && it.Extra.TryGetValue("RouteShort", out var rs)) ? rs : it.RouteKey;
            if (string.IsNullOrWhiteSpace(baseRoute)) return string.Empty;

            // Raw は原文（RouteKey）を優先して返す
            if (Config.RouteDisplay == RouteDisplayMode.Raw)
                return it.RouteKey ?? baseRoute ?? string.Empty;

            // P番号抽出
            var nums = new System.Collections.Generic.List<int>();
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(baseRoute ?? string.Empty, @"P?(\d+)"))
            {
                if (int.TryParse(m.Groups[1].Value, out var v)) nums.Add(v);
            }
            if (nums.Count == 0) return baseRoute ?? string.Empty;

            switch (Config.RouteDisplay)
            {
                case RouteDisplayMode.ShortIds:
                    return string.Join('>', nums.Select(n => $"P{n}"));
                case RouteDisplayMode.Letters:
                default:
                    // Resolver優先→学習（RouteNames）→P番号
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
                    var text = string.Join('>', parts);
                    // 全てP番号（未学習）なら、原文のほうが分かりやすいので原文を優先
                    return parts.All(p => p.StartsWith("P")) ? (baseRoute ?? text) : text;
            }
        }
        catch { return it.RouteKey ?? string.Empty; }
    }

    private bool IsChanged(SubmarineRecord it)
    {
        try
        {
            var prev = _uiPrevSnapshot?.Items?.FirstOrDefault(x => string.Equals(x.Name, it.Name, StringComparison.Ordinal));
            if (prev == null) return false;
            if (prev.Rank != it.Rank) return true;
            if (prev.DurationMinutes != it.DurationMinutes) return true;
            if (!string.Equals(prev.RouteKey ?? string.Empty, it.RouteKey ?? string.Empty, StringComparison.Ordinal)) return true;
            return false;
        }
        catch { return false; }
    }

    private int CompareItems(SubmarineRecord? a, SubmarineRecord? b, int field, bool asc)
    {
        try
        {
            int s = 0;
            switch (field)
            {
                case 0: s = string.Compare(a?.Name, b?.Name, StringComparison.OrdinalIgnoreCase); break;
                case 1: s = Nullable.Compare(a?.Slot, b?.Slot); break;
                case 2: s = Nullable.Compare(a?.Rank, b?.Rank); break;
                case 3:
                    long ea = a?.EtaUnix ?? long.MinValue;
                    long eb = b?.EtaUnix ?? long.MinValue;
                    s = ea.CompareTo(eb);
                    break;
            }
            return asc ? s : -s;
        }
        catch { return 0; }
    }

    private void TryImportFromMogship()
    {
        try
        {
            var aliasPath = System.IO.Path.Combine(_pi.ConfigDirectory?.FullName ?? string.Empty, "AliasIndex.json");
            var importer = new XIVSubmarinesReturn.Sectors.MogshipImporter(new System.Net.Http.HttpClient(), _log);
            var t = importer.ImportAsync(aliasPath); t.Wait();
            var maps = 0; var aliases = 0;
            try { (maps, aliases) = t.Result; } catch { }
            _mogshipLastMaps = maps; _mogshipLastAliases = aliases;
            _sectorResolver?.ReloadAliasIndex();
            _uiStatus = $"Mogship取り込み: { _mogshipLastAliases } aliases / { _mogshipLastMaps } maps";
        }
        catch (Exception ex) { _uiStatus = $"取込失敗: {ex.Message}"; }
    }

    private void TryOpenFolder(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            if (!System.IO.Directory.Exists(path)) return;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = path,
                UseShellExecute = true
            });
        }
        catch { }
    }

    private string ProbeToText()
    {
        var candidates = new[]
        {
            Config.AddonName,
            "SelectString", "SelectIconString",
            "CompanyCraftSubmersibleList", "FreeCompanyWorkshopSubmersible", "CompanyCraftSubmersible", "CompanyCraftList",
            "SubmersibleExploration", "SubmarineExploration", "SubmersibleVoyage", "ExplorationResult",
        };
        var lines = new System.Collections.Generic.List<string>();
        lines.Add("[Submarines] Probing addon availability...");
        unsafe
        {
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
                lines.Add($"  {n}: " + (shown ? $"visible (idx={foundIdx})" : (exists ? "exists (hidden)" : "not found")));
            }
        }
        return string.Join(Environment.NewLine, lines);
    }
}










`

### apps/XIVSubmarinesReturn/src/UI/OverviewTab.cs
`csharpusing System;
using System.Numerics;
using ImGui = Dalamud.Bindings.ImGui.ImGui;
using ImGuiTableFlags = Dalamud.Bindings.ImGui.ImGuiTableFlags;

namespace XIVSubmarinesReturn.UI
{
    internal static class OverviewTab
    {
        internal static void Draw(Plugin p)
        {
            using var _ = Theme.UseDensity(p.Config.UiRowDensity);
            try { ImGui.SetWindowFontScale(Math.Clamp(p.Config.UiFontScale, 0.9f, 1.3f)); } catch { }

            if (ImGui.BeginTable("ov_grid_top", 2, ImGuiTableFlags.SizingStretchSame))
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                using (Widgets.Card("ov_ops", new Vector2(0, 96)))
                {
                    Widgets.SectionHeader("Operations");
                    // Manual capture always via memory (UI capture hidden)
                    if (ImGui.Button("Manual (Memory)")) { p.Ui_DumpMemory(); }
                    ImGui.SameLine();
                    if (Widgets.IconButton(Dalamud.Interface.FontAwesomeIcon.Sync, "Reload")) { p.Ui_ReloadSnapshot(); }
                    ImGui.SameLine();
                    if (Widgets.IconButton(Dalamud.Interface.FontAwesomeIcon.CloudDownloadAlt, "Import Mogship")) { p.Ui_ImportFromMogship(); }
                    ImGui.SameLine();
                    if (Widgets.IconButton(Dalamud.Interface.FontAwesomeIcon.FolderOpen, "Open Bridge Folder")) { p.Ui_OpenBridgeFolder(); }
                    ImGui.SameLine();
                    if (Widgets.IconButton(Dalamud.Interface.FontAwesomeIcon.Cog, "Open Config Folder")) { p.Ui_OpenConfigFolder(); }
                    bool autoCap = p.Config.AutoCaptureOnWorkshopOpen;
                    ImGui.SameLine(); if (ImGui.Checkbox("AutoCaptureOnWorkshopOpen", ref autoCap)) { p.Config.AutoCaptureOnWorkshopOpen = autoCap; p.SaveConfig(); }
                    var status = p.Ui_GetUiStatus();
                    if (!string.IsNullOrEmpty(status)) { ImGui.SameLine(); ImGui.TextDisabled(status); }
                }

                ImGui.TableSetColumnIndex(1);
                using (Widgets.Card("ov_appearance", new Vector2(0, 120)))
                {
                    Widgets.SectionHeader("Appearance/Display");
                    int dens = p.Config.UiRowDensity == UiDensity.Compact ? 0 : 1;
                    if (ImGui.RadioButton("Compact", dens == 0)) { p.Config.UiRowDensity = UiDensity.Compact; p.SaveConfig(); dens = 0; }
                    ImGui.SameLine();
                    if (ImGui.RadioButton("Cozy", dens == 1)) { p.Config.UiRowDensity = UiDensity.Cozy; p.SaveConfig(); dens = 1; }
                    var fscale = p.Config.UiFontScale;
                    if (ImGui.SliderFloat("FontScale", ref fscale, 0.9f, 1.2f)) { p.Config.UiFontScale = fscale; p.SaveConfig(); }
                    var accStr = p.Config.AccentColor ?? "#1E90FF";
                    if (ImGui.InputText("Accent(#RRGGBB)", ref accStr, 16)) { p.Config.AccentColor = accStr; p.SaveConfig(); }
                    try { var acc = Theme.ParseColor(p.Config.AccentColor, new Vector4(0.12f, 0.55f, 0.96f, 1f)); ImGui.SameLine(); ImGui.ColorButton("acc_prev", acc); } catch { }

                    ImGui.Separator();
                    ImGui.Text("Route Display");
                    int rMode = p.Config.RouteDisplay switch
                    {
                        RouteDisplayMode.Letters => 0,
                        RouteDisplayMode.ShortIds => 1,
                        RouteDisplayMode.Raw => 2,
                        _ => 0
                    };
                    if (ImGui.RadioButton("Letters", rMode == 0)) { p.Config.RouteDisplay = RouteDisplayMode.Letters; p.SaveConfig(); rMode = 0; }
                    ImGui.SameLine();
                    if (ImGui.RadioButton("ShortIds", rMode == 1)) { p.Config.RouteDisplay = RouteDisplayMode.ShortIds; p.SaveConfig(); rMode = 1; }
                    ImGui.SameLine();
                    if (ImGui.RadioButton("Raw", rMode == 2)) { p.Config.RouteDisplay = RouteDisplayMode.Raw; p.SaveConfig(); rMode = 2; }
                    var mapHint = p.Config.SectorMapHint ?? string.Empty;
                    if (ImGui.InputText("MapHint", ref mapHint, 64)) { p.Config.SectorMapHint = mapHint; p.SaveConfig(); }
                }
                ImGui.EndTable();
            }

            var tableHeight = (int)(ImGui.GetTextLineHeightWithSpacing() * 10);
            using (Widgets.Card("ov_table", new Vector2(0, tableHeight)))
            {
                Widgets.SectionHeader("Snapshot");
                p.Ui_DrawSnapshotTable();
            }
        }
    }
}

`

### apps/XIVSubmarinesReturn/src/UI/Widgets.cs
`csharpusing System;
using ImGui = Dalamud.Bindings.ImGui.ImGui;
using Dalamud.Interface.Components;
using Dalamud.Interface;
using ImGuiInputTextFlags = Dalamud.Bindings.ImGui.ImGuiInputTextFlags;
using System.Numerics;

namespace XIVSubmarinesReturn.UI
{
    internal static class Widgets
    {
        public static bool IconButton(FontAwesomeIcon icon, string tooltip)
        {
            bool clicked = false;
            try
            {
                clicked = ImGuiComponents.IconButton(icon);
                if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered()) ImGui.SetTooltip(tooltip);
            }
            catch
            {
                // Fallback: text button
                clicked = ImGui.Button(tooltip);
            }
            return clicked;
        }

        // マスク付き入力（目アイコンで表示/非表示を切替）。値が変更された場合 true を返す。
        public static bool MaskedInput(string label, ref string value, int maxLen, ref bool reveal)
        {
            bool changed = false;
            try
            {
                ImGui.PushID(label);
                // 左側にラベル
                if (!string.IsNullOrEmpty(label)) ImGui.TextUnformatted(label);
                if (!string.IsNullOrEmpty(label)) { ImGui.SameLine(180); }

                // 表示/非表示切替（非表示時はPasswordフラグで隠す）
                if (reveal)
                {
                    changed = ImGui.InputText("##val", ref value, maxLen);
                }
                else
                {
                    changed = ImGui.InputText("##val", ref value, maxLen, ImGuiInputTextFlags.Password);
                }

                // 目アイコン（ツールチップは日本語: 表示/非表示）
                ImGui.SameLine();
                var icon = reveal ? FontAwesomeIcon.EyeSlash : FontAwesomeIcon.Eye;
                if (ImGuiComponents.IconButton(icon)) reveal = !reveal;
                try { if (ImGui.IsItemHovered()) ImGui.SetTooltip(reveal ? "非表示" : "表示"); } catch { }

                ImGui.PopID();
            }
            catch { }
            return changed;
        }

        // Simple card-like container using Child with border
        public readonly struct CardScope : IDisposable
        {
            public void Dispose() { try { ImGui.EndChild(); } catch { } }
        }
        public static CardScope Card(string id, Vector2? size = null)
        {
            try { ImGui.BeginChild(id, size ?? new Vector2(0, 0), true); } catch { }
            return new CardScope();
        }

        public static void SectionHeader(string text)
        {
            try { ImGui.Text(text ?? string.Empty); ImGui.Separator(); } catch { }
        }

        public static bool BeginForm(string id) { try { ImGui.PushID(id); return true; } catch { return false; } }
        public static void EndForm() { try { ImGui.PopID(); } catch { } }
        public static void FormRow(string label, Action draw)
        {
            try
            {
                ImGui.TextUnformatted(label ?? string.Empty);
                ImGui.SameLine(180);
                ImGui.PushItemWidth(-1);
                draw?.Invoke();
                ImGui.PopItemWidth();
            }
            catch { }
        }

        public static void StatusPill(bool on, string onText = "Enabled", string offText = "Disabled")
        {
            try
            {
                var col = on ? new Vector4(0.2f, 0.7f, 0.3f, 1f) : new Vector4(0.5f, 0.5f, 0.5f, 1f);
                ImGui.PushStyleColor(Dalamud.Bindings.ImGui.ImGuiCol.Text, col);
                ImGui.TextUnformatted(on ? onText : offText);
                ImGui.PopStyleColor();
            }
            catch { }
        }

        public static void Badge(string text)
        {
            try { ImGui.TextDisabled(text ?? string.Empty); } catch { }
        }
    }
}

`

### apps/XIVSubmarinesReturn/src/UI/SnapshotTable.cs
`csharpusing System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using ImGui = Dalamud.Bindings.ImGui.ImGui;
using ImGuiTableFlags = Dalamud.Bindings.ImGui.ImGuiTableFlags;
using ImGuiSortDirection = Dalamud.Bindings.ImGui.ImGuiSortDirection;

namespace XIVSubmarinesReturn.UI
{
    internal sealed class SnapshotTable
    {
        private string _filterText = string.Empty;
        private int _sortField = 3; // 0=Name 1=Slot 2=Rank 3=ETA
        private bool _sortAsc = true;

        public void Draw(IReadOnlyList<SubmarineRecord> itemsSource,
            Configuration cfg,
            Func<int, string?> aliasResolver,
            Action<string>? setStatus = null,
            Action? onConfigChanged = null)
        {
            if (itemsSource == null || itemsSource.Count == 0)
            {
                ImGui.TextDisabled("データがありません。取得を試してください。");
                return;
            }

            // init from config (first draw)
            if (string.IsNullOrEmpty(_filterText) && !string.IsNullOrEmpty(cfg.TableFilterText)) _filterText = cfg.TableFilterText;
            if (_sortField == 3 && cfg.TableSortField != 3) _sortField = cfg.TableSortField;
            if (_sortAsc != cfg.TableSortAsc) _sortAsc = cfg.TableSortAsc;

            ImGui.InputText("フィルタ", ref _filterText, 128);
            ImGui.SameLine(); ImGui.TextDisabled("(ヘッダクリックでソート)");
            if (cfg.TableFilterText != _filterText) { cfg.TableFilterText = _filterText; onConfigChanged?.Invoke(); }

            // Build working list (filter only here; sort is applied after reading TableSortSpecs)
            var list = new List<SubmarineRecord>(itemsSource);
            try
            {
                if (!string.IsNullOrWhiteSpace(_filterText))
                {
                    var ft = _filterText.Trim();
                    list = list.FindAll(x => (x.Name?.Contains(ft, StringComparison.OrdinalIgnoreCase) ?? false)
                        || (x.RouteKey?.Contains(ft, StringComparison.OrdinalIgnoreCase) ?? false)
                        || (x.Extra != null && x.Extra.TryGetValue("RouteShort", out var rs) && (rs?.Contains(ft, StringComparison.OrdinalIgnoreCase) ?? false)));
                }
            }
            catch { }

            // Accent header color
            var acc = Theme.ParseColor(cfg.AccentColor, new Vector4(0.12f, 0.55f, 0.96f, 1f));
            ImGui.PushStyleColor(Dalamud.Bindings.ImGui.ImGuiCol.TableHeaderBg, acc);
            ImGui.PushStyleColor(Dalamud.Bindings.ImGui.ImGuiCol.HeaderHovered, acc);
            ImGui.PushStyleColor(Dalamud.Bindings.ImGui.ImGuiCol.HeaderActive, acc);

            if (ImGui.BeginTable("subs", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.Sortable))
            {
                ImGui.TableSetupColumn("スロット");
                ImGui.TableSetupColumn("名前");
                ImGui.TableSetupColumn("ランク");
                ImGui.TableSetupColumn("残り");
                ImGui.TableSetupColumn("ETA/残り");
                ImGui.TableSetupColumn("ルート");
                ImGui.TableHeadersRow();

                // Read header sort specs and sync
                try
                {
                    var specs = ImGui.TableGetSortSpecs();
                    if (specs.SpecsCount > 0)
                    {
                        var spec = specs.Specs[0];
                        int col = spec.ColumnIndex;
                        bool asc = spec.SortDirection != ImGuiSortDirection.Descending;
                        int mapped = _sortField;
                        switch (col)
                        {
                            case 0: mapped = 1; break; // Slot
                            case 1: mapped = 0; break; // Name
                            case 2: mapped = 2; break; // Rank
                            case 3: mapped = 3; break; // Remaining -> ETA
                            case 4: mapped = 3; break; // ETA
                            case 5: mapped = 0; break; // Route ~ Name fallback
                        }
                        if (mapped != _sortField || asc != _sortAsc)
                        {
                            _sortField = mapped; _sortAsc = asc;
                            cfg.TableSortField = _sortField; cfg.TableSortAsc = _sortAsc;
                            onConfigChanged?.Invoke();
                        }
                        specs.SpecsDirty = false;
                    }
                }
                catch { }

                // Apply sort
                try { list.Sort((a, b) => Compare(a, b, _sortField, _sortAsc)); } catch { }

                foreach (var it in list)
                {
                    ImGui.TableNextRow();
                    // Slot
                    ImGui.TableSetColumnIndex(0);
                    ImGui.Text(it.Slot.HasValue ? $"S{it.Slot.Value}" : string.Empty);
                    // Name
                    ImGui.TableSetColumnIndex(1);
                    ImGui.Text(it.Name ?? string.Empty);
                    // Rank
                    ImGui.TableSetColumnIndex(2);
                    ImGui.Text(it.Rank?.ToString() ?? string.Empty);
                    // Remaining minutes (numeric) with accent if soon
                    ImGui.TableSetColumnIndex(3);
                    try
                    {
                        var rem = (it.Extra != null && it.Extra.TryGetValue("RemainingText", out var r)) ? r : string.Empty;
                        bool highlight = false;
                        int minsLeft = int.MaxValue;
                        try
                        {
                            if (it.EtaUnix.HasValue && it.EtaUnix.Value > 0)
                            {
                                // UTC基準で残り分を算出（表示は別途ローカルにて）
                                var eta = DateTimeOffset.FromUnixTimeSeconds(it.EtaUnix.Value);
                                minsLeft = (int)Math.Round((eta - DateTimeOffset.UtcNow).TotalMinutes);
                            }
                            else if (!string.IsNullOrWhiteSpace(rem))
                            {
                                var m = Regex.Match(rem, @"(?:(?<h>\d+)\s*時間)?\s*(?<m>\d+)\s*分");
                                if (m.Success)
                                {
                                    int h = m.Groups["h"].Success ? int.Parse(m.Groups["h"].Value) : 0;
                                    int mm = m.Groups["m"].Success ? int.Parse(m.Groups["m"].Value) : 0;
                                    minsLeft = Math.Max(0, h * 60 + mm);
                                }
                            }
                            if (minsLeft <= cfg.HighlightSoonMins) highlight = true;
                        }
                        catch { }
                        if (highlight) { ImGui.PushStyleColor(Dalamud.Bindings.ImGui.ImGuiCol.Text, acc); }
                        ImGui.Text(minsLeft == int.MaxValue ? string.Empty : minsLeft.ToString());
                        if (highlight) ImGui.PopStyleColor();
                    }
                    catch { ImGui.Text(string.Empty); }

                    // ETA/Remaining (with highlight)
                    ImGui.TableSetColumnIndex(4);
                    try
                    {
                        var etaLoc = (it.Extra != null && it.Extra.TryGetValue("EtaLocal", out var t)) ? t : string.Empty;
                        var rem = (it.Extra != null && it.Extra.TryGetValue("RemainingText", out var r)) ? r : string.Empty;
                        var txt = string.IsNullOrWhiteSpace(rem) ? (etaLoc ?? string.Empty) : $"{etaLoc} / {rem}";
                        bool highlight = false;
                        try
                        {
                            int minsLeft = int.MaxValue;
                            if (it.EtaUnix.HasValue && it.EtaUnix.Value > 0)
                            {
                                var eta = DateTimeOffset.FromUnixTimeSeconds(it.EtaUnix.Value);
                                minsLeft = (int)Math.Round((eta - DateTimeOffset.Now).TotalMinutes);
                            }
                            else if (!string.IsNullOrWhiteSpace(rem))
                            {
                                var m = Regex.Match(rem, @"(?:(?<h>\d+)\s*時間)?\s*(?<m>\d+)\s*分");
                                if (m.Success)
                                {
                                    int h = m.Groups["h"].Success ? int.Parse(m.Groups["h"].Value) : 0;
                                    int mm = m.Groups["m"].Success ? int.Parse(m.Groups["m"].Value) : 0;
                                    minsLeft = Math.Max(0, h * 60 + mm);
                                }
                            }
                            if (minsLeft <= cfg.HighlightSoonMins) highlight = true;
                        }
                        catch { }
                        if (highlight) { ImGui.PushStyleColor(Dalamud.Bindings.ImGui.ImGuiCol.Text, acc); }
                        ImGui.Text(txt ?? string.Empty);
                        if (highlight) ImGui.PopStyleColor();
                    }
                    catch { ImGui.Text(string.Empty); }

                    // Route (click to copy)
                    ImGui.TableSetColumnIndex(5);
                    try
                    {
                        var show = BuildRouteDisplay(it, cfg, aliasResolver);
                        if (ImGui.Selectable(show ?? string.Empty, false))
                        {
                            try { ImGui.SetClipboardText(show ?? string.Empty); } catch { }
                            setStatus?.Invoke("ルートをコピーしました");
                        }
                    }
                    catch { ImGui.Text(it.RouteKey ?? string.Empty); }
                }

                ImGui.EndTable();
            }
            ImGui.PopStyleColor(3);
        }

        private static int Compare(SubmarineRecord? a, SubmarineRecord? b, int field, bool asc)
        {
            try
            {
                int s = 0;
                switch (field)
                {
                    case 0: s = string.Compare(a?.Name, b?.Name, StringComparison.OrdinalIgnoreCase); break;
                    case 1: s = Nullable.Compare(a?.Slot, b?.Slot); break;
                    case 2: s = Nullable.Compare(a?.Rank, b?.Rank); break;
                    case 3:
                        long ea = a?.EtaUnix ?? long.MinValue;
                        long eb = b?.EtaUnix ?? long.MinValue;
                        s = ea.CompareTo(eb);
                        break;
                }
                return asc ? s : -s;
            }
            catch { return 0; }
        }

        private static string BuildRouteDisplay(SubmarineRecord it, Configuration cfg, Func<int, string?> aliasResolver)
        {
            var baseRoute = (it.Extra != null && it.Extra.TryGetValue("RouteShort", out var rs)) ? rs : it.RouteKey;
            if (string.IsNullOrWhiteSpace(baseRoute)) return string.Empty;

            if (cfg.RouteDisplay == RouteDisplayMode.Raw)
                return it.RouteKey ?? baseRoute ?? string.Empty;

            var nums = new List<int>();
            foreach (Match m in Regex.Matches(baseRoute ?? string.Empty, @"P?(\d+)"))
                if (int.TryParse(m.Groups[1].Value, out var v)) nums.Add(v);
            if (nums.Count == 0) return baseRoute ?? string.Empty;

            switch (cfg.RouteDisplay)
            {
                case RouteDisplayMode.ShortIds:
                    return string.Join('>', nums.Select(n => $"P{n}"));
                case RouteDisplayMode.Letters:
                default:
                    var parts = new List<string>(nums.Count);
                    foreach (var n in nums)
                    {
                        string? letter = null;
                        try { letter = aliasResolver?.Invoke(n); } catch { }
                        if (string.IsNullOrWhiteSpace(letter) && cfg.RouteNames != null && cfg.RouteNames.TryGetValue((byte)n, out var nm) && !string.IsNullOrWhiteSpace(nm))
                            letter = nm;
                        parts.Add(string.IsNullOrWhiteSpace(letter) ? $"P{n}" : letter!);
                    }
                    var text = string.Join('>', parts);
                    return parts.All(p => p.StartsWith("P")) ? (baseRoute ?? text) : text;
            }
        }
    }
}
`

## 備考/今後
- DataPointers が取得できない環境向けに連続キャストのフォールバックは最小限で温存。
- 5隻以上の想定外データは現状非対応（4隻固定）。
- 仕様/構造変更時はログ（bytes/offset/stride）で追跡可能。
