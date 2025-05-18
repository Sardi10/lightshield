using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Security;           // for SecurityException

namespace LightShield.LogParser
{
    class Program
    {
        static void Main(string[] args)
        {
            if (OperatingSystem.IsWindows())
                WatchWindowsLog("Security");
            else
                WatchLinuxAuthLog("/var/log/auth.log");

            Console.WriteLine("Press Ctrl+C to exit.");
            Thread.Sleep(Timeout.Infinite);
        }

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
                        Console.WriteLine(
                            $"[{e.Entry.TimeGenerated:o}] EventID={e.Entry.InstanceId} Msg={e.Entry.Message.Split('\n')[0]}");
                };
                log.EnableRaisingEvents = true;
                Console.WriteLine($"[LogParser] Watching Windows EventLog: {logName}");
            }
            catch (SecurityException)
            {
                Console.WriteLine($"[LogParser] ⚠️  Insufficient privileges to read '{logName}'. Please run as Administrator.");
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
                        Console.WriteLine($"[{DateTime.UtcNow:o}] {line}");
                }
                lastPos = fs.Position;
            };

            Console.WriteLine($"[LogParser] Watching Linux auth log: {path}");
        }
    }
}
