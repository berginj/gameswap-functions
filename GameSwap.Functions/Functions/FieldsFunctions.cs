using System.Net;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using GameSwap.Functions.Storage;
using System.Linq;

namespace GameSwap.Functions.Functions;

public class FieldsFunctions
{
    private readonly TableServiceClient _svc;
    private readonly ILogger _log;

    private const string TableName = "GameSwapFields";

    public FieldsFunctions(ILoggerFactory lf, TableServiceClient tableServiceClient)
    {
        _log = lf.CreateLogger<FieldsFunctions>();
        _svc = tableServiceClient;
    }

    public record FieldDto(string parkCode, string fieldCode, string parkName, string fieldName, string displayName, string address, string notes, bool isActive);
    public record FieldItem(string? parkName, string? fieldName, string? displayName, string? address, string? notes, bool? isActive);
    public record BulkReq(List<FieldItem>? items);

    [Function("ListFields")]
    public async Task<HttpResponseData> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "fields")] HttpRequestData req)
    {
        try
        {
            var leagueId = ApiGuards.RequireLeagueId(req);
            var me = IdentityUtil.GetMe(req);
            await ApiGuards.RequireMemberAsync(_svc, me.UserId, leagueId);

            var activeOnly = GetBoolQuery(req, "activeOnly", defaultValue: true);

            var table = _svc.GetTableClient(TableName);
            await table.CreateIfNotExistsAsync();

            // PartitionKey = FIELD#{leagueId}#{parkCode}
            var pkPrefix = $"FIELD#{leagueId}#";
            var next = pkPrefix + "\uffff";
            var filter = $"PartitionKey ge '{ApiGuards.EscapeOData(pkPrefix)}' and PartitionKey lt '{ApiGuards.EscapeOData(next)}'";

            var list = new List<FieldDto>();

            await foreach (var e in table.QueryAsync<TableEntity>(filter: filter))
            {
                var isActive = e.GetBoolean("IsActive") ?? true;
                if (activeOnly && !isActive) continue;

                var parkCode = ExtractParkCodeFromPk(e.PartitionKey, leagueId);
                var fieldCode = e.RowKey;

                list.Add(new FieldDto(
                    parkCode: parkCode,
                    fieldCode: fieldCode,
                    parkName: e.GetString("ParkName") ?? "",
                    fieldName: e.GetString("FieldName") ?? "",
                    displayName: e.GetString("DisplayName") ?? "",
                    address: e.GetString("Address") ?? "",
                    notes: e.GetString("Notes") ?? "",
                    isActive: isActive
                ));
            }

            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(list.OrderBy(x => x.displayName));
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
            _log.LogError(ex, "ListFields failed");
            return HttpUtil.Text(req, HttpStatusCode.InternalServerError, "Internal Server Error");
        }
    }

    [Function("FieldsBulk")]
    public async Task<HttpResponseData> Bulk(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "fields/bulk")] HttpRequestData req)
    {
        try
        {
            var leagueId = ApiGuards.RequireLeagueId(req);
            var me = IdentityUtil.GetMe(req);
            await ApiGuards.RequireMemberAsync(_svc, me.UserId, leagueId);

            var body = await HttpUtil.ReadJsonAsync<BulkReq>(req);
            var items = body?.items ?? new List<FieldItem>();
            if (items.Count == 0)
                return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = "items is required" });

            var table = _svc.GetTableClient(TableName);
            await table.CreateIfNotExistsAsync();

            int created = 0, updated = 0, failed = 0;
            var errors = new List<object>();

            foreach (var (item, i) in items.Select((x, idx) => (x, idx)))
            {
                try
                {
                    var parkName = (item.parkName ?? "").Trim();
                    var fieldName = (item.fieldName ?? "").Trim();
                    var displayName = (item.displayName ?? "").Trim();

                    if (string.IsNullOrWhiteSpace(parkName) || string.IsNullOrWhiteSpace(fieldName))
                    {
                        failed++;
                        errors.Add(new { row = i + 1, message = "parkName and fieldName are required" });
                        continue;
                    }

                    var parkCode = Slug.Make(parkName);
                    var fieldCode = Slug.Make(fieldName);

                    if (string.IsNullOrWhiteSpace(parkCode) || string.IsNullOrWhiteSpace(fieldCode))
                    {
                        failed++;
                        errors.Add(new { row = i + 1, message = "invalid parkName/fieldName; slug became empty" });
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(displayName))
                        displayName = $"{parkName} > {fieldName}";

                    var pk = $"FIELD#{leagueId}#{parkCode}";
                    var rk = fieldCode;

                    var exists = true;
                    try { await table.GetEntityAsync<TableEntity>(pk, rk); }
                    catch (RequestFailedException ex) when (ex.Status == 404) { exists = false; }

                    var entity = new TableEntity(pk, rk)
                    {
                        ["LeagueId"] = leagueId,
                        ["ParkCode"] = parkCode,
                        ["FieldCode"] = fieldCode,
                        ["ParkName"] = parkName,
                        ["FieldName"] = fieldName,
                        ["DisplayName"] = displayName,
                        ["Address"] = (item.address ?? "").Trim(),
                        ["Notes"] = (item.notes ?? "").Trim(),
                        ["IsActive"] = item.isActive ?? true,
                        ["UpdatedUtc"] = DateTimeOffset.UtcNow
                    };

                    await table.UpsertEntityAsync(entity, TableUpdateMode.Replace);

                    if (exists) updated++; else created++;
                }
                catch (Exception ex)
                {
                    failed++;
                    errors.Add(new { row = i + 1, message = ex.Message });
                }
            }

            return HttpUtil.Json(req, HttpStatusCode.OK, new { created, updated, failed, errors });
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
            _log.LogError(ex, "FieldsBulk failed");
            return HttpUtil.Text(req, HttpStatusCode.InternalServerError, "Internal Server Error");
        }
    }

    private static string ExtractParkCodeFromPk(string pk, string leagueId)
    {
        var prefix = $"FIELD#{leagueId}#";
        if (!pk.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return "";
        return pk[prefix.Length..];
    }

    private static bool GetBoolQuery(HttpRequestData req, string key, bool defaultValue)
    {
        var v = ApiGuards.GetQueryParam(req, key);
        if (string.IsNullOrWhiteSpace(v)) return defaultValue;
        return bool.TryParse(v, out var b) ? b : defaultValue;
    }
}
