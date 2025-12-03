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
                .Where(e =>
                       (e.Type == "filecreate" ||
                        e.Type == "filemodify" ||
                        e.Type == "filedelete" ||
                        e.Type == "filerename")
                        && e.Timestamp >= cutoff)
                .ToListAsync(stoppingToken);

            var groupedByHost = recentFileEvents.GroupBy(e => e.Hostname);

            // thresholds — configurable if desired
            const int CREATE_THRESHOLD = 75;
            const int MODIFY_THRESHOLD = 100;
            const int DELETE_THRESHOLD = 20;
            const int RENAME_THRESHOLD = 10;

            foreach (var group in groupedByHost)
            {
                int creates = group.Count(e => e.Type == "filecreate");
                int modifies = group.Count(e => e.Type == "filemodify");
                int deletes = group.Count(e => e.Type == "filedelete");
                int renames = group.Count(e => e.Type == "filerename");

                // ============================
                // FILE CREATE BURST
                // ============================
                if (creates >= CREATE_THRESHOLD)
                {
                    await TriggerAnomaly(
                        alertWriter,
                        db,
                        group.Key,
                        "FileCreateBurst",
                        $"{creates} files created in last 5 minutes.",
                        stoppingToken
                    );
                }

                // ============================
                // FILE MODIFY BURST
                // ============================
                if (modifies >= MODIFY_THRESHOLD)
                {
                    await TriggerAnomaly(
                        alertWriter,
                        db,
                        group.Key,
                        "FileModifyBurst",
                        $"{modifies} files modified in last 5 minutes.",
                        stoppingToken
                    );
                }

                // ============================
                // FILE DELETE BURST
                // ============================
                if (deletes >= DELETE_THRESHOLD)
                {
                    await TriggerAnomaly(
                        alertWriter,
                        db,
                        group.Key,
                        "FileDeleteBurst",
                        $"{deletes} files deleted in last 5 minutes.",
                        stoppingToken
                    );
                }

                // ============================
                // FILE RENAME BURST
                // ============================
                if (renames >= RENAME_THRESHOLD)
                {
                    await TriggerAnomaly(
                        alertWriter,
                        db,
                        group.Key,
                        "FileRenameBurst",
                        $"{renames} files renamed in last 5 minutes.",
                        stoppingToken
                    );
                }

                // ============================
                // RANSOMWARE / ENCRYPTION DETECTION
                // ============================
                if (modifies >= MODIFY_THRESHOLD / 2 && renames >= RENAME_THRESHOLD / 2)
                {
                    await TriggerAnomaly(
                        alertWriter,
                        db,
                        group.Key,
                        "FileEncryptionBurst",
                        $"Possible ransomware: {modifies} modifications + {renames} renames in last 5 minutes.",
                        stoppingToken
                    );
                }
            }
        }

        // ===================================================================
        //  REUSABLE ANOMALY + ALERT CREATION HELPER
        // ===================================================================
        private async Task TriggerAnomaly(
            AlertWriterService alertWriter,
            EventsDbContext db,
            string hostname,
            string type,
            string description,
            CancellationToken token = default)
        {
            var anomaly = new Anomaly
            {
                Type = type,
                Description = description,
                Hostname = hostname,
                Timestamp = DateTime.UtcNow
            };

            db.Anomalies.Add(anomaly);
            await db.SaveChangesAsync(token);

            _logger.LogWarning(
                "Anomaly detected: {Description} on host {Hostname}",
                anomaly.Description,
                anomaly.Hostname
            );

            await alertWriter.CreateAndSendAlertAsync(
                anomaly.Type,
                anomaly.Description,
                anomaly.Hostname
            );
        }
    }
}
