using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LightShield.Api.Data;
using LightShield.Api.Models;
using LightShield.Api.Services.Alerts;

namespace LightShield.Api.Services
{
    public class AlertWriterService
    {
        private readonly EventsDbContext _db;
        private readonly IAlertService _alertService;
        private readonly ConfigurationService _config;
        private readonly IServiceProvider _services;
        private readonly ILogger<AlertWriterService> _logger;

        public AlertWriterService(
            EventsDbContext db,
            IAlertService alertService,
            ConfigurationService config,
            IServiceProvider services,
            ILogger<AlertWriterService> logger)
        {
            _db = db;
            _alertService = alertService;
            _config = config;
            _services = services;
            _logger = logger;
        }

        public async Task CreateAndSendAlertAsync(
            string type,
            string description,
            string hostname,
            string phase)
        {
            

            // ----------------------------------------------------
            // Persist alert (ALWAYS)
            // ----------------------------------------------------
            var alert = new Alert
            {
                Timestamp = DateTime.UtcNow,
                Type = type,
                Phase = phase,
                Message = description,
                Hostname = hostname,
                Channel = "Email,Telegram"
            };

            _db.Alerts.Add(alert);
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Alert saved. Type={Type}, Phase={Phase}, Host={Host}",
                type, phase, hostname);

            // ----------------------------------------------------
            // Format message
            // ----------------------------------------------------
            string localTime = alert.Timestamp.ToLocalTime().ToString("f");

            string formattedMessage =
                $"[LightShield Alert]\n" +
                $"Type: {alert.Type}\n" +
                $"Phase: {alert.Phase}\n" +
                $"Host: {alert.Hostname}\n" +
                $"When: {localTime}\n" +
                $"Details: {alert.Message}";

            // ----------------------------------------------------
            // Load delivery configuration (DB-driven)
            // ----------------------------------------------------
            string? email = await _config.GetEmailAsync();
            string? botToken = await _config.GetTelegramBotTokenAsync();
            string? chatId = await _config.GetTelegramChatIdAsync();

            bool hasEmail = !string.IsNullOrWhiteSpace(email);
            bool hasTelegram =
                !string.IsNullOrWhiteSpace(botToken) &&
                !string.IsNullOrWhiteSpace(chatId);

            // ----------------------------------------------------
            // No delivery channels configured -> stop here
            // ----------------------------------------------------
            if (!hasEmail && !hasTelegram)
            {
                _logger.LogWarning(
                    "Alert saved but no delivery channels configured. Type={Type}, Phase={Phase}",
                    type, phase);
                return;
            }

            bool delivered = false;

            // ----------------------------------------------------
            // EMAIL delivery
            // ----------------------------------------------------
            if (hasEmail)
            {
                try
                {
                    await _alertService.SendAlertAsync(
                        email,
                        phone: null, // SMS optional / deprecated
                        formattedMessage);

                    delivered = true;
                    _logger.LogInformation("Email alert delivered.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Email delivery failed.");
                }
            }

            // ----------------------------------------------------
            // TELEGRAM delivery (SAFE, per-user, DB-driven)
            // ----------------------------------------------------
            if (hasTelegram)
            {
                try
                {
                    var telegram =
                        _services.GetRequiredService<TelegramAlertService>();

                    await telegram.SendAsync(
                        botToken!,
                        chatId!,
                        formattedMessage);

                    delivered = true;
                    _logger.LogInformation("Telegram alert delivered.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Telegram delivery failed.");
                }
            }

            // ----------------------------------------------------
            // Final delivery check
            // ----------------------------------------------------
            if (!delivered)
            {
                _logger.LogWarning(
                    "Alert saved but delivery failed on all channels. Type={Type}, Host={Host}",
                    type, hostname);
            }
        }
    }
}
