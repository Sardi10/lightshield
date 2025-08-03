using System;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LightShield.Api.Data;
using LightShield.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace LightShield.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConfigurationController : ControllerBase
    {
        private readonly EventsDbContext _db;

        public ConfigurationController(EventsDbContext db)
        {
            _db = db;
        }

        public class ConfigurationRequest
        {
            public string PhoneNumber { get; set; } = null!;
            public string Email { get; set; } = null!;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] ConfigurationRequest req)
        {
            // Phone must be E.164: +<country><number>, 10–15 digits
            if (string.IsNullOrWhiteSpace(req.PhoneNumber) ||
                !Regex.IsMatch(req.PhoneNumber, @"^\+\d{10,15}$"))
            {
                return BadRequest("Phone number must be in E.164 format (e.g. +15551234567).");
            }

            // Email validation
            if (string.IsNullOrWhiteSpace(req.Email) ||
                !new EmailAddressAttribute().IsValid(req.Email))
            {
                return BadRequest("Invalid email address.");
            }

            var config = new UserConfiguration
            {
                PhoneNumber = req.PhoneNumber,
                Email = req.Email,
                CreatedAt = DateTime.UtcNow
            };

            _db.Configurations.Add(config);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Configuration saved successfully." });
        }
    }
}
