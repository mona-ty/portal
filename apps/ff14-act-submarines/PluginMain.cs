using System;
using System.Windows.Forms;
using Advanced_Combat_Tracker;

namespace FF14SubmarinesAct
{
    public class PluginMain : IActPluginV1
    {
        private TabPage _tabPage;
        private Ui.SettingsControl _ui;
        private NetDump _dump = new NetDump();
        private SubmarineTracker _tracker;

        public void InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText)
        {
            _tabPage = pluginScreenSpace;
            _tabPage.Text = "FF14 Submarines";
            _ui = new Ui.SettingsControl();
            _ui.Dock = DockStyle.Fill;
            _tabPage.Controls.Add(_ui);

            var settings = SettingsStore.Load();
            _ui.Bind(settings);
            _tracker = new SubmarineTracker(
                () => SettingsStore.Load().DefaultDurationMinutes,
                (name) => SettingsStore.Load().TryGetOverrideMinutes(name)
            );

            // Subscribe log lines
            ActGlobals.oFormActMain.OnLogLineRead += OnLogLineRead;

            pluginStatusText.Text = "Initialized";
            _ui.Append("Plugin initialized");
            if (!string.IsNullOrEmpty(settings.ActFolder))
                _ui.Append($"ACTフォルダ: {settings.ActFolder}");
            if (!string.IsNullOrEmpty(settings.FfxivPluginPath))
                _ui.Append($"FFXIV_ACT_Plugin: {settings.FfxivPluginPath}");

            // Try to discover FFXIV_ACT_Plugin events (for later direct network hook)
            ReflectionHooks.DiscoverFfxivActPlugin(_ui);

            // Wire dump controls
            _ui.DumpStartRequested += () =>
            {
                try
                {
                    _dump.Start(settings);
                    _ui.SetDumpFile(_dump.CurrentFilePath);
                    _ui.Append("ネットワークダンプを開始しました。潜水艦一覧を一度開いてください。");
                }
                catch (Exception ex)
                {
                    _ui.Append($"[Error] ダンプ開始失敗: {ex.Message}");
                }
            };
            _ui.DumpStopRequested += () =>
            {
                try
                {
                    _dump.Stop();
                    _ui.Append("ネットワークダンプを停止しました。");
                }
                catch (Exception ex)
                {
                    _ui.Append($"[Error] ダンプ停止失敗: {ex.Message}");
                }
            };

            _ui.OverridesSaved += () =>
            {
                try
                {
                    var s2 = SettingsStore.Load();
                    _tracker.RefreshAll(name => (int)(s2.TryGetOverrideMinutes(name) ?? s2.DefaultDurationMinutes));
                    _ui.UpdateList(_tracker.Current);
                }
                catch (Exception ex)
                {
                    _ui.Append($"[Error] 再計算失敗: {ex.Message}");
                }
            };
        }

        public void DeInitPlugin()
        {
            ActGlobals.oFormActMain.OnLogLineRead -= OnLogLineRead;
            _dump.Dispose();
            if (_tabPage != null)
            {
                _tabPage.Controls.Clear();
            }
        }

        private void OnLogLineRead(bool isImport, LogLineEventArgs logInfo)
        {
            try
            {
                var line = logInfo.logLine;
                if (LogParser.TryParse(line, out var ev))
                {
                    _ui.Append($"[{ev.Timestamp:HH:mm:ss}] {ev.Kind}: {ev.Name}");
                    _tracker.Apply(ev);
                    _ui.UpdateList(_tracker.Current);
                }
            }
            catch (Exception ex)
            {
                _ui.Append($"[Error] {ex.Message}");
            }
        }
    }
}
