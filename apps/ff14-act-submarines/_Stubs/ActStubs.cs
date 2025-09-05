#if ACT_STUB
using System;
using System.Windows.Forms;

// Minimal stubs so the project compiles without ACT installed.
// NOTE: A DLL built against these stubsはACT上では読み込めません。
// 実運用時は ACT をインストールし、参照を本体に向けてビルドしてください。

namespace Advanced_Combat_Tracker
{
    public interface IActPluginV1
    {
        void InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText);
        void DeInitPlugin();
    }

    public class LogLineEventArgs : EventArgs
    {
        public string logLine;
        public LogLineEventArgs(string line) { logLine = line; }
    }

    public class FormActMain
    {
        public delegate void LogLineRead(bool isImport, LogLineEventArgs logInfo);
        public event LogLineRead OnLogLineRead;
        public void Raise(string line) => OnLogLineRead?.Invoke(false, new LogLineEventArgs(line));
    }

    public static class ActGlobals
    {
        public static FormActMain oFormActMain { get; } = new FormActMain();
    }
}
#endif

