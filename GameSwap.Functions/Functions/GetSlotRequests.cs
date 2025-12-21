using System.Net;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using GameSwap.Functions.Storage;
using System.Linq;

namespace GameSwap.Functions.Functions;

public class GetSlotRequests
{
    private readonly ILogger _log;
    private readonly TableServiceClient _svc;

    private const string RequestsTableName = "GameSwapSlotRequests";
    private const string SlotsTableName = "GameSwapSlots";

    public GetSlotRequests(ILoggerFactory lf, TableServiceClient tableServiceClient)
    {
        _log = lf.CreateLogger<GetSlotRequests>();
        _svc = tableServiceClient;
    }

    [Function("GetSlotRequests")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "slots/{division}/{slotId}/requests")] HttpRequestData req,
        string division,
        string slotId)
    {
        try
        {
            var leagueId = ApiGuards.RequireLeagueId(req);
            var me = IdentityUtil.GetMe(req);
            await ApiGuards.RequireMemberAsync(_svc, me.UserId, leagueId);

            // Validate slot exists (cheap guardrail)
            var slots = _svc.GetTableClient(SlotsTableName);
            await slots.CreateIfNotExistsAsync();
            var slotPk = $"SLOT#{leagueId}#{division}";
            try { _ = (await slots.GetEntityAsync<TableEntity>(slotPk, slotId)).Value; }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return HttpUtil.Json(req, HttpStatusCode.NotFound, new { error = "Slot not found" });
            }

            var table = _svc.GetTableClient(RequestsTableName);
            await table.CreateIfNotExistsAsync();

            var pk = $"SLOTREQ#{leagueId}#{division}#{slotId}";
            var filter = $"PartitionKey eq '{ApiGuards.EscapeOData(pk)}'";

            var list = new List<object>();
            await foreach (var e in table.QueryAsync<TableEntity>(filter: filter))
            {
                list.Add(new
                {
                    requestId = e.RowKey,
                    requestingTeamId = e.GetString("RequestingTeamId") ?? "",
                    requestingEmail = e.GetString("RequestingEmail") ?? "",
                    message = e.GetString("Message") ?? "",
                    status = e.GetString("Status") ?? "Pending",
                    requestedUtc = e.GetDateTimeOffset("RequestedUtc")
                });
            }

            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(list.OrderByDescending(x => ((dynamic)x).requestedUtc ?? DateTimeOffset.MinValue));
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
            _log.LogError(ex, "GetSlotRequests failed");
            return HttpUtil.Text(req, HttpStatusCode.InternalServerError, "Internal Server Error");
        }
    }
}
