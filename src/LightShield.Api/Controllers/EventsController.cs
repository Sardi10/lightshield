// src/LightShield.Api/Controllers/EventsController.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using LightShield.Api.Data;
using LightShield.Api.Models;
using LightShield.Api.Services.Alerts;

namespace LightShield.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EventsController : ControllerBase
    {
        private readonly EventsDbContext _db;
        private readonly IAlertService _alertService;
        private readonly ILogger<EventsController> _logger;

        public EventsController(
            EventsDbContext db,
            IAlertService alertService,
            ILogger<EventsController> logger)
        {
            _db = db;
            _alertService = alertService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Event evt)
        {
            // 1) Overwrite any client‐supplied timestamp
            evt.Timestamp = DateTime.UtcNow;

            // 2) Persist the incoming event (honor cancellation)
            _db.Events.Add(evt);
            await _db.SaveChangesAsync(HttpContext.RequestAborted);

            // 3) Normalize and log the event type
            var rawType = evt.Type ?? "(null)";
            var normalizedType = rawType.Trim().ToLowerInvariant();
            _logger.LogDebug(
                "Received event.Type = \"{RawType}\" (normalized = \"{NormalizedType}\")",
                rawType, normalizedType
            );

            // 4) Log the saved event’s details
            _logger.LogInformation(
                "[{Timestamp:O}] {Source}:{RawType} → {PathOrMessage} @ {Hostname}",
                evt.Timestamp, evt.Source, rawType, evt.PathOrMessage, evt.Hostname
            );

            // 5) Run the immediate‐trigger anomaly logic
            await CheckImmediateThresholdsAsync(evt, normalizedType, HttpContext.RequestAborted);

            // 6) Return 202 Accepted
            return Accepted();
        }

        [HttpGet]
        public async Task<IEnumerable<Event>> Get()
        {
            return await _db.Events
                .OrderByDescending(e => e.Timestamp)
                .Take(50)
                .ToListAsync();
        }

        private async Task CheckImmediateThresholdsAsync(
            Event evt,
            string normalizedType,
            CancellationToken cancellationToken)
        {
            // Only examine our four types
            if (normalizedType is not "failedlogin"
                        and not "filecreate"
                        and not "filemodify"
                        and not "filedelete")
            {
                _logger.LogDebug(
                    "Skipping immediate check for event type {Type}",
                    normalizedType
                );
                return;
            }

            var serverNow = evt.Timestamp;
            var since = serverNow.AddMinutes(-5);

            // Count each event type in the last 5 minutes
            var failedLoginCount = await _db.Events
                .Where(e =>
                    e.Type.ToLowerInvariant() == "failedlogin" &&
                    e.Hostname == evt.Hostname &&
                    e.Timestamp >= since)
                .CountAsync(cancellationToken);

            var fileCreateCount = await _db.Events
                .Where(e =>
                    e.Type.ToLowerInvariant() == "filecreate" &&
                    e.Hostname == evt.Hostname &&
                    e.Timestamp >= since)
                .CountAsync(cancellationToken);

            var fileModifyCount = await _db.Events
                .Where(e =>
                    e.Type.ToLowerInvariant() == "filemodify" &&
                    e.Hostname == evt.Hostname &&
                    e.Timestamp >= since)
                .CountAsync(cancellationToken);

            var fileDeleteCount = await _db.Events
                .Where(e =>
                    e.Type.ToLowerInvariant() == "filedelete" &&
                    e.Hostname == evt.Hostname &&
                    e.Timestamp >= since)
                .CountAsync(cancellationToken);

            _logger.LogDebug(
                "Counts since {Since:O} → FailedLogin={FailedLogin}, FileCreate={Create}, FileModify={Modify}, FileDelete={Delete}",
                since, failedLoginCount, fileCreateCount, fileModifyCount, fileDeleteCount
            );

            // Evaluate in priority order
            if (fileModifyCount >= 100)
                await InsertIfNotDuplicateAsync(
                    evt.Hostname,
                    "SevereFileModifyBurst",
                    $"{fileModifyCount} file modifications in last 5 minutes (≥100)",
                    serverNow,
                    cancellationToken
                );

            if (fileDeleteCount >= 20)
                await InsertIfNotDuplicateAsync(
                    evt.Hostname,
                    "SevereFileDeleteBurst",
                    $"{fileDeleteCount} file deletions in last 5 minutes (≥20)",
                    serverNow,
                    cancellationToken
                );

            if (fileCreateCount >= 75)
                await InsertIfNotDuplicateAsync(
                    evt.Hostname,
                    "SevereFileCreateBurst",
                    $"{fileCreateCount} file creations in last 5 minutes (≥75)",
                    serverNow,
                    cancellationToken
                );

            if (failedLoginCount >= 15)
                await InsertIfNotDuplicateAsync(
                    evt.Hostname,
                    "SevereFailedLoginBurst",
                    $"{failedLoginCount} failed logins in last 5 minutes (≥15)",
                    serverNow,
                    cancellationToken
                );
        }

        private async Task InsertIfNotDuplicateAsync(
            string hostname,
            string anomalyType,
            string description,
            DateTime serverNow,
            CancellationToken cancellationToken)
        {
            // Dedup within last 60 seconds
            var duplicateExists = await _db.Anomalies
                .Where(a =>
                    a.Type.ToLowerInvariant() == anomalyType.ToLowerInvariant() &&
                    a.Hostname == hostname &&
                    a.Timestamp >= serverNow.AddSeconds(-60))
                .AnyAsync(cancellationToken);

            if (duplicateExists)
            {
                _logger.LogDebug(
                    "Duplicate {AnomalyType} for host {Hostname} within 60s; skipping",
                    anomalyType, hostname
                );
                return;
            }

            // Insert the new anomaly
            var anomaly = new Anomaly
            {
                Type = anomalyType,
                Description = description,
                Hostname = hostname,
                Timestamp = serverNow
            };

            _db.Anomalies.Add(anomaly);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Anomaly detected: {Description} on host {Hostname}",
                anomaly.Description, anomaly.Hostname
            );

            // Send alert
            var alertMsg = $"[LightShield] {anomaly.Type} on {anomaly.Hostname} @ {anomaly.Timestamp:O}";
            try
            {
                await _alertService.SendAlertAsync(alertMsg);
                _logger.LogInformation("Alert sent: {AlertMsg}", alertMsg);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed sending alert for anomaly {AnomalyType} on host {Hostname}",
                    anomaly.Type,
                    anomaly.Hostname
                );
            }
        }
    }
}
