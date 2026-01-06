using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
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
        private readonly ILogger<AlertWriterService> _logger;

        public AlertWriterService(
            EventsDbContext db,
            IAlertService alertService,
            ConfigurationService config,
            ILogger<AlertWriterService> logger)

        {
            _db = db;
            _alertService = alertService;
            _config = config;
            _logger = logger;
        }

        public async Task CreateAndSendAlertAsync(
            string type,
            string description,
            string hostname,
            string phase)
        {
            // ----------------------------------------------------
            // Deduplicate START only
            // ----------------------------------------------------
            if (phase == "START")
            {
                var recentStart = await _db.Alerts.AnyAsync(a =>
                    a.Type == type &&
                    a.Hostname == hostname &&
                    a.Phase == "START" &&
                    a.Timestamp > DateTime.UtcNow.AddSeconds(-30));

                if (recentStart)
                {
                    _logger.LogWarning(
                        "Alert suppressed (duplicate START within 30s): {Type} on {Host}",
                        type,
                        hostname
                    );
                    return;
                }
            }

            // ----------------------------------------------------
            // Save alert to DB
            // ----------------------------------------------------
            var alert = new Alert
            {
                Timestamp = DateTime.UtcNow,
                Type = type,
                Phase = phase,
                Message = description,
                Hostname = hostname,
                Channel = "Email/SMS"
            };

            _db.Alerts.Add(alert);
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Alert saved. Type={Type}, Phase={Phase}, Host={Host}",
                alert.Type,
                alert.Phase,
                alert.Hostname
            );

            // ----------------------------------------------------
            // Build message
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
            // Delivery config
            // ----------------------------------------------------
            string? email = await _config.GetEmailAsync();
            string? phone = await _config.GetPhoneAsync();

            if (string.IsNullOrWhiteSpace(email) &&
                string.IsNullOrWhiteSpace(phone))
            {
                _logger.LogWarning("Alert generated but no delivery channels configured.");
                return;
            }

            // ----------------------------------------------------
            // Send notification
            // ----------------------------------------------------
            try
            {
                await _alertService.SendAlertAsync(email, phone, formattedMessage);

                _logger.LogInformation(
                    "Alert delivered. Type={Type}, Phase={Phase}, Host={Host}",
                    alert.Type,
                    alert.Phase,
                    alert.Hostname
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Alert saved but failed to deliver."
                );
            }
        }
    }
}
