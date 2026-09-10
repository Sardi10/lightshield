using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Collections.Generic;
using System.Diagnostics;
using LightShield.Common.Api;
using LightShield.Common.Models;

namespace LightShield.Agent
{
    class Program
    {
        static readonly string LogDir = @"C:\ProgramData\LightShield";
        static readonly string LogFile = Path.Combine(LogDir, "agent.log");

        static LightShieldApiClient? apiClient;
        static readonly List<FileSystemWatcher> watchers = new();
        static readonly object watcherLock = new();
        static readonly object deduplicationLock = new();
        static readonly Dictionary<string, DateTime> recentFileEvents = new(StringComparer.OrdinalIgnoreCase);
        static readonly TimeSpan FileEventDeduplicationWindow = TimeSpan.FromSeconds(1);

        static void Main()
        {
            Directory.CreateDirectory(LogDir);
            Log("Agent starting (SYSTEM mode)");

            InitializeApi();
            InitializeWatchers();

            // Watchdog loop (self-healing)
            Task.Run(WatchdogLoop);

            Log("Agent running");
            Thread.Sleep(Timeout.Infinite);
        }

        // ------------------------------------------------------------
        // API INITIALIZATION
        // ------------------------------------------------------------
        static void InitializeApi()
        {
            for (var attempt = 1; attempt <= 30; attempt++)
            {
                try
                {
                    var cfg = ConfigLoader.LoadConfig();
                    var apiUrl = cfg["apiUrl"];
                    if (string.IsNullOrWhiteSpace(apiUrl))
                        throw new Exception("apiUrl missing from config");
                    if (!cfg.TryGetValue("apiKey", out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
                        throw new Exception("apiKey missing from config");
                    apiClient = new LightShieldApiClient(
                        new HttpClient(),
                        apiUrl,
                        apiKey,
                        Path.Combine(LogDir, "agent-event-queue.jsonl"));
                    Log($"API connected → {apiUrl}");
                    return;
                }
                catch (Exception ex)
                {
                    Log($"API init attempt {attempt}/30 failed: {ex.Message}");
                    if (attempt < 30) Thread.Sleep(TimeSpan.FromSeconds(1));
                }
            }
            Log("API initialization exhausted all retries; telemetry delivery is disabled.");
        }

        // ------------------------------------------------------------
        // WATCHER INITIALIZATION
        // ------------------------------------------------------------
        static void InitializeWatchers()
        {
            lock (watcherLock)
            {
                CleanupWatchers();

                foreach (var path in BuildPathsToWatch())
                {
                    try
                    {
                        if (!Directory.Exists(path))
                            continue;

                        var watcher = new FileSystemWatcher(path)
                        {
                            IncludeSubdirectories = true,
                            NotifyFilter =
                                NotifyFilters.FileName |
                                NotifyFilters.DirectoryName |
                                NotifyFilters.LastWrite |
                                NotifyFilters.Size,
                            EnableRaisingEvents = true
                        };

                        watcher.Created += async (s, e) => await OnFileEvent("filecreate", e.FullPath);
                        watcher.Changed += async (s, e) => await OnFileEvent("filemodify", e.FullPath);
                        watcher.Deleted += async (s, e) => await OnFileEvent("filedelete", e.FullPath);
                        watcher.Renamed += async (s, e) =>
                            await OnFileEvent("filerename", $"{e.OldFullPath} → {e.FullPath}");
                        watcher.Error += OnWatcherError;

                        watchers.Add(watcher);
                        Log($"Watching: {path}");
                    }
                    catch (Exception ex)
                    {
                        Log($"Failed to watch {path}: {ex.Message}");
                    }
                }
            }
        }

        // ------------------------------------------------------------
        // WATCHDOG (AUTO-RECOVERY)
        // ------------------------------------------------------------
        static async Task WatchdogLoop()
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromMinutes(2));

                lock (watcherLock)
                {
                    bool needsRestart = false;

                    foreach (var w in watchers)
                    {
                        if (!w.EnableRaisingEvents)
                        {
                            needsRestart = true;
                            break;
                        }
                    }

                    if (needsRestart || watchers.Count == 0)
                    {
                        Log("Watchdog triggered — rebuilding watchers");
                        InitializeWatchers();
                    }
                }
            }
        }

        static void CleanupWatchers()
        {
            foreach (var w in watchers)
            {
                try { w.Dispose(); } catch { }
            }
            watchers.Clear();
        }

