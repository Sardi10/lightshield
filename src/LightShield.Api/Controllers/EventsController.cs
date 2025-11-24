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
        private readonly AlertWriterService _alertWriter;
        private readonly ILogger<EventsController> _logger;
        private readonly ConfigurationService _configService;
        private static readonly HttpClient _http = new HttpClient();

        public EventsController(
            EventsDbContext db,
            AlertWriterService alertWriter,
            ConfigurationService configService,
            ILogger<EventsController> logger)
        {
            _db = db;
            _alertWriter = alertWriter;
            _logger = logger;
            _configService = configService;
        }

        // ======================================================================
        // POST /api/events
        // ======================================================================
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Event evt)
        {
            evt.Timestamp = DateTime.UtcNow; // store in UTC
            evt.Type = (evt.Type ?? "").Trim().ToLowerInvariant();
            evt.OperatingSystem = evt.OperatingSystem?.ToLower() ?? "unknown";

            evt.Severity = ClassifySeverity(evt);
            await TryGeoEnrich(evt);

            _db.Events.Add(evt);
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "[{ts:O}] {source}:{type} {msg} @ {host}",
                evt.Timestamp, evt.Source, evt.Type, evt.PathOrMessage, evt.Hostname
            );

            await CheckImmediateThresholdsAsync(evt, evt.Type, HttpContext.RequestAborted);
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

            if (startDate.HasValue)
                query = query.Where(e => e.Timestamp >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(e => e.Timestamp <= endDate.Value);

            // Sorting
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

            // Search
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
                        e.Timestamp.ToString("yyyy-MM-dd HH:mm:ss").ToLower().Contains(s)
                    )
                    .ToList();
            }

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
        // Severity
        // ======================================================================
        private static string ClassifySeverity(Event evt)
        {
            if (evt.Type.Contains("loginfailureburst")) return "Critical";
            if (evt.Type.Contains("loginfailure")) return "Warning";
            if (evt.Type.Contains("unauthorized")) return "Critical";
            return "Info";
        }

        // ======================================================================
        // Geo Enrichment (safe if offline)
        // ======================================================================
        private async Task TryGeoEnrich(Event evt)
        {
            if (string.IsNullOrWhiteSpace(evt.IPAddress))
                return;

            try
            {
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
                // Offline or DNS fail: silently skip
            }
        }

        // ======================================================================
        // Threshold Alerts → Create Anomaly → Create Alert (UNIFIED)
        // ======================================================================
        private async Task CheckImmediateThresholdsAsync(
            Event evt, string type, CancellationToken ct)
        {
            if (type is not ("failedlogin" or "filecreate" or "filemodify" or "filedelete"))
                return;

            var since = evt.Timestamp.AddMinutes(-5);

            int failedLoginCount = await _db.Events
                .Where(e => e.Type == "failedlogin"
                         && e.Hostname == evt.Hostname
                         && e.Timestamp >= since)
                .CountAsync(ct);

            int fileCreateCount = await _db.Events
                .Where(e => e.Type == "filecreate"
                         && e.Hostname == evt.Hostname
                         && e.Timestamp >= since)
                .CountAsync(ct);

            int fileModifyCount = await _db.Events
                .Where(e => e.Type == "filemodify"
                         && e.Hostname == evt.Hostname
                         && e.Timestamp >= since)
                .CountAsync(ct);

            int fileDeleteCount = await _db.Events
                .Where(e => e.Type == "filedelete"
                         && e.Hostname == evt.Hostname
                         && e.Timestamp >= since)
                .CountAsync(ct);

            var modifyThreshold = await _configService.GetFileEditThresholdAsync();
            var deleteThreshold = await _configService.GetFileDeleteThresholdAsync();
            var createThreshold = await _configService.GetFileCreateThresholdAsync();
            var loginThreshold = await _configService.GetFailedLoginThresholdAsync();

            if (fileModifyCount >= modifyThreshold)
                await CreateBurstAnomalyAndAlert(evt.Hostname, "FileModifyBurst",
                    $"{fileModifyCount} file modifications in last 5 minutes (≥{modifyThreshold})");

            if (fileDeleteCount >= deleteThreshold)
                await CreateBurstAnomalyAndAlert(evt.Hostname, "FileDeleteBurst",
                    $"{fileDeleteCount} file deletions in last 5 minutes (≥{deleteThreshold})");

            if (fileCreateCount >= createThreshold)
                await CreateBurstAnomalyAndAlert(evt.Hostname, "FileCreateBurst",
                    $"{fileCreateCount} file creations in last 5 minutes (≥{createThreshold})");

            if (failedLoginCount >= loginThreshold)
                await CreateBurstAnomalyAndAlert(evt.Hostname, "FailedLoginBurst",
                    $"{failedLoginCount} failed logins in last 5 minutes (≥{loginThreshold})");
        }

        private async Task CreateBurstAnomalyAndAlert(
            string hostname, string type, string description)
        {
            var anomaly = new Anomaly
            {
                Type = type,
                Description = description,
                Hostname = hostname,
                Timestamp = DateTime.UtcNow
            };

            _db.Anomalies.Add(anomaly);
            await _db.SaveChangesAsync();

            await _alertWriter.CreateAndSendAlertAsync(type, description, hostname);
        }

        // ======================================================================
        // LOGIN FAILURE ANOMALIES (simple + burst)
        // ======================================================================
        private async Task DetectLoginFailureAnomalies(Event evt)
        {
            if (evt.Type != "loginfailure")
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

                // New: Unified alert pipeline
                await _alertWriter.CreateAndSendAlertAsync(
                    "LoginFailureBurst",
                    burst.Description,
                    evt.Hostname);
            }

            await _db.SaveChangesAsync();
        }
    }
}
