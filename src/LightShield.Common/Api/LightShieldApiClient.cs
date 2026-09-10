using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LightShield.Common.Models;

namespace LightShield.Common.Api;

public sealed class LightShieldApiClient
{
    private const int MaxQueueLength = 10_000;
    private readonly HttpClient _http;
    private readonly string _eventsEndpoint;
    private readonly string _queueFilePath;
    private readonly SemaphoreSlim _deliveryLock = new(1, 1);

    public LightShieldApiClient(HttpClient http, string baseUrl, string apiKey, string queueFilePath)
    {
        _http = http;
        _eventsEndpoint = $"{baseUrl.TrimEnd('/')}/api/events";
        _queueFilePath = queueFilePath;
        _http.DefaultRequestHeaders.Add("X-LightShield-Api-Key", apiKey);
    }

    public async Task<bool> PostEventAsync(Event evt)
    {
        await _deliveryLock.WaitAsync();
        try
        {
            await DrainQueueAsync();
            var result = await SendWithRetryAsync(evt);
            if (result == DeliveryResult.TransientFailure)
                await EnqueueAsync(evt);

            return result == DeliveryResult.Success;
        }
        finally
        {
            _deliveryLock.Release();
        }
    }

    private async Task<DeliveryResult> SendWithRetryAsync(Event evt)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                using var response = await _http.PostAsJsonAsync(_eventsEndpoint, evt);
                if (response.IsSuccessStatusCode)
                    return DeliveryResult.Success;

                var body = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[Agent API Error] Attempt={attempt} Status={response.StatusCode} Body={body}");
                if (!IsTransient(response.StatusCode))
                    return DeliveryResult.PermanentFailure;

                if (attempt < 3)
                    await Task.Delay(GetRetryDelay(response, attempt));
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                Console.WriteLine($"[Agent Exception] Attempt={attempt} Error={ex.Message}");
                if (attempt < 3)
                    await Task.Delay(GetBackoffWithJitter(attempt));
            }
        }

        return DeliveryResult.TransientFailure;
    }

    private async Task DrainQueueAsync()
    {
        if (!File.Exists(_queueFilePath))
            return;

        var lines = await File.ReadAllLinesAsync(_queueFilePath);
        var remaining = new List<string>();

        for (var index = 0; index < lines.Length; index++)
        {
            Event? queuedEvent;
            try
            {
                queuedEvent = JsonSerializer.Deserialize<Event>(lines[index]);
            }
            catch (JsonException)
            {
                continue;
            }

            if (queuedEvent == null)
                continue;

            var result = await SendWithRetryAsync(queuedEvent);
            if (result == DeliveryResult.TransientFailure)
            {
                remaining.AddRange(lines.Skip(index));
                break;
            }
        }

        await RewriteQueueAsync(remaining);
    }

    private async Task EnqueueAsync(Event evt)
    {
        var queued = File.Exists(_queueFilePath)
            ? (await File.ReadAllLinesAsync(_queueFilePath)).ToList()
            : [];

        queued.Add(JsonSerializer.Serialize(evt));
        if (queued.Count > MaxQueueLength)
            queued = queued.Skip(queued.Count - MaxQueueLength).ToList();

        await RewriteQueueAsync(queued);
        Console.WriteLine($"[Agent Queue] Event retained for retry. Pending={queued.Count}");
    }

    private async Task RewriteQueueAsync(IReadOnlyCollection<string> lines)
    {
        var directory = Path.GetDirectoryName(_queueFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var temporaryPath = _queueFilePath + ".tmp";
        await File.WriteAllLinesAsync(temporaryPath, lines);
        File.Move(temporaryPath, _queueFilePath, true);
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.RequestTimeout ||
        statusCode == HttpStatusCode.TooManyRequests ||
        (int)statusCode >= 500;

    private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
            return CapRetryDelay(delta);

        if (response.Headers.RetryAfter?.Date is { } date)
        {
            var delay = date - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
                return CapRetryDelay(delay);
        }

        return GetBackoffWithJitter(attempt);
    }

    private static TimeSpan CapRetryDelay(TimeSpan delay) =>
        delay > TimeSpan.FromSeconds(30) ? TimeSpan.FromSeconds(30) : delay;

    private static TimeSpan GetBackoffWithJitter(int attempt) =>
        TimeSpan.FromMilliseconds(
            Math.Min(30_000, Math.Pow(2, attempt - 1) * 1_000) + Random.Shared.Next(100, 500));

    private enum DeliveryResult
    {
        Success,
        TransientFailure,
        PermanentFailure
    }
}
