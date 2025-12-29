using System;

namespace LightShield.Api.Models
{
    public class IncidentState
    {
        public int Id { get; set; }
        public string Type { get; set; }      // FileModifyBurst
        public string Hostname { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime LastEventTime { get; set; }
        public int Count { get; set; }
        public bool IsActive { get; set; }
    }
} 