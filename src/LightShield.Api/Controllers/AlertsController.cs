using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LightShield.Api.Data;
using LightShield.Api.Models;

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
        public async Task<IEnumerable<Alert>> Get()
        {
            return await _db.Alerts
                .OrderByDescending(a => a.Timestamp)
                .Take(50)
                .ToListAsync();
        }
    }
}
