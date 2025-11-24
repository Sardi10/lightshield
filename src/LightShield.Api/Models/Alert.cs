using System;

namespace LightShield.Api.Models
{
    public class Alert
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }

        public string Type { get; set; } = string.Empty;   // anomaly type
        public string Message { get; set; } = string.Empty; // alert text
        public string Channel { get; set; } = string.Empty; // e.g. "SMS" or "Email"
        public string Hostname { get; set; } = string.Empty;
    }
}
