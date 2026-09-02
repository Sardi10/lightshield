using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;

namespace LightShield.Common.Api
{
    public static class ConfigLoader
    {
        public static Dictionary<string, string> LoadConfig()
        {
            var globalPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "LightShield",
                "lightshield_config.json");
            var localPath = Path.Combine(AppContext.BaseDirectory, "lightshield_config.json");
            var path = File.Exists(globalPath) ? globalPath : localPath;

            if (!File.Exists(path))
                throw new FileNotFoundException($"Missing config file: {path}");

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                   ?? new Dictionary<string, string>();
        }
    }
}
