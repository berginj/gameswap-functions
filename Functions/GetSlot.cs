using System.Net;
using Azure;
using Azure.Data.Tables;
using GameSwap.Functions.Models;
using GameSwap.Functions.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace GameSwap.Functions.Functions;

public class GetSlot
{
    private readonly ILogger _log;
    private readonly TableServiceClient _svc;

    private const string SlotsTableName = Constants.Tables.Slots;

    public GetSlot(ILoggerFactory lf, TableServiceClient tableServiceClient)
    {
        _log = lf.CreateLogger<GetSlot>();
        _svc = tableServiceClient;
    }

    [Function("GetSlot")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "slots/{division}/{slotId}")] HttpRequestData req,
        string division,
        string slotId)
    {
        try
        {
            var leagueId = ApiGuards.RequireLeagueId(req);
            var me = IdentityUtil.GetMe(req);
            await ApiGuards.RequireMemberAsync(_svc, me.UserId, leagueId);

            division = (division ?? "").Trim();
            slotId = (slotId ?? "").Trim();
            if (string.IsNullOrWhiteSpace(division) || string.IsNullOrWhiteSpace(slotId))
                return ApiResponses.Error(req, HttpStatusCode.BadRequest, "BAD_REQUEST", "division and slotId are required");

            var table = await TableClients.GetTableAsync(_svc, SlotsTableName);
            var pk = Constants.Pk.Slots(leagueId, division);
            TableEntity entity;
            try { entity = (await table.GetEntityAsync<TableEntity>(pk, slotId)).Value; }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return ApiResponses.Error(req, HttpStatusCode.NotFound, "NOT_FOUND", "Slot not found");
            }

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
            _log.LogError(ex, "GetSlot failed");
            return ApiResponses.Error(req, HttpStatusCode.InternalServerError, "INTERNAL", "Internal Server Error");
        }
    }
}
