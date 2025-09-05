using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace FF14SubmarinesAct.Ui
{
    public partial class SettingsControl : UserControl
    {
        private Settings _settings = new Settings();
        public event Action DumpStartRequested;
        public event Action DumpStopRequested;
        public event Action OverridesSaved;

        public SettingsControl()
        {
            InitializeComponent();
        }

        public void Bind(Settings settings)
        {
            _settings = settings ?? new Settings();
            _txtAct.Text = _settings.ActFolder ?? string.Empty;
            _txtFfxiv.Text = _settings.FfxivPluginPath ?? string.Empty;
            _txtLogs.Text = _settings.LogsFolder ?? string.Empty;
            _numDefaultMinutes.Value = Math.Max(_numDefaultMinutes.Minimum, Math.Min(_numDefaultMinutes.Maximum, _settings.DefaultDurationMinutes));

            // populate overrides grid
            _gridOverrides.Rows.Clear();
            if (_settings.DurationOverrides != null)
            {
                foreach (var o in _settings.DurationOverrides)
                {
                    _gridOverrides.Rows.Add(o.Key, o.Minutes);
                }
            }
        }

        public void Append(string line)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<string>(Append), line);
                return;
            }
            _textLog.AppendText(line + Environment.NewLine);
        }

        private void OnBrowseAct()
        {
            using (var d = new FolderBrowserDialog())
            {
                d.Description = "Advanced Combat Tracker のフォルダを選択";
                d.SelectedPath = _txtAct.Text;
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    _txtAct.Text = d.SelectedPath;
                    _settings.ActFolder = d.SelectedPath;
                    SettingsStore.Save(_settings);
                }
            }
        }

        private void OnBrowseFfxiv()
        {
            using (var d = new OpenFileDialog())
            {
                d.Filter = "FFXIV_ACT_Plugin.dll|FFXIV_ACT_Plugin.dll|DLL (*.dll)|*.dll|すべてのファイル (*.*)|*.*";
                d.FileName = _txtFfxiv.Text;
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    _txtFfxiv.Text = d.FileName;
                    _settings.FfxivPluginPath = d.FileName;
                    SettingsStore.Save(_settings);
                }
            }
        }

        private void OnBrowseLogs()
        {
            using (var d = new FolderBrowserDialog())
            {
                d.Description = "FFXIVLogs フォルダを選択 (例: %AppData%/Advanced Combat Tracker/FFXIVLogs)";
                d.SelectedPath = _txtLogs.Text;
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    _txtLogs.Text = d.SelectedPath;
                    _settings.LogsFolder = d.SelectedPath;
                    SettingsStore.Save(_settings);
                }
            }
        }

        public void SetDumpFile(string path)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<string>(SetDumpFile), path);
                return;
            }
            _lblDumpFile.Text = string.IsNullOrEmpty(path) ? "" : $"保存先: {path}";
        }

        private void OnDumpStart() => DumpStartRequested?.Invoke();
        private void OnDumpStop() => DumpStopRequested?.Invoke();

        private void OnDefaultMinutesChanged()
        {
            _settings.DefaultDurationMinutes = (int)_numDefaultMinutes.Value;
            SettingsStore.Save(_settings);
        }

        private void OnAddOverride()
        {
            _gridOverrides.Rows.Add("", 60);
        }

        private void OnRemoveOverride()
        {
            foreach (DataGridViewRow r in _gridOverrides.SelectedRows)
            {
                if (!r.IsNewRow) _gridOverrides.Rows.Remove(r);
            }
        }

        private void OnSaveOverrides()
        {
            var list = new System.Collections.Generic.List<DurationOverride>();
            foreach (DataGridViewRow r in _gridOverrides.Rows)
            {
                if (r.IsNewRow) continue;
                var key = (r.Cells[0].Value ?? "").ToString().Trim();
                var minsStr = (r.Cells[1].Value ?? "").ToString().Trim();
                if (string.IsNullOrEmpty(key)) continue;
                int mins;
                if (!int.TryParse(minsStr, out mins)) continue;
                if (mins <= 0) continue;
                list.Add(new DurationOverride { Key = key, Minutes = mins });
            }
            _settings.DurationOverrides = list;
            SettingsStore.Save(_settings);
            OverridesSaved?.Invoke();
        }

        public void UpdateList(System.Collections.Generic.IEnumerable<SubmarineInfo> items)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<System.Collections.Generic.IEnumerable<SubmarineInfo>>(UpdateList), items);
                return;
            }
            _list.BeginUpdate();
            _list.Items.Clear();
            var now = DateTime.Now;
            foreach (var s in items)
            {
                var remain = (int)Math.Max(0, (s.Eta - now).TotalMinutes);
                var li = new ListViewItem(new[]
                {
                    s.Name,
                    s.DepartedAt.ToString("yyyy-MM-dd HH:mm"),
                    s.Eta.ToString("yyyy-MM-dd HH:mm"),
                    remain.ToString()
                });
                _list.Items.Add(li);
            }
            _list.EndUpdate();
        }
    }
}
