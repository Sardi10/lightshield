// src/LightShield.Common/Models/Event.cs
using System;

namespace LightShield.Common.Models
{
    public class Event
    {
        public string Source { get; set; } = default!;
        public string Type { get; set; } = default!;
        public string PathOrMessage { get; set; } = default!;
        public DateTime Timestamp { get; set; }
        public string Hostname { get; set; } = default!;
    }
}

