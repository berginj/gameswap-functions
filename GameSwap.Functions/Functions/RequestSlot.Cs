using System.Net;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

public class RequestSlot
{
    private readonly ILogger _logger;
    private readonly TableServiceClient _tableServiceClient;

    // Table names (change if you want)
    private const string SlotsTableName = "GameSwapSlots";
    private const string RequestsTableName = "GameSwapSlotRequests";

    public RequestSlot(ILoggerFactory loggerFactory, TableServiceClient tableServiceClient)
    {
        _logger = loggerFactory.CreateLogger<RequestSlot>();
        _tableServiceClient = tableServiceClient;
    }

    public record RequestSlotBody(string RequestingTeamId, string? Message);

    public class SlotRequestEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = default!;
        public string RowKey { get; set; } = default!;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public string Division { get; set; } = default!;
        public string SlotId { get; set; } = default!;
        public string RequestingTeamId { get; set; } = default!;
        public string Message { get; set; } = "";
        public string Status { get; set; } = "Pending";
        public DateTimeOffset RequestedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    }

    [Function("RequestSlot")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "slots/{division}/{slotId}/requests")] HttpRequestData req,
        string division,
        string slotId)
    {
        try
        {
            // Parse body
            var body = await JsonSerializer.DeserializeAsync<RequestSlotBody>(req.Body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (body is null || string.IsNullOrWhiteSpace(body.RequestingTeamId))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("RequestingTeamId is required.");
                return bad;
            }

            // Ensure tables exist
            var slotsTable = _tableServiceClient.GetTableClient(SlotsTableName);
            var requestsTable = _tableServiceClient.GetTableClient(RequestsTableName);
            await slotsTable.CreateIfNotExistsAsync();
            await requestsTable.CreateIfNotExistsAsync();

            // Confirm slot exists
            // Your slot entity uses PartitionKey=Division and RowKey=SlotId (based on your output)
            var slotPk = division;
            var slotRk = slotId;

            try
            {
                _ = await slotsTable.GetEntityAsync<TableEntity>(slotPk, slotRk);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync("Slot not found.");
                return notFound;
            }

            // Insert request
            var requestId = Guid.NewGuid().ToString();
            var requestEntity = new SlotRequestEntity
            {
                PartitionKey = $"{division}|{slotId}",
                RowKey = requestId,
                Division = division,
                SlotId = slotId,
                RequestingTeamId = body.RequestingTeamId.Trim(),
                Message = body.Message?.Trim() ?? "",
                Status = "Pending",
                RequestedAtUtc = DateTimeOffset.UtcNow
            };

            await requestsTable.AddEntityAsync(requestEntity);

            // Optional: set slot Status to Pending (non-breaking if it fails)
            try
            {
                var slotEntityResponse = await slotsTable.GetEntityAsync<TableEntity>(slotPk, slotRk);
                var slotEntity = slotEntityResponse.Value;
                slotEntity["Status"] = "Pending";
                await slotsTable.UpdateEntityAsync(slotEntity, slotEntity.ETag, TableUpdateMode.Merge);
            }
            catch (Exception mergeEx)
            {
                _logger.LogWarning(mergeEx, "Could not update slot status to Pending.");
            }

            var ok = req.CreateResponse(HttpStatusCode.OK);
            ok.Headers.Add("Content-Type", "application/json");
            await ok.WriteStringAsync($@"{{""RequestId"":""{requestId}"",""Status"":""Pending""}}");
            return ok;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RequestSlot failed.");
            var resp = req.CreateResponse(HttpStatusCode.InternalServerError);
            await resp.WriteStringAsync("Internal Server Error");
            return resp;
        }
    }
}
