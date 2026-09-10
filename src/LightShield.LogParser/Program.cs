using System;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Security;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using LightShield.Common.Api;
using LightShield.Common.Models;

namespace LightShield.LogParser
{
    class Program
    {
        static readonly string LogFile = @"C:\ProgramData\LightShield\logparser.log";
        static LightShieldApiClient? apiClient;
        static EventLogWatcher? windowsEventWatcher;

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
                        @"C:\ProgramData\LightShield\logparser-event-queue.jsonl");
                    Log($"API client initialized: {apiUrl}");
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
        // WINDOWS EVENT LOG WATCHER
        // ------------------------------------------------------------
        [SupportedOSPlatform("windows")]
        static void WatchWindowsSecurityLog()
        {
            try
            {
                var query = new EventLogQuery(
                    "Security",
                    PathType.LogName,
                    "*[System[(EventID=4624 or EventID=4625)]]");
                windowsEventWatcher = new EventLogWatcher(query);
                windowsEventWatcher.EventRecordWritten += OnWindowsEvent;
                windowsEventWatcher.Enabled = true;

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

        [SupportedOSPlatform("windows")]
        static void OnWindowsEvent(object? sender, EventRecordWrittenEventArgs e)
        {
            if (e.EventException != null)
            {
                Log("Windows Event Log watcher error: " + e.EventException);
                return;
            }

            try
            {
                using var record = e.EventRecord;
                if (record?.Id is not (4624 or 4625))
                    return;

                var parsedEvent = ParseWindowsEvent(record);
                _ = Task.Run(() => HandleWindowsEvent(parsedEvent));
            }
            catch (Exception ex)
            {
                Log("Event handler error: " + ex);
            }
        }

        static async Task HandleWindowsEvent(WindowsLoginEvent parsedEvent)
        {
            try
            {
                bool isSuccess = parsedEvent.EventId == 4624;

                if (!ShouldKeepWindowsEvent(isSuccess, parsedEvent.LogonType))
                    return;

                var evt = new Event
                {
                    Source = "logparser",
                    OperatingSystem = "windows",
                    EventId = parsedEvent.EventId,
                    LogonType = parsedEvent.LogonType,
                    Username = parsedEvent.Username,
                    IPAddress = parsedEvent.IPAddress,
                    Type = isSuccess ? "LoginSuccess" : "LoginFailure",
                    PathOrMessage = $"Windows Security event {parsedEvent.EventId}",
                    Timestamp = parsedEvent.Timestamp,
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
        [SupportedOSPlatform("windows")]
        static WindowsLoginEvent ParseWindowsEvent(EventRecord record)
        {
            var xml = XDocument.Parse(record.ToXml());
            XNamespace ns = "http://schemas.microsoft.com/win/2004/08/events/event";
            var fields = xml.Descendants(ns + "Data")
                .Where(element => element.Attribute("Name") != null)
                .ToDictionary(
                    element => element.Attribute("Name")!.Value,
                    element => element.Value,
                    StringComparer.OrdinalIgnoreCase);

            var logonType = fields.TryGetValue("LogonType", out var rawLogonType) &&
                int.TryParse(rawLogonType, out var parsedLogonType)
                    ? parsedLogonType
                    : -1;

            fields.TryGetValue("TargetUserName", out var username);
            fields.TryGetValue("IpAddress", out var rawIpAddress);

            return new WindowsLoginEvent(
                record.Id,
                logonType,
                username,
                NormalizeIPAddress(rawIpAddress),
                record.TimeCreated?.ToUniversalTime() ?? DateTime.UtcNow);
        }

        static string? NormalizeIPAddress(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value == "-" ||
                !IPAddress.TryParse(value, out var address))
            {
                return null;
            }

            return address.ToString();
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

        sealed record WindowsLoginEvent(
            int EventId,
            int LogonType,
            string? Username,
            string? IPAddress,
            DateTime Timestamp);
    }
}
