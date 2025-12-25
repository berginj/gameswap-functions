using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;
using System.Linq;

namespace GameSwap.Functions.Storage;

public static class IdentityUtil
{
    public sealed record Me(string UserId, string Email, IReadOnlyCollection<string> Roles);

    public static Me GetMe(HttpRequestData req)
    {
        // 1) Static Web Apps / EasyAuth principal header (best)
        // Support both common casings.
        if (TryGetHeader(req, "x-ms-client-principal", out var encoded) ||
            TryGetHeader(req, "X-MS-CLIENT-PRINCIPAL", out encoded))
        {
            if (!string.IsNullOrWhiteSpace(encoded))
            {
                try
                {
                    var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    // Prefer canonical SWA root fields first (most reliable)
                    var userId = TryGetString(root, "userId");
                    var userDetails = TryGetString(root, "userDetails"); // often email/username

                    // Then fall back to claims if needed
                    var claims = root.TryGetProperty("claims", out var c) ? c : default;

                    string? FindClaim(params string[] types)
                    {
                        if (claims.ValueKind != JsonValueKind.Array) return null;
                        foreach (var item in claims.EnumerateArray())
                        {
                            var typ = item.TryGetProperty("typ", out var t) ? t.GetString() : null;
                            var val = item.TryGetProperty("val", out var v) ? v.GetString() : null;
                            if (typ != null && val != null && types.Contains(typ)) return val;
                        }
                        return null;
                    }

                    userId ??=
                        FindClaim(
                            "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier",
                            "nameidentifier",
                            "sub"
                        );

                    var email = userDetails;
                    email ??=
                        FindClaim(
                            "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress",
                            "emails",
                            "email",
                            "preferred_username",
                            "upn"
                        );

                    var roles = FindRoles(claims);

                    return new Me(
                        string.IsNullOrWhiteSpace(userId) ? "UNKNOWN" : userId!,
                        string.IsNullOrWhiteSpace(email) ? "UNKNOWN" : email!,
                        roles
                    );
                }
                catch
                {
                    // fall through to dev headers
                }
            }
        }

        // 2) Fallback headers (useful for local/dev/testing)
        var userIdFallback = req.Headers.TryGetValues("x-user-id", out var ids) ? ids.FirstOrDefault() : null;
        var emailFallback = req.Headers.TryGetValues("x-user-email", out var emails) ? emails.FirstOrDefault() : null;
        var rolesFallback = req.Headers.TryGetValues("x-user-roles", out var roleHeaders)
            ? ParseRoles(roleHeaders.FirstOrDefault())
            : Array.Empty<string>();

        return new Me(userIdFallback ?? "UNKNOWN", emailFallback ?? "UNKNOWN", rolesFallback);
    }

    private static bool TryGetHeader(HttpRequestData req, string name, out string? value)
    {
        value = null;
        if (req.Headers.TryGetValues(name, out var values))
        {
            value = values.FirstOrDefault();
            return true;
        }
        return false;
    }

    private static string? TryGetString(JsonElement root, string prop)
    {
        if (root.ValueKind != JsonValueKind.Object) return null;
        if (!root.TryGetProperty(prop, out var el)) return null;
        if (el.ValueKind != JsonValueKind.String) return null;
        return el.GetString();
    }

    private static IReadOnlyCollection<string> FindRoles(JsonElement claims)
    {
        if (claims.ValueKind != JsonValueKind.Array) return Array.Empty<string>();
        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in claims.EnumerateArray())
        {
            var typ = item.TryGetProperty("typ", out var t) ? t.GetString() : null;
            if (typ is null) continue;
            if (!IsRoleClaim(typ)) continue;
            var val = item.TryGetProperty("val", out var v) ? v.GetString() : null;
            if (!string.IsNullOrWhiteSpace(val))
            {
                roles.Add(val);
            }
        }

        return roles.ToArray();
    }

    private static bool IsRoleClaim(string type)
        => string.Equals(type, "roles", StringComparison.OrdinalIgnoreCase)
           || string.Equals(type, "role", StringComparison.OrdinalIgnoreCase)
           || string.Equals(type, "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
               StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyCollection<string> ParseRoles(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<string>();
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
