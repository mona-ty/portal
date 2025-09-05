using System;
using System.IO;
using Advanced_Combat_Tracker;
using System.Text;
using System.Threading;

namespace FF14SubmarinesAct
{
    public sealed class NetDump : IDisposable
    {
        private StreamWriter _writer;
        public string CurrentFilePath { get; private set; } = string.Empty;
        public bool IsRunning => _writer != null;

        private string DumpDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ff14_submarines_act", "netdump");

        // Tail latest Network_*.log for environments where OnLogLineRead does not include 00| lines
        private FileSystemWatcher _fsw;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> _lastPosMap = new System.Collections.Concurrent.ConcurrentDictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new object();
        private Settings _settings;

        public void Start(Settings settings = null)
        {
            if (IsRunning) return;
            _settings = settings;
            Directory.CreateDirectory(DumpDir);
            CurrentFilePath = Path.Combine(DumpDir, DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log");
            _writer = new StreamWriter(File.Open(CurrentFilePath, FileMode.Create, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
            _writer.WriteLine($"# FF14 Submarines NetDump started at {DateTime.Now:O}");
            ActGlobals.oFormActMain.OnLogLineRead += OnLogLineRead;

            TryStartTailer();
        }

        public void Stop()
        {
            if (!IsRunning) return;
            ActGlobals.oFormActMain.OnLogLineRead -= OnLogLineRead;
            _writer.WriteLine($"# stopped at {DateTime.Now:O}");
            _writer.Dispose();
            _writer = null;

            StopTailer();
        }

        private void OnLogLineRead(bool isImport, LogLineEventArgs logInfo)
        {
            try
            {
                if (_writer == null) return;
                // Write raw ACT log line; it already contains code like 0039/0038 etc when applicable
                _writer.WriteLine(logInfo.logLine);
            }
            catch { }
        }

        private void TryStartTailer()
        {
            try
            {
                var logsRoot = !string.IsNullOrEmpty(_settings?.LogsFolder)
                    ? _settings.LogsFolder
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Advanced Combat Tracker", "FFXIVLogs");
                if (!Directory.Exists(logsRoot)) return;

                // Prime last positions for existing Network_*.log files
                foreach (var fi in new DirectoryInfo(logsRoot).GetFiles("Network_*.log"))
                {
                    _lastPosMap[fi.FullName] = fi.Length; // start from end
                }

                _fsw = new FileSystemWatcher(logsRoot, "Network_*.log");
                _fsw.NotifyFilter = NotifyFilters.Size | NotifyFilters.LastWrite | NotifyFilters.FileName;
                _fsw.Changed += (s, e) => TailFile(e.FullPath);
                _fsw.Created += (s, e) => { _lastPosMap[e.FullPath] = 0; TailFile(e.FullPath); };
                _fsw.Renamed += (s, e) => { _lastPosMap[e.FullPath] = 0; };
                _fsw.EnableRaisingEvents = true;
            }
            catch { }
        }

        private FileInfo GetLatestNetworkLog(string root)
        {
            var dir = new DirectoryInfo(root);
            FileInfo latest = null;
            foreach (var fi in dir.GetFiles("Network_*.log"))
            {
                if (latest == null || fi.LastWriteTimeUtc > latest.LastWriteTimeUtc)
                    latest = fi;
            }
            return latest;
        }

        private void TailFile(string path)
        {
            lock (_lock)
            {
                try
                {
                    if (_writer == null) return;
                    if (!File.Exists(path)) return;
                    long start = _lastPosMap.GetOrAdd(path, 0);
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        if (start > fs.Length) start = 0; // rotated
                        fs.Seek(start, SeekOrigin.Begin);
                        using (var sr = new StreamReader(fs, Encoding.UTF8, true, 4096, true))
                        {
                            string line;
                            while ((line = sr.ReadLine()) != null)
                            {
                                _writer.WriteLine(line);
                            }
                        }
                        _lastPosMap[path] = fs.Position;
                    }
                }
                catch { }
            }
        }

        private void StopTailer()
        {
            try
            {
                if (_fsw != null)
                {
                    _fsw.EnableRaisingEvents = false;
                    _fsw.Dispose();
                    _fsw = null;
                }
            }
            catch { }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
