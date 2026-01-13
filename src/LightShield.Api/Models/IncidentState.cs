using System;

namespace LightShield.Api.Models
{
    public class IncidentState
    {
        public int Id { get; set; }
        public string Type { get; set; } = null!;
        public string Hostname { get; set; } = null!;
        public DateTime StartTime { get; set; }
        public DateTime LastEventTime { get; set; }
        public int Count { get; set; }
        public bool IsActive { get; set; }
        public DateTime? CooldownUntil { get; set; }
    }

}