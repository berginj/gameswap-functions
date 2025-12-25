using System.Net;
using System.Linq;
using Azure;
using Azure.Data.Tables;
using GameSwap.Functions.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace GameSwap.Functions.Functions;

public class ImportEvents
{
    private readonly ILogger _log;
    private readonly TableServiceClient _svc;

    public ImportEvents(ILoggerFactory lf, TableServiceClient tableServiceClient)
    {
        _log = lf.CreateLogger<ImportEvents>();
        _svc = tableServiceClient;
    }

    [Function("ImportEvents")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "import/events")] HttpRequestData req)
    {
        try
        {
            var leagueId = ApiGuards.RequireLeagueId(req);
            var me = IdentityUtil.GetMe(req);
            await ApiGuards.RequireLeagueAdminAsync(_svc, me.UserId, leagueId);

            var csvText = await CsvUpload.ReadCsvTextAsync(req);
            if (string.IsNullOrWhiteSpace(csvText))
                return ApiResponses.Error(req, HttpStatusCode.BadRequest, "BAD_REQUEST", "Empty CSV body.");

            var rows = CsvMini.Parse(csvText);
            if (rows.Count < 2)
                return ApiResponses.Error(req, HttpStatusCode.BadRequest, "BAD_REQUEST", "No CSV rows found.");

            return await ImportFromRowsAsync(req, leagueId, rows);
        }
        catch (ApiGuards.HttpError ex) { return ApiResponses.FromHttpError(req, ex); }
        catch (RequestFailedException ex)
        {
            var requestId = req.FunctionContext.InvocationId.ToString();
            _log.LogError(ex, "ImportEvents storage request failed. requestId={requestId}", requestId);
            return ApiResponses.Error(
                req,
                HttpStatusCode.BadGateway,
                "STORAGE_ERROR",
                "Storage request failed.",
                new { requestId, status = ex.Status, code = ex.ErrorCode });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "ImportEvents failed");
            return ApiResponses.Error(
                req,
                HttpStatusCode.InternalServerError,
                "INTERNAL",
                "Internal Server Error",
                new { exception = ex.GetType().Name, message = ex.Message });
        }
    }

    [Function("ImportEventsGrid")]
    public async Task<HttpResponseData> RunGrid(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "import/events/grid")] HttpRequestData req)
    {
        try
        {
            var leagueId = ApiGuards.RequireLeagueId(req);
            var me = IdentityUtil.GetMe(req);
            await ApiGuards.RequireLeagueAdminAsync(_svc, me.UserId, leagueId);

            var payload = await ImportHelpers.ReadGridPayloadAsync(req);
            if (payload is null)
                return ApiResponses.Error(req, HttpStatusCode.BadRequest, "BAD_REQUEST", "Invalid JSON body.");

            var rows = ImportHelpers.BuildRowsFromGrid(payload);
            if (rows.Count < 2)
                return ApiResponses.Error(req, HttpStatusCode.BadRequest, "BAD_REQUEST", "No rows found.");

            return await ImportFromRowsAsync(req, leagueId, rows);
        }
        catch (ApiGuards.HttpError ex) { return ApiResponses.FromHttpError(req, ex); }
        catch (RequestFailedException ex)
        {
            var requestId = req.FunctionContext.InvocationId.ToString();
            _log.LogError(ex, "ImportEvents storage request failed. requestId={requestId}", requestId);
            return ApiResponses.Error(
                req,
                HttpStatusCode.BadGateway,
                "STORAGE_ERROR",
                "Storage request failed.",
                new { requestId, status = ex.Status, code = ex.ErrorCode });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "ImportEvents failed");
            return ApiResponses.Error(
                req,
                HttpStatusCode.InternalServerError,
                "INTERNAL",
                "Internal Server Error",
                new { exception = ex.GetType().Name, message = ex.Message });
        }
    }

    private async Task<HttpResponseData> ImportFromRowsAsync(HttpRequestData req, string leagueId, List<string[]> rows)
    {
        var header = rows[0];
        if (header.Length > 0 && header[0] != null)
            header[0] = header[0].TrimStart('\uFEFF');

        var idx = CsvMini.HeaderIndex(header);
        if (!idx.ContainsKey("title") || !idx.ContainsKey("eventdate") || !idx.ContainsKey("starttime") || !idx.ContainsKey("endtime"))
        {
            var headerPreview = string.Join(",", header.Select(x => (x ?? "").Trim()).Take(12));
            return ApiResponses.Error(req, HttpStatusCode.BadRequest, "BAD_REQUEST",
                "Missing required columns. Required: title, eventDate, startTime, endTime. Optional: type, division, teamId, location, notes, status.",
                new { headerPreview });
        }

        var table = await TableClients.GetTableAsync(_svc, Constants.Tables.Events);

        int upserted = 0, rejected = 0, skipped = 0;
        var errors = new List<ImportHelpers.ImportError>();
        var actions = new List<TableTransactionAction>();

        for (int i = 1; i < rows.Count; i++)
        {
            var r = rows[i];
            if (CsvMini.IsBlankRow(r)) { skipped++; continue; }

            var rowNumber = i + 1;

            var type = CsvMini.Get(r, idx, "type").Trim();
            var division = CsvMini.Get(r, idx, "division").Trim();
            var teamId = CsvMini.Get(r, idx, "teamid").Trim();
            var title = CsvMini.Get(r, idx, "title").Trim();
            var eventDate = CsvMini.Get(r, idx, "eventdate").Trim();
            var startTime = CsvMini.Get(r, idx, "starttime").Trim();
            var endTime = CsvMini.Get(r, idx, "endtime").Trim();
            var location = CsvMini.Get(r, idx, "location").Trim();
            var notes = CsvMini.Get(r, idx, "notes").Trim();
            var status = CsvMini.Get(r, idx, "status").Trim();

            var hasError = false;
            if (string.IsNullOrWhiteSpace(title))
            {
                errors.Add(new ImportHelpers.ImportError(rowNumber, "title", "title is required."));
                hasError = true;
            }

            if (string.IsNullOrWhiteSpace(eventDate))
            {
                errors.Add(new ImportHelpers.ImportError(rowNumber, "eventDate", "eventDate is required."));
                hasError = true;
            }
            else if (!TryParseDate(eventDate))
            {
                errors.Add(new ImportHelpers.ImportError(rowNumber, "eventDate", "Use yyyy-MM-dd format.", eventDate));
                hasError = true;
            }

            if (string.IsNullOrWhiteSpace(startTime))
            {
                errors.Add(new ImportHelpers.ImportError(rowNumber, "startTime", "startTime is required."));
                hasError = true;
            }
            else if (!TryParseTime(startTime))
            {
                errors.Add(new ImportHelpers.ImportError(rowNumber, "startTime", "Use HH:mm format.", startTime));
                hasError = true;
            }

            if (string.IsNullOrWhiteSpace(endTime))
            {
                errors.Add(new ImportHelpers.ImportError(rowNumber, "endTime", "endTime is required."));
                hasError = true;
            }
            else if (!TryParseTime(endTime))
            {
                errors.Add(new ImportHelpers.ImportError(rowNumber, "endTime", "Use HH:mm format.", endTime));
                hasError = true;
            }

            if (!string.IsNullOrWhiteSpace(status) &&
                !string.Equals(status, Constants.Status.EventScheduled, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(status, Constants.Status.EventCancelled, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new ImportHelpers.ImportError(rowNumber, "status", "Status must be Scheduled or Cancelled.", status));
                hasError = true;
            }

            if (hasError)
            {
                rejected++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(type))
                type = Constants.EventTypes.Other;

            var normalizedStatus = string.IsNullOrWhiteSpace(status)
                ? Constants.Status.EventScheduled
                : status.Trim();

            var eventId = "evt_" + Guid.NewGuid().ToString("N");
            var pk = Constants.Pk.Events(leagueId);
            var now = DateTimeOffset.UtcNow;

            var entity = new TableEntity(pk, eventId)
            {
                ["LeagueId"] = leagueId,
                ["EventId"] = eventId,
                ["Type"] = type,
                ["Status"] = normalizedStatus,
                ["Division"] = division,
                ["TeamId"] = teamId,
                ["Title"] = title,
                ["EventDate"] = eventDate,
                ["StartTime"] = startTime,
                ["EndTime"] = endTime,
                ["Location"] = location,
                ["Notes"] = notes,
                ["CreatedUtc"] = now,
                ["UpdatedUtc"] = now
            };

            actions.Add(new TableTransactionAction(TableTransactionActionType.UpsertMerge, entity));

            if (actions.Count == 100)
            {
                var result = await table.SubmitTransactionAsync(actions);
                upserted += result.Value.Count;
                actions.Clear();
            }
        }

        if (actions.Count > 0)
        {
            var result = await table.SubmitTransactionAsync(actions);
            upserted += result.Value.Count;
        }

        return ApiResponses.Ok(req, new { leagueId, upserted, rejected, skipped, errors });
    }

    private static bool TryParseDate(string value)
        => DateOnly.TryParseExact(value, "yyyy-MM-dd", out _);

    private static bool TryParseTime(string value)
        => TimeOnly.TryParseExact(value, "HH:mm", out _);
}
