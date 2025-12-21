using System.Net;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker.Http;
using System.Linq;

namespace GameSwap.Functions.Storage;

public static class ApiGuards
{
    private const string MembershipsTableName = "GameSwapMemberships";

    /// <summary>
    /// Canonical league scoping rule:
    /// - Prefer header "x-league-id"
    /// - Fallback to querystring "?leagueId="
    /// - If both are present and disagree => 400
    /// </summary>
    public static string RequireLeagueId(HttpRequestData req)
    {
        var header = req.Headers.TryGetValues("x-league-id", out var vals) ? vals.FirstOrDefault() : null;
        header = (header ?? "").Trim();

        var query = GetQueryParam(req, "leagueId").Trim();

        if (!string.IsNullOrWhiteSpace(header) && !string.IsNullOrWhiteSpace(query) &&
            !string.Equals(header, query, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("leagueId mismatch between header x-league-id and query ?leagueId=...");
        }

        var leagueId = !string.IsNullOrWhiteSpace(header) ? header : query;
        if (string.IsNullOrWhiteSpace(leagueId))
            throw new InvalidOperationException("Missing leagueId. Send x-league-id header (preferred) or ?leagueId=...");

        return leagueId;
    }

    /// <summary>
    /// Throws if caller is not a member of the specified league.
    /// </summary>
    public static async Task RequireMemberAsync(TableServiceClient svc, string? userId, string leagueId)
    {
        if (!await IsMemberAsync(svc, userId, leagueId))
            throw new UnauthorizedAccessException("Forbidden");
    }

    /// <summary>
    /// "Admin" is an early-stage bootstrap concept.
    /// - If REQUIRE_ADMIN_ROLE=true => must have at least one membership row with Role == "Admin"
    /// - Else => must have at least one membership row (any role) in GameSwapMemberships
    /// </summary>
    public static async Task RequireAdmin(TableServiceClient svc, string? userId)
    {
        userId = (userId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(userId) || userId.Equals("UNKNOWN", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Forbidden");

        var requireAdminRole = string.Equals(
            Environment.GetEnvironmentVariable("REQUIRE_ADMIN_ROLE"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        var memberships = svc.GetTableClient(MembershipsTableName);
        await memberships.CreateIfNotExistsAsync();

        // Membership table is PK=userId, RK=leagueId
        var filter = $"PartitionKey eq '{EscapeOData(userId)}'";
        var hasAny = false;

        await foreach (var e in memberships.QueryAsync<TableEntity>(filter: filter, maxPerPage: 100))
        {
            hasAny = true;

            if (!requireAdminRole)
                return;

            var role = (e.GetString("Role") ?? "").Trim();
            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
                return;
        }

        if (!hasAny)
            throw new UnauthorizedAccessException("Forbidden");

        // If we got here: REQUIRE_ADMIN_ROLE=true and no Admin membership found
        throw new UnauthorizedAccessException("Admin role required.");
    }

    public static async Task<bool> IsMemberAsync(TableServiceClient svc, string? userId, string leagueId)
    {
        userId = (userId ?? "").Trim();
        leagueId = (leagueId ?? "").Trim();

        if (string.IsNullOrWhiteSpace(userId) || userId.Equals("UNKNOWN", StringComparison.OrdinalIgnoreCase))
            return false;
        if (string.IsNullOrWhiteSpace(leagueId))
            return false;

        var memberships = svc.GetTableClient(MembershipsTableName);
        await memberships.CreateIfNotExistsAsync();

        try
        {
            _ = await memberships.GetEntityAsync<TableEntity>(userId, leagueId);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    public static async Task<string> GetRoleAsync(TableServiceClient svc, string? userId, string leagueId)
    {
        userId = (userId ?? "").Trim();
        leagueId = (leagueId ?? "").Trim();

        if (string.IsNullOrWhiteSpace(userId) || userId.Equals("UNKNOWN", StringComparison.OrdinalIgnoreCase))
            return "";

        var memberships = svc.GetTableClient(MembershipsTableName);
        await memberships.CreateIfNotExistsAsync();

        try
        {
            var e = (await memberships.GetEntityAsync<TableEntity>(userId, leagueId)).Value;
            return (e.GetString("Role") ?? "").Trim();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return "";
        }
    }

    public static string GetQueryParam(HttpRequestData req, string key)
    {
        var q = req.Url.Query ?? "";
        if (string.IsNullOrWhiteSpace(q)) return "";
        var raw = q.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in raw)
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            var k = Uri.UnescapeDataString(kv[0]);
            if (!string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) continue;
            return Uri.UnescapeDataString(kv[1] ?? "");
        }
        return "";
    }

    public static string EscapeOData(string s) => (s ?? "").Replace("'", "''");
}
