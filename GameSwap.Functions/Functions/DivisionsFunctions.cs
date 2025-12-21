using System.Net;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using GameSwap.Functions.Storage;
using System.Linq;

namespace GameSwap.Functions.Functions;

public class DivisionsFunctions
{
    private readonly TableServiceClient _svc;
    private readonly ILogger _log;

    private const string TableName = "GameSwapDivisions";

    public DivisionsFunctions(ILoggerFactory lf, TableServiceClient tableServiceClient)
    {
        _log = lf.CreateLogger<DivisionsFunctions>();
        _svc = tableServiceClient;
    }

    public record DivisionDto(string code, string name, bool isActive);
    public record CreateReq(string? name, string? code, bool? isActive);
    public record UpdateReq(string? name, bool? isActive);

    [Function("GetDivisions")]
    public async Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "divisions")] HttpRequestData req)
    {
        try
        {
            var leagueId = ApiGuards.RequireLeagueId(req);
            var me = IdentityUtil.GetMe(req);
            await ApiGuards.RequireMemberAsync(_svc, me.UserId, leagueId);

            var pk = $"DIV#{leagueId}";
            var table = _svc.GetTableClient(TableName);
            await table.CreateIfNotExistsAsync();

            var list = new List<DivisionDto>();
            await foreach (var e in table.QueryAsync<TableEntity>(x => x.PartitionKey == pk))
            {
                list.Add(new DivisionDto(
                    code: e.RowKey,
                    name: e.GetString("Name") ?? "",
                    isActive: e.GetBoolean("IsActive") ?? true
                ));
            }

            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(list.OrderBy(x => x.code));
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
            _log.LogError(ex, "GetDivisions failed");
            return HttpUtil.Text(req, HttpStatusCode.InternalServerError, "Internal Server Error");
        }
    }

    [Function("CreateDivision")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "divisions")] HttpRequestData req)
    {
        try
        {
            var leagueId = ApiGuards.RequireLeagueId(req);
            var me = IdentityUtil.GetMe(req);
            await ApiGuards.RequireMemberAsync(_svc, me.UserId, leagueId);

            var body = await HttpUtil.ReadJsonAsync<CreateReq>(req);
            if (body is null) return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = "Invalid JSON body" });

            var name = (body.name ?? "").Trim();
            var code = (body.code ?? "").Trim();

            if (string.IsNullOrWhiteSpace(name))
                return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = "name is required" });

            if (string.IsNullOrWhiteSpace(code))
                code = Slug.Make(name);

            if (string.IsNullOrWhiteSpace(code))
                return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = "code is required" });

            var pk = $"DIV#{leagueId}";
            var table = _svc.GetTableClient(TableName);
            await table.CreateIfNotExistsAsync();

            var entity = new TableEntity(pk, code)
            {
                ["LeagueId"] = leagueId,
                ["Code"] = code,
                ["Name"] = name,
                ["IsActive"] = body.isActive ?? true,
                ["UpdatedUtc"] = DateTimeOffset.UtcNow
            };

            try
            {
                await table.AddEntityAsync(entity);
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                return HttpUtil.Json(req, HttpStatusCode.Conflict, new { error = $"division code already exists: {code}" });
            }

            var res = req.CreateResponse(HttpStatusCode.Created);
            await res.WriteAsJsonAsync(new DivisionDto(code, name, (bool)entity["IsActive"]));
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
            _log.LogError(ex, "CreateDivision failed");
            return HttpUtil.Text(req, HttpStatusCode.InternalServerError, "Internal Server Error");
        }
    }

    [Function("UpdateDivision")]
    public async Task<HttpResponseData> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "divisions/{code}")] HttpRequestData req,
        string code)
    {
        try
        {
            var leagueId = ApiGuards.RequireLeagueId(req);
            var me = IdentityUtil.GetMe(req);
            await ApiGuards.RequireMemberAsync(_svc, me.UserId, leagueId);

            var body = await HttpUtil.ReadJsonAsync<UpdateReq>(req);
            if (body is null) return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = "Invalid JSON body" });

            var name = (body.name ?? "").Trim();
            if (string.IsNullOrWhiteSpace(name))
                return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = "name is required" });

            var pk = $"DIV#{leagueId}";
            var table = _svc.GetTableClient(TableName);
            await table.CreateIfNotExistsAsync();

            TableEntity existing;
            try
            {
                existing = (await table.GetEntityAsync<TableEntity>(pk, code)).Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return HttpUtil.Json(req, HttpStatusCode.NotFound, new { error = $"division not found: {code}" });
            }

            existing["Name"] = name;
            existing["IsActive"] = body.isActive ?? (existing.GetBoolean("IsActive") ?? true);
            existing["UpdatedUtc"] = DateTimeOffset.UtcNow;

            await table.UpdateEntityAsync(existing, existing.ETag, TableUpdateMode.Replace);

            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(new DivisionDto(code, name, (bool)existing["IsActive"]));
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
            _log.LogError(ex, "UpdateDivision failed");
            return HttpUtil.Text(req, HttpStatusCode.InternalServerError, "Internal Server Error");
        }
    }
}
