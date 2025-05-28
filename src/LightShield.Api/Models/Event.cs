// src/LightShield.Api/Models/Event.cs
using System;
using System.ComponentModel.DataAnnotations;


namespace LightShield.Api.Models
{
    public class Event
    {
        [Key]
        public int Id { get; set; }

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
