// src/LightShield.Api/Controllers/AlertsController.cs

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using LightShield.Api.Services.Alerts;

namespace LightShield.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AlertsController : ControllerBase
    {
        private readonly IAlertService _alertService;

        public AlertsController(IAlertService alertService)
        {
            _alertService = alertService;
        }

        [HttpPost("test")]
        public async Task<IActionResult> SendTestAlert()
        {
            var msg = $"[LightShield TEST] alert generated at {DateTime.UtcNow:O}";
            await _alertService.SendAlertAsync(msg);
            return Ok("Test alert sent (SMS + email).");
        }
    }
}
