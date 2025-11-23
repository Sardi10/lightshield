using System;
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
using Newtonsoft.Json;
using System.Net.Http;

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
        private static readonly HttpClient _http = new HttpClient();

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

        // ======================================================================
        // POST /api/events
        // ======================================================================
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Event evt)
        {
            // -----------------------------------------------------------
            // Normalize + enforce local timestamp
            // -----------------------------------------------------------
            evt.Timestamp = DateTime.Now;
            evt.Type = (evt.Type ?? "").Trim().ToLowerInvariant();
            evt.OperatingSystem = evt.OperatingSystem?.ToLower() ?? "unknown";

            // -----------------------------------------------------------
            // Assign severity (Info, Warning, Critical)
            // -----------------------------------------------------------
            evt.Severity = ClassifySeverity(evt);

            // -----------------------------------------------------------
            // Try GeoIP Enrichment (offline safe)
            // -----------------------------------------------------------
            await TryGeoEnrich(evt);

            // -----------------------------------------------------------
            // Save event to DB
            // -----------------------------------------------------------
            _db.Events.Add(evt);
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "[{ts:O}] {source}:{type} {msg} @ {host}",
                evt.Timestamp, evt.Source, evt.Type, evt.PathOrMessage, evt.Hostname
            );

            await CheckImmediateThresholdsAsync(
                evt, evt.Type, HttpContext.RequestAborted);

            // -----------------------------------------------------------
            // IDS login-failure anomaly detection (simple + burst)
            // -----------------------------------------------------------
            await DetectLoginFailureAnomalies(evt);

            return Accepted();
        }

        // ======================================================================
        // GET /api/events
        // ======================================================================
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

            // -----------------------------------------------------------
            // Date filters
            // -----------------------------------------------------------
            if (startDate.HasValue)
                query = query.Where(e => e.Timestamp >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(e => e.Timestamp <= endDate.Value);

            // -----------------------------------------------------------
            // Sorting
            // -----------------------------------------------------------
            query = (sortBy?.ToLower(), sortDir?.ToLower()) switch
            {
                ("hostname", "asc") => query.OrderBy(e => e.Hostname),
                ("hostname", "desc") => query.OrderByDescending(e => e.Hostname),

                ("type", "asc") => query.OrderBy(e => e.Type),
                ("type", "desc") => query.OrderByDescending(e => e.Type),

                ("severity", "asc") => query.OrderBy(e => e.Severity),
                ("severity", "desc") => query.OrderByDescending(e => e.Severity),

                ("source", "asc") => query.OrderBy(e => e.Source),
                ("source", "desc") => query.OrderByDescending(e => e.Source),

                ("timestamp", "asc") => query.OrderBy(e => e.Timestamp),
                _ => query.OrderByDescending(e => e.Timestamp)
            };

            var all = await query.ToListAsync();

            // -----------------------------------------------------------
            // Universal Search
            // -----------------------------------------------------------
            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.ToLower();
                all = all
                    .Where(e =>
                        (e.Hostname ?? "").ToLower().Contains(s) ||
                        (e.Type ?? "").ToLower().Contains(s) ||
                        (e.Source ?? "").ToLower().Contains(s) ||
                        (e.PathOrMessage ?? "").ToLower().Contains(s) ||
                        (e.Username ?? "").ToLower().Contains(s) ||
                        (e.IPAddress ?? "").ToLower().Contains(s) ||
                        e.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")
                            .ToLower().Contains(s)
                    )
                    .ToList();
            }

            // -----------------------------------------------------------
            // Pagination
            // -----------------------------------------------------------
            var totalCount = all.Count;

            var items = all
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(e => new
                {
                    e.Id,
                    e.Timestamp,
                    e.Hostname,
                    e.Type,
                    e.Source,
                    Message = e.PathOrMessage,
                    e.Severity,
                    e.Username,
                    e.IPAddress,
                    e.Country,
                    e.City,
                    e.OperatingSystem
                })
                .ToList();

            return Ok(new { totalCount, page, pageSize, items });
        }

        // ======================================================================
        // SEVERITY CLASSIFICATION
        // ======================================================================
        private static string ClassifySeverity(Event evt)
        {
            if (evt.Type.Contains("loginfailureburst"))
                return "Critical";

            if (evt.Type.Contains("loginfailure"))
                return "Warning";

            if (evt.Type.Contains("unauthorized"))
                return "Critical";

            return "Info";
        }

        // ======================================================================
        // OFFLINE-SAFE GEO ENRICHMENT
        // ======================================================================
        private async Task TryGeoEnrich(Event evt)
        {
            if (string.IsNullOrWhiteSpace(evt.IPAddress))
                return;

            try
            {
                // Free, no-key lookup (we replace with MaxMind later)
                string url = $"http://ip-api.com/json/{evt.IPAddress}";
                string json = await _http.GetStringAsync(url);
                dynamic data = JsonConvert.DeserializeObject(json)!;

                if (data.status == "success")
                {
                    evt.Country = data.country;
                    evt.City = data.city;
                }
            }
            catch
            {
                // No internet? VPN? Firewalled? Quietly ignore.
            }
        }

        // ======================================================================
        // EXISTING IMMEDIATE THRESHOLD LOGIC 
        // ======================================================================
        private async Task CheckImmediateThresholdsAsync(
            Event evt, string type, CancellationToken ct)
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

            var modifyThreshold = await _configService.GetFileEditThresholdAsync();
            var deleteThreshold = await _configService.GetFileDeleteThresholdAsync();
            var createThreshold = await _configService.GetFileCreateThresholdAsync();
            var loginThreshold = await _configService.GetFailedLoginThresholdAsync();

            if (fileModifyCount >= modifyThreshold)
                await InsertIfNotDuplicateAsync(
                    evt.Hostname, "SevereFileModifyBurst",
                    $"{fileModifyCount} file modifications in last 5 minutes (≥{modifyThreshold})",
                    evt.Timestamp, ct);

            if (fileDeleteCount >= deleteThreshold)
                await InsertIfNotDuplicateAsync(
                    evt.Hostname, "SevereFileDeleteBurst",
                    $"{fileDeleteCount} file deletions in last 5 minutes (≥{deleteThreshold})",
                    evt.Timestamp, ct);

            if (fileCreateCount >= createThreshold)
                await InsertIfNotDuplicateAsync(
                    evt.Hostname, "SevereFileCreateBurst",
                    $"{fileCreateCount} file creations in last 5 minutes (≥{createThreshold})",
                    evt.Timestamp, ct);

            if (failedLoginCount >= loginThreshold)
                await InsertIfNotDuplicateAsync(
                    evt.Hostname, "SevereFailedLoginBurst",
                    $"{failedLoginCount} failed logins in last 5 minutes (≥{loginThreshold})",
                    evt.Timestamp, ct);
        }

        // ======================================================================
        // BURST LOGIC 
        // ======================================================================
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

            var alertMsg = $"[LightShield] {anomaly.Type} on {anomaly.Hostname} @ {anomaly.Timestamp:O}";
            try
            {
                await _alertService.SendAlertAsync(alertMsg);

                _db.Alerts.Add(new Alert
                {
                    Timestamp = DateTime.Now,
                    Type = anomaly.Type,
                    Message = alertMsg,
                    Channel = "SMS/Email"
                });
                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed sending alert for {Type} on {Host}",
                    anomalyType, hostname);
            }
        }

        // ======================================================================
        // LOGIN FAILURE ANOMALIES
        // ======================================================================
        private async Task DetectLoginFailureAnomalies(Event evt)
        {
            var type = evt.Type?.ToLowerInvariant();
            if (type != "loginfailure")
                return;

            // simple anomaly
            var simple = new Anomaly
            {
                Timestamp = evt.Timestamp,
                Type = "LoginFailure",
                Description = $"Login failed on {evt.Hostname}.",
                Hostname = evt.Hostname
            };

            _db.Anomalies.Add(simple);

            // burst (within 30 seconds)
            var now = evt.Timestamp;
            var windowStart = now.AddSeconds(-30);

            int failures = await _db.Events
                .Where(e => e.Type == "loginfailure"
                         && e.Timestamp >= windowStart
                         && e.Timestamp <= now)
                .CountAsync();

            if (failures >= 5)
            {
                var burst = new Anomaly
                {
                    Timestamp = now,
                    Type = "LoginFailureBurst",
                    Description = $"{failures} failed logins in last 30 seconds.",
                    Hostname = evt.Hostname
                };
                _db.Anomalies.Add(burst);

                var alert = new Alert
                {
                    Timestamp = now,
                    Type = "LoginFailureBurst",
                    Message = $"{failures} login failures within 30 seconds on host {evt.Hostname}.",
                    Channel = "system"
                };

                _db.Alerts.Add(alert);
            }

            await _db.SaveChangesAsync();
        }
    }
}
