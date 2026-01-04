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
                    e.Type == "failedlogin" &&
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
                    var newestEventTime = group.Max(e => e.Timestamp);

                    var incident = await db.IncidentStates
                        .FirstOrDefaultAsync(i =>
                            i.Type == "FailedLoginBurst" &&
                            i.Hostname == group.Key,
                            stoppingToken);

                    
                    if (incident != null && newestEventTime <= incident.LastEventTime)
                        continue;

                    await HandleIncidentAsync(
                        db,
                        alertWriter,
                        group.Key,
                        "FailedLoginBurst",
                        count,
                        newestEventTime,
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

            const int CREATE_THRESHOLD = 75;
            const int MODIFY_THRESHOLD = 100;
            const int DELETE_THRESHOLD = 20;
            const int RENAME_THRESHOLD = 10;

            var quietTime = TimeSpan.FromSeconds(120);

            foreach (var group in groupedByHost)
            {
                string hostname = group.Key;

                int creates = group.Count(e => e.Type == "filecreate");
                int modifies = group.Count(e => e.Type == "filemodify");
                int deletes = group.Count(e => e.Type == "filedelete");
                int renames = group.Count(e => e.Type == "filerename");

                // ============================
                // FILE DELETE BURST
                // ============================
                if (deletes >= DELETE_THRESHOLD)
                {
                    var newestEventTime = group.Max(e => e.Timestamp);

                    var incident = await db.IncidentStates
                        .FirstOrDefaultAsync(i =>
                            i.Type == "FileDeleteBurst" &&
                            i.Hostname == hostname,
                            stoppingToken);

                    if (incident != null && newestEventTime <= incident.LastEventTime)
                        continue;

                    await HandleIncidentAsync(
                        db,
                        alertWriter,
                        hostname,
                        "FileDeleteBurst",
                        deletes,
                        newestEventTime,
                        quietTime,
                        stoppingToken
                    );
                }

                // ============================
                // FILE MODIFY BURST
                // ============================
                if (modifies >= MODIFY_THRESHOLD)
                {
                    var newestEventTime = group.Max(e => e.Timestamp);

                    var incident = await db.IncidentStates
                        .FirstOrDefaultAsync(i =>
                            i.Type == "FileModifyBurst" &&
                            i.Hostname == hostname,
                            stoppingToken);

                    if (incident != null && newestEventTime <= incident.LastEventTime)
                        continue;

                    await HandleIncidentAsync(
                        db,
                        alertWriter,
                        hostname,
                        "FileModifyBurst",
                        modifies,
                        newestEventTime,
                        quietTime,
                        stoppingToken
                    );
                }

                // ============================
                // FILE CREATE BURST
                // ============================
                if (creates >= CREATE_THRESHOLD)
                {
                    var newestEventTime = group.Max(e => e.Timestamp);

                    var incident = await db.IncidentStates
                        .FirstOrDefaultAsync(i =>
                            i.Type == "FileCreateBurst" &&
                            i.Hostname == hostname,
                            stoppingToken);

                    if (incident != null && newestEventTime <= incident.LastEventTime)
                        continue;

                    await HandleIncidentAsync(
                        db,
                        alertWriter,
                        hostname,
                        "FileCreateBurst",
                        creates,
                        newestEventTime,
                        quietTime,
                        stoppingToken
                    );

                }

                // ============================
                // FILE RENAME BURST
                // ============================
                if (renames >= RENAME_THRESHOLD)
                {
                    var newestEventTime = group.Max(e => e.Timestamp);


                    var incident = await db.IncidentStates
                        .FirstOrDefaultAsync(i =>
                            i.Type == "FileRenameBurst" &&
                            i.Hostname == hostname,
                            stoppingToken);

                    if (incident != null && newestEventTime <= incident.LastEventTime)
                        continue;

                    await HandleIncidentAsync(
                        db,
                        alertWriter,
                        hostname,
                        "FileRenameBurst",
                        renames,
                        newestEventTime,
                        quietTime,
                        stoppingToken
                    );

                }

                // ============================
                // RANSOMWARE / ENCRYPTION BURST
                // ============================
                if (modifies >= MODIFY_THRESHOLD / 2 &&
                    renames >= RENAME_THRESHOLD / 2)
                {
                    var newestEventTime = group.Max(e => e.Timestamp);

                    var incident = await db.IncidentStates
                        .FirstOrDefaultAsync(i =>
                            i.Type == "FileEncryptionBurst" &&
                            i.Hostname == hostname,
                            stoppingToken);

                    if (incident != null && newestEventTime <= incident.LastEventTime)
                        continue;

                    await HandleIncidentAsync(
                        db,
                        alertWriter,
                        hostname,
                        "FileEncryptionBurst",
                        modifies + renames,
                        newestEventTime,
                        quietTime,
                        stoppingToken
                    );
                }
            }

            // ============================
            // CLOSE STALE INCIDENTS
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
            DateTime newestEventTime,
            TimeSpan quietTime,
            CancellationToken token)
        {
            var now = DateTime.UtcNow;

            var incident = await db.IncidentStates
                .FirstOrDefaultAsync(i =>
                    i.Type == type &&
                    i.Hostname == hostname,
                    token);






            // ============================
            // OPEN INCIDENT
            // ============================
            bool isNewIncident =
                incident == null ||
                (newestEventTime - incident.LastEventTime) > quietTime;



            if (isNewIncident)
            {
                if (incident == null)
                {
                    // First-ever incident for this host/type
                    incident = new IncidentState
                    {
                        Type = type,
                        Hostname = hostname
                    };

                    db.IncidentStates.Add(incident);
                }

                // RESET incident state
                incident.StartTime = newestEventTime;
                incident.LastEventTime = newestEventTime;
                incident.Count = currentCount;
                incident.IsActive = true;

                db.Anomalies.Add(new Anomaly
                {
                    Timestamp = now,
                    Type = type,
                    Description = $"Incident STARTED. Initial count: {currentCount}.",
                    Hostname = hostname
                });

                await db.SaveChangesAsync(token);

                await alertWriter.CreateAndSendAlertAsync(
                    type,
                    $"Incident STARTED at {now:u}. Initial count: {currentCount}.",
                    hostname,
                    "START"
                );

                return;
            }
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

                await alertWriter.CreateAndSendAlertAsync(
                    incident.Type,
                    $"Incident ended. Total events: {incident.Count}. " +
                    $"Duration: {(incident.LastEventTime - incident.StartTime).TotalSeconds:F1}s",
                    incident.Hostname,
                    "END"


                );

                _logger.LogInformation(
                   "Incident CLOSED: {Type} on {Host} (Total={Count})",
                   incident.Type, incident.Hostname, incident.Count
               );
            }

            await db.SaveChangesAsync(token);

        }


        private async Task<bool> IsIncidentActiveAsync(
            EventsDbContext db,
            string type,
            string hostname,
            CancellationToken token)
        {
            return await db.IncidentStates.AnyAsync(i =>
                i.Type == type &&
                i.Hostname == hostname &&
                i.IsActive,
                token);
        }

    }
}
