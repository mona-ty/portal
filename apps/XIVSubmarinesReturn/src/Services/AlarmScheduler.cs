using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XIVSubmarinesReturn;

namespace XIVSubmarinesReturn.Services
{
    public interface IAlarmScheduler
    {
        void UpdateSnapshot(SubmarineSnapshot snap);
        void Tick(DateTimeOffset now);
    }
}

    public sealed class AlarmScheduler : XIVSubmarinesReturn.Services.IAlarmScheduler
    {
        private readonly XIVSubmarinesReturn.Configuration _cfg;
        private readonly Dalamud.Plugin.Services.IChatGui _chat;
        private readonly Dalamud.Plugin.Services.IToastGui _toast;
        private readonly Dalamud.Plugin.Services.IPluginLog _log;
        private readonly XIVSubmarinesReturn.Services.IDiscordNotifier _discord;
        private readonly XIVSubmarinesReturn.Services.INotionClient? _notion;

        private readonly object _gate = new();
        private XIVSubmarinesReturn.SubmarineSnapshot? _current;
        private readonly HashSet<string> _fired = new(StringComparer.Ordinal);
        private string _lastSnapshotKey = string.Empty;
        private DateTimeOffset _lastDiscordSnapshotUtc = DateTimeOffset.MinValue;
        private readonly Dictionary<string, int> _prevMins = new(StringComparer.Ordinal);

        public AlarmScheduler(XIVSubmarinesReturn.Configuration cfg,
            Dalamud.Plugin.Services.IChatGui chat,
            Dalamud.Plugin.Services.IToastGui toast,
            Dalamud.Plugin.Services.IPluginLog log,
            XIVSubmarinesReturn.Services.IDiscordNotifier discord,
            XIVSubmarinesReturn.Services.INotionClient? notion = null)
        {
            _cfg = cfg; _chat = chat; _toast = toast; _log = log; _discord = discord;
            _notion = notion;
        }

        public void UpdateSnapshot(XIVSubmarinesReturn.SubmarineSnapshot snap)
        {
            try
            {
                // Enrich identity from active profile if missing
                try
                {
                    if (string.IsNullOrWhiteSpace(snap.Character) || string.IsNullOrWhiteSpace(snap.FreeCompany))
                    {
                        var key = _cfg.ActiveContentId;
                        var prof = (_cfg.Profiles ?? new List<XIVSubmarinesReturn.CharacterProfile>()).FirstOrDefault(p => key.HasValue && p.ContentId == key.Value) 
                                   ?? (_cfg.Profiles?.FirstOrDefault());
                        if (prof != null)
                        {
                            if (string.IsNullOrWhiteSpace(snap.Character)) snap.Character = prof.CharacterName;
                            if (string.IsNullOrWhiteSpace(snap.FreeCompany)) snap.FreeCompany = prof.FreeCompanyName;
                        }
                    }
                }
                catch { }

                string newKey = ComputeSnapshotKey(snap);
                bool changed = false;
                lock (_gate)
                {
                    _current = snap;
                    if (!string.Equals(_lastSnapshotKey, newKey, StringComparison.Ordinal))
                    {
                        _lastSnapshotKey = newKey;
                        changed = true;
                    }
                }
                // Discord snapshot通知（変更時のみ、非同期）
                var now = DateTimeOffset.UtcNow;
                var minIv = TimeSpan.FromMinutes(Math.Max(0, _cfg.DiscordMinIntervalMinutes));
                var intervalOk = (minIv <= TimeSpan.Zero) || (now - _lastDiscordSnapshotUtc >= minIv);
                if (changed && intervalOk)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _discord.NotifySnapshotAsync(snap, _cfg.DiscordLatestOnly).ConfigureAwait(false);
                        }
                        catch (Exception ex) { _log.Warning(ex, "Discord snapshot task failed"); }
                    });
                    _lastDiscordSnapshotUtc = now;
                }

                // Notion upsert（非同期）
                _ = Task.Run(async () =>
                {
                    try { if (_notion != null) await _notion.UpsertSnapshotAsync(snap, _cfg.NotionLatestOnly).ConfigureAwait(false); }
                    catch (Exception ex) { _log.Warning(ex, "Notion upsert task failed"); }
                });
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "UpdateSnapshot failed");
            }
        }

        public void Tick(DateTimeOffset now)
        {
            try
            {
                List<SubmarineRecord>? items;
                lock (_gate)
                {
                    items = _current?.Items?.ToList();
                }
                if (items == null || items.Count == 0) return;
                var leads = _cfg.AlarmLeadMinutes ?? new List<int>();
                if (!_cfg.GameAlarmEnabled || leads.Count == 0) return;

                foreach (var it in items)
                {
                    if (!it.EtaUnix.HasValue || it.EtaUnix.Value <= 0) continue;
                    var eta = DateTimeOffset.FromUnixTimeSeconds(it.EtaUnix.Value);
                    var mins = (int)Math.Round((eta - now).TotalMinutes);
                    
var idKey = $"{(it.Slot ?? 0)}-{(it.EtaUnix ?? 0)}";
                    
var hadPrev = _prevMins.TryGetValue(idKey, out var prevMins);
                    foreach (var lead in leads)
                    {
                        if ((hadPrev && prevMins > lead && mins <= lead) || (!hadPrev && mins == lead))
                        {
                            var key = $"{it.Slot ?? 0}-{it.EtaUnix ?? 0}-{lead}";
                            if (_fired.Add(key))
                            {
                                // in-game chat + toast(SE)
                                try
                                {
                                    var etaLoc = it.Extra != null && it.Extra.TryGetValue("EtaLocal", out var t) ? t : eta.ToLocalTime().ToString("HH:mm");
                                    var rt = it.Extra != null && it.Extra.TryGetValue("RouteShort", out var r) ? r : it.RouteKey;
                                    var msg = $"[Sub] {(it.Slot.HasValue ? $"S{it.Slot.Value} " : string.Empty)}{it.Name} 残り{lead}分 (ETA {etaLoc}) {rt} <se.1>"; // 半角スペースは <se.1> の直前に挿入済み
                                    _chat.Print(msg);
                                    try { _toast.ShowQuest(msg, new Dalamud.Game.Gui.Toast.QuestToastOptions()); } catch { try { _toast.ShowError(msg); } catch { } }
                                }
                                catch { }

                                // discord (optional)
                                var snapCopy = _current; // capture for closure
                                _ = Task.Run(async () =>
                                {
                                    try { await _discord.NotifyAlarmAsync(it, lead, snapCopy).ConfigureAwait(false); } catch (Exception ex) { _log.Warning(ex, "Discord alarm task failed"); }
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Alarm Tick failed");
            }
        }

        private static string ComputeSnapshotKey(XIVSubmarinesReturn.SubmarineSnapshot snap)
    {
        try
        {
            var items = (snap.Items ?? new List<SubmarineRecord>())
                .Select(x => new { x.Name, x.Slot, x.Rank, x.RouteKey, x.EtaUnix })
                .OrderBy(x => x.Slot ?? 0).ThenBy(x => x.Name ?? string.Empty, StringComparer.Ordinal)
                .ToArray();
            var obj = new { Items = items };
            return System.Text.Json.JsonSerializer.Serialize(obj);
        }
        catch { return string.Empty; }
    }
    }
