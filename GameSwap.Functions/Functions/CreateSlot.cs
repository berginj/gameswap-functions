using System.Net;
using System.Text.Json;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

public record SlotCreateDto(
    string Division,
    string OfferingTeamId,
    string GameDate,
    string StartTime,
    string EndTime,
    string Field,
    string GameType,
    string? Notes
);

public class CreateSlot
{
    private readonly TableServiceClient _tables;

    public CreateSlot(TableServiceClient tables)
    {
        _tables = tables;
    }

    [Function("CreateSlot")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "slots")] HttpRequestData req)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var dto = JsonSerializer.Deserialize<SlotCreateDto>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (dto is null)
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Invalid payload");
            return bad;
        }

        var table = _tables.GetTableClient("GameSlots");
        await table.CreateIfNotExistsAsync();

        var slotId = Guid.NewGuid().ToString();

        var entity = new GameSlotEntity
        {
            PartitionKey = dto.Division,
            RowKey = slotId,
            OfferingTeamId = dto.OfferingTeamId,
            GameDate = dto.GameDate,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            Field = dto.Field,
            GameType = dto.GameType,
            Status = "Open",
            Notes = dto.Notes,
            CreatedBy = "TEMP-USER",
            CreatedAt = DateTime.UtcNow
        };

        await table.AddEntityAsync(entity);

        var res = req.CreateResponse(HttpStatusCode.Created);
        await res.WriteAsJsonAsync(new { SlotId = slotId });
        return res;
    }
}
