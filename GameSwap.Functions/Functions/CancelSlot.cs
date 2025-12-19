using System.Net;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

public class CancelSlot
{
    private readonly TableServiceClient _tables;

    public CancelSlot(TableServiceClient tables) => _tables = tables;

    [Function("CancelSlot")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "slots/{division}/{slotId}/cancel")] HttpRequestData req,
        string division,
        string slotId)
    {
        var table = _tables.GetTableClient("GameSlots");
        await table.CreateIfNotExistsAsync();

        try
        {
            var entity = await table.GetEntityAsync<GameSlotEntity>(division, slotId);

            entity.Value.Status = "Cancelled";
            await table.UpdateEntityAsync(entity.Value, entity.Value.ETag, TableUpdateMode.Replace);

            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(new { SlotId = slotId, Status = "Cancelled" });
            return res;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync("Slot not found");
            return notFound;
        }
    }
}
