using System;
using System.IO;
using System.Text.Json;

namespace PhotoPresenter.Services
{
    public class AppSettings
    {
        public string? LastFolder { get; set; }
        public int MonitorIndex { get; set; }
        public int FadeMilliseconds { get; set; } = 500;
        public string SortKey { get; set; } = "FileName";
        public bool SortDesc { get; set; } = false;
    }

    public static class SettingsManager
    {
        private static readonly string _path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        public static AppSettings Settings { get; private set; } = new AppSettings();

        public static void Load()
        {
            try
            {
                if (File.Exists(_path))
                {
                    var json = File.ReadAllText(_path);
                    var s = JsonSerializer.Deserialize<AppSettings>(json);
                    if (s != null) Settings = s;
                }
            }
            catch
            {
                // ignore
            }
        }

        public static void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_path, json);
            }
            catch
            {
                // ignore
            }
        }
    }
}
