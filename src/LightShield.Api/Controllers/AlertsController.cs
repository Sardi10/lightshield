using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LightShield.Api.Data;
using LightShield.Api.Models;
using LightShield.Api.Services.Alerts;
using LightShield.Api.Services;   // <-- needed for ConfigurationService

namespace LightShield.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AlertsController : ControllerBase
    {
        private readonly EventsDbContext _db;
        private readonly ConfigurationService _config;

        public AlertsController(EventsDbContext db, ConfigurationService config)
        {
            _db = db;
            _config = config;
        }

        // --------------------------------------------------------------------
        // GET /api/alerts   (pagination, sorting, filtering)
        // --------------------------------------------------------------------
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

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                query = query.Where(a =>
                    a.Type.ToLower().Contains(search) ||
                    a.Message.ToLower().Contains(search) ||
                    a.Channel.ToLower().Contains(search)
                );
            }

            if (startDate.HasValue)
                query = query.Where(a => a.Timestamp >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(a => a.Timestamp <= endDate.Value);

            query = (sortBy.ToLower(), sortDir.ToLower()) switch
            {
                ("type", "asc") => query.OrderBy(a => a.Type),
                ("type", "desc") => query.OrderByDescending(a => a.Type),

                ("channel", "asc") => query.OrderBy(a => a.Channel),
                ("channel", "desc") => query.OrderByDescending(a => a.Channel),

                ("timestamp", "asc") => query.OrderBy(a => a.Timestamp),
                _ => query.OrderByDescending(a => a.Timestamp)
            };

            var totalCount = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new
                {
                    a.Id,
                    Timestamp = a.Timestamp.ToUniversalTime().ToString("o"),
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

        // --------------------------------------------------------------------
        // TEST ALERT ENDPOINT
        // --------------------------------------------------------------------
        [HttpGet("test")]
        public async Task<IActionResult> TestAlert(
            [FromServices] IAlertService alertService)
        {
            var email = await _config.GetEmailAsync();
            var phone = await _config.GetPhoneAsync();

            if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phone))
                return BadRequest("No email or phone number is configured.");

            string message = "[LightShield Test Alert]\nThis is a test notification.";

            await alertService.SendAlertAsync(email, phone, message);

            return Ok("Test alert sent successfully.");
        }
    }
}
