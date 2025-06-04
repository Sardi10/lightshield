using System;

namespace LightShield.Api.Models
{
    public class Anomaly
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Type { get; set; }        // e.g., "FailedLoginBurst"
        public string Description { get; set; } // e.g., "7 failed logins in last 5 minutes"
        public string Hostname { get; set; }    // which machine/source triggered it
    }
}
