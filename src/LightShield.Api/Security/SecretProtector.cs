using Microsoft.AspNetCore.DataProtection;

namespace LightShield.Api.Security;

public sealed class SecretProtector
{
    private const string Prefix = "protected:v1:";
    private readonly IDataProtector _protector;

    public SecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("LightShield.UserConfiguration.Secrets.v1");
    }

    public string Protect(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        if (value.StartsWith(Prefix, StringComparison.Ordinal)) return value;
        return Prefix + _protector.Protect(value);
    }

    public string Unprotect(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        if (!value.StartsWith(Prefix, StringComparison.Ordinal)) return value;
        return _protector.Unprotect(value[Prefix.Length..]);
    }

    public bool NeedsProtection(string? value) =>
        !string.IsNullOrWhiteSpace(value) && !value.StartsWith(Prefix, StringComparison.Ordinal);
}
