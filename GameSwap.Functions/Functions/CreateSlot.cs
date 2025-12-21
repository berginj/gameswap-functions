using System.Net;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using GameSwap.Functions.Storage;

namespace GameSwap.Functions.Functions;

public class CreateSlot
{
    private readonly ILogger _log;
    private readonly TableServiceClient _svc;

    private const string SlotsTableName = "GameSwapSlots";
    private const string FieldsTableName = "GameSwapFields";

    public CreateSlot(ILoggerFactory lf, TableServiceClient tableServiceClient)
    {
        _log = lf.CreateLogger<CreateSlot>();
        _svc = tableServiceClient;
    }

    public record CreateSlotReq(
        string? division,
        string? offeringTeamId,
        string? gameDate,
        string? startTime,
        string? endTime,
        string? parkName,
        string? fieldName,
        string? offeringEmail,
        string? gameType,
        string? notes
    );

    [Function("CreateSlot")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "slots")] HttpRequestData req)
    {
        try
        {
            var leagueId = ApiGuards.RequireLeagueId(req);
            var me = IdentityUtil.GetMe(req);
            await ApiGuards.RequireMemberAsync(_svc, me.UserId, leagueId);

            var body = await HttpUtil.ReadJsonAsync<CreateSlotReq>(req);
            if (body is null) return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = "Invalid JSON body" });

            var division = (body.division ?? "").Trim();
            var offeringTeamId = (body.offeringTeamId ?? "").Trim();
            var offeringEmail = (body.offeringEmail ?? me.Email ?? "").Trim();

            var gameDate = (body.gameDate ?? "").Trim();
            var startTime = (body.startTime ?? "").Trim();
            var endTime = (body.endTime ?? "").Trim();

            var parkName = (body.parkName ?? "").Trim();
            var fieldName = (body.fieldName ?? "").Trim();

            var gameType = string.IsNullOrWhiteSpace(body.gameType) ? "Swap" : body.gameType!.Trim();
            var notes = (body.notes ?? "").Trim();

            if (string.IsNullOrWhiteSpace(division) ||
                string.IsNullOrWhiteSpace(offeringTeamId) ||
                string.IsNullOrWhiteSpace(gameDate) ||
                string.IsNullOrWhiteSpace(startTime) ||
                string.IsNullOrWhiteSpace(endTime) ||
                string.IsNullOrWhiteSpace(parkName) ||
                string.IsNullOrWhiteSpace(fieldName))
            {
                return HttpUtil.Json(req, HttpStatusCode.BadRequest, new
                {
                    error = "division, offeringTeamId, gameDate, startTime, endTime, parkName, fieldName are required"
                });
            }

            // Validate field exists + active, and normalize display name
            var fieldsTable = _svc.GetTableClient(FieldsTableName);
            await fieldsTable.CreateIfNotExistsAsync();

            var parkCode = Slug.Make(parkName);
            var fieldCode = Slug.Make(fieldName);
            if (string.IsNullOrWhiteSpace(parkCode) || string.IsNullOrWhiteSpace(fieldCode))
                return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = "Invalid parkName/fieldName; slug became empty." });

            var fieldPk = $"FIELD#{leagueId}#{parkCode}";
            var fieldRk = fieldCode;

            TableEntity field;
            try { field = (await fieldsTable.GetEntityAsync<TableEntity>(fieldPk, fieldRk)).Value; }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = "Field not found in fields table. Import/create fields first." });
            }

            var isActive = field.GetBoolean("IsActive") ?? true;
            if (!isActive)
                return HttpUtil.Json(req, HttpStatusCode.Conflict, new { error = "Field exists but IsActive=false." });

            var displayName = field.GetString("DisplayName") ?? $"{parkName} > {fieldName}";

            var slotsTable = _svc.GetTableClient(SlotsTableName);
            await slotsTable.CreateIfNotExistsAsync();

            var slotId = Guid.NewGuid().ToString("N");
            var pk = $"SLOT#{leagueId}#{division}";
            var now = DateTimeOffset.UtcNow;

            var entity = new TableEntity(pk, slotId)
            {
                ["LeagueId"] = leagueId,
                ["SlotId"] = slotId,
                ["Division"] = division,

                ["OfferingTeamId"] = offeringTeamId,
                ["OfferingEmail"] = offeringEmail,

                ["GameDate"] = gameDate,
                ["StartTime"] = startTime,
                ["EndTime"] = endTime,

                ["ParkName"] = field.GetString("ParkName") ?? parkName,
                ["FieldName"] = field.GetString("FieldName") ?? fieldName,
                ["DisplayName"] = displayName,
                ["FieldKey"] = $"{parkCode}/{fieldCode}",

                ["GameType"] = gameType,
                ["Status"] = "Open",
                ["Notes"] = notes,

                ["CreatedUtc"] = now,
                ["UpdatedUtc"] = now
            };

            await slotsTable.AddEntityAsync(entity);

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
            _log.LogError(ex, "CreateSlot failed");
            return HttpUtil.Text(req, HttpStatusCode.InternalServerError, "Internal Server Error");
        }
    }
}
