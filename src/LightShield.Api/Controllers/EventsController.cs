// src/LightShield.Api/Controllers/EventsController.cs

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
            // 1) Normalize & overwrite timestamp + type
            evt.Timestamp = DateTime.UtcNow;
            var rawType = evt.Type ?? "";
            var normalizedType = rawType.Trim().ToLowerInvariant();
            evt.Type = normalizedType;

            // 2) Persist
            _db.Events.Add(evt);
            await _db.SaveChangesAsync(HttpContext.RequestAborted);

            _logger.LogDebug(
                "Received event.Type = \"{Raw}\" → stored as \"{Norm}\"",
                rawType, normalizedType
            );

            // 3) Log the saved event’s details
            _logger.LogInformation(
                "[{Timestamp:O}] {Source}:{Type} → {PathOrMessage} @ {Hostname}",
                evt.Timestamp, evt.Source, normalizedType, evt.PathOrMessage, evt.Hostname
            );

            // 4) Run immediate-trigger logic
            await CheckImmediateThresholdsAsync(
                evt, normalizedType, HttpContext.RequestAborted
            );

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
            string type,
            CancellationToken ct)
        {
            // only these four
            if (type is not "failedlogin"
                     and not "filecreate"
                     and not "filemodify"
                     and not "filedelete")
            {
                _logger.LogDebug("Skipping check for {Type}", type);
                return;
            }

            var since = evt.Timestamp.AddMinutes(-5);

            // now we can compare directly, no ToLower calls
            var failedLoginCount = await _db.Events
                .Where(e => e.Type == "failedlogin"
                         && e.Hostname == evt.Hostname
                         && e.Timestamp >= since)
                .CountAsync(ct);

            var fileCreateCount = await _db.Events
                .Where(e => e.Type == "filecreate"
                         && e.Hostname == evt.Hostname
                         && e.Timestamp >= since)
                .CountAsync(ct);

            var fileModifyCount = await _db.Events
                .Where(e => e.Type == "filemodify"
                         && e.Hostname == evt.Hostname
                         && e.Timestamp >= since)
                .CountAsync(ct);

            var fileDeleteCount = await _db.Events
                .Where(e => e.Type == "filedelete"
                         && e.Hostname == evt.Hostname
                         && e.Timestamp >= since)
                .CountAsync(ct);

            if (fileModifyCount >= 100)
                await InsertIfNotDuplicateAsync(
                    evt.Hostname,
                    "SevereFileModifyBurst",
                    $"{fileModifyCount} file modifications in last 5 minutes (≥100)",
                    evt.Timestamp,
                    ct
                );

            if (fileDeleteCount >= 20)
                await InsertIfNotDuplicateAsync(
                    evt.Hostname,
                    "SevereFileDeleteBurst",
                    $"{fileDeleteCount} file deletions in last 5 minutes (≥20)",
                    evt.Timestamp,
                    ct
                );

            if (fileCreateCount >= 75)
                await InsertIfNotDuplicateAsync(
                    evt.Hostname,
                    "SevereFileCreateBurst",
                    $"{fileCreateCount} file creations in last 5 minutes (≥75)",
                    evt.Timestamp,
                    ct
                );

            if (failedLoginCount >= 15)
                await InsertIfNotDuplicateAsync(
                    evt.Hostname,
                    "SevereFailedLoginBurst",
                    $"{failedLoginCount} failed logins in last 5 minutes (≥15)",
                    evt.Timestamp,
                    ct
                );
        }

        private async Task InsertIfNotDuplicateAsync(
            string hostname,
            string anomalyType,
            string description,
            DateTime detectedAt,
            CancellationToken ct)
        {
            var exists = await _db.Anomalies
                .Where(a => a.Type == anomalyType
                         && a.Hostname == hostname
                         && a.Timestamp >= detectedAt.AddSeconds(-60))
                .AnyAsync(ct);

            if (exists)
            {
                _logger.LogDebug(
                    "Duplicate {Type} for {Host} in last minute, skipping",
                    anomalyType, hostname
                );
                return;
            }

            var anomaly = new Anomaly
            {
                Type = anomalyType,
                Description = description,
                Hostname = hostname,
                Timestamp = detectedAt
            };

            _db.Anomalies.Add(anomaly);
            await _db.SaveChangesAsync(ct);

            _logger.LogWarning(
                "Anomaly detected: {Desc} on {Host}",
                anomaly.Description, anomaly.Hostname
            );

            var alertMsg = $"[LightShield] {anomaly.Type} on {anomaly.Hostname} @ {anomaly.Timestamp:O}";
            try
            {
                await _alertService.SendAlertAsync(alertMsg);
                _logger.LogInformation("Alert sent: {Msg}", alertMsg);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed sending alert for {Type} on {Host}",
                    anomaly.Type, anomaly.Hostname
                );
            }
        }
    }
}

