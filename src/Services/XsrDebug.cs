using System;
using System.IO;

namespace XIVSubmarinesReturn.Services
{
    internal static class XsrDebug
    {
        public static void Log(Configuration cfg, string message, Exception? ex = null)
        {
            try
            {
                if (cfg == null || !cfg.DebugLogging) return;
                var dir = Path.GetDirectoryName(BridgeWriter.CurrentFilePath()) ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "xsr_debug.log");
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message} {ex?.ToString() ?? string.Empty}";
                File.AppendAllText(path, line + Environment.NewLine);
            }
            catch { }
        }
    }
}

