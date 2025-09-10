using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace XIVSubmarinesReturn.Services
{
    public interface IAlarmScheduler
    {
        void UpdateSnapshot(SubmarineSnapshot snap);
        void Tick(DateTimeOffset now);
    }

    public sealed class AlarmScheduler : IAlarmScheduler
    {
        private readonly Configuration _cfg;
        private readonly Dalamud.Plugin.Services.IChatGui _chat;
        private readonly Dalamud.Plugin.Services.IToastGui _toast;
        private readonly Dalamud.Plugin.Services.IPluginLog _log;
        private readonly IDiscordNotifier _discord;
        private readonly INotionClient? _notion;

        private readonly object _gate = new();
        private SubmarineSnapshot? _current;
        private readonly HashSet<string> _fired = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _prevMins = new(StringComparer.Ordinal);

        public AlarmScheduler(Configuration cfg,
            Dalamud.Plugin.Services.IChatGui chat,
            Dalamud.Plugin.Services.IToastGui toast,
            Dalamud.Plugin.Services.IPluginLog log,
            IDiscordNotifier discord,
            INotionClient? notion = null)
        {
            _cfg = cfg; _chat = chat; _toast = toast; _log = log; _discord = discord;
            _notion = notion;
        }

        public void UpdateSnapshot(SubmarineSnapshot snap)
        {
            try
            {
                lock (_gate)
                {
                    _current = snap;
                }

                // Discord snapshot通知（非同期）
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _discord.NotifySnapshotAsync(snap, _cfg.DiscordLatestOnly).ConfigureAwait(false);
                    }
                    catch (Exception ex) { _log.Warning(ex, "Discord snapshot task failed"); }
                });

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
                                _ = Task.Run(async () =>
                                {
                                    try { await _discord.NotifyAlarmAsync(it, lead).ConfigureAwait(false); } catch (Exception ex) { _log.Warning(ex, "Discord alarm task failed"); }
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

    }
}
