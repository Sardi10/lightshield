using System.ComponentModel.DataAnnotations;
using System.Net;

namespace LightShield.Api.Dtos;

public sealed class EventIngestDto : IValidatableObject
{
    private static readonly HashSet<string> AgentTypes = new(StringComparer.OrdinalIgnoreCase)
        { "filecreate", "filemodify", "filedelete", "filerename" };
    private static readonly HashSet<string> LogParserTypes = new(StringComparer.OrdinalIgnoreCase)
        { "loginsuccess", "loginfailure" };
    private static readonly HashSet<string> OperatingSystems = new(StringComparer.OrdinalIgnoreCase)
        { "windows", "linux", "macos" };

    [Required, StringLength(32, MinimumLength = 3)]
    public string Source { get; set; } = string.Empty;

    [Required, StringLength(64, MinimumLength = 3)]
    public string Type { get; set; } = string.Empty;

    [Required, StringLength(8192, MinimumLength = 1)]
    public string PathOrMessage { get; set; } = string.Empty;

    [Required, StringLength(255, MinimumLength = 1)]
    public string Hostname { get; set; } = string.Empty;

    [Required, StringLength(16)]
    public string OperatingSystem { get; set; } = string.Empty;

    public int? EventId { get; set; }
    [Range(0, 99)] public int? LogonType { get; set; }
    [StringLength(256)] public string? Username { get; set; }
    [StringLength(45)] public string? IPAddress { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var source = Source.Trim();
        var type = Type.Trim();
        if (!source.Equals("agent", StringComparison.OrdinalIgnoreCase) &&
            !source.Equals("logparser", StringComparison.OrdinalIgnoreCase))
            yield return new ValidationResult("Source must be 'agent' or 'logparser'.", [nameof(Source)]);
        else if (source.Equals("agent", StringComparison.OrdinalIgnoreCase) && !AgentTypes.Contains(type))
            yield return new ValidationResult("The agent source may only submit file operation events.", [nameof(Type)]);
        else if (source.Equals("logparser", StringComparison.OrdinalIgnoreCase) && !LogParserTypes.Contains(type))
            yield return new ValidationResult("The logparser source may only submit login events.", [nameof(Type)]);

        if (!OperatingSystems.Contains(OperatingSystem.Trim()))
            yield return new ValidationResult("OperatingSystem must be windows, linux, or macos.", [nameof(OperatingSystem)]);

        if (!string.IsNullOrWhiteSpace(IPAddress) && !System.Net.IPAddress.TryParse(IPAddress, out _))
            yield return new ValidationResult("IPAddress must be a valid IPv4 or IPv6 address.", [nameof(IPAddress)]);
    }
}
