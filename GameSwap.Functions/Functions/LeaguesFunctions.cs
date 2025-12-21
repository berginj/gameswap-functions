using System.Net;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using GameSwap.Functions.Storage;
using System.Linq;

namespace GameSwap.Functions.Functions;

public class LeaguesFunctions
{
    private readonly TableServiceClient _svc;
    private readonly ILogger _log;

    // Normalize naming: keep leagues in one table (still not league-scoped; it is the league directory)
    private const string LeaguesTableName = "GameSwapLeagues";
    private const string PK = "LEAGUE";

    public LeaguesFunctions(ILoggerFactory lf, TableServiceClient tableServiceClient)
    {
        _log = lf.CreateLogger<LeaguesFunctions>();
        _svc = tableServiceClient;
    }

    public record LeagueDto(string leagueId, string name, string shortName, string timezone, string primaryContactEmail, string status);
    public record CreateLeagueReq(string? leagueId, string? name, string? shortName, string? timezone, string? primaryContactEmail);

    [Function("ListLeagues_Admin")]
    public async Task<HttpResponseData> ListAdmin(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "admin/leagues")] HttpRequestData req)
    {
        try
        {
            var me = IdentityUtil.GetMe(req);
            await ApiGuards.RequireAdmin(_svc, me.UserId);

            var table = _svc.GetTableClient(LeaguesTableName);
            await table.CreateIfNotExistsAsync();

            var list = new List<LeagueDto>();
            await foreach (var e in table.QueryAsync<TableEntity>(x => x.PartitionKey == PK))
                list.Add(ToDto(e));

            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(list.OrderBy(x => x.leagueId));
            return res;
        }
        catch (UnauthorizedAccessException ua)
        {
            return HttpUtil.Text(req, HttpStatusCode.Forbidden, ua.Message);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "ListLeagues_Admin failed");
            return HttpUtil.Text(req, HttpStatusCode.InternalServerError, "Internal Server Error");
        }
    }

    [Function("CreateLeague_Admin")]
    public async Task<HttpResponseData> CreateAdmin(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "admin/leagues")] HttpRequestData req)
    {
        try
        {
            var me = IdentityUtil.GetMe(req);
            await ApiGuards.RequireAdmin(_svc, me.UserId);

            var body = await HttpUtil.ReadJsonAsync<CreateLeagueReq>(req);
            if (body is null) return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = "Invalid JSON body" });

            var leagueId = (body.leagueId ?? "").Trim();
            var name = (body.name ?? "").Trim();

            if (string.IsNullOrWhiteSpace(name))
                return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = "name is required" });

            if (string.IsNullOrWhiteSpace(leagueId))
                leagueId = Slug.Make(name);

            if (string.IsNullOrWhiteSpace(leagueId))
                return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = "leagueId is required" });

            var now = DateTimeOffset.UtcNow;

            var entity = new TableEntity(PK, leagueId)
            {
                ["LeagueId"] = leagueId,
                ["Name"] = name,
                ["ShortName"] = (body.shortName ?? "").Trim(),
                ["Timezone"] = string.IsNullOrWhiteSpace(body.timezone) ? "America/New_York" : body.timezone!.Trim(),
                ["PrimaryContactEmail"] = (body.primaryContactEmail ?? "").Trim(),
                ["Status"] = "Active",
                ["CreatedUtc"] = now,
                ["UpdatedUtc"] = now
            };

            var table = _svc.GetTableClient(LeaguesTableName);
            await table.CreateIfNotExistsAsync();

            try
            {
                await table.AddEntityAsync(entity);
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                return HttpUtil.Json(req, HttpStatusCode.Conflict, new { error = $"league already exists: {leagueId}" });
            }

            var res = req.CreateResponse(HttpStatusCode.Created);
            await res.WriteAsJsonAsync(ToDto(entity));
            return res;
        }
        catch (UnauthorizedAccessException ua)
        {
            return HttpUtil.Text(req, HttpStatusCode.Forbidden, ua.Message);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "CreateLeague_Admin failed");
            return HttpUtil.Text(req, HttpStatusCode.InternalServerError, "Internal Server Error");
        }
    }

    [Function("GetMyLeague")]
    public async Task<HttpResponseData> GetMyLeague(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me/league")] HttpRequestData req)
    {
        try
        {
            var leagueId = ApiGuards.RequireLeagueId(req);

            var me = IdentityUtil.GetMe(req);
            if (!await ApiGuards.IsMemberAsync(_svc, me.UserId, leagueId))
                return HttpUtil.Text(req, HttpStatusCode.Forbidden, "Forbidden");

            var table = _svc.GetTableClient(LeaguesTableName);
            await table.CreateIfNotExistsAsync();

            try
            {
                var e = (await table.GetEntityAsync<TableEntity>(PK, leagueId)).Value;
                var res = req.CreateResponse(HttpStatusCode.OK);
                await res.WriteAsJsonAsync(ToDto(e));
                return res;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return HttpUtil.Json(req, HttpStatusCode.NotFound, new { error = $"league not found: {leagueId}" });
            }
        }
        catch (InvalidOperationException inv)
        {
            return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = inv.Message });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "GetMyLeague failed");
            return HttpUtil.Text(req, HttpStatusCode.InternalServerError, "Internal Server Error");
        }
    }

    private static LeagueDto ToDto(TableEntity e) =>
        new(
            leagueId: e.RowKey,
            name: e.GetString("Name") ?? "",
            shortName: e.GetString("ShortName") ?? "",
            timezone: e.GetString("Timezone") ?? "America/New_York",
            primaryContactEmail: e.GetString("PrimaryContactEmail") ?? "",
            status: e.GetString("Status") ?? "Active"
        );
}
