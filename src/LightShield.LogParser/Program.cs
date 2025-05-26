//  ../src/LightShield.LogParser/Program.cs

using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Security;           // for SecurityException
using System.Net.Http;
using System.Threading.Tasks;
using LightShield.Common.Api;
using LightShield.Common.Models;

namespace LightShield.LogParser
{
    class Program
    {
        static void Main(string[] args)
        {
            // Read API URL from environment
            var apiUrl = Environment.GetEnvironmentVariable("LIGHTSHIELD_API_URL");
            if (!string.IsNullOrEmpty(apiUrl))
            {
                apiClient = new LightShieldApiClient(new HttpClient(), apiUrl);
            }
            else
            {
                Console.WriteLine("[LogParser]   LIGHTSHIELD_API_URL not set—events will not be sent.");
            }

            using var http = new HttpClient();
            apiClient = new LightShieldApiClient(http, apiUrl ?? "");

            if (OperatingSystem.IsWindows())
                WatchWindowsLog("Security");
            else
                WatchLinuxAuthLog("/var/log/auth.log");

            Console.WriteLine("Press Ctrl+C to exit.");
            Thread.Sleep(Timeout.Infinite);
        }

        static LightShieldApiClient? apiClient = null;

        //static async void OnFileEvent(string eventType, string path)
        //{
        //    LogEvent(eventType, path);
        //    if (apiClient != null)
        //    {
        //        var evt = new Event
        //        {
        //            Source = "agent",
        //            Type = eventType,
        //            PathOrMessage = path,
        //            Timestamp = DateTime.UtcNow,
        //            Hostname = Environment.MachineName
        //        };
        //        var ok = await apiClient.PostEventAsync(evt);
        //        Console.WriteLine(ok
        //            ? $"[Agent]  Event posted: {eventType} @ {path}"
        //            : $"[Agent]  Failed to post event: {eventType}");
        //    }
        //}

        //static void LogEvent(string eventType, string path)
        //{
        //    Console.WriteLine($"[{DateTime.UtcNow:o}] {eventType} => {path}");
        //}

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
                    if (e.Entry.InstanceId == 4624 || e.Entry.InstanceId == 4625)
                    {
                        _ = Task.Run(async () =>
                        {
                            var evt = new Event
                            {
                                Source = "logparser",
                                Type = e.Entry.InstanceId == 4624 ? "LoginSuccess" : "LoginFailure",
                                PathOrMessage = e.Entry.Message.Split('\n')[0],
                                Timestamp = e.Entry.TimeGenerated,
                                Hostname = Environment.MachineName
                            };

                            if (apiClient != null)
                            {
                                var ok = await apiClient.PostEventAsync(evt);
                                Console.WriteLine(ok
                                    ? $"[LogParser]   Posted login event {evt.Type}"
                                    : $"[LogParser]   Failed to post login event");
                            }
                            else
                            {
                                Console.WriteLine($"[{evt.Timestamp:o}] {evt.Type} => {evt.PathOrMessage}");
                            }
                        });
                    }
                };
                log.EnableRaisingEvents = true;
                Console.WriteLine($"[LogParser] Watching Windows EventLog: {logName}");
            }
            catch (SecurityException)
            {
                Console.WriteLine($"[LogParser]  Insufficient privileges to read '{logName}'. Please run as Administrator.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogParser] Error initializing EventLog: {ex.Message}");
            }
        }

        static void WatchLinuxAuthLog(string path)
        {
            var dir = Path.GetDirectoryName(path);
            var file = Path.GetFileName(path);
            long lastPos = File.Exists(path) ? new FileInfo(path).Length : 0;

            var watcher = new FileSystemWatcher(dir, file)
            {
                NotifyFilter = NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };

            watcher.Changed += (s, e) =>
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                fs.Seek(lastPos, SeekOrigin.Begin);
                using var sr = new StreamReader(fs);
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.Contains("session opened for user") || line.Contains("Failed password"))
                    {
                        var type = line.Contains("session opened for user") ? "LoginSuccess" : "LoginFailure";

                        _ = Task.Run(async () =>
                        {
                            var evt = new Event
                            {
                                Source = "logparser",
                                Type = type,
                                PathOrMessage = line,
                                Timestamp = DateTime.UtcNow,
                                Hostname = Environment.MachineName
                            };

                            if (apiClient != null)
                            {
                                var ok = await apiClient.PostEventAsync(evt);
                                Console.WriteLine(ok
                                    ? $"[LogParser]   Posted login event {evt.Type}"
                                    : $"[LogParser]   Failed to post login event");
                            }
                            else
                            {
                                Console.WriteLine($"[{evt.Timestamp:o}] {evt.Type} => {evt.PathOrMessage}");
                            }
                        });
                    }
                }
                lastPos = fs.Position;
            };

            Console.WriteLine($"[LogParser] Watching Linux auth log: {path}");
        }
    }
}
