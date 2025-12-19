using System.Net;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

public class GetSlots
{
    private readonly TableServiceClient _tables;

    public GetSlots(TableServiceClient tables)
    {
        _tables = tables;
    }

    [Function("GetSlots")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "slots")] HttpRequestData req)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var division = query["division"];
        var status = query["status"] ?? "Open";

        var table = _tables.GetTableClient("GameSlots");
        await table.CreateIfNotExistsAsync();

        var results = new List<object>();

        if (!string.IsNullOrEmpty(division))
        {
            var filter = TableClient.CreateQueryFilter<GameSlotEntity>(
                s => s.PartitionKey == division && s.Status == status);

            await foreach (var e in table.QueryAsync<GameSlotEntity>(filter))
                results.Add(Map(e));
        }
        else
        {
            var filter = TableClient.CreateQueryFilter<GameSlotEntity>(
                s => s.Status == status);

            await foreach (var e in table.QueryAsync<GameSlotEntity>(filter))
                results.Add(Map(e));
        }

        var res = req.CreateResponse(HttpStatusCode.OK);
        await res.WriteAsJsonAsync(results);
        return res;
    }

    private static object Map(GameSlotEntity e) => new
    {
        SlotId = e.RowKey,
        Division = e.PartitionKey,
        e.OfferingTeamId,
        e.GameDate,
        e.StartTime,
        e.EndTime,
        e.Field,
        e.GameType,
        e.Status,
        e.Notes
    };
}
