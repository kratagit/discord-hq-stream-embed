using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DiscordStreamOverlay
{
    public class Preset
    {
        public int OFFSET_X { get; set; } = 325;
        public int OFFSET_Y { get; set; } = 38;
        public int MARGIN_RIGHT { get; set; } = 8;
        public int MARGIN_BOTTOM { get; set; } = 66;
    }

    public class AppConfig
    {
        public bool ATTACH_TO_DISCORD { get; set; } = true;
        public string STREAM_URL { get; set; } = "http://192.168.8.122:8889/stream";
        public string HOTKEY_TOGGLE_STREAM { get; set; } = "f7+f8";
        public string HOTKEY_TOGGLE_MODE { get; set; } = "f8+f9";
        public string WINDOW_TITLE { get; set; } = "MY_STREAM";
        public int OFFSET_X { get; set; } = 325;
        public int OFFSET_Y { get; set; } = 38;
        public int MARGIN_RIGHT { get; set; } = 8;
        public int MARGIN_BOTTOM { get; set; } = 66;
        public Dictionary<string, Preset> PRESETS { get; set; } = new Dictionary<string, Preset>
        {
            { "1", new Preset() },
            { "2", new Preset() },
            { "3", new Preset() }
        };
    }

    public static class ConfigManager
    {
        private static readonly string AppName = "Discord_Stream_Overlay";
        private static string ConfigDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);
        private static string ConfigFile => Path.Combine(ConfigDir, "config.json");

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigFile))
                {
                    string json = File.ReadAllText(ConfigFile);
                    var config = JsonSerializer.Deserialize<AppConfig>(json);
                    if (config != null) return config;
                }
            }
            catch { }
            return new AppConfig();
        }

        public static void Save(AppConfig config)
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(ConfigFile, json);
            }
            catch { }
        }
    }
}
