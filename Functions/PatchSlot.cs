using System.Net;
using Azure;
using Azure.Data.Tables;
using GameSwap.Functions.Models;
using GameSwap.Functions.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace GameSwap.Functions.Functions;

public class PatchSlot
{
    private readonly ILogger _log;
    private readonly TableServiceClient _svc;

    private const string SlotsTableName = Constants.Tables.Slots;
    private const string FieldsTableName = Constants.Tables.Fields;

    public PatchSlot(ILoggerFactory lf, TableServiceClient tableServiceClient)
    {
        _log = lf.CreateLogger<PatchSlot>();
        _svc = tableServiceClient;
    }

    public record PatchSlotReq(
        string? gameDate,
        string? startTime,
        string? endTime,
        string? fieldKey,
        string? gameType,
        string? sport,
        string? skill,
        string? notes
    );

    [Function("PatchSlot")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "slots/{division}/{slotId}")] HttpRequestData req,
        string division,
        string slotId)
    {
        try
        {
            var leagueId = ApiGuards.RequireLeagueId(req);
            var me = IdentityUtil.GetMe(req);
            await ApiGuards.RequireNotViewerAsync(_svc, me.UserId, leagueId);

            division = (division ?? "").Trim();
            slotId = (slotId ?? "").Trim();
            if (string.IsNullOrWhiteSpace(division) || string.IsNullOrWhiteSpace(slotId))
                return ApiResponses.Error(req, HttpStatusCode.BadRequest, "BAD_REQUEST", "division and slotId are required");

            var body = await HttpUtil.ReadJsonAsync<PatchSlotReq>(req);
            if (body is null) return ApiResponses.Error(req, HttpStatusCode.BadRequest, "BAD_REQUEST", "Invalid JSON body");

            if (body.gameDate != null && string.IsNullOrWhiteSpace(body.gameDate))
                return ApiResponses.Error(req, HttpStatusCode.BadRequest, "BAD_REQUEST", "gameDate is required");
            if (body.startTime != null && string.IsNullOrWhiteSpace(body.startTime))
                return ApiResponses.Error(req, HttpStatusCode.BadRequest, "BAD_REQUEST", "startTime is required");
            if (body.endTime != null && string.IsNullOrWhiteSpace(body.endTime))
                return ApiResponses.Error(req, HttpStatusCode.BadRequest, "BAD_REQUEST", "endTime is required");
            if (body.fieldKey != null && string.IsNullOrWhiteSpace(body.fieldKey))
                return ApiResponses.Error(req, HttpStatusCode.BadRequest, "BAD_REQUEST", "fieldKey is required");

            var table = await TableClients.GetTableAsync(_svc, SlotsTableName);
            var pk = Constants.Pk.Slots(leagueId, division);
            TableEntity entity;
            try { entity = (await table.GetEntityAsync<TableEntity>(pk, slotId)).Value; }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return ApiResponses.Error(req, HttpStatusCode.NotFound, "NOT_FOUND", "Slot not found");
            }

            var status = (entity.GetString("Status") ?? Constants.Status.SlotOpen).Trim();
            if (!string.Equals(status, Constants.Status.SlotOpen, StringComparison.OrdinalIgnoreCase))
                return ApiResponses.Error(req, HttpStatusCode.Conflict, "CONFLICT", "Only open slots can be updated.");

            var isGlobalAdmin = await ApiGuards.IsGlobalAdminAsync(_svc, me.UserId);
            if (!isGlobalAdmin)
            {
                var mem = await ApiGuards.GetMembershipAsync(_svc, me.UserId, leagueId);
                var role = ApiGuards.GetRole(mem);
                if (string.Equals(role, Constants.Roles.Coach, StringComparison.OrdinalIgnoreCase))
                {
                    var (myDivision, myTeamId) = ApiGuards.GetCoachTeam(mem);
                    var offeringTeamId = (entity.GetString("OfferingTeamId") ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(myTeamId))
                        return ApiResponses.Error(req, HttpStatusCode.BadRequest, "COACH_TEAM_REQUIRED", "Coach role requires an assigned team to update a slot.");

                    if (!string.Equals(myDivision, division, StringComparison.OrdinalIgnoreCase))
                        return ApiResponses.Error(req, HttpStatusCode.Conflict, "DIVISION_MISMATCH", "You can only update slots within your assigned division (exact match).");

                    if (!string.Equals(myTeamId, offeringTeamId, StringComparison.OrdinalIgnoreCase))
                        return ApiResponses.Error(req, HttpStatusCode.Forbidden, "FORBIDDEN", "You can only update your own offered slots.");
                }
            }

            void SetIfNotNull(string key, string? value)
            {
                if (value is null) return;
                entity[key] = value.Trim();
            }

            SetIfNotNull("GameDate", body.gameDate);
            SetIfNotNull("StartTime", body.startTime);
            SetIfNotNull("EndTime", body.endTime);
            SetIfNotNull("GameType", body.gameType);
            SetIfNotNull("Sport", body.sport);
            SetIfNotNull("Skill", body.skill);
            SetIfNotNull("Notes", body.notes);

            if (body.fieldKey != null)
            {
                if (!ScheduleValidation.TryParseFieldKey(body.fieldKey, out var parkCode, out var fieldCode))
                    return ApiResponses.Error(req, HttpStatusCode.BadRequest, "BAD_REQUEST", "fieldKey must be parkCode/fieldCode.");

                var fieldsTable = await TableClients.GetTableAsync(_svc, FieldsTableName);
                var fieldPk = Constants.Pk.Fields(leagueId, parkCode);
                var fieldRk = fieldCode;

                TableEntity field;
                try { field = (await fieldsTable.GetEntityAsync<TableEntity>(fieldPk, fieldRk)).Value; }
                catch (RequestFailedException ex) when (ex.Status == 404)
                {
                    return ApiResponses.Error(req, HttpStatusCode.BadRequest, "BAD_REQUEST", "Field not found. Import fields first.");
                }

                var isActive = field.GetBoolean("IsActive") ?? true;
                if (!isActive)
                    return ApiResponses.Error(req, HttpStatusCode.Conflict, "CONFLICT", "Field exists but is inactive.");

                var normalizedParkName = field.GetString("ParkName") ?? "";
                var normalizedFieldName = field.GetString("FieldName") ?? "";
                var displayName = field.GetString("DisplayName") ?? $"{normalizedParkName} > {normalizedFieldName}";

                entity["ParkName"] = normalizedParkName;
                entity["FieldName"] = normalizedFieldName;
                entity["DisplayName"] = displayName;
                entity["FieldKey"] = $"{parkCode}/{fieldCode}";
            }

            var finalGameDate = (entity.GetString("GameDate") ?? "").Trim();
            var finalStartTime = (entity.GetString("StartTime") ?? "").Trim();
            var finalEndTime = (entity.GetString("EndTime") ?? "").Trim();

            if (!ScheduleValidation.TryValidateDate(finalGameDate, "gameDate", out var dateErr))
                return ApiResponses.Error(req, HttpStatusCode.BadRequest, "BAD_REQUEST", dateErr);
            if (!ScheduleValidation.TryValidateTimeRange(finalStartTime, finalEndTime, out var timeErr))
                return ApiResponses.Error(req, HttpStatusCode.BadRequest, "BAD_REQUEST", timeErr);

            entity["UpdatedUtc"] = DateTimeOffset.UtcNow;
            await table.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Merge);

            var dto = new SlotOpportunityDto(
                slotId: entity.RowKey,
                leagueId: leagueId,
                division: (entity.GetString("Division") ?? division).Trim(),
                offeringTeamId: (entity.GetString("OfferingTeamId") ?? "").Trim(),
                confirmedTeamId: (entity.GetString("ConfirmedTeamId") ?? "").Trim(),
                gameDate: (entity.GetString("GameDate") ?? "").Trim(),
                startTime: (entity.GetString("StartTime") ?? "").Trim(),
                endTime: (entity.GetString("EndTime") ?? "").Trim(),
                parkName: (entity.GetString("ParkName") ?? "").Trim(),
                fieldName: (entity.GetString("FieldName") ?? "").Trim(),
                displayName: (entity.GetString("DisplayName") ?? "").Trim(),
                fieldKey: (entity.GetString("FieldKey") ?? "").Trim(),
                gameType: (entity.GetString("GameType") ?? "Swap").Trim(),
                status: (entity.GetString("Status") ?? Constants.Status.SlotOpen).Trim(),
                sport: (entity.GetString("Sport") ?? "").Trim(),
                skill: (entity.GetString("Skill") ?? "").Trim(),
                notes: (entity.GetString("Notes") ?? "").Trim()
            );

            return ApiResponses.Ok(req, dto);
        }
        catch (ApiGuards.HttpError ex) { return ApiResponses.FromHttpError(req, ex); }
        catch (Exception ex)
        {
            _log.LogError(ex, "PatchSlot failed");
            return ApiResponses.Error(req, HttpStatusCode.InternalServerError, "INTERNAL", "Internal Server Error");
        }
    }
}
