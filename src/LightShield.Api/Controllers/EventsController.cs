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
using LightShield.Api.Services;

namespace LightShield.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EventsController : ControllerBase
    {
        private readonly EventsDbContext _db;
        private readonly IAlertService _alertService;
        private readonly ILogger<EventsController> _logger;
        private readonly ConfigurationService _configService;

        public EventsController(
            EventsDbContext db,
            IAlertService alertService,
            ConfigurationService configService,
            ILogger<EventsController> logger)
        {
            _db = db;
            _alertService = alertService;
            _logger = logger;
            _configService = configService; 
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Event evt)
        {
            // 1) Normalize & overwrite timestamp + type
            evt.Timestamp = DateTime.Now;   // local machine time
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

            // Run anomaly + alert detection
            await DetectLoginFailureAnomalies(evt);

            return Accepted();
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] string? search,
            [FromQuery] string? sortBy = "timestamp",
            [FromQuery] string? sortDir = "desc",
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var query = _db.Events.AsNoTracking().AsQueryable();

            // -------------------------- DATE FILTERING
            if (startDate.HasValue)
                query = query.Where(e => e.Timestamp >= startDate.Value.ToUniversalTime());

            if (endDate.HasValue)
                query = query.Where(e => e.Timestamp <= endDate.Value.ToUniversalTime());

            // -------------------------- SORTING
            query = (sortBy?.ToLower(), sortDir?.ToLower()) switch
            {
                ("hostname", "asc") => query.OrderBy(e => e.Hostname),
                ("hostname", "desc") => query.OrderByDescending(e => e.Hostname),

                ("type", "asc") => query.OrderBy(e => e.Type),
                ("type", "desc") => query.OrderByDescending(e => e.Type),

                ("source", "asc") => query.OrderBy(e => e.Source),
                ("source", "desc") => query.OrderByDescending(e => e.Source),

                ("timestamp", "asc") => query.OrderBy(e => e.Timestamp),
                _ => query.OrderByDescending(e => e.Timestamp)
            };

            // -------------------------- MATERIALIZE (small dataset)
            var all = await query.ToListAsync();

            // -------------------------- UNIVERSAL SEARCH
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();

                all = all
                    .Where(e =>
                        (e.Hostname ?? "").ToLower().Contains(s) ||
                        (e.Type ?? "").ToLower().Contains(s) ||
                        (e.Source ?? "").ToLower().Contains(s) ||
                        (e.PathOrMessage ?? "").ToLower().Contains(s) ||
                        e.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")
                            .ToLower().Contains(s)
                    )
                    .ToList();
            }

            // -------------------------- PAGINATION
            var totalCount = all.Count;

            var items = all
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(e => new
                {
                    id = e.Id,
                    timestamp = e.Timestamp,
                    hostname = e.Hostname,
                    type = e.Type,
                    source = e.Source,
                    message = e.PathOrMessage
                })
                .ToList();

            return Ok(new
            {
                totalCount,
                page,
                pageSize,
                items
            });
        }


        private async Task CheckImmediateThresholdsAsync(
            Event evt,
            string type,
            CancellationToken ct)
        {
            if (type is not "failedlogin"
                     and not "filecreate"
                     and not "filemodify"
                     and not "filedelete")
            {
                _logger.LogDebug("Skipping check for {Type}", type);
                return;
            }

            var since = evt.Timestamp.AddMinutes(-5);

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

            //  thresholds from ConfigurationService
            var modifyThreshold = await _configService.GetFileEditThresholdAsync();
            var deleteThreshold = await _configService.GetFileDeleteThresholdAsync();
            var createThreshold = await _configService.GetFileCreateThresholdAsync();
            var loginThreshold = await _configService.GetFailedLoginThresholdAsync();

            if (fileModifyCount >= modifyThreshold)
                await InsertIfNotDuplicateAsync(
                    evt.Hostname,
                    "SevereFileModifyBurst",
                    $"{fileModifyCount} file modifications in last 5 minutes (≥{modifyThreshold})",
                    evt.Timestamp,
                    ct
                );

            if (fileDeleteCount >= deleteThreshold)
                await InsertIfNotDuplicateAsync(
                    evt.Hostname,
                    "SevereFileDeleteBurst",
                    $"{fileDeleteCount} file deletions in last 5 minutes (≥{deleteThreshold})",
                    evt.Timestamp,
                    ct
                );

            if (fileCreateCount >= createThreshold)
                await InsertIfNotDuplicateAsync(
                    evt.Hostname,
                    "SevereFileCreateBurst",
                    $"{fileCreateCount} file creations in last 5 minutes (≥{createThreshold})",
                    evt.Timestamp,
                    ct
                );

            if (failedLoginCount >= loginThreshold)
                await InsertIfNotDuplicateAsync(
                    evt.Hostname,
                    "SevereFailedLoginBurst",
                    $"{failedLoginCount} failed logins in last 5 minutes (≥{loginThreshold})",
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
            // Prevent duplicate anomalies within the last minute
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

            // Create and persist anomaly record
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

            // Build alert message
            var alertMsg = $"[LightShield] {anomaly.Type} on {anomaly.Hostname} @ {anomaly.Timestamp:O}";
            try
            {
                // Send alert via configured services (SMS/Email)
                await _alertService.SendAlertAsync(alertMsg);

                //  Persist alert into database
                _db.Alerts.Add(new Alert
                {
                    Timestamp = DateTime.Now,
                    Type = anomaly.Type,
                    Message = alertMsg,
                    Channel = "SMS/Email" // later: split if you want separate records
                });
                await _db.SaveChangesAsync(ct);

                _logger.LogInformation("Alert sent and logged: {Msg}", alertMsg);
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


        private async Task DetectLoginFailureAnomalies(Event evt)
        {
            // Normalize again just to be safe
            var type = evt.Type?.Trim().ToLowerInvariant();

            if (type != "loginfailure")
                return;

            // 1. Simple anomaly
            var simpleAnomaly = new Anomaly
            {
                Timestamp = evt.Timestamp,
                Type = "LoginFailure",
                Description = $"Login failed on {evt.Hostname}. Message: {evt.PathOrMessage}",
                Hostname = evt.Hostname
            };

            _db.Anomalies.Add(simpleAnomaly);


            // 2. Burst detection
            var now = evt.Timestamp;
            var windowStart = now.AddSeconds(-30);
            int threshold = 5;

            var recentFailures = await _db.Events
                .Where(e => e.Type == "loginfailure" &&
                            e.Timestamp >= windowStart &&
                            e.Timestamp <= now)
                .CountAsync();

            if (recentFailures >= threshold)
            {
                var burst = new Anomaly
                {
                    Timestamp = now,
                    Type = "LoginFailureBurst",
                    Description = $"{recentFailures} failed logins in last 30 seconds.",
                    Hostname = evt.Hostname
                };
                _db.Anomalies.Add(burst);

                var alert = new Alert
                {
                    Timestamp = now,
                    Type = "LoginFailureBurst",
                    Message = $"{recentFailures} login failures within 30 seconds on host {evt.Hostname}.",
                    Channel = "system"
                };
                _db.Alerts.Add(alert);
            }

            await _db.SaveChangesAsync();
        }



    }
}

