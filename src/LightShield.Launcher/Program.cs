using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

class Program
{
    static void Main()
    {
        Console.WriteLine("[Launcher] Starting LightShield background services...");

        string launcherDir = AppContext.BaseDirectory;
        string rootDir = Path.GetFullPath(Path.Combine(launcherDir, ".."));

        string apiPath = Path.Combine(rootDir, "api", "LightShield.Api.exe");
        string agentPath = Path.Combine(rootDir, "agent", "LightShield.Agent.exe");
        string parserPath = Path.Combine(rootDir, "logparser", "LightShield.LogParser.exe");

        StartProcess(apiPath, "API");
        Thread.Sleep(1500);

        StartProcess(agentPath, "Agent");
        StartProcess(parserPath, "LogParser");

        Console.WriteLine("[Launcher] Background services started.");
    }

    static void StartProcess(string path, string name)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"[Launcher] ERROR: Cannot find {name} at: {path}");
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = path,
            WorkingDirectory = Path.GetDirectoryName(path),
            UseShellExecute = true
        };

        Process.Start(psi);
        Console.WriteLine($"[Launcher] Started: {name}");
    }
}
