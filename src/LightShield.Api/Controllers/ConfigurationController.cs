using System;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LightShield.Api.Data;
using LightShield.Api.Dtos;
using LightShield.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LightShield.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConfigurationController : ControllerBase
    {
        private readonly EventsDbContext _db;
        public ConfigurationController(EventsDbContext db) => _db = db;

        public class ContactsRequest
        {
            public string PhoneNumber { get; set; } = "";
            public string Email { get; set; } = "";

            // OPTIONAL
            public string? TelegramBotToken { get; set; }
            public string? TelegramChatId { get; set; }
        }

        // Defaults used only on first GET (no row yet)
        private static readonly UserConfiguration Defaults = new()
        {
            MaxFailedLogins = 15,
            MaxFileDeletes = 20,
            MaxFileCreates = 75,
            MaxFileModifies = 100,
            PhoneNumber = "",
            Email = "",
            TelegramBotToken = "",
            TelegramChatId = ""
        };

        [HttpGet]
        public async Task<ActionResult<ConfigurationDto>> Get()
        {
            var cfg = await _db.Configurations.AsNoTracking().FirstOrDefaultAsync();
            if (cfg == null)
            {
                cfg = new UserConfiguration
                {
                    MaxFailedLogins = Defaults.MaxFailedLogins,
                    MaxFileDeletes = Defaults.MaxFileDeletes,
                    MaxFileCreates = Defaults.MaxFileCreates,
                    MaxFileModifies = Defaults.MaxFileModifies,
                    PhoneNumber = Defaults.PhoneNumber,
                    Email = Defaults.Email,
                    TelegramBotToken = Defaults.TelegramBotToken,
                    TelegramChatId = Defaults.TelegramChatId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.Configurations.Add(cfg);
                await _db.SaveChangesAsync();
            }
            return Ok(ToDto(cfg));
        }

        [HttpPut]
        public async Task<ActionResult<ConfigurationDto>> Put([FromBody] ConfigurationDto req)
        {
            // validate thresholds
            if (req.MaxFailedLogins < 0 || req.MaxFileDeletes < 0 || req.MaxFileCreates < 0 || req.MaxFileModifies < 0)
                return BadRequest("Thresholds must be non-negative.");

            // validate contacts if present
            if (!string.IsNullOrWhiteSpace(req.Email) && !new EmailAddressAttribute().IsValid(req.Email))
                return BadRequest("Invalid email address.");

            if (!string.IsNullOrWhiteSpace(req.PhoneNumber) && !Regex.IsMatch(req.PhoneNumber, @"^\+\d{8,15}$"))
                return BadRequest("Phone must be E.164 (e.g., +15551234567).");

            var cfg = await _db.Configurations.FirstOrDefaultAsync();
            if (cfg == null)
            {
                cfg = new UserConfiguration
                {
                    MaxFailedLogins = req.MaxFailedLogins,
                    MaxFileDeletes = req.MaxFileDeletes,
                    MaxFileCreates = req.MaxFileCreates,
                    MaxFileModifies = req.MaxFileModifies,
                    PhoneNumber = req.PhoneNumber ?? "",
                    Email = req.Email ?? "",
                    TelegramBotToken = req.TelegramBotToken ?? "",
                    TelegramChatId = req.TelegramChatId ?? "",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.Configurations.Add(cfg);
            }
            else
            {
                cfg.MaxFailedLogins = req.MaxFailedLogins;
                cfg.MaxFileDeletes = req.MaxFileDeletes;
                cfg.MaxFileCreates = req.MaxFileCreates;
                cfg.MaxFileModifies = req.MaxFileModifies;
                cfg.PhoneNumber = req.PhoneNumber ?? "";
                cfg.Email = req.Email ?? "";
                cfg.TelegramBotToken = req.TelegramBotToken ?? "";
                cfg.TelegramChatId = req.TelegramChatId ?? "";
                cfg.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            return Ok(ToDto(cfg));
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] ContactsRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email) &&
                string.IsNullOrWhiteSpace(req.PhoneNumber) &&
                string.IsNullOrWhiteSpace(req.TelegramBotToken))
            {
                return BadRequest(new { error = "At least one alert channel must be provided." });
            }


            var cfg = await _db.Configurations.FirstOrDefaultAsync();
            if (cfg == null)
            {
                cfg = new UserConfiguration
                {
                    MaxFailedLogins = 15,
                    MaxFileDeletes = 20,
                    MaxFileCreates = 75,
                    MaxFileModifies = 100,
                    PhoneNumber = req.PhoneNumber,
                    Email = req.Email,
                    TelegramBotToken = req.TelegramBotToken,
                    TelegramChatId = req.TelegramChatId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.Configurations.Add(cfg);
            }
            else
            {
                cfg.PhoneNumber = req.PhoneNumber;
                cfg.Email = req.Email;
                cfg.TelegramBotToken = req.TelegramBotToken ?? "";
                cfg.TelegramChatId = req.TelegramChatId ?? "";
                cfg.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            // Minimal response; onboarding ignores content
            return Ok(new { message = "Configuration saved successfully." });
        }

        private static ConfigurationDto ToDto(UserConfiguration u) => new()
        {
            Id = u.Id,
            MaxFailedLogins = u.MaxFailedLogins,
            MaxFileDeletes = u.MaxFileDeletes,
            MaxFileCreates = u.MaxFileCreates,
            MaxFileModifies = u.MaxFileModifies,
            PhoneNumber = u.PhoneNumber,
            Email = u.Email,
            TelegramBotToken = u.TelegramBotToken,
            TelegramChatId = u.TelegramChatId,
            CreatedAt = u.CreatedAt,
            UpdatedAt = u.UpdatedAt
        };
    }
}
