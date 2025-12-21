using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using GameSwap.Functions.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

public class ApproveSlotRequest
{
    private readonly ILogger _logger;
    private readonly TableServiceClient _tableServiceClient;

    private const string SlotsTableName = "GameSwapSlots";
    private const string RequestsTableName = "GameSwapSlotRequests";
    private const string MembershipsTableName = "GameSwapMemberships";

    private static readonly HttpClient _http = new HttpClient();

    public ApproveSlotRequest(ILoggerFactory loggerFactory, TableServiceClient tableServiceClient)
    {
        _logger = loggerFactory.CreateLogger<ApproveSlotRequest>();
        _tableServiceClient = tableServiceClient;
    }

    [Function("ApproveSlotRequest")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch",
            Route = "slots/{division}/{slotId}/requests/{requestId}/approve")] HttpRequestData req,
        string division,
        string slotId,
        string requestId)
    {
        try
        {
            // League scope (header-first; query fallback; mismatch protection inside ApiGuards)
            string leagueId;
            try
            {
                leagueId = ApiGuards.RequireLeagueId(req);
            }
            catch (Exception ex)
            {
                return Text(req, HttpStatusCode.BadRequest, ex.Message);
            }

            // AuthN/AuthZ: must be a member of this league (per "everyone can approve for now")
            var me = IdentityUtil.GetMe(req);

            var membershipsTable = _tableServiceClient.GetTableClient(MembershipsTableName);
            await membershipsTable.CreateIfNotExistsAsync();

            var isMember = await IsMemberAsync(membershipsTable, me.UserId, leagueId);
            if (!isMember)
                return Text(req, HttpStatusCode.Forbidden, "Forbidden");

            var slotsTable = _tableServiceClient.GetTableClient(SlotsTableName);
            var requestsTable = _tableServiceClient.GetTableClient(RequestsTableName);
            await slotsTable.CreateIfNotExistsAsync();
            await requestsTable.CreateIfNotExistsAsync();

            // 1) Load slot (canonical PK first, then legacy PK=division)
            var slotPkNew = $"SLOT#{leagueId}#{division}";
            var slotPkLegacy = division;
            var slotRk = slotId;

            TableEntity slot;
            string slotPkUsed;

            var slotFound = await TryGetEntityAsync(slotsTable, slotPkNew, slotRk);
            if (slotFound is not null)
            {
                slot = slotFound;
                slotPkUsed = slotPkNew;
            }
            else
            {
                slotFound = await TryGetEntityAsync(slotsTable, slotPkLegacy, slotRk);
                if (slotFound is null)
                    return Text(req, HttpStatusCode.NotFound, "Slot not found.");

                slot = slotFound;
                slotPkUsed = slotPkLegacy;
            }

            var currentStatus = (slot.GetString("Status") ?? "").Trim();
            var statusLower = currentStatus.ToLowerInvariant();

            if (statusLower == "cancelled")
                return Text(req, HttpStatusCode.Conflict, "Slot is Cancelled.");

            // idempotency: if already confirmed, return OK with current confirm fields
            if (statusLower == "confirmed")
            {
                var already = new
                {
                    LeagueId = leagueId,
                    Division = division,
                    SlotId = slotId,
                    Status = "Confirmed",
                    ConfirmedRequestId = slot.GetString("ConfirmedRequestId") ?? "",
                    ConfirmedTeamId = slot.GetString("ConfirmedTeamId") ?? ""
                };
                return Json(req, HttpStatusCode.OK, already);
            }

            // 2) Load request being approved (canonical request PK first, then legacy)
            var requestPkNew = $"SLOTREQ#{leagueId}#{division}#{slotId}";
            var requestPkLegacy = $"{division}|{slotId}"; // legacy partition style used by older code
            var requestRk = requestId;

            TableEntity approvedReq;
            string requestPkUsed;

            var reqFound = await TryGetEntityAsync(requestsTable, requestPkNew, requestRk);
            if (reqFound is not null)
            {
                approvedReq = reqFound;
                requestPkUsed = requestPkNew;
            }
            else
            {
                reqFound = await TryGetEntityAsync(requestsTable, requestPkLegacy, requestRk);
                if (reqFound is null)
                    return Text(req, HttpStatusCode.NotFound, "Request not found.");

                approvedReq = reqFound;
                requestPkUsed = requestPkLegacy;
            }

            var approvedReqStatus = (approvedReq.GetString("Status") ?? "").Trim().ToLowerInvariant();
            if (approvedReqStatus == "rejected")
                return Text(req, HttpStatusCode.Conflict, "Request was already rejected.");

            var approvedTeamId = (approvedReq.GetString("RequestingTeamId") ?? "").Trim();

            // 3) Update slot => Confirmed
            var now = DateTimeOffset.UtcNow;

            slot["Status"] = "Confirmed";
            slot["ConfirmedRequestId"] = requestId;
            slot["ConfirmedTeamId"] = approvedTeamId;
            slot["UpdatedUtc"] = now;
            slot["LastUpdatedUtc"] = now;

            // If legacy slot row is missing LeagueId/Division, backfill for sanity
            if (string.IsNullOrWhiteSpace(slot.GetString("LeagueId")))
                slot["LeagueId"] = leagueId;
            if (string.IsNullOrWhiteSpace(slot.GetString("Division")))
                slot["Division"] = division;

            await slotsTable.UpdateEntityAsync(slot, ETag.All, TableUpdateMode.Merge);

            // 4) Update request statuses within the partition we actually found the request in
            int approvedCount = 0, rejectedCount = 0;

            await foreach (var e in requestsTable.QueryAsync<TableEntity>(
                filter: $"PartitionKey eq '{EscapeOData(requestPkUsed)}'"))
            {
                if (string.Equals(e.RowKey, requestId, StringComparison.OrdinalIgnoreCase))
                {
                    e["Status"] = "Approved";
                    e["ApprovedAtUtc"] = now;
                    await requestsTable.UpdateEntityAsync(e, ETag.All, TableUpdateMode.Merge);
                    approvedCount++;
                }
                else
                {
                    var s = (e.GetString("Status") ?? "").Trim().ToLowerInvariant();
                    if (s != "rejected" && s != "approved")
                    {
                        e["Status"] = "Rejected";
                        e["RejectedAtUtc"] = now;
                        await requestsTable.UpdateEntityAsync(e, ETag.All, TableUpdateMode.Merge);
                        rejectedCount++;
                    }
                }
            }

            // 5) Notify (best-effort; never fail the approval because email fails)
            var offeringEmail = (slot.GetString("OfferingEmail") ?? "").Trim();
            var requestingEmail = (approvedReq.GetString("RequestingEmail") ?? "").Trim();

            try
            {
                await TryNotifyAsync(division, slotId, offeringEmail, requestingEmail, slot);
            }
            catch (Exception notifyEx)
            {
                _logger.LogWarning(notifyEx, "ApproveSlotRequest: notification failed (non-fatal)");
            }

            // 6) Response
            var respBody = new
            {
                LeagueId = leagueId,
                Division = division,
                SlotId = slotId,
                Status = "Confirmed",
                ApprovedRequestId = requestId,
                ApprovedTeamId = approvedTeamId,
                RequestsApproved = approvedCount,
                RequestsRejected = rejectedCount,
                SlotPartitionKey = slotPkUsed,
                RequestsPartitionKey = requestPkUsed,
                UpdatedUtc = now.ToString("o")
            };

            return Json(req, HttpStatusCode.OK, respBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ApproveSlotRequest failed");
            return Text(req, HttpStatusCode.InternalServerError, "Internal Server Error");
        }
    }

    private async Task TryNotifyAsync(string division, string slotId, string offeringEmail, string requestingEmail, TableEntity slot)
    {
        var apiKey = Environment.GetEnvironmentVariable("SENDGRID_API_KEY");
        var from = Environment.GetEnvironmentVariable("NOTIFY_FROM_EMAIL");

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(from))
            return; // no-op

        var gameDate = slot.GetString("GameDate") ?? "";
        var startTime = slot.GetString("StartTime") ?? "";
        var endTime = slot.GetString("EndTime") ?? "";

        // normalize toward FieldName; fall back to Field for legacy
        var fieldName = slot.GetString("FieldName");
        if (string.IsNullOrWhiteSpace(fieldName))
            fieldName = slot.GetString("Field") ?? "";

        var offeringTeamId = slot.GetString("OfferingTeamId") ?? "";
        var confirmedTeamId = slot.GetString("ConfirmedTeamId") ?? "";

        var subject = $"GameSwap approved: {division} {gameDate} {startTime}-{endTime}";
        var body =
$@"A GameSwap request was approved.

Division: {division}
Date: {gameDate}
Time: {startTime}–{endTime}
Field: {fieldName}

Offering Team: {offeringTeamId}
Approved Opponent Team: {confirmedTeamId}
SlotId: {slotId}
";

        var tos = new List<string>();
        if (!string.IsNullOrWhiteSpace(offeringEmail)) tos.Add(offeringEmail);
        if (!string.IsNullOrWhiteSpace(requestingEmail) &&
            !tos.Contains(requestingEmail, StringComparer.OrdinalIgnoreCase))
        {
            tos.Add(requestingEmail);
        }

        foreach (var to in tos)
            await SendSendGridEmailAsync(apiKey, from, to, subject, body);
    }

    private static async Task SendSendGridEmailAsync(string apiKey, string from, string to, string subject, string body)
    {
        var payload = new
        {
            personalizations = new[] { new { to = new[] { new { email = to } } } },
            from = new { email = from },
            subject,
            content = new[] { new { type = "text/plain", value = body } }
        };

        using var msg = new HttpRequestMessage(HttpMethod.Post, "https://api.sendgrid.com/v3/mail/send");
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        msg.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var resp = await _http.SendAsync(msg).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            var text = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new Exception($"SendGrid email failed: {(int)resp.StatusCode} {resp.ReasonPhrase}. {text}");
        }
    }

    private static async Task<bool> IsMemberAsync(TableClient membershipsTable, string userId, string leagueId)
    {
        if (string.IsNullOrWhiteSpace(userId) || userId == "UNKNOWN") return false;
        if (string.IsNullOrWhiteSpace(leagueId)) return false;

        try
        {
            _ = await membershipsTable.GetEntityAsync<TableEntity>(userId, leagueId);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    private static async Task<TableEntity?> TryGetEntityAsync(TableClient table, string pk, string rk)
    {
        try
        {
            var resp = await table.GetEntityAsync<TableEntity>(pk, rk);
            return resp.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    private static HttpResponseData Text(HttpRequestData req, HttpStatusCode status, string msg)
    {
        var resp = req.CreateResponse(status);
        resp.Headers.Add("Content-Type", "text/plain; charset=utf-8");
        resp.WriteString(msg);
        return resp;
    }

    private static HttpResponseData Json(HttpRequestData req, HttpStatusCode status, object body)
    {
        var resp = req.CreateResponse(status);
        resp.Headers.Add("Content-Type", "application/json; charset=utf-8");
        resp.WriteString(JsonSerializer.Serialize(body));
        return resp;
    }

    private static string EscapeOData(string s) => (s ?? "").Replace("'", "''");
}
