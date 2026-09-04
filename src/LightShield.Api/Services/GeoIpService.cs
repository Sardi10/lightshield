using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace LightShield.Api.Services;

public sealed record GeoLocation(string? Country, string? City);

public sealed class GeoIpService
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(24);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeoIpService> _logger;
    private readonly IMemoryCache _cache;
    private readonly SemaphoreSlim _concurrency = new(4, 4);
    private readonly Queue<DateTimeOffset> _requests = new();
    private readonly object _rateLock = new();

    public GeoIpService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<GeoIpService> logger, IMemoryCache cache)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _cache = cache;
    }

    public async Task<GeoLocation?> LookupAsync(string? value, CancellationToken cancellationToken)
    {
        if (!IPAddress.TryParse(value, out var address) || !IsPublic(address)) return null;
        var key = address.ToString();
        if (_cache.TryGetValue<GeoLocation>(key, out var cached)) return cached;

        var template = _configuration["GeoIp:HttpsEndpointTemplate"];
        if (string.IsNullOrWhiteSpace(template)) return null; // Explicitly opt-in.
        if (!template.Contains("{ip}", StringComparison.Ordinal))
        {
            _logger.LogWarning("GeoIp endpoint must contain the {{ip}} placeholder.");
            return null;
        }

        var url = template.Replace("{ip}", Uri.EscapeDataString(key), StringComparison.Ordinal);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            _logger.LogWarning("GeoIp enrichment was skipped because its endpoint is not HTTPS.");
            return null;
        }

        try
        {
            if (!TryAcquireRateSlot()) return null;
            await _concurrency.WaitAsync(cancellationToken);
            try
            {
            using var response = await _httpClientFactory.CreateClient("GeoIp").GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = json.RootElement;
            var location = new GeoLocation(ReadString(root, "country"), ReadString(root, "city"));
            _cache.Set(key, location, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheLifetime,
                Size = 1
            });
            return location;
            }
            finally
            {
                _concurrency.Release();
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogDebug(ex, "GeoIp enrichment failed for {Address}.", key);
            return null;
        }
    }

    private bool TryAcquireRateSlot()
    {
        lock (_rateLock)
        {
            var cutoff = DateTimeOffset.UtcNow.AddMinutes(-1);
            while (_requests.Count > 0 && _requests.Peek() < cutoff) _requests.Dequeue();
            if (_requests.Count >= 60) return false;
            _requests.Enqueue(DateTimeOffset.UtcNow);
            return true;
        }
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool IsPublic(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast) return false;
        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return !(bytes[0] == 10 || bytes[0] == 127 || bytes[0] == 0 ||
                (bytes[0] == 169 && bytes[1] == 254) ||
                (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                (bytes[0] == 192 && bytes[1] == 168) ||
                bytes[0] >= 224);
        }
        return (bytes[0] & 0xFE) != 0xFC && bytes[0] != 0xFF;
    }
}
