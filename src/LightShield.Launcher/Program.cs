using System;
using System.Diagnostics;
using System.IO;

class Program
{
    static readonly string LogFile = @"C:\ProgramData\LightShield\launcher.log";

    static void Main()
    {
        Log("Launcher started");

        string exePath = Process.GetCurrentProcess().MainModule!.FileName!;
        string launcherDir = Path.GetDirectoryName(exePath)!;
        string rootDir = Directory.GetParent(launcherDir)!.FullName;

        StartProcess(Path.Combine(rootDir, "api", "LightShield.Api.exe"), "API");
        StartProcess(Path.Combine(rootDir, "agent", "LightShield.Agent.exe"), "Agent");
        StartProcess(Path.Combine(rootDir, "logparser", "LightShield.LogParser.exe"), "LogParser");

        Log("Launcher finished");
    }

    static void StartProcess(string path, string name)
    {
        if (!File.Exists(path))
        {
            Log($"ERROR: {name} not found at {path}");
            return;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = path,
                WorkingDirectory = Path.GetDirectoryName(path)!,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process.Start(psi);
            Log($"Started {name}");
        }
        catch (Exception ex)
        {
            Log($"FAILED {name}: {ex}");
        }
    }

    static void Log(string msg)
    {
        Directory.CreateDirectory(@"C:\ProgramData\LightShield");
        File.AppendAllText(LogFile, $"[{DateTime.Now}] {msg}{Environment.NewLine}");
    }
}
