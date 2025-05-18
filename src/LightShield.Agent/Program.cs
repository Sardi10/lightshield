using System;
using System.IO;
using System.Threading;

namespace LightShield.Agent
{
    class Program
    {
        static void Main(string[] args)
        {
            var path = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
            if (!Directory.Exists(path))
            {
                Console.WriteLine($"[LightShield] Warning: “{path}” not found. Defaulting to current directory.");
                path = Directory.GetCurrentDirectory();
            }

            Console.WriteLine($"[LightShield] Monitoring: {path}");
            using var watcher = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            watcher.Created += (s, e) => LogEvent("Created", e.FullPath);
            watcher.Changed += (s, e) => LogEvent("Changed", e.FullPath);
            watcher.Deleted += (s, e) => LogEvent("Deleted", e.FullPath);
            watcher.Renamed += (s, e) => LogEvent("Renamed", $"{e.OldFullPath} → {e.FullPath}");

            Console.WriteLine("Press [Ctrl+C] to exit.");
            Thread.Sleep(-1);  // keep running
        }

        static void LogEvent(string eventType, string path)
        {
            var timestamp = DateTime.UtcNow.ToString("o");
            Console.WriteLine($"[{timestamp}] {eventType}: {path}");
            // TODO: push to API or store locally
        }
    }
}
