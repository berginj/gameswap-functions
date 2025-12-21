using System.Net;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using GameSwap.Functions.Storage;
using System.Linq;

namespace GameSwap.Functions.Functions;

public class GetSlots
{
    private readonly ILogger _log;
    private readonly TableServiceClient _svc;

    private const string SlotsTableName = "GameSwapSlots";

    public GetSlots(ILoggerFactory lf, TableServiceClient tableServiceClient)
    {
        _log = lf.CreateLogger<GetSlots>();
        _svc = tableServiceClient;
    }

    public record SlotDto(
        string slotId,
        string division,
        string offeringTeamId,
        string offeringEmail,
        string gameDate,
        string startTime,
        string endTime,
        string parkName,
        string fieldName,
        string displayName,
        string fieldKey,
        string gameType,
        string status,
        string notes,
        string confirmedTeamId,
        string confirmedRequestId
    );

    [Function("GetSlots")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "slots")] HttpRequestData req)
    {
        try
        {
            var leagueId = ApiGuards.RequireLeagueId(req);
            var me = IdentityUtil.GetMe(req);
            await ApiGuards.RequireMemberAsync(_svc, me.UserId, leagueId);

            var division = ApiGuards.GetQueryParam(req, "division").Trim();
            var status = ApiGuards.GetQueryParam(req, "status").Trim(); // optional

            var table = _svc.GetTableClient(SlotsTableName);
            await table.CreateIfNotExistsAsync();

            // PartitionKey = SLOT#{leagueId}#{division}
            string filter;
            if (!string.IsNullOrWhiteSpace(division))
            {
                var pk = $"SLOT#{leagueId}#{division}";
                filter = $"PartitionKey eq '{ApiGuards.EscapeOData(pk)}'";
            }
            else
            {
                var pkPrefix = $"SLOT#{leagueId}#";
                var next = pkPrefix + "\uffff";
                filter = $"PartitionKey ge '{ApiGuards.EscapeOData(pkPrefix)}' and PartitionKey lt '{ApiGuards.EscapeOData(next)}'";
            }

            var list = new List<SlotDto>();

            await foreach (var e in table.QueryAsync<TableEntity>(filter: filter))
            {
                var eStatus = e.GetString("Status") ?? "Open";
                if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, eStatus, StringComparison.OrdinalIgnoreCase))
                    continue;

                list.Add(new SlotDto(
                    slotId: e.RowKey,
                    division: e.GetString("Division") ?? ExtractDivisionFromPk(e.PartitionKey, leagueId),
                    offeringTeamId: e.GetString("OfferingTeamId") ?? "",
                    offeringEmail: e.GetString("OfferingEmail") ?? "",
                    gameDate: e.GetString("GameDate") ?? "",
                    startTime: e.GetString("StartTime") ?? "",
                    endTime: e.GetString("EndTime") ?? "",
                    parkName: e.GetString("ParkName") ?? "",
                    fieldName: e.GetString("FieldName") ?? "",
                    displayName: e.GetString("DisplayName") ?? "",
                    fieldKey: e.GetString("FieldKey") ?? "",
                    gameType: e.GetString("GameType") ?? "Swap",
                    status: eStatus,
                    notes: e.GetString("Notes") ?? "",
                    confirmedTeamId: e.GetString("ConfirmedTeamId") ?? "",
                    confirmedRequestId: e.GetString("ConfirmedRequestId") ?? ""
                ));
            }

            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(list.OrderBy(x => x.gameDate).ThenBy(x => x.startTime).ThenBy(x => x.displayName));
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
            _log.LogError(ex, "GetSlots failed");
            return HttpUtil.Text(req, HttpStatusCode.InternalServerError, "Internal Server Error");
        }
    }

    private static string ExtractDivisionFromPk(string pk, string leagueId)
    {
        var prefix = $"SLOT#{leagueId}#";
        if (!pk.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return "";
        return pk[prefix.Length..];
    }
}
