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

            string localTime = alert.Timestamp.ToLocalTime().ToString("f");

            string formattedMessage =
                $"[LightShield Alert]\n" +
                $"Type: {alert.Type}\n" +
                $"Host: {alert.Hostname}\n" +
                $"When: {localTime} (local time)\n" +
                $"Details: {alert.Message}";

            try
            {
                // Get dynamic email + phone from DB
                var email = await _config.GetEmailAsync();
                var phone = await _config.GetPhoneAsync();

                // Pass into composite alert service
                await _alertService.SendAlertAsync(email, phone, formattedMessage);

                _logger.LogInformation("Alert created + notification sent: {Message}", formattedMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification for alert.");
            }
        }
    }
}
