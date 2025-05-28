// src/LightShield.Api/Controllers/EventsController.cs
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
            // 1) Persist to the database
            _db.Events.Add(evt);
            await _db.SaveChangesAsync();

            // 2) Log to console for debugging
            Console.WriteLine(
                $"[{evt.Timestamp:o}] {evt.Source}:{evt.Type} → {evt.PathOrMessage} @ {evt.Hostname}"
            );

            // 3) Return 202 Accepted
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
    }
}
