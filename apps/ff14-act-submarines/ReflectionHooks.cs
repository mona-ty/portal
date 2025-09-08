using System;
using System.Linq;
using System.Reflection;
using Advanced_Combat_Tracker;

namespace FF14SubmarinesAct
{
    public static class ReflectionHooks
    {
        public static void DiscoverFfxivActPlugin(Ui.SettingsControl ui)
        {
            try
            {
                var asm = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name.IndexOf("FFXIV_ACT_Plugin", StringComparison.OrdinalIgnoreCase) >= 0);
                if (asm == null)
                {
                    ui.Append("[Info] FFXIV_ACT_Plugin assembly not found in AppDomain.");
                }
                else
                {
                    ui.Append($"[Info] Found assembly: {asm.FullName}");

                    foreach (var t in asm.GetTypes())
                    {
                        var events = t.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                        if (events.Length > 0)
                        {
                            var names = string.Join(", ", events.Select(e => e.Name));
                            ui.Append($"[Info] Type {t.FullName} events: {names}");
                        }
                    }
                }

                // Also inspect the actual loaded plugin instance from ACT
                var ap = ActGlobals.oFormActMain?.ActPlugins?.FirstOrDefault(p =>
                    (p?.dllFile?.FullName?.IndexOf("FFXIV_ACT_Plugin", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                    (p?.lblPluginTitle?.Text?.IndexOf("FFXIV", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0);
                if (ap?.pluginObj != null)
                {
                    var inst = ap.pluginObj;
                    var t = inst.GetType();
                    ui.Append($"[Info] Plugin instance: {t.FullName}");
                    foreach (var ev in t.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
                    {
                        var eh = ev.EventHandlerType;
                        var sig = DescribeDelegate(eh);
                        ui.Append($"[Info] Event {ev.Name}: {sig}");
                    }

                    // Heuristic: look for properties likely holding network/data subscriptions
                    foreach (var prop in t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
                    {
                        var pn = prop.Name;
                        if (pn.IndexOf("Network", StringComparison.OrdinalIgnoreCase) >= 0 || pn.IndexOf("Subscription", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            ui.Append($"[Info] Property {pn}: {prop.PropertyType.FullName}");
                            var pv = SafeGet(() => prop.GetValue(inst));
                            if (pv != null)
                            {
                                foreach (var subEv in pv.GetType().GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
                                {
                                    ui.Append($"[Info]  -> {pv.GetType().FullName}.{subEv.Name}: {DescribeDelegate(subEv.EventHandlerType)}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ui.Append($"[Error] Discover failed: {ex.Message}");
            }
        }

        private static string DescribeDelegate(Type delType)
        {
            try
            {
                if (delType == null) return "(null)";
                var invoke = delType.GetMethod("Invoke");
                if (invoke == null) return delType.FullName;
                var pars = invoke.GetParameters();
                var ps = string.Join(", ", pars.Select(p => p.ParameterType.FullName + " " + p.Name));
                return $"delegate {delType.FullName}({ps})";
            }
            catch { return delType?.FullName ?? "(unknown)"; }
        }

        private static T SafeGet<T>(Func<T> fn)
        {
            try { return fn(); }
            catch { return default; }
        }
    }
}
