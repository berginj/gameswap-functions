using System.Net;
using Azure;
using Azure.Data.Tables;
using GameSwap.Functions.Models;
using GameSwap.Functions.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace GameSwap.Functions.Functions;

public class GetEvent
{
    private readonly ILogger _log;
    private readonly TableServiceClient _svc;

    private const string EventsTableName = Constants.Tables.Events;

    public GetEvent(ILoggerFactory lf, TableServiceClient tableServiceClient)
    {
        _log = lf.CreateLogger<GetEvent>();
        _svc = tableServiceClient;
    }

    [Function("GetEvent")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events/{eventId}")] HttpRequestData req,
        string eventId)
    {
        try
        {
            var leagueId = ApiGuards.RequireLeagueId(req);
            var me = IdentityUtil.GetMe(req);
            await ApiGuards.RequireMemberAsync(_svc, me.UserId, leagueId);

            eventId = (eventId ?? "").Trim();
            if (string.IsNullOrWhiteSpace(eventId))
                return ApiResponses.Error(req, HttpStatusCode.BadRequest, "BAD_REQUEST", "eventId is required");

            var table = await TableClients.GetTableAsync(_svc, EventsTableName);
            var pk = Constants.Pk.Events(leagueId);
            TableEntity entity;
            try { entity = (await table.GetEntityAsync<TableEntity>(pk, eventId)).Value; }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return ApiResponses.Error(req, HttpStatusCode.NotFound, "NOT_FOUND", "Event not found");
            }

            var createdUtc = entity.TryGetValue("CreatedUtc", out var cu) ? (cu?.ToString() ?? "") : "";
            var updatedUtc = entity.TryGetValue("UpdatedUtc", out var uu) ? (uu?.ToString() ?? "") : "";
            var createdBy = (entity.GetString("CreatedByUserId") ?? entity.GetString("CreatedBy") ?? "").Trim();

            var dto = new CalendarEventDto(
                eventId: entity.RowKey,
                type: (entity.GetString("Type") ?? "").Trim(),
                status: (entity.GetString("Status") ?? "").Trim(),
                division: (entity.GetString("Division") ?? "").Trim(),
                teamId: (entity.GetString("TeamId") ?? "").Trim(),
                opponentTeamId: (entity.GetString("OpponentTeamId") ?? "").Trim(),
                title: (entity.GetString("Title") ?? "").Trim(),
                eventDate: (entity.GetString("EventDate") ?? "").Trim(),
                startTime: (entity.GetString("StartTime") ?? "").Trim(),
                endTime: (entity.GetString("EndTime") ?? "").Trim(),
                location: (entity.GetString("Location") ?? "").Trim(),
                sport: (entity.GetString("Sport") ?? "").Trim(),
                skill: (entity.GetString("Skill") ?? "").Trim(),
                notes: (entity.GetString("Notes") ?? "").Trim(),
                createdByUserId: createdBy,
                acceptedByUserId: ((entity.GetString("AcceptedByUserId") ?? "").Trim()),
                createdUtc: createdUtc,
                updatedUtc: updatedUtc
            );

            return ApiResponses.Ok(req, dto);
        }
        catch (ApiGuards.HttpError ex) { return ApiResponses.FromHttpError(req, ex); }
        catch (Exception ex)
        {
            _log.LogError(ex, "GetEvent failed");
            return ApiResponses.Error(req, HttpStatusCode.InternalServerError, "INTERNAL", "Internal Server Error");
        }
    }
}
