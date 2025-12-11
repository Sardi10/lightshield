using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Collections.Generic;
using LightShield.Common.Api;
using LightShield.Common.Models;

namespace LightShield.Agent
{
    class Program
    {
        static LightShieldApiClient? apiClient;

        static void Main(string[] args)
        {
            // 1. Initialize API client
            var apiUrl = Environment.GetEnvironmentVariable("LIGHTSHIELD_API_URL");
            if (!string.IsNullOrWhiteSpace(apiUrl))
            {
                apiClient = new LightShieldApiClient(new HttpClient(), apiUrl);
                Console.WriteLine($"[Agent] ✔ Posting events to {apiUrl}");
            }
            else
            {
                Console.WriteLine("[Agent] ⚠ LIGHTSHIELD_API_URL not set — events will not be sent.");
            }

            string user = Environment.UserName;

            // ----------------------------------------------------------
            // 2. FINAL OPTIMIZED MONITORING PATHS
            // ----------------------------------------------------------
            string[] pathsToWatch = new[]
            {
                // High-value ransomware targets
                $@"C:\Users\{user}\Documents",
                $@"C:\Users\{user}\Desktop",
                $@"C:\Users\{user}\Pictures",
                $@"C:\Users\{user}\Videos",
                $@"C:\Users\{user}\Downloads",

                // Cloud sync folders
                $@"C:\Users\{user}\OneDrive",
                $@"C:\Users\{user}\Dropbox",
                $@"C:\Users\{user}\Google Drive",

                // Persistence locations (ONLY startup, NOT full Roaming)
                $@"C:\Users\{user}\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup",
                @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Startup",

                // Scheduled task persistence
                @"C:\Windows\System32\Tasks",

                // Sensitive system folders (drivers only, NOT full System32)
                @"C:\Windows\System32\drivers",
            };

            Console.WriteLine("[Agent] Initializing watchers...");

            List<FileSystemWatcher> watchers = new();

            foreach (var path in pathsToWatch)
            {
                try
                {
                    if (!Directory.Exists(path))
                        continue;

                    var watcher = new FileSystemWatcher(path)
                    {
                        IncludeSubdirectories = true,
                        NotifyFilter = NotifyFilters.FileName |
                                       NotifyFilters.DirectoryName |
                                       NotifyFilters.LastWrite |
                                       NotifyFilters.Size,
                        EnableRaisingEvents = true
                    };

                    watcher.Created += (s, e) => OnFileEvent("filecreate", e.FullPath);
                    watcher.Changed += (s, e) => OnFileEvent("filemodify", e.FullPath);
                    watcher.Deleted += (s, e) => OnFileEvent("filedelete", e.FullPath);
                    watcher.Renamed += (s, e) =>
                        OnFileEvent("filerename", $"{e.OldFullPath} → {e.FullPath}");

                    watchers.Add(watcher);
                    Console.WriteLine($"[Agent] ✔ Monitoring: {path}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Agent] ⚠ Failed to watch {path}: {ex.Message}");
                }
            }

            Console.WriteLine("[Agent] ✔ All watchers active.");
            Console.WriteLine("Press [Ctrl+C] to exit.");
            Thread.Sleep(Timeout.Infinite);
        }

        // ----------------------------------------------------------
        // NOISE FILE FILTERING (high-churn file EXTENSIONS)
        // ----------------------------------------------------------
        static bool IsNoiseFile(string path)
        {
            string lower = path.ToLower();

            // Extension noise
            if (lower.EndsWith(".log")) return true;
            if (lower.EndsWith(".tmp")) return true;
            if (lower.EndsWith(".temp")) return true;
            if (lower.EndsWith(".json-journal")) return true;
            if (lower.EndsWith(".etl")) return true;
            if (lower.EndsWith(".evtx")) return true;

            // Pattern noise
            if (lower.Contains("cache")) return true;
            if (lower.Contains("-journal")) return true;
            if (lower.Contains("temp")) return true;

            // Sync temp files
            if (Path.GetFileName(lower).StartsWith("~")) return true;
            if (Path.GetFileName(lower).EndsWith(".tmp")) return true;

            return false;
        }

        // ----------------------------------------------------------
        // NOISE PATH FILTERING (high-churn system folders)
        // ----------------------------------------------------------
        static bool IsNoisePath(string path)
        {
            string lower = path.ToLower();

            string[] noiseDirs =
            {
                @"\appdata\roaming\microsoft\crypto",
                @"\appdata\roaming\microsoft\spelling",
                @"\appdata\roaming\sql developer",
                @"\appdata\local\packages",             // Teams/Edge/Store noise
                @"\windows\system32\sru",               // telemetry DB
                @"\programdata\microsoft\windows\apprepository",
                @"\programdata\nvidia corporation",
                @"\programdata\intel",                  // GPU logs
            };

            foreach (var d in noiseDirs)
                if (lower.Contains(d)) return true;

            return false;
        }

        // ----------------------------------------------------------
        // EVENT HANDLER
        // ----------------------------------------------------------
        static async void OnFileEvent(string eventType, string path)
        {
            if (IsNoiseFile(path)) return;
            if (IsNoisePath(path)) return;  

            var ts = DateTime.UtcNow.ToString("o");
            Console.WriteLine($"[{ts}] {eventType} → {path}");

            if (apiClient != null)
            {
                var evt = new Event
                {
                    Source = "agent",
                    Type = eventType,
                    PathOrMessage = path,
                    Timestamp = DateTime.UtcNow,
                    Hostname = Environment.MachineName,
                    OperatingSystem = "windows"
                };

                var ok = await apiClient.PostEventAsync(evt);

                Console.WriteLine(ok
                    ? $"[Agent] ✔ Posted {eventType}"
                    : $"[Agent] ❌ Failed to post {eventType}");
            }
        }
    }
}
