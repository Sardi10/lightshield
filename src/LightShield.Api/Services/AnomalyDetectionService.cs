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

        public AnomalyDetectionService(
            IServiceProvider services,
            ILogger<AnomalyDetectionService> logger)
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

        // =============================================================
        // FAILED LOGIN BURST
        // =============================================================
        private async Task DetectFailedLoginBursts(CancellationToken token)
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
            var alertWriter = scope.ServiceProvider.GetRequiredService<AlertWriterService>();

            var cutoff = DateTime.UtcNow.AddMinutes(-5);

            var events = await db.Events
                .Where(e => e.Type == "failedlogin" && e.Timestamp >= cutoff)
                .ToListAsync(token);

            const int THRESHOLD = 5;

            var groupedByHost = events.GroupBy(e => e.Hostname).ToList();

            foreach (var group in groupedByHost)
            {
                var hostname = group.Key;
                var count = group.Count();
                var newestEventTime = group.Max(e => e.Timestamp);

                _logger.LogInformation(
                    "FailedLogin host={Host} count={Count}",
                    hostname, count);

                var incident = await db.IncidentStates
                    .FirstOrDefaultAsync(i =>
                        i.Type == "FailedLoginBurst" &&
                        i.Hostname == hostname,
                        token);

                if (count >= THRESHOLD)
                {
                    await HandleIncidentAsync(
                        db, alertWriter,
                        hostname,
                        "FailedLoginBurst",
                        count,
                        newestEventTime,
                        token);
                }
                else if (incident != null && incident.IsActive)
                {
                    await CloseIncidentAsync(db, alertWriter, incident, token);
                }
            }

            // close incidents for hosts with ZERO events
            await CloseMissingHostsAsync(
                db, alertWriter,
                "FailedLoginBurst",
                groupedByHost.Select(g => g.Key).ToList(),
                token);
        }

        // =============================================================
        // FILE TAMPER BURSTS
        // =============================================================
        private async Task DetectFileTamperBursts(CancellationToken token)
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();
            var alertWriter = scope.ServiceProvider.GetRequiredService<AlertWriterService>();

            var cutoff = DateTime.UtcNow.AddMinutes(-5);

            var events = await db.Events
                .Where(e =>
                    (e.Type == "filecreate" ||
                     e.Type == "filemodify" ||
                     e.Type == "filedelete" ||
                     e.Type == "filerename") &&
                    e.Timestamp >= cutoff)
                .ToListAsync(token);

            const int CREATE_THRESHOLD = 75;
            const int MODIFY_THRESHOLD = 100;
            const int DELETE_THRESHOLD = 20;
            const int RENAME_THRESHOLD = 10;

            var groupedByHost = events.GroupBy(e => e.Hostname).ToList();

            foreach (var group in groupedByHost)
            {
                var hostname = group.Key;
                var newestEventTime = group.Max(e => e.Timestamp);

                int creates = group.Count(e => e.Type == "filecreate");
                int modifies = group.Count(e => e.Type == "filemodify");
                int deletes = group.Count(e => e.Type == "filedelete");
                int renames = group.Count(e => e.Type == "filerename");

                _logger.LogInformation(
                    "FileBurst host={Host} c={C} m={M} d={D} r={R}",
                    hostname, creates, modifies, deletes, renames);

                await EvaluateBurst(db, alertWriter, hostname, "FileCreateBurst", creates, CREATE_THRESHOLD, newestEventTime, token);
                await EvaluateBurst(db, alertWriter, hostname, "FileModifyBurst", modifies, MODIFY_THRESHOLD, newestEventTime, token);
                await EvaluateBurst(db, alertWriter, hostname, "FileDeleteBurst", deletes, DELETE_THRESHOLD, newestEventTime, token);
                await EvaluateBurst(db, alertWriter, hostname, "FileRenameBurst", renames, RENAME_THRESHOLD, newestEventTime, token);

                int encryptionScore = modifies + renames;
                int encryptionThreshold = (MODIFY_THRESHOLD / 2) + (RENAME_THRESHOLD / 2);

                await EvaluateBurst(db, alertWriter, hostname, "FileEncryptionBurst", encryptionScore, encryptionThreshold, newestEventTime, token);
            }

            await CloseMissingHostsAsync(
                db, alertWriter,
                typePrefix: "File",
                activeHosts: groupedByHost.Select(g => g.Key).ToList(),
                token);
        }

        // =============================================================
        // START / CONTINUE
        // =============================================================
        private async Task HandleIncidentAsync(
    EventsDbContext db,
    AlertWriterService alertWriter,
    string hostname,
    string type,
    int currentCount,
    DateTime newestEventTime,
    CancellationToken token)
        {
            var incident = await db.IncidentStates
                .FirstOrDefaultAsync(i => i.Type == type && i.Hostname == hostname, token);

            // =============================================================
            // START NEW OR RESTART CLOSED INCIDENT
            // =============================================================
            if (incident == null || !incident.IsActive)
            {
                if (incident == null)
                {
                    incident = new IncidentState
                    {
                        Type = type,
                        Hostname = hostname
                    };
                    db.IncidentStates.Add(incident);
                }

                incident.IsActive = true;
                incident.StartTime = newestEventTime;
                incident.LastEventTime = newestEventTime;
                incident.Count = currentCount;

                db.Anomalies.Add(new Anomaly
                {
                    Timestamp = DateTime.UtcNow,
                    Type = type,
                    Hostname = hostname,
                    Description = $"Incident STARTED. Initial count: {currentCount}."
                });

                await db.SaveChangesAsync(token);

                await alertWriter.CreateAndSendAlertAsync(
                    type,
                    $"Incident STARTED at {newestEventTime:u}. Initial count: {currentCount}.",
                    hostname,
                    "START"
                );

                return;
            }

            // =============================================================
            // CONTINUE ACTIVE INCIDENT
            // =============================================================
            if (newestEventTime > incident.LastEventTime)
            {
                incident.LastEventTime = newestEventTime;
                incident.Count = Math.Max(incident.Count, currentCount);
                await db.SaveChangesAsync(token);
            }
        }


        // =============================================================
        // CLOSE
        // =============================================================
        private async Task CloseIncidentAsync(
            EventsDbContext db,
            AlertWriterService alertWriter,
            IncidentState incident,
            CancellationToken token)
        {
            _logger.LogInformation(
                "Closing incident {Type} on {Host}",
                incident.Type, incident.Hostname);

            incident.IsActive = false;

            db.Anomalies.Add(new Anomaly
            {
                Timestamp = DateTime.UtcNow,
                Type = incident.Type,
                Hostname = incident.Hostname,
                Description =
                    $"Incident ENDED. Total events: {incident.Count}. " +
                    $"Duration: {(incident.LastEventTime - incident.StartTime).TotalSeconds:F1}s"
            });

            await alertWriter.CreateAndSendAlertAsync(
                incident.Type,
                $"Incident ENDED. Total events: {incident.Count}.",
                incident.Hostname,
                "END");

            await db.SaveChangesAsync(token);
        }

        // =============================================================
        // EVALUATE ONE BURST
        // =============================================================
        private async Task EvaluateBurst(
            EventsDbContext db,
            AlertWriterService alertWriter,
            string hostname,
            string type,
            int count,
            int threshold,
            DateTime newestEventTime,
            CancellationToken token)
        {
            var incident = await db.IncidentStates
                .FirstOrDefaultAsync(i => i.Type == type && i.Hostname == hostname, token);

            if (count >= threshold)
            {
                await HandleIncidentAsync(db, alertWriter, hostname, type, count, newestEventTime, token);
            }
            else if (incident != null && incident.IsActive)
            {
                await CloseIncidentAsync(db, alertWriter, incident, token);
            }
        }

        // =============================================================
        // CLOSE INCIDENTS FOR HOSTS WITH ZERO EVENTS
        // =============================================================
        private async Task CloseMissingHostsAsync(
            EventsDbContext db,
            AlertWriterService alertWriter,
            string typePrefix,
            System.Collections.Generic.List<string> activeHosts,
            CancellationToken token)
        {
            var activeIncidents = await db.IncidentStates
                .Where(i =>
                    i.IsActive &&
                    (typePrefix == "File"
                        ? i.Type.StartsWith("File")
                        : i.Type == typePrefix))
                .ToListAsync(token);

            foreach (var incident in activeIncidents)
            {
                if (!activeHosts.Contains(incident.Hostname))
                {
                    await CloseIncidentAsync(db, alertWriter, incident, token);
                }
            }
        }
    }
}
