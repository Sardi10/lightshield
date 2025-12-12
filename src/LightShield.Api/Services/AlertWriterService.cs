using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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

        public async Task CreateAndSendAlertAsync(string type, string description, string hostname)
        {
            // --------------------------------------------
            // 1) Save alert to DB
            // --------------------------------------------
            var alert = new Alert
            {
                Timestamp = DateTime.UtcNow,
                Type = type,
                Message = description,
                Hostname = hostname,
                Channel = "Email/SMS"
            };

            _db.Alerts.Add(alert);
            await _db.SaveChangesAsync();

            // --------------------------------------------
            // 2) Format message for SMS + email
            // --------------------------------------------
            string localTime = alert.Timestamp.ToLocalTime().ToString("f");

            string formattedMessage =
                $"[LightShield Alert]\n" +
                $"Type: {alert.Type}\n" +
                $"Host: {alert.Hostname}\n" +
                $"When: {localTime} (local time)\n" +
                $"Details: {alert.Message}";

            // --------------------------------------------
            // 3) Retrieve dynamic user configuration
            // --------------------------------------------
            string? email = await _config.GetEmailAsync();
            string? phone = await _config.GetPhoneAsync();

            if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phone))
            {
                _logger.LogWarning(
                    "Alert generated but no delivery channels configured. User email/phone not set."
                );
                return;
            }

            // --------------------------------------------
            // 4) Deliver alert through CompositeAlertService
            // --------------------------------------------
            try
            {
                await _alertService.SendAlertAsync(email, phone, formattedMessage);

                _logger.LogInformation(
                    "Alert created + notification delivered. Type={Type}, Host={Host}",
                    alert.Type,
                    alert.Hostname
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Alert was saved to DB but FAILED to deliver via email/SMS."
                );
            }
        }
    }
}
