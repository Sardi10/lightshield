using System;
using System.Linq;
using System.Threading.Tasks;
using LightShield.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LightShield.Api.Models;


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

        // GET: /api/anomalies
        [HttpGet]
        public async Task<IActionResult> Get(
            [FromQuery] string? eventType,
            [FromQuery] string? hostname,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            var query = _db.Anomalies.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(eventType))
                query = query.Where(a =>
                    a.Type.Equals(eventType, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(hostname))
                query = query.Where(a =>
                    a.Hostname.Equals(hostname, StringComparison.OrdinalIgnoreCase));

            if (startDate.HasValue)
                query = query.Where(a =>
                    a.Timestamp >= startDate.Value.ToUniversalTime());

            if (endDate.HasValue)
                query = query.Where(a =>
                    a.Timestamp <= endDate.Value.ToUniversalTime());

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(a => a.Timestamp)
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
                .ToListAsync();

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
