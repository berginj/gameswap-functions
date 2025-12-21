using System.Net;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using GameSwap.Functions.Storage;

namespace GameSwap.Functions.Functions;

public class ApproveSlotRequest
{
    private readonly ILogger _log;
    private readonly TableServiceClient _svc;

    private const string SlotsTableName = "GameSwapSlots";
    private const string RequestsTableName = "GameSwapSlotRequests";

    public ApproveSlotRequest(ILoggerFactory lf, TableServiceClient tableServiceClient)
    {
        _log = lf.CreateLogger<ApproveSlotRequest>();
        _svc = tableServiceClient;
    }

    public record ApproveReq(string? approvedByEmail);

    [Function("ApproveSlotRequest")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "slots/{division}/{slotId}/requests/{requestId}/approve")] HttpRequestData req,
        string division,
        string slotId,
        string requestId)
    {
        try
        {
            var leagueId = ApiGuards.RequireLeagueId(req);
            var me = IdentityUtil.GetMe(req);
            await ApiGuards.RequireMemberAsync(_svc, me.UserId, leagueId); // per your note: all roles can approve (membership is enough)

            var body = await HttpUtil.ReadJsonAsync<ApproveReq>(req);
            var approvedBy = (body?.approvedByEmail ?? me.Email ?? "").Trim();

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
            if (string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return HttpUtil.Json(req, HttpStatusCode.Conflict, new { error = "Slot is cancelled" });
            if (string.Equals(status, "Confirmed", StringComparison.OrdinalIgnoreCase))
                return HttpUtil.Json(req, HttpStatusCode.Conflict, new { error = "Slot already confirmed" });

            var reqPk = $"SLOTREQ#{leagueId}#{division}#{slotId}";
            TableEntity request;
            try { request = (await requests.GetEntityAsync<TableEntity>(reqPk, requestId)).Value; }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return HttpUtil.Json(req, HttpStatusCode.NotFound, new { error = "Request not found" });
            }

            var reqStatus = request.GetString("Status") ?? "Pending";
            if (!string.Equals(reqStatus, "Pending", StringComparison.OrdinalIgnoreCase))
                return HttpUtil.Json(req, HttpStatusCode.Conflict, new { error = $"Request not pending (status: {reqStatus})" });

            var now = DateTimeOffset.UtcNow;

            // Approve this request
            request["Status"] = "Approved";
            request["ApprovedBy"] = approvedBy;
            request["ApprovedUtc"] = now;
            request["UpdatedUtc"] = now;
            await requests.UpdateEntityAsync(request, request.ETag, TableUpdateMode.Replace);

            // Reject all other pending requests for the slot
            var filter = $"PartitionKey eq '{ApiGuards.EscapeOData(reqPk)}'";
            await foreach (var other in requests.QueryAsync<TableEntity>(filter: filter))
            {
                if (other.RowKey == requestId) continue;
                var otherStatus = other.GetString("Status") ?? "Pending";
                if (!string.Equals(otherStatus, "Pending", StringComparison.OrdinalIgnoreCase)) continue;

                other["Status"] = "Rejected";
                other["RejectedUtc"] = now;
                other["UpdatedUtc"] = now;

                try { await requests.UpdateEntityAsync(other, other.ETag, TableUpdateMode.Replace); }
                catch (RequestFailedException ex)
                {
                    _log.LogWarning(ex, "Failed to reject request {requestId} for slot {slotId}", other.RowKey, slotId);
                }
            }

            // Confirm slot
            slot["Status"] = "Confirmed";
            slot["ConfirmedTeamId"] = request.GetString("RequestingTeamId") ?? "";
            slot["ConfirmedRequestId"] = requestId;
            slot["ConfirmedBy"] = approvedBy;
            slot["ConfirmedUtc"] = now;
            slot["UpdatedUtc"] = now;

            await slots.UpdateEntityAsync(slot, slot.ETag, TableUpdateMode.Replace);

            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(new
            {
                ok = true,
                slotId,
                division,
                requestId,
                status = "Confirmed"
            });
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
            _log.LogError(ex, "ApproveSlotRequest failed");
            return HttpUtil.Text(req, HttpStatusCode.InternalServerError, "Internal Server Error");
        }
    }
}
