using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using LightShield.Api.Data;
using LightShield.Api.Models;
using LightShield.Api.Services.Alerts;

namespace LightShield.Api.Services
{
    /// <summary>
    /// Service responsible for creating and sending alerts.
    /// </summary>
    public class AlertWriterService
    {
        private readonly EventsDbContext _db;
        private readonly IAlertService _alertService;
        private readonly ILogger<AlertWriterService> _logger;

        public AlertWriterService(EventsDbContext db, IAlertService alertService, ILogger<AlertWriterService> logger)
        {
            _db = db;
            _alertService = alertService;
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

            // Convert to LOCAL TIME for email/SMS
            var localTime = alert.Timestamp.ToLocalTime().ToString("f");

            // Construct notification message
            string formattedMessage =
                $"[LightShield Alert]\n" +
                $"Type: {alert.Type}\n" +
                $"Host: {alert.Hostname}\n" +
                $"When: {localTime} (local time)\n" +
                $"Details: {alert.Message}";

            try
            {
                await _alertService.SendAlertAsync(formattedMessage);

                _logger.LogInformation(
                    "Alert created + notification sent: {Message}",
                    formattedMessage
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification for alert.");
            }
        }

    }
}