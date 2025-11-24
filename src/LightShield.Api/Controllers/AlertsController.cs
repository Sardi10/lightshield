using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LightShield.Api.Data;
using LightShield.Api.Models;
using LightShield.Api.Services.Alerts;

namespace LightShield.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AlertsController : ControllerBase
    {
        private readonly EventsDbContext _db;

        public AlertsController(EventsDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            string? search,
            DateTime? startDate,
            DateTime? endDate,
            int page = 1,
            int pageSize = 10,
            string sortBy = "timestamp",
            string sortDir = "desc")
        {
            var query = _db.Alerts.AsQueryable();

            // ---------------- SEARCH ----------------
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                query = query.Where(a =>
                    a.Type.ToLower().Contains(search) ||
                    a.Message.ToLower().Contains(search) ||
                    a.Channel.ToLower().Contains(search)
                );
            }

            // ---------------- DATE FILTERS ----------------
            if (startDate.HasValue)
                query = query.Where(a => a.Timestamp >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(a => a.Timestamp <= endDate.Value);

            // ---------------- SORTING ----------------
            query = (sortBy.ToLower(), sortDir.ToLower()) switch
            {
                ("type", "asc") => query.OrderBy(a => a.Type),
                ("type", "desc") => query.OrderByDescending(a => a.Type),

                ("channel", "asc") => query.OrderBy(a => a.Channel),
                ("channel", "desc") => query.OrderByDescending(a => a.Channel),

                ("timestamp", "asc") => query.OrderBy(a => a.Timestamp),
                _ => query.OrderByDescending(a => a.Timestamp)
            };

            // ---------------- PAGINATION ----------------
            var totalCount = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new
                {
                    a.Id,
                    Timestamp = a.Timestamp.ToUniversalTime().ToString("o"),  // ✅ FIXED
                    a.Type,
                    a.Message,
                    a.Channel,
                    a.Hostname
                })
                .ToListAsync();

            return Ok(new
            {
                totalCount,
                page,
                pageSize,
                items
            });
        }

        [HttpGet("test-email")]
        public async Task<IActionResult> TestEmail([FromServices] IAlertService alertService)
        {
            await alertService.SendAlertAsync("This is a LightShield test alert email.");
            return Ok("Test email sent successfully.");
        }

    }
}
