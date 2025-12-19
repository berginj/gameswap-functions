using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

public class ApproveSlotRequest
{
    private readonly ILogger _logger;
    private readonly TableServiceClient _tableServiceClient;

    private const string SlotsTableName = "GameSwapSlots";
    private const string RequestsTableName = "GameSwapSlotRequests";

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
            var slotsTable = _tableServiceClient.GetTableClient(SlotsTableName);
            var requestsTable = _tableServiceClient.GetTableClient(RequestsTableName);
            await slotsTable.CreateIfNotExistsAsync();
            await requestsTable.CreateIfNotExistsAsync();

            // 1) Load slot
            TableEntity slot;
            try
            {
                var slotResp = await slotsTable.GetEntityAsync<TableEntity>(division, slotId);
                slot = slotResp.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return Text(req, HttpStatusCode.NotFound, "Slot not found.");
            }

            var currentStatus = (slot.GetString("Status") ?? "").Trim();
            if (string.Equals(currentStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
                return Text(req, HttpStatusCode.Conflict, "Slot is Cancelled.");

            if (string.Equals(currentStatus, "Confirmed", StringComparison.OrdinalIgnoreCase))
                return Text(req, HttpStatusCode.Conflict, "Slot is already Confirmed.");

            // 2) Load request being approved
            var requestPk = $"{division}|{slotId}";
            TableEntity approvedReq;
            try
            {
                var reqResp = await requestsTable.GetEntityAsync<TableEntity>(requestPk, requestId);
                approvedReq = reqResp.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return Text(req, HttpStatusCode.NotFound, "Request not found.");
            }

            var approvedTeamId = approvedReq.GetString("RequestingTeamId") ?? "";

            // 3) Update slot => Confirmed
            slot["Status"] = "Confirmed";
            slot["ConfirmedRequestId"] = requestId;
            slot["ConfirmedTeamId"] = approvedTeamId;

            await slotsTable.UpdateEntityAsync(slot, slot.ETag, TableUpdateMode.Merge);

            // 4) Update request statuses (Approved + reject the rest)
            int approvedCount = 0, rejectedCount = 0;

            await foreach (var e in requestsTable.QueryAsync<TableEntity>(
                filter: $"PartitionKey eq '{EscapeOData(requestPk)}'"))
            {
                var rk = e.RowKey;

                if (rk == requestId)
                {
                    e["Status"] = "Approved";
                    await requestsTable.UpdateEntityAsync(e, e.ETag, TableUpdateMode.Merge);
                    approvedCount++;
                }
                else
                {
                    var s = (e.GetString("Status") ?? "");
                    if (!string.Equals(s, "Rejected", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(s, "Withdrawn", StringComparison.OrdinalIgnoreCase))
                    {
                        e["Status"] = "Rejected";
                        await requestsTable.UpdateEntityAsync(e, e.ETag, TableUpdateMode.Merge);
                        rejectedCount++;
                    }
                }
            }

            // 5) Notify (only if emails exist + config set)
            var offeringEmail = slot.GetString("OfferingEmail") ?? "";
            var requestingEmail = approvedReq.GetString("RequestingEmail") ?? "";

            await TryNotifyAsync(
                division, slotId,
                offeringEmail,
                requestingEmail,
                slot);

            var resp = req.CreateResponse(HttpStatusCode.OK);
            resp.Headers.Add("Content-Type", "application/json");
            await resp.WriteStringAsync(JsonSerializer.Serialize(new
            {
                Division = division,
                SlotId = slotId,
                Status = "Confirmed",
                ApprovedRequestId = requestId,
                ApprovedTeamId = approvedTeamId,
                RequestsApproved = approvedCount,
                RequestsRejected = rejectedCount,
                NotifiedOfferingEmail = string.IsNullOrWhiteSpace(offeringEmail) ? false : true,
                NotifiedRequestingEmail = string.IsNullOrWhiteSpace(requestingEmail) ? false : true
            }));
            return resp;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ApproveSlotRequest failed");
            return Text(req, HttpStatusCode.InternalServerError, "Internal Server Error");
        }
    }

    private async Task TryNotifyAsync(string division, string slotId, string offeringEmail, string requestingEmail, TableEntity slot)
    {
        // SendGrid via raw HTTP (no NuGet package needed)
        var apiKey = Environment.GetEnvironmentVariable("SENDGRID_API_KEY");
        var from = Environment.GetEnvironmentVariable("NOTIFY_FROM_EMAIL");

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(from))
        {
            _logger.LogInformation("Email not configured (SENDGRID_API_KEY / NOTIFY_FROM_EMAIL missing). Skipping notify.");
            return;
        }

        var gameDate = slot.GetString("GameDate") ?? "";
        var startTime = slot.GetString("StartTime") ?? "";
        var endTime = slot.GetString("EndTime") ?? "";
        var field = slot.GetString("Field") ?? "";
        var offeringTeamId = slot.GetString("OfferingTeamId") ?? "";
        var confirmedTeamId = slot.GetString("ConfirmedTeamId") ?? "";

        var subject = $"GameSwap approved: {division} {gameDate} {startTime}-{endTime}";
        var body =
$@"A GameSwap request was approved.

Division: {division}
Date: {gameDate}
Time: {startTime}–{endTime}
Field: {field}

Offering Team: {offeringTeamId}
Approved Opponent Team: {confirmedTeamId}
SlotId: {slotId}
";

        // Notify both, if present
        var tos = new List<string>();
        if (!string.IsNullOrWhiteSpace(offeringEmail)) tos.Add(offeringEmail);
        if (!string.IsNullOrWhiteSpace(requestingEmail) && !tos.Contains(requestingEmail, StringComparer.OrdinalIgnoreCase))
            tos.Add(requestingEmail);

        foreach (var to in tos)
        {
            await SendSendGridEmailAsync(apiKey, from, to, subject, body);
        }
    }

    private static async Task SendSendGridEmailAsync(string apiKey, string from, string to, string subject, string body)
    {
        var payload = new
        {
            personalizations = new[] { new { to = new[] { new { email = to } } } },
            from = new { email = from },
            subject = subject,
            content = new[] { new { type = "text/plain", value = body } }
        };

        var json = JsonSerializer.Serialize(payload);

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.sendgrid.com/v3/mail/send");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var resp = await _http.SendAsync(req);
        // SendGrid returns 202 Accepted on success
        if ((int)resp.StatusCode < 200 || (int)resp.StatusCode >= 300)
        {
            var text = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new Exception($"SendGrid email failed: {(int)resp.StatusCode} {resp.ReasonPhrase}. {text}");
        }
    }

    private static HttpResponseData Text(HttpRequestData req, HttpStatusCode status, string msg)
    {
        var resp = req.CreateResponse(status);
        resp.Headers.Add("Content-Type", "text/plain");
        resp.WriteString(msg);
        return resp;
    }

    private static string EscapeOData(string s) => s.Replace("'", "''");
}
