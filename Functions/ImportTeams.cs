using System.Net;
using System.Linq;
using Azure;
using Azure.Data.Tables;
using GameSwap.Functions.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace GameSwap.Functions.Functions;

public class ImportTeams
{
    private readonly ILogger _log;
    private readonly TableServiceClient _svc;

    public ImportTeams(ILoggerFactory lf, TableServiceClient tableServiceClient)
    {
        _log = lf.CreateLogger<ImportTeams>();
        _svc = tableServiceClient;
    }

    [Function("ImportTeams")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "import/teams")] HttpRequestData req)
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
            _log.LogError(ex, "ImportTeams storage request failed. requestId={requestId}", requestId);
            return ApiResponses.Error(
                req,
                HttpStatusCode.BadGateway,
                "STORAGE_ERROR",
                "Storage request failed.",
                new { requestId, status = ex.Status, code = ex.ErrorCode });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "ImportTeams failed");
            return ApiResponses.Error(
                req,
                HttpStatusCode.InternalServerError,
                "INTERNAL",
                "Internal Server Error",
                new { exception = ex.GetType().Name, message = ex.Message });
        }
    }

    [Function("ImportTeamsGrid")]
    public async Task<HttpResponseData> RunGrid(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "import/teams/grid")] HttpRequestData req)
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
            _log.LogError(ex, "ImportTeams storage request failed. requestId={requestId}", requestId);
            return ApiResponses.Error(
                req,
                HttpStatusCode.BadGateway,
                "STORAGE_ERROR",
                "Storage request failed.",
                new { requestId, status = ex.Status, code = ex.ErrorCode });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "ImportTeams failed");
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
        if (!idx.ContainsKey("division") || !idx.ContainsKey("teamid") || !idx.ContainsKey("name"))
        {
            var headerPreview = string.Join(",", header.Select(x => (x ?? "").Trim()).Take(12));
            return ApiResponses.Error(req, HttpStatusCode.BadRequest, "BAD_REQUEST",
                "Missing required columns. Required: division, teamId, name. Optional: primaryContactName, primaryContactEmail, primaryContactPhone.",
                new { headerPreview });
        }

        var table = await TableClients.GetTableAsync(_svc, Constants.Tables.Teams);

        int upserted = 0, rejected = 0, skipped = 0;
        var errors = new List<ImportHelpers.ImportError>();
        var actionsByPk = new Dictionary<string, List<TableTransactionAction>>(StringComparer.OrdinalIgnoreCase);

        for (int i = 1; i < rows.Count; i++)
        {
            var r = rows[i];
            if (CsvMini.IsBlankRow(r)) { skipped++; continue; }

            var rowNumber = i + 1;

            var division = CsvMini.Get(r, idx, "division").Trim();
            var teamId = CsvMini.Get(r, idx, "teamid").Trim();
            var name = CsvMini.Get(r, idx, "name").Trim();
            var primaryContactName = CsvMini.Get(r, idx, "primarycontactname").Trim();
            var primaryContactEmail = CsvMini.Get(r, idx, "primarycontactemail").Trim();
            var primaryContactPhone = CsvMini.Get(r, idx, "primarycontactphone").Trim();

            var hasError = false;
            if (string.IsNullOrWhiteSpace(division))
            {
                errors.Add(new ImportHelpers.ImportError(rowNumber, "division", "division is required."));
                hasError = true;
            }

            if (string.IsNullOrWhiteSpace(teamId))
            {
                errors.Add(new ImportHelpers.ImportError(rowNumber, "teamId", "teamId is required."));
                hasError = true;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(new ImportHelpers.ImportError(rowNumber, "name", "name is required."));
                hasError = true;
            }

            if (hasError)
            {
                rejected++;
                continue;
            }

            var pk = $"TEAM#{leagueId}#{division}";
            var now = DateTimeOffset.UtcNow;

            var entity = new TableEntity(pk, teamId)
            {
                ["LeagueId"] = leagueId,
                ["Division"] = division,
                ["TeamId"] = teamId,
                ["Name"] = name,
                ["PrimaryContactName"] = primaryContactName,
                ["PrimaryContactEmail"] = primaryContactEmail,
                ["PrimaryContactPhone"] = primaryContactPhone,
                ["UpdatedUtc"] = now
            };

            if (!actionsByPk.TryGetValue(pk, out var actions))
            {
                actions = new List<TableTransactionAction>();
                actionsByPk[pk] = actions;
            }

            actions.Add(new TableTransactionAction(TableTransactionActionType.UpsertMerge, entity));
        }

        foreach (var (pk, actions) in actionsByPk)
        {
            for (int i = 0; i < actions.Count; i += 100)
            {
                var chunk = actions.Skip(i).Take(100).ToList();
                try
                {
                    var result = await table.SubmitTransactionAsync(chunk);
                    upserted += result.Value.Count;
                }
                catch (RequestFailedException ex)
                {
                    _log.LogError(ex, "ImportTeams transaction failed for PK {pk}", pk);
                    errors.Add(new ImportHelpers.ImportError(0, "partitionKey", ex.Message, pk));
                }
            }
        }

        return ApiResponses.Ok(req, new { leagueId, upserted, rejected, skipped, errors });
    }
}
