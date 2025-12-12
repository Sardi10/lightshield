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
            var path = Path.Combine(AppContext.BaseDirectory, "lightshield_config.json");

            if (!File.Exists(path))
                throw new FileNotFoundException($"Missing config file: {path}");

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                   ?? new Dictionary<string, string>();
        }
    }
}
