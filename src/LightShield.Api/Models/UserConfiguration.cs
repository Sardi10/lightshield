using System;

namespace LightShield.Api.Models
{
    public class UserConfiguration
    {
        public int Id { get; set; }

        // thresholds (editable)
        public int MaxFailedLogins { get; set; }
        public int MaxFileDeletes { get; set; }
        public int MaxFileCreates { get; set; }
        public int MaxFileModifies { get; set; }

        // contacts (editable)
        public string PhoneNumber { get; set; } = string.Empty; // E.164
        public string Email { get; set; } = string.Empty;

        // metadata
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
