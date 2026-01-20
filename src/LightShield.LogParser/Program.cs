using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LightShield.Common.Api;
using LightShield.Common.Models;

namespace LightShield.LogParser
{
    class Program
    {
        static readonly string LogFile = @"C:\ProgramData\LightShield\logparser.log";
        static LightShieldApiClient? apiClient;

        static void Main()
        {
            try
            {
                Log("LogParser starting (SYSTEM mode)");

                InitializeApi();

                if (OperatingSystem.IsWindows())
                    WatchWindowsSecurityLog();
                else
                    WatchLinuxAuthLog("/var/log/auth.log");

                Log("LogParser running");
                Thread.Sleep(Timeout.Infinite);
            }
            catch (Exception ex)
            {
                Log("FATAL: " + ex);
                Thread.Sleep(Timeout.Infinite);
            }
        }

        // ------------------------------------------------------------
        // API INITIALIZATION
        // ------------------------------------------------------------
        static void InitializeApi()
        {
            try
            {
                var cfg = ConfigLoader.LoadConfig();
                var apiUrl = cfg["apiUrl"];

                if (string.IsNullOrWhiteSpace(apiUrl))
                    throw new Exception("apiUrl missing from config");

                apiClient = new LightShieldApiClient(new HttpClient(), apiUrl);
                Log($"API client initialized: {apiUrl}");
            }
            catch (Exception ex)
            {
                Log("API init failed: " + ex);
            }
        }

        // ------------------------------------------------------------
        // WINDOWS EVENT LOG WATCHER
        // ------------------------------------------------------------
        static void WatchWindowsSecurityLog()
        {
            try
            {
                const string logName = "Security";

                if (!EventLog.Exists(logName))
                {
                    Log($"EventLog '{logName}' not found");
                    return;
                }

                var log = new EventLog(logName);
                log.EntryWritten += OnWindowsEvent;
                log.EnableRaisingEvents = true;

                Log("Watching Windows Security EventLog");
            }
            catch (SecurityException)
            {
                Log("Insufficient privileges to read Security log (should not happen under SYSTEM)");
            }
            catch (Exception ex)
            {
                Log("Failed to initialize EventLog: " + ex);
            }
        }

        static void OnWindowsEvent(object sender, EntryWrittenEventArgs e)
        {
            try
            {
                long eventId = (long)e.Entry.InstanceId;

                // Only login success/failure
                if (eventId != 4624 && eventId != 4625)
                    return;

                _ = Task.Run(() => HandleWindowsEvent(e.Entry, eventId));
            }
            catch (Exception ex)
            {
                Log("Event handler error: " + ex);
            }
        }

        static async Task HandleWindowsEvent(EventLogEntry entry, long eventId)
        {
            try
            {
                string raw = entry.Message;

                int logonType = ExtractLogonType(raw);
                bool isSuccess = eventId == 4624;

                if (!ShouldKeepWindowsEvent(isSuccess, logonType))
                    return;

                var evt = new Event
                {
                    Source = "logparser",
                    OperatingSystem = "windows",
                    EventId = (int)eventId,
                    LogonType = logonType,
                    Username = ExtractUsername(raw),
                    IPAddress = ExtractIPAddress(raw),
                    Type = isSuccess ? "LoginSuccess" : "LoginFailure",
                    PathOrMessage = raw.Split('\n')[0],
                    Timestamp = DateTime.UtcNow,
                    Hostname = Environment.MachineName,
                    Severity = isSuccess ? "Info" : "Warning"
                };

                if (apiClient != null)
                {
                    await apiClient.PostEventAsync(evt);
                }
            }
            catch (Exception ex)
            {
                Log("Failed to process Windows event: " + ex);
            }
        }

        // ------------------------------------------------------------
        // LINUX AUTH LOG WATCHER (kept for parity)
        // ------------------------------------------------------------
        static void WatchLinuxAuthLog(string path)
        {
            try
            {
                long lastPos = File.Exists(path) ? new FileInfo(path).Length : 0;
                var watcher = new FileSystemWatcher(Path.GetDirectoryName(path)!, Path.GetFileName(path))
                {
                    NotifyFilter = NotifyFilters.LastWrite,
                    EnableRaisingEvents = true
                };

                watcher.Changed += (_, __) =>
                {
                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    fs.Seek(lastPos, SeekOrigin.Begin);

                    using var sr = new StreamReader(fs);
                    string? line;
                    while ((line = sr.ReadLine()) != null)
                        HandleLinuxLine(line).Wait();

                    lastPos = fs.Position;
                };

                Log("Watching Linux auth log");
            }
            catch (Exception ex)
            {
                Log("Linux watcher failed: " + ex);
            }
        }

        static async Task HandleLinuxLine(string line)
        {
            bool success = line.Contains("session opened for user");
            bool failure = line.Contains("Failed password");

            if (!success && !failure)
                return;

            var evt = new Event
            {
                Source = "logparser",
                OperatingSystem = "linux",
                Type = success ? "LoginSuccess" : "LoginFailure",
                PathOrMessage = line,
                Timestamp = DateTime.UtcNow,
                Hostname = Environment.MachineName,
                Username = ExtractLinuxUsername(line),
                IPAddress = ExtractLinuxIP(line),
                Severity = success ? "Info" : "Warning"
            };

            if (apiClient != null)
                await apiClient.PostEventAsync(evt);
        }

        // ------------------------------------------------------------
        // HELPERS
        // ------------------------------------------------------------
        static int ExtractLogonType(string msg)
        {
            foreach (var line in msg.Split('\n'))
                if (line.Contains("Logon Type:", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(line.Split(':').Last().Trim(), out int t))
                    return t;

            return -1;
        }

        static string? ExtractUsername(string msg)
        {
            foreach (var line in msg.Split('\n'))
                if (line.Contains("Account Name:", StringComparison.OrdinalIgnoreCase))
                    return line.Split(':').Last().Trim();

            return null;
        }

        static string? ExtractIPAddress(string msg)
        {
            var m = Regex.Match(msg, @"\b\d{1,3}(\.\d{1,3}){3}\b");
            return m.Success ? m.Value : null;
        }

        static bool ShouldKeepWindowsEvent(bool success, int logonType)
        {
            return success
                ? (logonType == 2 || logonType == 10)
                : (logonType == 2 || logonType == 3 || logonType == 10);
        }

        static string? ExtractLinuxUsername(string line)
        {
            var m = Regex.Match(line, @"for user (\w+)");
            return m.Success ? m.Groups[1].Value : null;
        }

        static string? ExtractLinuxIP(string line)
        {
            var m = Regex.Match(line, @"from (\d{1,3}(\.\d{1,3}){3})");
            return m.Success ? m.Groups[1].Value : null;
        }

        static void Log(string msg)
        {
            Directory.CreateDirectory(@"C:\ProgramData\LightShield");
            File.AppendAllText(LogFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}\n");
        }
    }
}
