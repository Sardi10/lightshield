// src/LightShield.Api/Models/Event.cs
using System;

namespace LightShield.Api.Models
{
    public class Event
    {
        // "agent" or "logparser"
        public string Source { get; set; }

        // e.g. "FileCreated", "LoginFailed"
        public string Type { get; set; }

        // file path or log message
        public string PathOrMessage { get; set; }

        public DateTime Timestamp { get; set; }

        // name of the machine that sent the event
        public string Hostname { get; set; }
    }
}