        static void OnWatcherError(object sender, ErrorEventArgs e)
        {
            var exception = e.GetException();
            var errorKind = exception is InternalBufferOverflowException
                ? "buffer overflow"
                : "watcher error";
            Log($"TELEMETRY LOSS: File-system {errorKind}: {exception.Message}");

            // Rebuild outside the watcher callback so disposal cannot deadlock it.
            _ = Task.Run(InitializeWatchers);
        }

        // ------------------------------------------------------------
        // PATH DISCOVERY (SYSTEM SAFE)
        // ------------------------------------------------------------
        static IEnumerable<string> BuildPathsToWatch()
        {
            var paths = new List<string>();

            // User profiles
            foreach (var userDir in GetRealUserProfiles())
            {
                paths.Add(Path.Combine(userDir, "Documents"));
                paths.Add(Path.Combine(userDir, "Desktop"));
                paths.Add(Path.Combine(userDir, "Downloads"));
                paths.Add(Path.Combine(userDir, "Pictures"));
                paths.Add(Path.Combine(userDir, "Videos"));

                // Optional cloud folders
                paths.Add(Path.Combine(userDir, "OneDrive"));
                paths.Add(Path.Combine(userDir, "Dropbox"));
                paths.Add(Path.Combine(userDir, "Google Drive"));
            }

            // SYSTEM-level persistence paths
            paths.Add(@"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Startup");
            paths.Add(@"C:\Windows\System32\Tasks");
            paths.Add(@"C:\Windows\System32\drivers");

            return paths;
        }

        static IEnumerable<string> GetRealUserProfiles()
        {
            const string usersRoot = @"C:\Users";

            if (!Directory.Exists(usersRoot))
                yield break;

            foreach (var dir in Directory.GetDirectories(usersRoot))
            {
                var name = Path.GetFileName(dir);

                if (name.Equals("Public", StringComparison.OrdinalIgnoreCase)) continue;
                if (name.Equals("Default", StringComparison.OrdinalIgnoreCase)) continue;
                if (name.Equals("Default User", StringComparison.OrdinalIgnoreCase)) continue;
                if (name.Equals("All Users", StringComparison.OrdinalIgnoreCase)) continue;

                yield return dir;
            }
        }

        // ------------------------------------------------------------
        // EVENT HANDLING
        // ------------------------------------------------------------
        static async Task OnFileEvent(string eventType, string path)
        {
            try
            {
                if (IsNoiseFile(path)) return;
                if (IsNoisePath(path)) return;
                if (IsDuplicateFileEvent(eventType, path)) return;

                Log($"{eventType} → {path}");

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

                    await apiClient.PostEventAsync(evt);
                }
            }
            catch (Exception ex)
            {
                Log($"Event handler error: {ex}");
            }
        }

        static bool IsDuplicateFileEvent(string eventType, string path)
        {
            var now = DateTime.UtcNow;
            var key = $"{eventType}|{path}";

            lock (deduplicationLock)
            {
                if (recentFileEvents.TryGetValue(key, out var previous) &&
                    now - previous < FileEventDeduplicationWindow)
                {
                    return true;
                }

                recentFileEvents[key] = now;
                if (recentFileEvents.Count > 5_000)
                {
                    var staleCutoff = now - FileEventDeduplicationWindow;
                    foreach (var staleKey in recentFileEvents
                        .Where(pair => pair.Value < staleCutoff)
                        .Select(pair => pair.Key)
                        .ToList())
                    {
                        recentFileEvents.Remove(staleKey);
                    }
                }

                return false;
            }
        }

        // ------------------------------------------------------------
        // NOISE FILTERING
        // ------------------------------------------------------------
        static bool IsNoiseFile(string path)
        {
            var lower = path.ToLowerInvariant();

            return lower.EndsWith(".log") ||
                   lower.EndsWith(".tmp") ||
                   lower.EndsWith(".temp") ||
                   lower.EndsWith(".etl") ||
                   lower.EndsWith(".evtx") ||
                   Path.GetFileName(lower).StartsWith("~");
        }

        static bool IsNoisePath(string path)
        {
            var lower = path.ToLowerInvariant();

            string[] noiseDirs =
            {
                @"\appdata\local\packages",
                @"\windows\system32\sru",
                @"\programdata\microsoft\windows\apprepository",
                @"\programdata\nvidia",
                @"\programdata\intel"
            };

            foreach (var d in noiseDirs)
                if (lower.Contains(d)) return true;

            return false;
        }

        // ------------------------------------------------------------
        // LOGGING
        // ------------------------------------------------------------
        static void Log(string msg)
        {
            File.AppendAllText(
                LogFile,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}{Environment.NewLine}"
            );
        }
    }
}
