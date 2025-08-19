using System;

namespace LightShield.Api.Dtos
{
    public class ConfigurationDto
    {
        public int? Id { get; set; }
        public int MaxFailedLogins { get; set; }
        public int MaxFileDeletes { get; set; }
        public int MaxFileCreates { get; set; }
        public int MaxFileModifies { get; set; }
        public string PhoneNumber { get; set; } = "";
        public string Email { get; set; } = "";
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
