using System.Net;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

public class GetSlotRequests
{
    private readonly ILogger _logger;
    private readonly TableServiceClient _tableServiceClient;

    private const string RequestsTableName = "GameSwapSlotRequests";

    public GetSlotRequests(ILoggerFactory loggerFactory, TableServiceClient tableServiceClient)
    {
        _logger = loggerFactory.CreateLogger<GetSlotRequests>();
        _tableServiceClient = tableServiceClient;
    }

    [Function("GetSlotRequests")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "slots/{division}/{slotId}/requests")] HttpRequestData req,
        string division,
        string slotId)
    {
        try
        {
            var requestsTable = _tableServiceClient.GetTableClient(RequestsTableName);
            await requestsTable.CreateIfNotExistsAsync();

            var pk = $"{division}|{slotId}";
            var filter = $"PartitionKey eq '{EscapeOData(pk)}'";

            var items = new List<object>();

            await foreach (var e in requestsTable.QueryAsync<TableEntity>(filter: filter))
            {
                items.Add(new
                {
                    RequestId = e.RowKey,
                    Division = e.GetString("Division") ?? division,
                    SlotId = e.GetString("SlotId") ?? slotId,
                    RequestingTeamId = e.GetString("RequestingTeamId") ?? "",
                    RequestingEmail = e.GetString("RequestingEmail") ?? "",
                    Message = e.GetString("Message") ?? "",
                    Status = e.GetString("Status") ?? "",
                    RequestedAtUtc = e.GetDateTimeOffset("RequestedAtUtc")?.ToString("o") ?? ""
                });
            }

            // Sort newest-first for UI convenience
            items = items
                .OrderByDescending(x =>
                {
                    var prop = x.GetType().GetProperty("RequestedAtUtc")?.GetValue(x)?.ToString();
                    return DateTimeOffset.TryParse(prop, out var dt) ? dt : DateTimeOffset.MinValue;
                })
                .Cast<object>()
                .ToList();

            var resp = req.CreateResponse(HttpStatusCode.OK);
            resp.Headers.Add("Content-Type", "application/json");
            await resp.WriteStringAsync(JsonSerializer.Serialize(items));
            return resp;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetSlotRequests failed");
            var resp = req.CreateResponse(HttpStatusCode.InternalServerError);
            await resp.WriteStringAsync("Internal Server Error");
            return resp;
        }
    }

    private static string EscapeOData(string s) => s.Replace("'", "''");
}
