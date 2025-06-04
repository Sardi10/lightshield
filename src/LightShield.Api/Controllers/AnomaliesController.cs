using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LightShield.Api.Data;
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
        public async Task<IEnumerable<Anomaly>> GetAll()
        {
            // Return the most recent 50 anomalies, ordered by timestamp descending
            return await _db.Anomalies
                            .AsNoTracking()
                            .OrderByDescending(a => a.Timestamp)
                            .Take(50)
                            .ToListAsync();
        }
    }
}
