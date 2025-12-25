namespace GameSwap.Functions.Auth;

public sealed class AuthOptions
{
    public string? TenantId { get; init; }
    public string? ClientId { get; init; }
    public string? Audience { get; init; }
    public string? Authority { get; init; }
    public string? Issuer { get; init; }
    public bool RequireAuthentication { get; init; }
    public string? AdminRoles { get; init; }

    public string? ResolveAuthority()
    {
        if (!string.IsNullOrWhiteSpace(Authority)) return Authority;
        if (string.IsNullOrWhiteSpace(TenantId)) return null;
        return $"https://login.microsoftonline.com/{TenantId}";
    }

    public string? ResolveIssuer()
    {
        if (!string.IsNullOrWhiteSpace(Issuer)) return Issuer;
        var authority = ResolveAuthority();
        return authority is null ? null : $"{authority}/v2.0";
    }

    public IEnumerable<string> ResolveAudiences()
    {
        if (!string.IsNullOrWhiteSpace(Audience)) return new[] { Audience };
        if (!string.IsNullOrWhiteSpace(ClientId)) return new[] { ClientId };
        return Array.Empty<string>();
    }
}
