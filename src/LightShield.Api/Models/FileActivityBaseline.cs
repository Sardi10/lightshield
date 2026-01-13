using System;

namespace LightShield.Api.Models
{
    public class FileActivityBaseline
    {
        public int Id { get; set; }

        public string Hostname { get; set; } = null!;

        // per-minute averages
        public double CreateAvg { get; set; }
        public double ModifyAvg { get; set; }
        public double DeleteAvg { get; set; }
        public double RenameAvg { get; set; }

        // standard deviation
        public double CreateStd { get; set; }
        public double ModifyStd { get; set; }
        public double DeleteStd { get; set; }
        public double RenameStd { get; set; }

        public DateTime LastUpdated { get; set; }
    }
}
