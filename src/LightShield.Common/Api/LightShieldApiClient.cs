using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LightShield.Common.Models;

namespace LightShield.Common.Api
{
    public class LightShieldApiClient
    {
        private readonly HttpClient _http;
        private readonly string _eventsEndpoint;

        public LightShieldApiClient(HttpClient http, string baseUrl)
        {
            _http = http;
            _eventsEndpoint = $"{baseUrl.TrimEnd('/')}/api/events";
        }

        public async Task<bool> PostEventAsync(Event evt)
        {
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    var resp = await _http.PostAsJsonAsync(_eventsEndpoint, evt);

                    if (!resp.IsSuccessStatusCode)
                    {
                        var body = await resp.Content.ReadAsStringAsync();
                        Console.WriteLine($"[Agent API Error] Attempt={attempt} Status={resp.StatusCode} Body={body}");
                        return false;
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Agent Exception] Attempt={attempt} Error={ex.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                }
            }

            return false;
        }

    }
}
