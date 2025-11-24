using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LightShield.Api.Data;
using LightShield.Api.Models;
using LightShield.Api.Services.Alerts;

namespace LightShield.Api.Services
{
    public class AnomalyDetectionService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<AnomalyDetectionService> _logger;

        public AnomalyDetectionService(IServiceProvider services, ILogger<AnomalyDetectionService> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AnomalyDetectionService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await DetectFailedLoginBursts(stoppingToken);
                    await DetectFileTamperBursts(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during anomaly detection loop.");
                }

                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }

            _logger.LogInformation("AnomalyDetectionService stopped.");
        }

        // ===================================================================
        //  FAILED LOGIN BURST DETECTION
        // ===================================================================
        private async Task DetectFailedLoginBursts(CancellationToken stoppingToken)
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
            var alertWriter = scope.ServiceProvider.GetRequiredService<AlertWriterService>();

            var cutoff = DateTime.UtcNow.AddMinutes(-5);

            var recentFailedEvents = await db.Events
                .Where(e => e.Type == "FailedLogin" && e.Timestamp >= cutoff)
                .ToListAsync(stoppingToken);

            var groupedByHost = recentFailedEvents.GroupBy(e => e.Hostname);

            foreach (var group in groupedByHost)
            {
                if (group.Count() >= 5)
                {
                    var anomaly = new Anomaly
                    {
                        Type = "FailedLoginBurst",
                        Description = $"{group.Count()} failed login attempts in the last 5 minutes.",
                        Hostname = group.Key,
                        Timestamp = DateTime.UtcNow
                    };

                    db.Anomalies.Add(anomaly);
                    await db.SaveChangesAsync(stoppingToken);

                    _logger.LogWarning(
                        "Anomaly detected: {Description} on host {Hostname}",
                        anomaly.Description,
                        anomaly.Hostname
                    );

                    // CREATE ALERT (stored in Alerts table) + SEND EMAIL + SMS
                    await alertWriter.CreateAndSendAlertAsync(
                        anomaly.Type,
                        anomaly.Description,
                        anomaly.Hostname
                    );
                }
            }
        }

        // ===================================================================
        //  FILE TAMPER BURST DETECTION
        // ===================================================================
        private async Task DetectFileTamperBursts(CancellationToken stoppingToken)
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
            var alertWriter = scope.ServiceProvider.GetRequiredService<AlertWriterService>();

            var cutoff = DateTime.UtcNow.AddMinutes(-5);

            var recentFileEvents = await db.Events
                .Where(e => e.Type == "FileTamper" && e.Timestamp >= cutoff)
                .ToListAsync(stoppingToken);

            var groupedByHost = recentFileEvents.GroupBy(e => e.Hostname);

            foreach (var group in groupedByHost)
            {
                if (group.Count() >= 10)
                {
                    var anomaly = new Anomaly
                    {
                        Type = "FileTamperBurst",
                        Description = $"{group.Count()} suspicious file modifications in last 5 minutes.",
                        Hostname = group.Key,
                        Timestamp = DateTime.UtcNow
                    };

                    db.Anomalies.Add(anomaly);
                    await db.SaveChangesAsync(stoppingToken);

                    _logger.LogWarning(
                        "Anomaly detected: {Description} on host {Hostname}",
                        anomaly.Description,
                        anomaly.Hostname
                    );

                    // CREATE ALERT AND SEND NOTIFICATIONS
                    await alertWriter.CreateAndSendAlertAsync(
                        anomaly.Type,
                        anomaly.Description,
                        anomaly.Hostname
                    );
                }
            }
        }
    }
}
