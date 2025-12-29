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

            var failedLogins = await db.Events
                .Where(e =>
                    e.Type == "FailedLogin" &&
                    e.Timestamp >= cutoff)
                .ToListAsync(stoppingToken);

            var groupedByHost = failedLogins.GroupBy(e => e.Hostname);

            const int FAILED_LOGIN_THRESHOLD = 5;
            var quietTime = TimeSpan.FromSeconds(60);

            foreach (var group in groupedByHost)
            {
                int count = group.Count();

                if (count >= FAILED_LOGIN_THRESHOLD)
                {
                    await HandleIncidentAsync(
                        db,
                        alertWriter,
                        group.Key,
                        "FailedLoginBurst",
                        count,
                        quietTime,
                        stoppingToken
                    );
                }
            }

            await CloseStaleIncidentsAsync(
                db,
                alertWriter,
                "FailedLoginBurst",
                quietTime,
                stoppingToken
            );

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
                     e.Type == "filerename") &&
                    e.Timestamp >= cutoff)
                .ToListAsync(stoppingToken);

            var groupedByHost = recentFileEvents.GroupBy(e => e.Hostname);

            // thresholds (same as before)
            const int CREATE_THRESHOLD = 75;
            const int MODIFY_THRESHOLD = 100;
            const int DELETE_THRESHOLD = 20;
            const int RENAME_THRESHOLD = 10;

            var quietTime = TimeSpan.FromSeconds(15);

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
                    await HandleIncidentAsync(
                        db,
                        alertWriter,
                        group.Key,
                        "FileCreateBurst",
                        creates,
                        quietTime,
                        stoppingToken
                    );
                }

                // ============================
                // FILE MODIFY BURST
                // ============================
                if (modifies >= MODIFY_THRESHOLD)
                {
                    await HandleIncidentAsync(
                        db,
                        alertWriter,
                        group.Key,
                        "FileModifyBurst",
                        modifies,
                        quietTime,
                        stoppingToken
                    );
                }

                // ============================
                // FILE DELETE BURST
                // ============================
                if (deletes >= DELETE_THRESHOLD)
                {
                    await HandleIncidentAsync(
                        db,
                        alertWriter,
                        group.Key,
                        "FileDeleteBurst",
                        deletes,
                        quietTime,
                        stoppingToken
                    );
                }

                // ============================
                // FILE RENAME BURST
                // ============================
                if (renames >= RENAME_THRESHOLD)
                {
                    await HandleIncidentAsync(
                        db,
                        alertWriter,
                        group.Key,
                        "FileRenameBurst",
                        renames,
                        quietTime,
                        stoppingToken
                    );
                }

                // ============================
                // RANSOMWARE / ENCRYPTION
                // ============================
                if (modifies >= MODIFY_THRESHOLD / 2 &&
                    renames >= RENAME_THRESHOLD / 2)
                {
                    await HandleIncidentAsync(
                        db,
                        alertWriter,
                        group.Key,
                        "FileEncryptionBurst",
                        modifies + renames,
                        quietTime,
                        stoppingToken
                    );
                }
            }

            // ============================
            // CLOSE INCIDENTS AFTER QUIET TIME
            // ============================
            await CloseStaleIncidentsAsync(db, alertWriter, "FileCreateBurst", quietTime, stoppingToken);
            await CloseStaleIncidentsAsync(db, alertWriter, "FileModifyBurst", quietTime, stoppingToken);
            await CloseStaleIncidentsAsync(db, alertWriter, "FileDeleteBurst", quietTime, stoppingToken);
            await CloseStaleIncidentsAsync(db, alertWriter, "FileRenameBurst", quietTime, stoppingToken);
            await CloseStaleIncidentsAsync(db, alertWriter, "FileEncryptionBurst", quietTime, stoppingToken);

        }


        private async Task HandleIncidentAsync(
            EventsDbContext db,
            AlertWriterService alertWriter,
            string hostname,
            string type,
            int currentCount,
            TimeSpan quietTime,
            CancellationToken token)
        {
            var now = DateTime.UtcNow;

            var incident = await db.IncidentStates
                .FirstOrDefaultAsync(i =>
                    i.Type == type &&
                    i.Hostname == hostname &&
                    i.IsActive,
                    token);

            // ============================
            // OPEN INCIDENT
            // ============================
            if (incident == null)
            {
                incident = new IncidentState
                {
                    Type = type,
                    Hostname = hostname,
                    StartTime = now,
                    LastEventTime = now,
                    Count = currentCount,
                    IsActive = true
                };

                db.IncidentStates.Add(incident);
                await db.SaveChangesAsync(token);

                await alertWriter.CreateAndSendAlertAsync(
                    type,
                    $"Incident started: {currentCount} events detected.",
                    hostname
                );

                _logger.LogWarning(
                    "Incident OPENED: {Type} on {Host} ({Count} events)",
                    type, hostname, currentCount
                );

                return;
            }

            // ============================
            // UPDATE INCIDENT
            // ============================
            incident.LastEventTime = now;
            incident.Count += (currentCount - incident.Count > 0 ? currentCount - incident.Count : 0);

            await db.SaveChangesAsync(token);
        }

        private async Task CloseStaleIncidentsAsync(
            EventsDbContext db,
            AlertWriterService alertWriter,
            string incidentType,
            TimeSpan quietTime,
            CancellationToken token)

        {
            var now = DateTime.UtcNow;

            var candidates = await db.IncidentStates
                .Where(i =>
                    i.IsActive &&
                    i.Type == incidentType)
                .ToListAsync(token);

            var staleIncidents = candidates
                .Where(i => now - i.LastEventTime > quietTime)
                .ToList();



            foreach (var incident in staleIncidents)
            {
                incident.IsActive = false;
                await db.SaveChangesAsync(token);

                await alertWriter.CreateAndSendAlertAsync(
                    incident.Type,
                    $"Incident ended. Total events: {incident.Count}. " +
                    $"Duration: {(incident.LastEventTime - incident.StartTime).TotalSeconds:F1}s",
                    incident.Hostname
                );

                _logger.LogInformation(
                    "Incident CLOSED: {Type} on {Host} (Total={Count})",
                    incident.Type, incident.Hostname, incident.Count
                );
            }
        }


    }
}
