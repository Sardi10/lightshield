using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using LightShield.Common.Api;
using LightShield.Common.Models;

namespace LightShield.Agent
{
    class Program
    {
        // Shared API client (null if URL isn’t set)
        static LightShieldApiClient? apiClient;

        static void Main(string[] args)
        {
            // 1) Initialize API client from env var
            var apiUrl = Environment.GetEnvironmentVariable("LIGHTSHIELD_API_URL");
            if (!string.IsNullOrWhiteSpace(apiUrl))
            {
                apiClient = new LightShieldApiClient(new HttpClient(), apiUrl);
                Console.WriteLine($"[Agent] ✔  Will post events to {apiUrl}");
            }
            else
            {
                Console.WriteLine("[Agent] ⚠  LIGHTSHIELD_API_URL not set—events will not be sent.");
            }

            // 2) Determine path to watch
            var path = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
            if (!Directory.Exists(path))
            {
                Console.WriteLine($"[Agent] ⚠  “{path}” not found. Defaulting to current directory.");
                path = Directory.GetCurrentDirectory();
            }

            Console.WriteLine($"[Agent] Monitoring: {path}");

            // 3) Wire up FileSystemWatcher
            using var watcher = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            // On each event, call our async poster
            watcher.Created += (s, e) => OnFileEvent("Created", e.FullPath);
            watcher.Changed += (s, e) => OnFileEvent("Changed", e.FullPath);
            watcher.Deleted += (s, e) => OnFileEvent("Deleted", e.FullPath);
            watcher.Renamed += (s, e) => OnFileEvent("Renamed", $"{e.OldFullPath} → {e.FullPath}");

            Console.WriteLine("Press [Ctrl+C] to exit.");
            Thread.Sleep(Timeout.Infinite);
        }

        static async void OnFileEvent(string eventType, string path)
        {
            // 1) Local console log
            var ts = DateTime.UtcNow.ToString("o");
            Console.WriteLine($"[{ts}] {eventType} → {path}");

            // 2) If we have an API client, post it
            if (apiClient != null)
            {
                var evt = new Event
                {
                    Source = "agent",
                    Type = eventType,
                    PathOrMessage = path,
                    Timestamp = DateTime.UtcNow,
                    Hostname = Environment.MachineName
                };

                var ok = await apiClient.PostEventAsync(evt);
                Console.WriteLine(ok
                    ? $"[Agent] ✔  Posted file event {eventType}"
                    : $"[Agent] ❌  Failed to post file event {eventType}");
            }
        }
    }
}
