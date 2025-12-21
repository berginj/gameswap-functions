using System.Net;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using GameSwap.Functions.Storage;

namespace GameSwap.Functions.Functions;

public class CancelSlot
{
    private readonly ILogger _log;
    private readonly TableServiceClient _svc;

    private const string SlotsTableName = "GameSwapSlots";

    public CancelSlot(ILoggerFactory lf, TableServiceClient tableServiceClient)
    {
        _log = lf.CreateLogger<CancelSlot>();
        _svc = tableServiceClient;
    }

    [Function("CancelSlot")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "slots/{division}/{slotId}/cancel")] HttpRequestData req,
        string division,
        string slotId)
    {
        try
        {
            var leagueId = ApiGuards.RequireLeagueId(req);
            var me = IdentityUtil.GetMe(req);
            await ApiGuards.RequireMemberAsync(_svc, me.UserId, leagueId);

            var pk = $"SLOT#{leagueId}#{division}";
            var table = _svc.GetTableClient(SlotsTableName);
            await table.CreateIfNotExistsAsync();

            TableEntity slot;
            try
            {
                slot = (await table.GetEntityAsync<TableEntity>(pk, slotId)).Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return HttpUtil.Json(req, HttpStatusCode.NotFound, new { error = "Slot not found" });
            }

            var status = slot.GetString("Status") ?? "Open";
            if (string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return HttpUtil.Json(req, HttpStatusCode.OK, new { ok = true, status = "Cancelled" });

            // Optional safety: only offeringEmail can cancel (when present)
            var offeringEmail = (slot.GetString("OfferingEmail") ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(offeringEmail) &&
                !string.IsNullOrWhiteSpace(me.Email) &&
                !string.Equals(offeringEmail, me.Email, StringComparison.OrdinalIgnoreCase))
            {
                return HttpUtil.Text(req, HttpStatusCode.Forbidden, "Only the offering team can cancel this slot.");
            }

            slot["Status"] = "Cancelled";
            slot["UpdatedUtc"] = DateTimeOffset.UtcNow;

            await table.UpdateEntityAsync(slot, slot.ETag, TableUpdateMode.Replace);

            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(new { ok = true, status = "Cancelled" });
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
            _log.LogError(ex, "CancelSlot failed");
            return HttpUtil.Text(req, HttpStatusCode.InternalServerError, "Internal Server Error");
        }
    }
}
