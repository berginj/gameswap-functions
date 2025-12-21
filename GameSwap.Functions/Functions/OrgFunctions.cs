using System.Net;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using GameSwap.Functions.Storage;
using System.Linq;

namespace GameSwap.Functions.Functions;

public class OrgFunctions
{
    private readonly ILogger _log;
    private readonly TableServiceClient _svc;

    private const string SeasonsTableName = "GameSwapSeasons";
    private const string SeasonDivisionsTableName = "GameSwapSeasonDivisions";
    private const string TeamsTableName = "GameSwapTeams";
    private const string TeamContactsTableName = "GameSwapTeamContacts";

    public OrgFunctions(ILoggerFactory lf, TableServiceClient tableServiceClient)
    {
        _log = lf.CreateLogger<OrgFunctions>();
        _svc = tableServiceClient;
    }

    public record SeasonDto(string seasonId, string name, string startDate, string endDate, bool isActive);
    public record UpsertSeasonReq(string? seasonId, string? name, string? startDate, string? endDate, bool? isActive);

    [Function("ListSeasons")]
    public async Task<HttpResponseData> ListSeasons(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "seasons")] HttpRequestData req)
    {
        try
        {
            var leagueId = ApiGuards.RequireLeagueId(req);
            var me = IdentityUtil.GetMe(req);
            await ApiGuards.RequireMemberAsync(_svc, me.UserId, leagueId);

            var table = _svc.GetTableClient(SeasonsTableName);
            await table.CreateIfNotExistsAsync();

            var pk = $"SEASON#{leagueId}";
            var list = new List<SeasonDto>();

            await foreach (var e in table.QueryAsync<TableEntity>(x => x.PartitionKey == pk))
            {
                list.Add(new SeasonDto(
                    seasonId: e.RowKey,
                    name: e.GetString("Name") ?? "",
                    startDate: e.GetString("StartDate") ?? "",
                    endDate: e.GetString("EndDate") ?? "",
                    isActive: e.GetBoolean("IsActive") ?? true
                ));
            }

            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(list.OrderByDescending(x => x.startDate).ThenBy(x => x.seasonId));
            return res;
        }
        catch (InvalidOperationException inv)
        {
            return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = inv.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return HttpUtil.Text(req, HttpStatusCode.Forbidden, "Forbidden");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "ListSeasons failed");
            return HttpUtil.Text(req, HttpStatusCode.InternalServerError, "Internal Server Error");
        }
    }

    [Function("UpsertSeason")]
    public async Task<HttpResponseData> UpsertSeason(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "seasons")] HttpRequestData req)
    {
        try
        {
            var leagueId = ApiGuards.RequireLeagueId(req);
            var me = IdentityUtil.GetMe(req);
            await ApiGuards.RequireMemberAsync(_svc, me.UserId, leagueId);

            var body = await HttpUtil.ReadJsonAsync<UpsertSeasonReq>(req);
            if (body is null) return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = "Invalid JSON body" });

            var name = (body.name ?? "").Trim();
            if (string.IsNullOrWhiteSpace(name))
                return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = "name is required" });

            var seasonId = (body.seasonId ?? "").Trim();
            if (string.IsNullOrWhiteSpace(seasonId))
                seasonId = Slug.Make(name);

            if (string.IsNullOrWhiteSpace(seasonId))
                return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = "seasonId is required" });

            var pk = $"SEASON#{leagueId}";
            var table = _svc.GetTableClient(SeasonsTableName);
            await table.CreateIfNotExistsAsync();

            var entity = new TableEntity(pk, seasonId)
            {
                ["LeagueId"] = leagueId,
                ["SeasonId"] = seasonId,
                ["Name"] = name,
                ["StartDate"] = (body.startDate ?? "").Trim(),
                ["EndDate"] = (body.endDate ?? "").Trim(),
                ["IsActive"] = body.isActive ?? true,
                ["UpdatedUtc"] = DateTimeOffset.UtcNow
            };

            await table.UpsertEntityAsync(entity, TableUpdateMode.Merge);

            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(new SeasonDto(seasonId, name, entity.GetString("StartDate") ?? "", entity.GetString("EndDate") ?? "", (bool)entity["IsActive"]));
            return res;
        }
        catch (InvalidOperationException inv)
        {
            return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = inv.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return HttpUtil.Text(req, HttpStatusCode.Forbidden, "Forbidden");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "UpsertSeason failed");
            return HttpUtil.Text(req, HttpStatusCode.InternalServerError, "Internal Server Error");
        }
    }

    public record DivisionDto(string seasonId, string divisionCode, string name, bool isActive);
    public record UpsertDivisionReq(string? seasonId, string? divisionCode, string? name, bool? isActive);

    [Function("ListDivisions")]
    public async Task<HttpResponseData> ListDivisions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "divisions/{seasonId}")] HttpRequestData req,
        string seasonId)
    {
        try
        {
            var leagueId = ApiGuards.RequireLeagueId(req);
            var me = IdentityUtil.GetMe(req);
            await ApiGuards.RequireMemberAsync(_svc, me.UserId, leagueId);

            seasonId = (seasonId ?? "").Trim();
            if (string.IsNullOrWhiteSpace(seasonId))
                return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = "seasonId is required" });

            var pk = $"DIV#{leagueId}#{seasonId}";
            var table = _svc.GetTableClient(SeasonDivisionsTableName);
            await table.CreateIfNotExistsAsync();

            var list = new List<DivisionDto>();
            await foreach (var e in table.QueryAsync<TableEntity>(x => x.PartitionKey == pk))
            {
                list.Add(new DivisionDto(
                    seasonId: seasonId,
                    divisionCode: e.RowKey,
                    name: e.GetString("Name") ?? "",
                    isActive: e.GetBoolean("IsActive") ?? true
                ));
            }

            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(list.OrderBy(x => x.divisionCode));
            return res;
        }
        catch (InvalidOperationException inv)
        {
            return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = inv.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return HttpUtil.Text(req, HttpStatusCode.Forbidden, "Forbidden");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "ListDivisions failed");
            return HttpUtil.Text(req, HttpStatusCode.InternalServerError, "Internal Server Error");
        }
    }

    // NOTE: route changed to avoid colliding with CreateDivision (global divisions CRUD)
    [Function("UpsertDivision")]
    public async Task<HttpResponseData> UpsertDivision(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "org/divisions")] HttpRequestData req)
    {
        try
        {
            var leagueId = ApiGuards.RequireLeagueId(req);
            var me = IdentityUtil.GetMe(req);
            await ApiGuards.RequireMemberAsync(_svc, me.UserId, leagueId);

            var body = await HttpUtil.ReadJsonAsync<UpsertDivisionReq>(req);
            if (body is null) return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = "Invalid JSON body" });

            var seasonId = (body.seasonId ?? "").Trim();
            var divisionCode = (body.divisionCode ?? "").Trim();
            var name = (body.name ?? "").Trim();

            if (string.IsNullOrWhiteSpace(seasonId) || string.IsNullOrWhiteSpace(divisionCode) || string.IsNullOrWhiteSpace(name))
                return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = "seasonId, divisionCode, and name are required" });

            var pk = $"DIV#{leagueId}#{seasonId}";
            var table = _svc.GetTableClient(SeasonDivisionsTableName);
            await table.CreateIfNotExistsAsync();

            var entity = new TableEntity(pk, divisionCode)
            {
                ["LeagueId"] = leagueId,
                ["SeasonId"] = seasonId,
                ["DivisionCode"] = divisionCode,
                ["Name"] = name,
                ["IsActive"] = body.isActive ?? true,
                ["UpdatedUtc"] = DateTimeOffset.UtcNow
            };

            await table.UpsertEntityAsync(entity, TableUpdateMode.Merge);

            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(new DivisionDto(seasonId, divisionCode, name, (bool)entity["IsActive"]));
            return res;
        }
        catch (InvalidOperationException inv)
        {
            return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = inv.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return HttpUtil.Text(req, HttpStatusCode.Forbidden, "Forbidden");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "UpsertDivision failed");
            return HttpUtil.Text(req, HttpStatusCode.InternalServerError, "Internal Server Error");
        }
    }

    public record TeamDto(string seasonId, string divisionCode, string teamId, string name, bool isActive);
    public record UpsertTeamReq(string? seasonId, string? divisionCode, string? teamId, string? name, bool? isActive);

    [Function("ListTeams")]
    public async Task<HttpResponseData> ListTeams(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "teams/{seasonId}/{divisionCode}")] HttpRequestData req,
        string seasonId,
        string divisionCode)
    {
        try
        {
            var leagueId = ApiGuards.RequireLeagueId(req);
            var me = IdentityUtil.GetMe(req);
            await ApiGuards.RequireMemberAsync(_svc, me.UserId, leagueId);

            seasonId = (seasonId ?? "").Trim();
            divisionCode = (divisionCode ?? "").Trim();

            if (string.IsNullOrWhiteSpace(seasonId) || string.IsNullOrWhiteSpace(divisionCode))
                return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = "seasonId and divisionCode are required" });

            var pk = $"TEAM#{leagueId}#{seasonId}#{divisionCode}";
            var table = _svc.GetTableClient(TeamsTableName);
            await table.CreateIfNotExistsAsync();

            var list = new List<TeamDto>();
            await foreach (var e in table.QueryAsync<TableEntity>(x => x.PartitionKey == pk))
            {
                list.Add(new TeamDto(
                    seasonId: seasonId,
                    divisionCode: divisionCode,
                    teamId: e.RowKey,
                    name: e.GetString("Name") ?? "",
                    isActive: e.GetBoolean("IsActive") ?? true
                ));
            }

            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(list.OrderBy(x => x.name));
            return res;
        }
        catch (InvalidOperationException inv)
        {
            return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = inv.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return HttpUtil.Text(req, HttpStatusCode.Forbidden, "Forbidden");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "ListTeams failed");
            return HttpUtil.Text(req, HttpStatusCode.InternalServerError, "Internal Server Error");
        }
    }

    [Function("UpsertTeam")]
    public async Task<HttpResponseData> UpsertTeam(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "teams")] HttpRequestData req)
    {
        try
        {
            var leagueId = ApiGuards.RequireLeagueId(req);
            var me = IdentityUtil.GetMe(req);
            await ApiGuards.RequireMemberAsync(_svc, me.UserId, leagueId);

            var body = await HttpUtil.ReadJsonAsync<UpsertTeamReq>(req);
            if (body is null) return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = "Invalid JSON body" });

            var seasonId = (body.seasonId ?? "").Trim();
            var divisionCode = (body.divisionCode ?? "").Trim();
            var name = (body.name ?? "").Trim();

            if (string.IsNullOrWhiteSpace(seasonId) || string.IsNullOrWhiteSpace(divisionCode) || string.IsNullOrWhiteSpace(name))
                return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = "seasonId, divisionCode, and name are required" });

            var teamId = (body.teamId ?? "").Trim();
            if (string.IsNullOrWhiteSpace(teamId))
                teamId = Slug.Make(name);

            if (string.IsNullOrWhiteSpace(teamId))
                return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = "teamId is required" });

            var pk = $"TEAM#{leagueId}#{seasonId}#{divisionCode}";
            var table = _svc.GetTableClient(TeamsTableName);
            await table.CreateIfNotExistsAsync();

            var entity = new TableEntity(pk, teamId)
            {
                ["LeagueId"] = leagueId,
                ["SeasonId"] = seasonId,
                ["DivisionCode"] = divisionCode,
                ["TeamId"] = teamId,
                ["Name"] = name,
                ["IsActive"] = body.isActive ?? true,
                ["UpdatedUtc"] = DateTimeOffset.UtcNow
            };

            await table.UpsertEntityAsync(entity, TableUpdateMode.Merge);

            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(new TeamDto(seasonId, divisionCode, teamId, name, (bool)entity["IsActive"]));
            return res;
        }
        catch (InvalidOperationException inv)
        {
            return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = inv.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return HttpUtil.Text(req, HttpStatusCode.Forbidden, "Forbidden");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "UpsertTeam failed");
            return HttpUtil.Text(req, HttpStatusCode.InternalServerError, "Internal Server Error");
        }
    }

    public record TeamContactDto(string seasonId, string divisionCode, string teamId, string email, string name, string role, bool isActive);
    public record UpsertTeamContactReq(string? seasonId, string? divisionCode, string? teamId, string? email, string? name, string? role, bool? isActive);

    [Function("ListTeamContacts")]
    public async Task<HttpResponseData> ListContacts(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "teamcontacts/{seasonId}/{divisionCode}/{teamId}")] HttpRequestData req,
        string seasonId,
        string divisionCode,
        string teamId)
    {
        try
        {
            var leagueId = ApiGuards.RequireLeagueId(req);
            var me = IdentityUtil.GetMe(req);
            await ApiGuards.RequireMemberAsync(_svc, me.UserId, leagueId);

            seasonId = (seasonId ?? "").Trim();
            divisionCode = (divisionCode ?? "").Trim();
            teamId = (teamId ?? "").Trim();

            if (string.IsNullOrWhiteSpace(seasonId) || string.IsNullOrWhiteSpace(divisionCode) || string.IsNullOrWhiteSpace(teamId))
                return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = "seasonId, divisionCode, and teamId are required" });

            var pk = $"CONTACT#{leagueId}#{seasonId}#{divisionCode}#{teamId}";
            var table = _svc.GetTableClient(TeamContactsTableName);
            await table.CreateIfNotExistsAsync();

            var list = new List<TeamContactDto>();
            await foreach (var e in table.QueryAsync<TableEntity>(x => x.PartitionKey == pk))
            {
                list.Add(new TeamContactDto(
                    seasonId: seasonId,
                    divisionCode: divisionCode,
                    teamId: teamId,
                    email: e.RowKey,
                    name: e.GetString("Name") ?? "",
                    role: e.GetString("Role") ?? "",
                    isActive: e.GetBoolean("IsActive") ?? true
                ));
            }

            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(list.OrderBy(x => x.name).ThenBy(x => x.email));
            return res;
        }
        catch (InvalidOperationException inv)
        {
            return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = inv.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return HttpUtil.Text(req, HttpStatusCode.Forbidden, "Forbidden");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "ListTeamContacts failed");
            return HttpUtil.Text(req, HttpStatusCode.InternalServerError, "Internal Server Error");
        }
    }

    [Function("UpsertTeamContact")]
    public async Task<HttpResponseData> UpsertContact(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "teamcontacts")] HttpRequestData req)
    {
        try
        {
            var leagueId = ApiGuards.RequireLeagueId(req);
            var me = IdentityUtil.GetMe(req);
            await ApiGuards.RequireMemberAsync(_svc, me.UserId, leagueId);

            var body = await HttpUtil.ReadJsonAsync<UpsertTeamContactReq>(req);
            if (body is null) return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = "Invalid JSON body" });

            var seasonId = (body.seasonId ?? "").Trim();
            var divisionCode = (body.divisionCode ?? "").Trim();
            var teamId = (body.teamId ?? "").Trim();
            var email = (body.email ?? "").Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(seasonId) || string.IsNullOrWhiteSpace(divisionCode) || string.IsNullOrWhiteSpace(teamId))
                return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = "seasonId, divisionCode, and teamId are required" });

            if (string.IsNullOrWhiteSpace(email))
                return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = "email is required" });

            var name = (body.name ?? "").Trim();
            var role = (body.role ?? "").Trim();

            var pk = $"CONTACT#{leagueId}#{seasonId}#{divisionCode}#{teamId}";
            var table = _svc.GetTableClient(TeamContactsTableName);
            await table.CreateIfNotExistsAsync();

            var entity = new TableEntity(pk, email)
            {
                ["LeagueId"] = leagueId,
                ["SeasonId"] = seasonId,
                ["DivisionCode"] = divisionCode,
                ["TeamId"] = teamId,
                ["Email"] = email,
                ["Name"] = name,
                ["Role"] = role,
                ["IsActive"] = body.isActive ?? true,
                ["UpdatedUtc"] = DateTimeOffset.UtcNow
            };

            await table.UpsertEntityAsync(entity, TableUpdateMode.Merge);

            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(new TeamContactDto(seasonId, divisionCode, teamId, email, name, role, (bool)entity["IsActive"]));
            return res;
        }
        catch (InvalidOperationException inv)
        {
            return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = inv.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return HttpUtil.Text(req, HttpStatusCode.Forbidden, "Forbidden");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "UpsertTeamContact failed");
            return HttpUtil.Text(req, HttpStatusCode.InternalServerError, "Internal Server Error");
        }
    }
}
