// src/LightShield.LogParser/Program.cs

using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Security;
using System.Net.Http;
using System.Threading.Tasks;
using LightShield.Common.Api;
using LightShield.Common.Models;
using System.Linq;
using System.Text.RegularExpressions;

namespace LightShield.LogParser
{
    class Program
    {
        static LightShieldApiClient? apiClient = null;

        static void Main(string[] args)
        {
            // ------------------------------------------------------------
            // API URL
            // ------------------------------------------------------------
            var apiUrl = Environment.GetEnvironmentVariable("LIGHTSHIELD_API_URL");

            if (!string.IsNullOrEmpty(apiUrl))
            {
                apiClient = new LightShieldApiClient(new HttpClient(), apiUrl);
            }
            else
            {
                Console.WriteLine("[LogParser]   LIGHTSHIELD_API_URL not set—events will not be sent.");
            }

            // ------------------------------------------------------------
            // Decide which OS parser to run
            // ------------------------------------------------------------
            if (OperatingSystem.IsWindows())
                WatchWindowsLog("Security");
            else
                WatchLinuxAuthLog("/var/log/auth.log");

            Console.WriteLine("Press Ctrl+C to exit.");
            Thread.Sleep(Timeout.Infinite);
        }

        // ======================================================================
        // WINDOWS EVENT LOG PARSER
        // ======================================================================
        static void WatchWindowsLog(string logName)
        {
            try
            {
                if (!EventLog.Exists(logName))
                {
                    Console.WriteLine($"[LogParser] EventLog '{logName}' not found.");
                    return;
                }

                var log = new EventLog(logName);

                log.EntryWritten += (s, e) =>
                {
                    long eventId = (long)e.Entry.InstanceId;

                    // Only monitor Windows LoginSuccess / LoginFailure
                    if (eventId != 4624 && eventId != 4625)
                        return;

                    _ = Task.Run(async () =>
                    {
                        var rawMessage = e.Entry.Message;

                        int logonType = ExtractLogonType(rawMessage);
                        string? username = ExtractUsername(rawMessage);
                        string? sourceIp = ExtractIPAddress(rawMessage);

                        bool isSuccess = eventId == 4624;
                        bool keep = ShouldKeepWindowsEvent(isSuccess, logonType);

                        if (!keep)
                            return;

                        var evt = new Event
                        {
                            Source = "logparser",
                            OperatingSystem = "windows",
                            EventId = (int)eventId,
                            LogonType = logonType,
                            Username = username,
                            IPAddress = sourceIp,
                            Type = isSuccess ? "LoginSuccess" : "LoginFailure",
                            PathOrMessage = rawMessage.Split('\n')[0],
                            Timestamp = DateTime.Now,
                            Hostname = Environment.MachineName,
                            Severity = isSuccess ? "Info" : "Warning"
                        };

                        if (apiClient != null)
                        {
                            var ok = await apiClient.PostEventAsync(evt);
                            Console.WriteLine(ok
                                ? $"[LogParser] Posted Windows {evt.Type} (LogonType={logonType})"
                                : $"[LogParser] Failed to post Windows event");
                        }
                    });
                };

                log.EnableRaisingEvents = true;
                Console.WriteLine($"[LogParser] Watching Windows EventLog: {logName}");
            }
            catch (SecurityException)
            {
                Console.WriteLine($"[LogParser] Insufficient privileges to read '{logName}'. Run as Administrator.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogParser] Error initializing EventLog: {ex.Message}");
            }
        }

        // ======================================================================
        // LINUX AUTH.LOG PARSER
        // ======================================================================
        static void WatchLinuxAuthLog(string path)
        {
            var dir = Path.GetDirectoryName(path);
            var file = Path.GetFileName(path);
            long lastPos = File.Exists(path) ? new FileInfo(path).Length : 0;

            var watcher = new FileSystemWatcher(dir!, file)
            {
                NotifyFilter = NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };

            watcher.Changed += (s, e) =>
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                fs.Seek(lastPos, SeekOrigin.Begin);

                using var sr = new StreamReader(fs);
                string? line;

                while ((line = sr.ReadLine()) != null)
                {
                    bool isSuccess = line.Contains("session opened for user");
                    bool isFailure = line.Contains("Failed password");

                    if (!isSuccess && !isFailure)
                        continue;

                    string? username = ExtractLinuxUsername(line);
                    string? ip = ExtractLinuxIP(line);

                    _ = Task.Run(async () =>
                    {
                        var evt = new Event
                        {
                            Source = "logparser",
                            OperatingSystem = "linux",
                            Type = isSuccess ? "LoginSuccess" : "LoginFailure",
                            PathOrMessage = line,
                            Timestamp = DateTime.Now,
                            Hostname = Environment.MachineName,
                            Username = username,
                            IPAddress = ip,
                            Severity = isSuccess ? "Info" : "Warning"
                        };

                        if (apiClient != null)
                        {
                            var ok = await apiClient.PostEventAsync(evt);
                            Console.WriteLine(ok
                                ? $"[LogParser] Posted Linux {evt.Type}"
                                : $"[LogParser] Failed to post Linux event");
                        }
                    });
                }

                lastPos = fs.Position;
            };

            Console.WriteLine($"[LogParser] Watching Linux auth log: {path}");
        }

        // ======================================================================
        // HELPERS FOR WINDOWS
        // ======================================================================
        static int ExtractLogonType(string msg)
        {
            foreach (var line in msg.Split('\n'))
            {
                if (line.Contains("Logon Type:", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(':');
                    if (parts.Length > 1 && int.TryParse(parts[1].Trim(), out int lt))
                        return lt;
                }
            }
            return -1;
        }

        static string? ExtractUsername(string msg)
        {
            foreach (var line in msg.Split('\n'))
            {
                if (line.Contains("Account Name:", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(':');
                    if (parts.Length > 1)
                        return parts[1].Trim();
                }
            }
            return null;
        }

        static string? ExtractIPAddress(string msg)
        {
            var ipMatch = Regex.Match(msg, @"\b\d{1,3}(\.\d{1,3}){3}\b");
            return ipMatch.Success ? ipMatch.Value : null;
        }

        // Windows event noise filtering
        static bool ShouldKeepWindowsEvent(bool isSuccess, int logonType)
        {
            if (isSuccess)
            {
                // Only real human or remote interactive logons
                return logonType == 2 || logonType == 10;
            }
            else
            {
                // Keep failures for brute-force detection
                return logonType == 2 || logonType == 3 || logonType == 10;
            }
        }

        // ======================================================================
        // HELPERS FOR LINUX
        // ======================================================================
        static string? ExtractLinuxUsername(string line)
        {
            var match = Regex.Match(line, @"for user (\w+)");
            return match.Success ? match.Groups[1].Value : null;
        }

        static string? ExtractLinuxIP(string line)
        {
            var match = Regex.Match(line, @"from (\d{1,3}(\.\d{1,3}){3})");
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}
