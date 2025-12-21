using System.Net;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using GameSwap.Functions.Storage;

namespace GameSwap.Functions.Functions;

public class RequestSlot
{
    private readonly ILogger _log;
    private readonly TableServiceClient _svc;

    private const string SlotsTableName = "GameSwapSlots";
    private const string RequestsTableName = "GameSwapSlotRequests";

    public RequestSlot(ILoggerFactory lf, TableServiceClient tableServiceClient)
    {
        _log = lf.CreateLogger<RequestSlot>();
        _svc = tableServiceClient;
    }

    public record RequestSlotReq(string? requestingTeamId, string? requestingEmail, string? message);

    [Function("RequestSlot")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "slots/{division}/{slotId}/requests")] HttpRequestData req,
        string division,
        string slotId)
    {
        try
        {
            var leagueId = ApiGuards.RequireLeagueId(req);
            var me = IdentityUtil.GetMe(req);
            await ApiGuards.RequireMemberAsync(_svc, me.UserId, leagueId);

            var body = await HttpUtil.ReadJsonAsync<RequestSlotReq>(req);
            if (body is null) return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = "Invalid JSON body" });

            var requestingTeamId = (body.requestingTeamId ?? "").Trim();
            var requestingEmail = (body.requestingEmail ?? me.Email ?? "").Trim();
            var message = (body.message ?? "").Trim();

            if (string.IsNullOrWhiteSpace(requestingTeamId))
                return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = "requestingTeamId is required" });

            var slots = _svc.GetTableClient(SlotsTableName);
            var requests = _svc.GetTableClient(RequestsTableName);
            await slots.CreateIfNotExistsAsync();
            await requests.CreateIfNotExistsAsync();

            var slotPk = $"SLOT#{leagueId}#{division}";
            TableEntity slot;
            try { slot = (await slots.GetEntityAsync<TableEntity>(slotPk, slotId)).Value; }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return HttpUtil.Json(req, HttpStatusCode.NotFound, new { error = "Slot not found" });
            }

            var status = slot.GetString("Status") ?? "Open";
            if (!string.Equals(status, "Open", StringComparison.OrdinalIgnoreCase))
                return HttpUtil.Json(req, HttpStatusCode.Conflict, new { error = $"Slot not open (status: {status})" });

            // prevent duplicate requests from same team
            var reqPk = $"SLOTREQ#{leagueId}#{division}#{slotId}";
            var dupFilter = $"PartitionKey eq '{ApiGuards.EscapeOData(reqPk)}' and RequestingTeamId eq '{ApiGuards.EscapeOData(requestingTeamId)}'";
            await foreach (var _ in requests.QueryAsync<TableEntity>(filter: dupFilter))
            {
                return HttpUtil.Json(req, HttpStatusCode.Conflict, new { error = "This team already requested this slot." });
            }

            var requestId = Guid.NewGuid().ToString("N");
            var now = DateTimeOffset.UtcNow;

            var entity = new TableEntity(reqPk, requestId)
            {
                ["LeagueId"] = leagueId,
                ["Division"] = division,
                ["SlotId"] = slotId,
                ["RequestId"] = requestId,

                ["RequestingTeamId"] = requestingTeamId,
                ["RequestingEmail"] = requestingEmail,
                ["Message"] = message,

                ["Status"] = "Pending",
                ["RequestedUtc"] = now,
                ["UpdatedUtc"] = now
            };

            await requests.AddEntityAsync(entity);

            // Optional: surface "pending" state on slot after first request
            slot["Status"] = "Pending";
            slot["UpdatedUtc"] = now;
            await slots.UpdateEntityAsync(slot, slot.ETag, TableUpdateMode.Replace);

            var res = req.CreateResponse(HttpStatusCode.Created);
            await res.WriteAsJsonAsync(entity);
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
            _log.LogError(ex, "RequestSlot failed");
            return HttpUtil.Text(req, HttpStatusCode.InternalServerError, "Internal Server Error");
        }
    }
}
