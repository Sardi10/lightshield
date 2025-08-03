using System;

namespace LightShield.Api.Models
{
    public class UserConfiguration
    {
        public int Id { get; set; }

        // E.164 format: +15551234567
        public string PhoneNumber { get; set; } = null!;

        public string Email { get; set; } = null!;

        public DateTime CreatedAt { get; set; }
    }
}
