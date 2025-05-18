// src/LightShield.Api/Controllers/EventsController.cs
using Microsoft.AspNetCore.Mvc;
using LightShield.Api.Models;

namespace LightShield.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EventsController : ControllerBase
    {
        [HttpPost]
        public IActionResult Post([FromBody] Event evt)
        {
            // For now just log to console
            Console.WriteLine(
                $"[{evt.Timestamp:o}] {evt.Source}:{evt.Type} → {evt.PathOrMessage} @ {evt.Hostname}"
            );

            // TODO: persist to DB, apply anomaly rules, etc.
            return Accepted();
        }
    }
}
