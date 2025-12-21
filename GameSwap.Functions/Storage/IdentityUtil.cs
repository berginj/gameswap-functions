using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;
using System.Linq;

namespace GameSwap.Functions.Storage;

public static class IdentityUtil
{
    public sealed record Me(string UserId, string Email);

    public static Me GetMe(HttpRequestData req)
    {
        // 1) EasyAuth / App Service Authentication header (best)
        if (req.Headers.TryGetValues("X-MS-CLIENT-PRINCIPAL", out var principalHeader))
        {
            var encoded = principalHeader.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(encoded))
            {
                try
                {
                    var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                    using var doc = JsonDocument.Parse(json);

                    var claims = doc.RootElement.TryGetProperty("claims", out var c) ? c : default;

                    string? FindClaim(params string[] types)
                    {
                        if (claims.ValueKind != JsonValueKind.Array) return null;
                        foreach (var item in claims.EnumerateArray())
                        {
                            var typ = item.GetProperty("typ").GetString();
                            var val = item.GetProperty("val").GetString();
                            if (typ != null && val != null && types.Contains(typ)) return val;
                        }
                        return null;
                    }

                    var userId =
                        FindClaim(
                            "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier",
                            "nameidentifier",
                            "sub"
                        ) ?? "UNKNOWN";

                    var email =
                        FindClaim(
                            "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress",
                            "email",
                            "preferred_username",
                            "upn"
                        ) ?? "UNKNOWN";

                    return new Me(userId, email);
                }
                catch
                {
                    // fall through to header-based
                }
            }
        }

        // 2) Fallback headers (useful for local/dev/testing)
        var userIdFallback = req.Headers.TryGetValues("x-user-id", out var ids) ? ids.FirstOrDefault() : null;
        var emailFallback = req.Headers.TryGetValues("x-user-email", out var emails) ? emails.FirstOrDefault() : null;

        return new Me(userIdFallback ?? "UNKNOWN", emailFallback ?? "UNKNOWN");
    }
}
