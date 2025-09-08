using System;
using System.IO;
using System.Xml.Serialization;

namespace FF14SubmarinesAct
{
    public static class SettingsStore
    {
        private static string BaseDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ff14_submarines_act");
        private static string FilePath => Path.Combine(BaseDir, "settings.xml");

        public static Settings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var ser = new XmlSerializer(typeof(Settings));
                    using (var fs = File.OpenRead(FilePath))
                        return (Settings)ser.Deserialize(fs);
                }
            }
            catch { }
            return new Settings();
        }

        public static void Save(Settings s)
        {
            Directory.CreateDirectory(BaseDir);
            var ser = new XmlSerializer(typeof(Settings));
            using (var fs = File.Create(FilePath))
                ser.Serialize(fs, s);
        }
    }
}

