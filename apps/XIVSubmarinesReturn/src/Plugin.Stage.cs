using System;
using System.Collections.Generic;
using System.IO;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace XIVSubmarinesReturn;

public sealed partial class Plugin
{
    private unsafe void CmdDumpStage()
    {
        var candidates = new List<string>
        {
            "SelectString","SelectIconString","SelectYesno","ContextMenu","ContextIconMenu","Balloon","Tooltip",
            "CompanyCraftSubmersible","CompanyCraftSubmersibleList","CompanyCraftShipList","CompanyCraftList","CompanyCraft",
            "CompanyCraftSelect","CompanyCraftProcess","CompanyCraftSchedule","CompanyCraftResult",
            "SubmarineExploration","SubmarineExplorationResult","SubmarineExplorationMap","SubmarineExplorationMapSelect",
            "SubmersibleExploration","SubmersibleExplorationResult","SubmersibleExplorationMap","SubmersibleExplorationMapSelect",
            "FreeCompanyWorkshopSubmersible",
        };

        var lines = new List<string>();
        foreach (var n in candidates)
        {
            for (int idx = 0; idx < 32; idx++)
            {
                var unit = ResolveAddonPtr(n, idx);
                if (unit == null) continue;
                try
                {
                    bool vis = unit->IsVisible;
                    lines.Add($"## {n} (idx={idx}, visible={vis})");
                    // NodeList 直下
                    var cnt = unit->UldManager.NodeListCount;
                    for (var j = 0; j < cnt; j++)
                    {
                        var node = unit->UldManager.NodeList[j];
                        if (node == null) continue;
                        if (node->Type == NodeType.Text)
                        {
                            var t = ((AtkTextNode*)node)->NodeText.ToString();
                            if (!string.IsNullOrWhiteSpace(t)) lines.Add("  " + t.Trim());
                        }
                    }

                    // DFS で深い階層まで取得（安全ガード付き）
                    var visited = new HashSet<nint>();
                    void Dfs(AtkResNode* rn, int depth)
                    {
                        if (rn == null || depth > 2048) return;
                        var key = (nint)rn; if (!visited.Add(key)) return;
                        if (rn->Type == NodeType.Text)
                        {
                            try
                            {
                                var t = ((AtkTextNode*)rn)->NodeText.ToString();
                                if (!string.IsNullOrWhiteSpace(t)) lines.Add("  " + t.Trim());
                            }
                            catch { }
                        }
                        else if (rn->Type == NodeType.Component)
                        {
                            try
                            {
                                var comp = ((AtkComponentNode*)rn)->Component;
                                if (comp != null)
                                {
                                    var root = comp->UldManager.RootNode; if (root != null) Dfs(root, depth + 1);
                                    var c2 = comp->UldManager.NodeListCount;
                                    for (var k = 0; k < c2; k++)
                                    {
                                        var cn = comp->UldManager.NodeList[k];
                                        if (cn != null) Dfs(cn, depth + 1);
                                    }
                                }
                            }
                            catch { }
                        }
                        if (rn->ChildNode != null) Dfs(rn->ChildNode, depth + 1);
                        if (rn->NextSiblingNode != null) Dfs(rn->NextSiblingNode, depth);
                    }
                    try { if (unit->RootNode != null) Dfs(unit->RootNode, 0); } catch { }
                }
                catch { }
            }
        }

        if (lines.Count == 0)
        {
            _chat.Print("[Submarines] dumpstage: no lines found");
            return;
        }

        try
        {
            var dir = Path.GetDirectoryName(BridgeWriter.CurrentFilePath()) ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var path = Path.Combine(dir, "stage_dump.txt");
            File.WriteAllLines(path, lines);
            _chat.Print($"[Submarines] wrote stage dump: {path} (lines: {lines.Count})");
        }
        catch { }
    }

    private unsafe AtkUnitBase* ResolveAddonPtr(string name, int idx)
    {
        try { return ToPtr(_gameGui.GetAddonByName(name, idx)); } catch { return null; }
    }

    // 全アドオン網羅走査（RaptureAtkUnitManager 経由）
    private unsafe void CmdDumpStageAll()
    {
        try
        {
            // 互換性のため、全列挙が利用できない環境では候補走査にフォールバック
            _chat.Print("[Submarines] dumpstageall: fallback to candidate scan (compat)");
            CmdDumpStage();
        }
        catch { }
    }
}
