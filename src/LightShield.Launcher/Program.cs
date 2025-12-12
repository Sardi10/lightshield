using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

class Program
{
    static void Main()
    {
        Console.WriteLine("[Launcher] Starting LightShield ecosystem...");

        // Where the exe files will live (same folder as launcher)
        string baseDir = AppContext.BaseDirectory;

        string apiPath = Path.Combine(baseDir, "LightShield.Api.exe");
        string agentPath = Path.Combine(baseDir, "LightShield.Agent.exe");
        string parserPath = Path.Combine(baseDir, "LightShield.LogParser.exe");
        string desktopPath = Path.Combine(baseDir, "LightShield.Desktop.exe");

        StartProcess(apiPath, "API");
        Thread.Sleep(1500); // give API time to start

        StartProcess(agentPath, "Agent");
        StartProcess(parserPath, "LogParser");
        StartProcess(desktopPath, "Desktop UI");

        Console.WriteLine("[Launcher] All components started.");
    }

    static void StartProcess(string path, string name)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"[Launcher] ERROR: Cannot find {name} at: {path}");
            return;
        }

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = path,
            WorkingDirectory = Path.GetDirectoryName(path),
            UseShellExecute = true,
        };

        Process.Start(psi);
        Console.WriteLine($"[Launcher] Started: {name}");
    }
}
