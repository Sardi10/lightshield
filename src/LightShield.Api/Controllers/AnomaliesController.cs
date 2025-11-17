using System;
using System.Linq;
using System.Threading.Tasks;
using LightShield.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace LightShield.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AnomaliesController : ControllerBase
    {
        private readonly EventsDbContext _db;

        public AnomaliesController(EventsDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
    [FromQuery] string? search,
    [FromQuery] string? sortBy = "timestamp",
    [FromQuery] string? sortDir = "desc",
    [FromQuery] DateTime? startDate = null,
    [FromQuery] DateTime? endDate = null,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 5)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 500) pageSize = 5;

            var query = _db.Anomalies.AsNoTracking().AsQueryable();

            // ---------------------------
            // SQL FILTERING FIRST
            // ---------------------------
            if (startDate.HasValue)
                query = query.Where(a => a.Timestamp >= startDate.Value.ToUniversalTime());

            if (endDate.HasValue)
                query = query.Where(a => a.Timestamp <= endDate.Value.ToUniversalTime());

            // ---------------------------
            // SQL SORTING
            // ---------------------------
            query = (sortBy?.ToLower(), sortDir?.ToLower()) switch
            {
                ("hostname", "asc") => query.OrderBy(a => a.Hostname),
                ("hostname", "desc") => query.OrderByDescending(a => a.Hostname),

                ("type", "asc") => query.OrderBy(a => a.Type),
                ("type", "desc") => query.OrderByDescending(a => a.Type),

                ("timestamp", "asc") => query.OrderBy(a => a.Timestamp),
                _ => query.OrderByDescending(a => a.Timestamp)
            };

            // ---------------------------
            // MATERIALIZE FROM DATABASE
            // (THIS GIVES US A SMALL DATASET)
            // ---------------------------
            var all = await query.ToListAsync();

            // ---------------------------
            // IN-MEMORY UNIVERSAL SEARCH
            // WORKS 100%
            // ---------------------------
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();

                all = all
                    .Where(a =>
                        (a.Hostname ?? "").ToLower().Contains(s) ||
                        (a.Type ?? "").ToLower().Contains(s) ||
                        (a.Description ?? "").ToLower().Contains(s) ||
                        a.Timestamp.ToString("yyyy-MM-dd HH:mm:ss").ToLower().Contains(s)
                    )
                    .ToList();
            }

            // ---------------------------
            // PAGINATION AFTER FILTERING
            // ---------------------------
            var totalCount = all.Count;

            var items = all
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new
                {
                    id = a.Id,
                    type = a.Type,
                    hostname = a.Hostname,
                    timestamp = a.Timestamp,
                    description = a.Description
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

    }
}
