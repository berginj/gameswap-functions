using System.Net;
using Azure;
using Azure.Data.Tables;
using GameSwap.Functions.Models;
using GameSwap.Functions.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace GameSwap.Functions.Functions;

public class GetEvents
{
    private readonly ILogger _log;
    private readonly TableServiceClient _svc;

    private const string EventsTableName = Constants.Tables.Events;

    public GetEvents(ILoggerFactory lf, TableServiceClient tableServiceClient)
    {
        _log = lf.CreateLogger<GetEvents>();
        _svc = tableServiceClient;
    }

    [Function("GetEvents")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events")] HttpRequestData req)
    {
        try
        {
            var leagueId = ApiGuards.RequireLeagueId(req);
            var me = IdentityUtil.GetMe(req);
            await ApiGuards.RequireMemberAsync(_svc, me.UserId, leagueId);

            var division = (ApiGuards.GetQueryParam(req, "division") ?? "").Trim();
            var dateFrom = (ApiGuards.GetQueryParam(req, "dateFrom") ?? "").Trim();
            var dateTo = (ApiGuards.GetQueryParam(req, "dateTo") ?? "").Trim();
            var sport = (ApiGuards.GetQueryParam(req, "sport") ?? "").Trim();
            var skill = (ApiGuards.GetQueryParam(req, "skill") ?? "").Trim();
            var location = (ApiGuards.GetQueryParam(req, "location") ?? "").Trim();

            if (!ScheduleValidation.TryValidateOptionalDate(dateFrom, "dateFrom", out var dateFromErr))
                return ApiResponses.Error(req, HttpStatusCode.BadRequest, "BAD_REQUEST", dateFromErr);
            if (!ScheduleValidation.TryValidateOptionalDate(dateTo, "dateTo", out var dateToErr))
                return ApiResponses.Error(req, HttpStatusCode.BadRequest, "BAD_REQUEST", dateToErr);

            var table = await TableClients.GetTableAsync(_svc, EventsTableName);
            var pk = Constants.Pk.Events(leagueId);
            var filter = $"PartitionKey eq '{ApiGuards.EscapeOData(pk)}'";

            if (!string.IsNullOrWhiteSpace(division))
            {
                // When division is provided, include league-wide events where Division is empty.
                filter += $" and (Division eq '{ApiGuards.EscapeOData(division)}' or Division eq '')";
            }

            if (!string.IsNullOrWhiteSpace(dateFrom))
                filter += $" and EventDate ge '{ApiGuards.EscapeOData(dateFrom)}'";
            if (!string.IsNullOrWhiteSpace(dateTo))
                filter += $" and EventDate le '{ApiGuards.EscapeOData(dateTo)}'";

            if (!string.IsNullOrWhiteSpace(sport))
                filter += $" and Sport eq '{ApiGuards.EscapeOData(sport)}'";
            if (!string.IsNullOrWhiteSpace(skill))
                filter += $" and Skill eq '{ApiGuards.EscapeOData(skill)}'";

            var list = new List<CalendarEventDto>();
            await foreach (var e in table.QueryAsync<TableEntity>(filter: filter))
            {
                var createdUtc = e.TryGetValue("CreatedUtc", out var cu) ? (cu?.ToString() ?? "") : "";
                var updatedUtc = e.TryGetValue("UpdatedUtc", out var uu) ? (uu?.ToString() ?? "") : "";
                var createdBy = (e.GetString("CreatedByUserId") ?? e.GetString("CreatedBy") ?? "").Trim();
                var eventLocation = (e.GetString("Location") ?? "").Trim();
                if (!ScheduleValidation.LocationMatches(location, eventLocation))
                    continue;

                list.Add(new CalendarEventDto(
                    eventId: e.RowKey,
                    type: (e.GetString("Type") ?? "").Trim(),
                    status: (e.GetString("Status") ?? "").Trim(),
                    division: (e.GetString("Division") ?? "").Trim(),
                    teamId: (e.GetString("TeamId") ?? "").Trim(),
                    opponentTeamId: (e.GetString("OpponentTeamId") ?? "").Trim(),
                    title: (e.GetString("Title") ?? "").Trim(),
                    eventDate: (e.GetString("EventDate") ?? "").Trim(),
                    startTime: (e.GetString("StartTime") ?? "").Trim(),
                    endTime: (e.GetString("EndTime") ?? "").Trim(),
                    location: eventLocation,
                    sport: (e.GetString("Sport") ?? "").Trim(),
                    skill: (e.GetString("Skill") ?? "").Trim(),
                    notes: (e.GetString("Notes") ?? "").Trim(),
                    createdByUserId: createdBy,
                    acceptedByUserId: ((e.GetString("AcceptedByUserId") ?? "").Trim()),
                    createdUtc: createdUtc,
                    updatedUtc: updatedUtc
                ));
            }

            return ApiResponses.Ok(req, list
                .OrderBy(x => x.eventDate)
                .ThenBy(x => x.startTime)
                .ThenBy(x => x.title)
                .ToList());
        }
        catch (ApiGuards.HttpError ex) { return ApiResponses.FromHttpError(req, ex); }
        catch (Exception ex)
        {
            _log.LogError(ex, "GetEvents failed");
            return ApiResponses.Error(req, HttpStatusCode.InternalServerError, "INTERNAL", "Internal Server Error");
        }
    }
}
