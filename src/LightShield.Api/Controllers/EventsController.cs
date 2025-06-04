// src/LightShield.Api/Controllers/EventsController.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LightShield.Api.Data;
using LightShield.Api.Models;

namespace LightShield.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EventsController : ControllerBase
    {
        private readonly EventsDbContext _db;

        public EventsController(EventsDbContext db)
        {
            _db = db;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Event evt)
        {
            // 1) Overwrite any client‐supplied timestamp with server's UTC now
            evt.Timestamp = DateTime.UtcNow;

            // 2) Persist the incoming event
            _db.Events.Add(evt);
            await _db.SaveChangesAsync();

            // 3) Log what Type we actually received (trimmed + case‐normalized)
            var rawType = evt.Type ?? "(null)";
            var normalizedType = rawType.Trim().ToLowerInvariant();
            Console.WriteLine(
                $"[DEBUG] Received event.Type = \"{rawType}\" (normalized = \"{normalizedType}\")"
            );

            // 4) Log the saved event’s details
            Console.WriteLine(
                $"[{evt.Timestamp:O}] {evt.Source}:{rawType} → {evt.PathOrMessage} @ {evt.Hostname}"
            );

            // 5) Now run the immediate‐trigger logic
            await CheckImmediateThresholdsAsync(evt, normalizedType);

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

        /// <summary>
        /// If, in the 5 minutes before this event’s server‐UTC timestamp, we see:
        ///   • ≥ 15 FailedLogin
        ///   • ≥ 100 FileModify
        ///   • ≥ 20 FileDelete
        ///   • ≥ 75 FileCreate
        /// then immediately insert a matching “Severe…” Anomaly and log once to the console.
        /// Uses a case‐insensitive check so “filemodify” or “FileModify” both match.
        /// </summary>
        private async Task CheckImmediateThresholdsAsync(Event evt, string normalizedType)
        {
            // Only examine these four types
            if (normalizedType is not "failedlogin"
                        and not "filecreate"
                        and not "filemodify"
                        and not "filedelete")
            {
                Console.WriteLine($"[DEBUG] Skipping CheckImmediateThresholdsAsync for type \"{normalizedType}\"");
                return;
            }

            var serverNow = evt.Timestamp;       // we did evt.Timestamp = DateTime.UtcNow in Post()
            var since = serverNow.AddMinutes(-5);

            // 1) Count how many of each in the last 5' (server time)
            var failedLoginCount = await _db.Events
                .Where(e =>
                    e.Type.ToLower() == "failedlogin" &&
                    e.Hostname == evt.Hostname &&
                    e.Timestamp >= since)
                .CountAsync();

            var fileCreateCount = await _db.Events
                .Where(e =>
                    e.Type.ToLower() == "filecreate" &&
                    e.Hostname == evt.Hostname &&
                    e.Timestamp >= since)
                .CountAsync();

            var fileModifyCount = await _db.Events
                .Where(e =>
                    e.Type.ToLower() == "filemodify" &&
                    e.Hostname == evt.Hostname &&
                    e.Timestamp >= since)
                .CountAsync();

            var fileDeleteCount = await _db.Events
                .Where(e =>
                    e.Type.ToLower() == "filedelete" &&
                    e.Hostname == evt.Hostname &&
                    e.Timestamp >= since)
                .CountAsync();

            Console.WriteLine(
                $"[DEBUG] Counts since {since:O} → "
              + $"FailedLogin={failedLoginCount}, FileCreate={fileCreateCount}, "
              + $"FileModify={fileModifyCount}, FileDelete={fileDeleteCount}"
            );

            // 2) Check “FileModify” first—this way it can’t be short‐circuited by other severities.
            if (fileModifyCount >= 100)
            {
                await InsertIfNotDuplicateAsync(
                    evt.Hostname,
                    "SevereFileModifyBurst",
                    $"{fileModifyCount} file modifications in last 5 minutes (≥100 threshold)",
                    serverNow
                );
            }

            // 3) Next, check “FileDelete” (≥20)
            if (fileDeleteCount >= 20)
            {
                await InsertIfNotDuplicateAsync(
                    evt.Hostname,
                    "SevereFileDeleteBurst",
                    $"{fileDeleteCount} file deletions in last 5 minutes (≥20 threshold)",
                    serverNow
                );
            }

            // 4) Next, check “FileCreate” (≥75)
            if (fileCreateCount >= 75)
            {
                await InsertIfNotDuplicateAsync(
                    evt.Hostname,
                    "SevereFileCreateBurst",
                    $"{fileCreateCount} file creations in last 5 minutes (≥75 threshold)",
                    serverNow
                );
            }

            // 5) Finally, check “FailedLogin” (≥15)
            if (failedLoginCount >= 15)
            {
                await InsertIfNotDuplicateAsync(
                    evt.Hostname,
                    "SevereFailedLoginBurst",
                    $"{failedLoginCount} failed logins in last 5 minutes (≥15 threshold)",
                    serverNow
                );
            }
        }

        /// <summary>
        /// Inserts a “Severe…” anomaly if an identical anomaly does not already exist
        /// within the last 60 seconds. To disable de-duplication entirely (for testing),
        /// simply comment out the duplicate-check block below.
        /// </summary>
        private async Task InsertIfNotDuplicateAsync(
            string hostname,
            string anomalyType,
            string description,
            DateTime serverNow)
        {
            // ─── DUPLICATE CHECK ─────────────────────
            // If you *always* want a new row (no de-dup), comment out the next 5 lines.
            var duplicateExists = await _db.Anomalies
                .Where(a =>
                    a.Type.ToLower() == anomalyType.ToLower() &&
                    a.Hostname == hostname &&
                    a.Timestamp >= serverNow.AddSeconds(-60))  // last 60 seconds
                .AnyAsync();
            if (duplicateExists)
            {
                Console.WriteLine($"[DEBUG] Duplicate \"{anomalyType}\" for {hostname} within 60 s; skipping.");
                return;
            }
            // ──────────────────────────────────────────

            // Insert the new anomaly
            var anomaly = new Anomaly
            {
                Type = anomalyType,
                Description = description,
                Hostname = hostname,
                Timestamp = serverNow
            };
            _db.Anomalies.Add(anomaly);
            await _db.SaveChangesAsync();

            Console.WriteLine(
                $"[ANOMALY IMMEDIATE] {anomaly.Type} → {anomaly.Description} @ {anomaly.Hostname} (detected at {anomaly.Timestamp:O})"
            );
        }


    }
}
