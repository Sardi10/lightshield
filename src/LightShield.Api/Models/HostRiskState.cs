using System;

namespace LightShield.Api.Models
{
	public class HostRiskState
	{
		public int Id { get; set; }

		public string Hostname { get; set; } = null!;

		// Accumulated risk score
		public double RiskScore { get; set; }

		// Last time risk was updated (for decay)
		public DateTime LastUpdated { get; set; }
	}
}
