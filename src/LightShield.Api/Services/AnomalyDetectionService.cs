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

namespace LightShield.Api.Services
{
    /// <summary>
    /// Periodically scans for anomalies (e.g., bursts of failed logins).
    /// On startup, it does one immediate pass, then waits 5 minutes between each subsequent pass.
    /// </summary>
    public class AnomalyDetectionService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AnomalyDetectionService> _logger;

        public AnomalyDetectionService(
            IServiceScopeFactory scopeFactory,
            ILogger<AnomalyDetectionService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AnomalyDetectionService starting up.");

            // —––––– 1) Immediate scan on startup —–––––
            await RunDetectionPass(stoppingToken);

            // —––––– 2) Then loop every 5 minutes —–––––
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                await RunDetectionPass(stoppingToken);
            }
        }

        /// <summary>
        /// Executes one “detection pass”: 
        /// - Queries the last 5 minutes of FailedLogin events 
        /// - Groups by hostname 
        /// - If count >= 5, writes an Anomaly record
        /// </summary>
        private async Task RunDetectionPass(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<EventsDbContext>();

                // 1) Define the 5-minute window
                var since = DateTime.UtcNow.AddMinutes(-5);

                // 2) Fetch all FailedLogin events in that window
                var recentFailedLogins = await db.Events
                    .Where(e => e.Type == "FailedLogin" && e.Timestamp >= since)
                    .ToListAsync(stoppingToken);

                // 3) Group by Hostname and insert an anomaly if count >= 5
                var groupedByHost = recentFailedLogins.GroupBy(e => e.Hostname);
                foreach (var group in groupedByHost)
                {
                    if (group.Count() >= 5)
                    {
                        var anomaly = new Anomaly
                        {
                            Type = "FailedLoginBurst",
                            Description = $"{group.Count()} failed logins in last 5 minutes",
                            Hostname = group.Key
                        };

                        db.Anomalies.Add(anomaly);
                        await db.SaveChangesAsync(stoppingToken);

                        _logger.LogWarning(
                            "Anomaly detected: {Description} on host {Hostname}",
                            anomaly.Description,
                            anomaly.Hostname);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while detecting anomalies");
            }
        }
    }
}
