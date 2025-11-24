// src/LightShield.Common/Models/Event.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace LightShield.Common.Models
{
    public class Event
    {
        [Key]
        public int Id { get; set; }
        public string Source { get; set; } = default!;          // “logparser”, “agent”, etc.
        public string Type { get; set; } = default!;            // LoginSuccess, LoginFailure, etc.
        public string PathOrMessage { get; set; } = default!;
        public DateTime Timestamp { get; set; }
        public string Hostname { get; set; } = default!;

        
        public string OperatingSystem { get; set; } = default!; // "windows", "linux", "macos"
        public string Severity { get; set; } = "Info";          // Info, Warning, Critical

        public int? EventId { get; set; }                       // Windows EventID (4624/4625)
        public int? LogonType { get; set; }                     // Windows LogonType (2,3,10)

        public string? Username { get; set; }                   // user performing login
        public string? IPAddress { get; set; }                  // attacker IP or local IP
        public string? Country { get; set; }                    // GeoIP lookups
        public string? City { get; set; }                       // Geolocation city
    }
}
