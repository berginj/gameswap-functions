using System.Net;
using Azure;
using Azure.Data.Tables;
using GameSwap.Functions.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace GameSwap.Functions.Functions;

public class ListFields
{
    private readonly ILogger _log;
    private readonly TableServiceClient _svc;

    private const string FieldsTableName = "GameSwapFields";

    public ListFields(ILoggerFactory lf, TableServiceClient tableServiceClient)
    {
        _log = lf.CreateLogger<ListFields>();
        _svc = tableServiceClient;
    }

    public sealed record FieldDto(
        string parkName,
        string fieldName,
        string displayName,
        string address,
        string notes,
        bool isActive
    );

    [Function("ListFields")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "fields")] HttpRequestData req)
    {
        try
        {
            // leagueId is required (header preferred, query allowed)
            string leagueId;
            try { leagueId = ApiGuards.RequireLeagueId(req); }
            catch (Exception ex) { return await Err(req, ex.Message, HttpStatusCode.BadRequest); }

            // membership gate
            var me = IdentityUtil.GetMe(req);
            if (!await ApiGuards.IsMemberAsync(_svc, me.UserId, leagueId))
                return await Err(req, "Forbidden", HttpStatusCode.Forbidden);

            // optional query param
            var activeOnly = GetBoolQuery(req, "activeOnly", defaultValue: true);

            var table = _svc.GetTableClient(FieldsTableName);
            await table.CreateIfNotExistsAsync();

            // Canonical: PK = FIELD#{leagueId}#{parkCode}, RK = {fieldCode}
            var pkPrefix = $"FIELD#{leagueId}#";
            var pkNext = pkPrefix + "\uffff";
            var filter = $"PartitionKey ge '{EscapeOData(pkPrefix)}' and PartitionKey lt '{EscapeOData(pkNext)}'";

            var list = new List<FieldDto>();

            await foreach (var e in table.QueryAsync<TableEntity>(filter: filter))
            {
                var isActive = e.GetBoolean("IsActive") ?? true;
                if (activeOnly && !isActive) continue;

                var parkName = e.GetString("ParkName") ?? "";
                var fieldName = e.GetString("FieldName") ?? "";
                var displayName = e.GetString("DisplayName") ?? "";

                if (string.IsNullOrWhiteSpace(displayName) && !string.IsNullOrWhiteSpace(parkName) && !string.IsNullOrWhiteSpace(fieldName))
                    displayName = $"{parkName} > {fieldName}";

                list.Add(new FieldDto(
                    parkName: parkName,
                    fieldName: fieldName,
                    displayName: displayName,
                    address: e.GetString("Address") ?? "",
                    notes: e.GetString("Notes") ?? "",
                    isActive: isActive
                ));
            }

            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(list.OrderBy(x => x.displayName));
            return res;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "ListFields failed");
            return await Err(req, "Internal Server Error", HttpStatusCode.InternalServerError);
        }
    }

    private static bool GetBoolQuery(HttpRequestData req, string key, bool defaultValue)
    {
        var q = req.Url.Query;
        if (string.IsNullOrWhiteSpace(q)) return defaultValue;

        foreach (var part in q.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            var k = Uri.UnescapeDataString(kv[0]);
            if (!string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) continue;

            var v = Uri.UnescapeDataString(kv[1]);
            return bool.TryParse(v, out var b) ? b : defaultValue;
        }

        return defaultValue;
    }

    private static string EscapeOData(string s) => (s ?? "").Replace("'", "''");

    private static async Task<HttpResponseData> Err(HttpRequestData req, string msg, HttpStatusCode code)
    {
        var res = req.CreateResponse(code);
        await res.WriteAsJsonAsync(new { error = msg });
        return res;
    }
}
