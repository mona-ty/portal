using System;
using System.IO;
using System.Text.Json;

namespace XIVSubmarinesReturn
{
    public static class BridgeWriter
    {
        private static readonly string NewAppFolder = "XIVSubmarinesReturn";
        private static readonly string LegacyAppFolder1 = "ff14_submarines_act"; // 旧ACT連携名
        private static readonly string LegacyAppFolder2 = "14SubmarinesReturn";   // 途中移行名

        private static string BaseDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), NewAppFolder, "bridge");
        private static string FilePath => Path.Combine(BaseDir, "submarines.json");

        private static string LegacyDir1 => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), LegacyAppFolder1, "bridge");
        private static string LegacyDir2 => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), LegacyAppFolder2, "bridge");
        private static string LegacyFile1 => Path.Combine(LegacyDir1, "submarines.json");
        private static string LegacyFile2 => Path.Combine(LegacyDir2, "submarines.json");

        public static void Write(SubmarineSnapshot snapshot)
        {
            EnsureMigrated();

            var opts = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(snapshot, opts);

            // 新パスへ原本を書き出し
            WriteAtomic(FilePath, json);

            // 互換: 旧パスにもミラー（ツール移行期間のため）
            TryWriteLegacy(LegacyFile1, json);
            TryWriteLegacy(LegacyFile2, json);
        }

        public static bool WriteIfChanged(SubmarineSnapshot snapshot)
        {
            try
            {
                EnsureMigrated();
                var newNorm = NormalizeSnapshotJson(snapshot);
                string? oldNorm = null;
                if (File.Exists(FilePath))
                {
                    try
                    {
                        var prev = File.ReadAllText(FilePath);
                        var prevSnap = System.Text.Json.JsonSerializer.Deserialize<SubmarineSnapshot>(prev) ?? new SubmarineSnapshot();
                        oldNorm = NormalizeSnapshotJson(prevSnap);
                    }
                    catch { }
                }
                if (!string.Equals(newNorm, oldNorm, StringComparison.Ordinal))
                {
                    Write(snapshot);
                    return true;
                }
                return false;
            }
            catch { try { Write(snapshot); } catch { } return true; }
        }

        private static string NormalizeSnapshotJson(SubmarineSnapshot s)
        {
            try
            {
                var lite = new
                {
                    s.SchemaVersion,
                    s.Source,
                    Items = (s.Items ?? new System.Collections.Generic.List<SubmarineRecord>())
                        .Select(x => new { x.Name, x.Rank, x.DurationMinutes, x.RouteKey, x.EtaUnix })
                        .OrderBy(x => x.Name, StringComparer.Ordinal)
                        .ToArray()
                };
                return System.Text.Json.JsonSerializer.Serialize(lite);
            }
            catch { return string.Empty; }
        }

        public static string CurrentFilePath() => FilePath;

        private static void EnsureMigrated()
        {
            try
            {
                Directory.CreateDirectory(BaseDir);
                if (!File.Exists(FilePath))
                {
                    string? src = null;
                    DateTime tBest = DateTime.MinValue;
                    if (File.Exists(LegacyFile1))
                    {
                        var t = File.GetLastWriteTimeUtc(LegacyFile1);
                        if (t > tBest) { tBest = t; src = LegacyFile1; }
                    }
                    if (File.Exists(LegacyFile2))
                    {
                        var t = File.GetLastWriteTimeUtc(LegacyFile2);
                        if (t > tBest) { tBest = t; src = LegacyFile2; }
                    }
                    if (src != null)
                    {
                        File.Copy(src, FilePath, overwrite: true);
                    }
                }
            }
            catch { }
        }

        private static void WriteAtomic(string path, string content)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var tmp = path + ".tmp";

            // 軽いリトライ（他プロセスが一時的に掴んでいる場合の緩和）
            const int maxAttempts = 5;
            const int delayMs = 100;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    File.WriteAllText(tmp, content);
                    if (File.Exists(path)) File.Delete(path);
                    File.Move(tmp, path);
                    return;
                }
                catch (IOException)
                {
                    try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                    if (attempt == maxAttempts) throw;
                    System.Threading.Thread.Sleep(delayMs);
                }
            }
        }

        private static void TryWriteLegacy(string path, string content)
        {
            try { WriteAtomic(path, content); } catch { }
        }
    }
}

