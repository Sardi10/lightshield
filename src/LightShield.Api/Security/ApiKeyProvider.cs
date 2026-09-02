using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LightShield.Api.Security;

public sealed class ApiKeyProvider
{
    public const string HeaderName = "X-LightShield-Api-Key";
    private readonly string _apiKey;

    public ApiKeyProvider(IHostEnvironment environment, ILogger<ApiKeyProvider> logger)
    {
        var configPath = GetConfigPath(environment);
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);

        JsonObject config;
        try
        {
            config = File.Exists(configPath)
                ? JsonNode.Parse(File.ReadAllText(configPath))?.AsObject() ?? new JsonObject()
                : new JsonObject();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not parse {ConfigPath}; a safe configuration will be recreated.", configPath);
            config = new JsonObject();
        }

        _apiKey = config["apiKey"]?.GetValue<string>() ?? string.Empty;
        if (_apiKey.Length < 32)
        {
            _apiKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            config["apiKey"] = _apiKey;
            config["apiUrl"] ??= "http://localhost:5213";
            File.WriteAllText(configPath, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            logger.LogInformation("Generated a new local API credential at {ConfigPath}.", configPath);
        }
    }

    public bool IsValid(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return false;
        var expected = System.Text.Encoding.UTF8.GetBytes(_apiKey);
        var supplied = System.Text.Encoding.UTF8.GetBytes(candidate);
        return expected.Length == supplied.Length && CryptographicOperations.FixedTimeEquals(expected, supplied);
    }

    private static string GetConfigPath(IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            var overridePath = Environment.GetEnvironmentVariable("LIGHTSHIELD_CONFIG_PATH");
            if (!string.IsNullOrWhiteSpace(overridePath)) return overridePath;
        }

        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return Path.Combine(programData, "LightShield", "lightshield_config.json");
    }
}
