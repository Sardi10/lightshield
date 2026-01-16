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

        private const double FILE_Z_THRESHOLD = 4.0;
        private const double RANSOMWARE_Z_SINGLE = 3.0;
        private const double RANSOMWARE_Z_COMBINED = 7.0;
        private const double RISK_ALERT_THRESHOLD = 10.0;

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
                    using var scope = _services.CreateScope();

                    var baselineService =
                        scope.ServiceProvider.GetRequiredService<FileBaselineService>();
                    await baselineService.UpdateBaselinesAsync(stoppingToken);

                    var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

                    await PruneOldEventsAsync(db, stoppingToken);

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

            var groupedByHost = events.GroupBy(e => e.Hostname).ToList();

            foreach (var group in groupedByHost)
            {
                var hostname = group.Key;
                var newestEventTime = group.Max(e => e.Timestamp);

                var baseline = await db.FileActivityBaselines
                    .FirstOrDefaultAsync(b => b.Hostname == hostname, token);

                if (baseline == null)
                    continue;

                // -------------------------------------------------
                // Baseline stabilization gate
                // -------------------------------------------------
                if (!baseline.DetectionEnabled)
                {
                    var hoursLearning =
                        (DateTime.UtcNow - baseline.FirstSeen).TotalHours;

                    bool hasVariance =
                        baseline.CreateStd > 0.001 ||
                        baseline.ModifyStd > 0.001 ||
                        baseline.DeleteStd > 0.001 ||
                        baseline.RenameStd > 0.001;

                    if (hoursLearning >= 12 && hasVariance)
                    {
                        baseline.DetectionEnabled = true;
                        await db.SaveChangesAsync(token);

                        _logger.LogInformation(
                            "Baseline stabilized for host={Host}. Detection ENABLED.",
                            hostname);
                    }
                    else
                    {
                        continue;
                    }
                }

                int creates = group.Count(e => e.Type == "filecreate");
                int modifies = group.Count(e => e.Type == "filemodify");
                int deletes = group.Count(e => e.Type == "filedelete");
                int renames = group.Count(e => e.Type == "filerename");

                double minutes = 5.0;

                double zCreate = ZScore(creates / minutes, baseline.CreateAvg, baseline.CreateStd);
                double zModify = ZScore(modifies / minutes, baseline.ModifyAvg, baseline.ModifyStd);
                double zDelete = ZScore(deletes / minutes, baseline.DeleteAvg, baseline.DeleteStd);
                double zRename = ZScore(renames / minutes, baseline.RenameAvg, baseline.RenameStd);

                // -------------------------------------------------
                // RISK ACCUMULATION (ONCE PER CYCLE)
                // -------------------------------------------------
                double cycleRiskDelta = 0;

                if (zCreate >= FILE_Z_THRESHOLD) cycleRiskDelta += 1.0;
                if (zModify >= FILE_Z_THRESHOLD) cycleRiskDelta += 1.0;
                if (zDelete >= FILE_Z_THRESHOLD) cycleRiskDelta += 3.0;
                if (zRename >= FILE_Z_THRESHOLD) cycleRiskDelta += 4.0;

                // -------------------------------------------------
                // RANSOMWARE CORRELATION (BONUS RISK)
                // -------------------------------------------------
                double ransomwareScore = zModify + zRename;

                if (zModify >= RANSOMWARE_Z_SINGLE &&
                    zRename >= RANSOMWARE_Z_SINGLE &&
                    ransomwareScore >= RANSOMWARE_Z_COMBINED)
                {
                    cycleRiskDelta += 5.0; // correlation bonus
                }

                if (cycleRiskDelta <= 0)
                    continue;

                double risk = await UpdateHostRiskAsync(
                    db,
                    hostname,
                    cycleRiskDelta,
                    token);

                _logger.LogInformation(
                    "Host {Host} risk updated to {Risk:F2} (Δ={Delta})",
                    hostname, risk, cycleRiskDelta);

                // -------------------------------------------------
                // SINGLE INCIDENT DECISION
                // -------------------------------------------------
                if (risk >= RISK_ALERT_THRESHOLD)
                {
                    await HandleIncidentAsync(
                        db,
                        alertWriter,
                        hostname,
                        "FileBehaviorAnomaly",
                        creates + modifies + deletes + renames,
                        newestEventTime,
                        token);
                }
            }

            await CloseMissingHostsAsync(
                db,
                alertWriter,
                "File",
                groupedByHost.Select(g => g.Key).ToList(),
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

            if (incident?.CooldownUntil != null &&
               DateTime.UtcNow < incident.CooldownUntil)
            {
                return;
            }

            const int MIN_EVENTS_FOR_START = 5;
            if (incident == null && currentCount < MIN_EVENTS_FOR_START)
                return;

            bool isNewStart = false;

            if (incident == null)
            {
                incident = new IncidentState
                {
                    Type = type,
                    Hostname = hostname,
                    StartTime = newestEventTime,
                    IsActive = true
                };
                db.IncidentStates.Add(incident);
                isNewStart = true;
            }
            else if (!incident.IsActive)
            {
                incident.IsActive = true;
                incident.StartTime = newestEventTime;
                isNewStart = true;
            }

            // Always update activity
            incident.LastEventTime = newestEventTime;
            incident.Count = Math.Max(incident.Count, currentCount);

            if (isNewStart)
            {
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
                    "START");
            }
            else
            {
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
            incident.IsActive = false;
            incident.CooldownUntil = DateTime.UtcNow.AddMinutes(10);

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

            var riskState = await db.HostRiskStates
                .FirstOrDefaultAsync(h => h.Hostname == incident.Hostname, token);

            if (riskState != null)
            {
                // Reduce risk but do not reset it
                riskState.RiskScore *= 0.3;
                riskState.LastUpdated = DateTime.UtcNow;
            }


            await db.SaveChangesAsync(token);
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
            var now = DateTime.UtcNow;

            var activeIncidents = await db.IncidentStates
                .Where(i =>
                    i.IsActive &&
                    (typePrefix == "File"
                        ? i.Type.StartsWith("File")
                        : i.Type == typePrefix))
                .ToListAsync(token);

            foreach (var incident in activeIncidents)
            {
                if (!activeHosts.Contains(incident.Hostname) &&
                    incident.LastEventTime < now.AddSeconds(-15))
                {
                    await CloseIncidentAsync(db, alertWriter, incident, token);
                }
            }
        }

        // =============================================================
        // Z-SCORE
        // =============================================================
        private static double ZScore(double current, double mean, double std)
        {
            if (std <= 0.000001)
                return 0;

            const double MIN_EFFECTIVE_STD = 0.2;
            double effectiveStd = Math.Max(std, MIN_EFFECTIVE_STD);
            return (current - mean) / effectiveStd;
        }

        // =============================================================
        // EVENT RETENTION
        // =============================================================
        private async Task PruneOldEventsAsync(
            EventsDbContext db,
            CancellationToken token)
        {
            var cutoff = DateTime.UtcNow.AddDays(-14);

            var oldEvents = await db.Events
                .Where(e => e.Timestamp < cutoff)
                .ToListAsync(token);

            if (oldEvents.Count == 0)
                return;

            db.Events.RemoveRange(oldEvents);
            await db.SaveChangesAsync(token);

            _logger.LogInformation(
                "Event retention cleanup: removed {Count} events older than {Cutoff}",
                oldEvents.Count,
                cutoff);
        }

        private async Task<double> UpdateHostRiskAsync(
            EventsDbContext db,
            string hostname,
            double riskDelta,
            CancellationToken token)
        {
            var now = DateTime.UtcNow;

            var riskState = await db.HostRiskStates
                .FirstOrDefaultAsync(h => h.Hostname == hostname, token);

            if (riskState == null)
            {
                riskState = new HostRiskState
                {
                    Hostname = hostname,
                    RiskScore = 0,
                    LastUpdated = now
                };
                db.HostRiskStates.Add(riskState);
            }

            // --------------------------------------------
            // Apply time decay
            // --------------------------------------------
            var minutesElapsed = (now - riskState.LastUpdated).TotalMinutes;

            const double DECAY_PER_MINUTE = 1.0 / 30.0; // 1 point every 30 min
            var decay = minutesElapsed * DECAY_PER_MINUTE;

            riskState.RiskScore = Math.Max(0, riskState.RiskScore - decay);

            // --------------------------------------------
            // Apply new risk
            // --------------------------------------------
            riskState.RiskScore += riskDelta;
            riskState.LastUpdated = now;

            await db.SaveChangesAsync(token);

            return riskState.RiskScore;
        }
    }
}
