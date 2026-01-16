using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LightShield.Api.Services.Alerts
{
    public class TelegramAlertService
    {
        private readonly HttpClient _http;
        private readonly ILogger<TelegramAlertService> _logger;

        public TelegramAlertService(
            IHttpClientFactory httpFactory,
            ILogger<TelegramAlertService> logger)
        {
            _http = httpFactory.CreateClient();
            _logger = logger;
        }

        public async Task SendAsync(
            string botToken,
            string chatId,
            string message)
        {
            if (string.IsNullOrWhiteSpace(botToken) ||
                string.IsNullOrWhiteSpace(chatId))
            {
                _logger.LogWarning("Telegram alert skipped (missing credentials).");
                return;
            }

            var url = $"https://api.telegram.org/bot{botToken}/sendMessage";

            var payload = new
            {
                chat_id = chatId,
                text = message
            };

            var json = JsonSerializer.Serialize(payload);

            var res = await _http.PostAsync(
                url,
                new StringContent(json, Encoding.UTF8, "application/json"));

            if (!res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Telegram alert failed: {Status} {Body}",
                    res.StatusCode,
                    body);
            }
            else
            {
                _logger.LogInformation("Telegram alert sent successfully.");
            }
        }
    }
}
