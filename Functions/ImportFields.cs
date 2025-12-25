using System.Net;
using System.Linq;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using GameSwap.Functions.Storage;

namespace GameSwap.Functions.Functions;

public class ImportFields
{
    private readonly ILogger _log;
    private readonly TableServiceClient _svc;

    public ImportFields(ILoggerFactory lf, TableServiceClient tableServiceClient)
    {
        _log = lf.CreateLogger<ImportFields>();
        _svc = tableServiceClient;
    }

    [Function("ImportFields")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "import/fields")] HttpRequestData req)
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
            _log.LogError(ex, "ImportFields storage request failed. requestId={requestId}", requestId);
            return ApiResponses.Error(
                req,
                HttpStatusCode.BadGateway,
                "STORAGE_ERROR",
                "Storage request failed.",
                new { requestId, status = ex.Status, code = ex.ErrorCode });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "ImportFields failed");
            return ApiResponses.Error(
                req,
                HttpStatusCode.InternalServerError,
                "INTERNAL",
                "Internal Server Error",
                new { exception = ex.GetType().Name, message = ex.Message });
        }
    }

    private static bool TryParseFieldKeyFlexible(string raw, string parkName, string fieldName,
        out string parkCode, out string fieldCode, out string normalizedFieldKey)
    {
        parkCode = ""; fieldCode = ""; normalizedFieldKey = "";
        var v = (raw ?? "").Trim().Trim('/', '\\');

        var slashParts = v.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (slashParts.Length == 2)
        {
            parkCode = Slug.Make(slashParts[0]);
            fieldCode = Slug.Make(slashParts[1]);
            if (string.IsNullOrWhiteSpace(parkCode) || string.IsNullOrWhiteSpace(fieldCode)) return false;
            normalizedFieldKey = $"{parkCode}/{fieldCode}";
            return true;
        }

        var us = v.Split('_', 2, StringSplitOptions.TrimEntries);
        if (us.Length == 2)
        {
            parkCode = Slug.Make(us[0]);
            fieldCode = Slug.Make(us[1]);
            if (!string.IsNullOrWhiteSpace(parkCode) && !string.IsNullOrWhiteSpace(fieldCode))
            {
                normalizedFieldKey = $"{parkCode}/{fieldCode}";
                return true;
            }
        }

        parkCode = Slug.Make(parkName);
        fieldCode = Slug.Make(fieldName);
        if (string.IsNullOrWhiteSpace(parkCode) || string.IsNullOrWhiteSpace(fieldCode)) return false;

        normalizedFieldKey = $"{parkCode}/{fieldCode}";
        return true;
    }

    private static bool ParseIsActive(string statusRaw, string isActiveRaw)
    {
        if (!string.IsNullOrWhiteSpace(statusRaw))
        {
            var s = statusRaw.Trim();
            if (string.Equals(s, Constants.Status.FieldInactive, StringComparison.OrdinalIgnoreCase)) return false;
            if (string.Equals(s, Constants.Status.FieldActive, StringComparison.OrdinalIgnoreCase)) return true;
        }

        if (!string.IsNullOrWhiteSpace(isActiveRaw) && bool.TryParse(isActiveRaw, out var b))
            return b;

        return true;
    }

    private static string AppendOptionalFieldNotes(string existingNotes, string[] row, Dictionary<string, int> headerIndex)
    {
        var notes = (existingNotes ?? "").Trim();
        var extras = new List<string>();
        string GetOpt(string key) => CsvMini.Get(row, headerIndex, key).Trim();

        var lights = GetOpt("lights");
        if (!string.IsNullOrWhiteSpace(lights)) extras.Add($"Lights: {lights}");

        var cage = GetOpt("battingcage");
        if (!string.IsNullOrWhiteSpace(cage)) extras.Add($"Batting cage: {cage}");

        var mound = GetOpt("portablemound");
        if (!string.IsNullOrWhiteSpace(mound)) extras.Add($"Portable mound: {mound}");

        var lockCode = GetOpt("fieldlockcode");
        if (!string.IsNullOrWhiteSpace(lockCode)) extras.Add($"Lock code: {lockCode}");

        var fieldNotes = GetOpt("fieldnotes");
        if (!string.IsNullOrWhiteSpace(fieldNotes)) extras.Add(fieldNotes);

        if (extras.Count == 0) return notes;

        var extraText = string.Join(" | ", extras);
        if (string.IsNullOrWhiteSpace(notes)) return extraText;
        if (notes.Contains(extraText, StringComparison.OrdinalIgnoreCase)) return notes;
        return $"{notes} | {extraText}";
    }

    [Function("ImportFieldsGrid")]
    public async Task<HttpResponseData> RunGrid(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "import/fields/grid")] HttpRequestData req)
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
            _log.LogError(ex, "ImportFields storage request failed. requestId={requestId}", requestId);
            return ApiResponses.Error(
                req,
                HttpStatusCode.BadGateway,
                "STORAGE_ERROR",
                "Storage request failed.",
                new { requestId, status = ex.Status, code = ex.ErrorCode });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "ImportFields failed");
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
            header[0] = header[0].TrimStart('\uFEFF'); // strip BOM

        var idx = CsvMini.HeaderIndex(header);

        if (!idx.ContainsKey("fieldkey") || !idx.ContainsKey("parkname") || !idx.ContainsKey("fieldname"))
        {
            var headerPreview = string.Join(",", header.Select(x => (x ?? "").Trim()).Take(12));
            return ApiResponses.Error(req, HttpStatusCode.BadRequest, "BAD_REQUEST",
                "Missing required columns. Required: fieldKey, parkName, fieldName. Optional: displayName, address, notes, status (Active/Inactive).",
                new { headerPreview });
        }

        var table = await TableClients.GetTableAsync(_svc, Constants.Tables.Fields);

        int upserted = 0, rejected = 0, skipped = 0;
        var errors = new List<ImportHelpers.ImportError>();
        var actions = new List<TableTransactionAction>();

        for (int i = 1; i < rows.Count; i++)
        {
            var r = rows[i];
            if (CsvMini.IsBlankRow(r)) { skipped++; continue; }

            var fieldKeyRaw = CsvMini.Get(r, idx, "fieldkey").Trim();
            var parkName = CsvMini.Get(r, idx, "parkname").Trim();
            var fieldName = CsvMini.Get(r, idx, "fieldname").Trim();

            var displayName = CsvMini.Get(r, idx, "displayname").Trim();
            var address = CsvMini.Get(r, idx, "address").Trim();
            var notes = CsvMini.Get(r, idx, "notes").Trim();

            var statusRaw = CsvMini.Get(r, idx, "status").Trim();
            var isActiveRaw = CsvMini.Get(r, idx, "isactive").Trim();

            var rowNumber = i + 1;
            bool hasError = false;

            if (string.IsNullOrWhiteSpace(fieldKeyRaw))
            {
                errors.Add(new ImportHelpers.ImportError(rowNumber, "fieldKey", "fieldKey is required."));
                hasError = true;
            }

            if (string.IsNullOrWhiteSpace(parkName))
            {
                errors.Add(new ImportHelpers.ImportError(rowNumber, "parkName", "parkName is required."));
                hasError = true;
            }

            if (string.IsNullOrWhiteSpace(fieldName))
            {
                errors.Add(new ImportHelpers.ImportError(rowNumber, "fieldName", "fieldName is required."));
                hasError = true;
            }

            if (hasError)
            {
                rejected++;
                continue;
            }

            if (!TryParseFieldKeyFlexible(fieldKeyRaw, parkName, fieldName, out var parkCode, out var fieldCode, out var normalizedFieldKey))
            {
                rejected++;
                errors.Add(new ImportHelpers.ImportError(rowNumber, "fieldKey",
                    "Invalid fieldKey. Use parkCode/fieldCode or parkCode_fieldCode, or valid parkName/fieldName.", fieldKeyRaw));
                continue;
            }

            if (string.IsNullOrWhiteSpace(displayName))
                displayName = $"{parkName} > {fieldName}";

            var isActive = ParseIsActive(statusRaw, isActiveRaw);

            var pk = Constants.Pk.Fields(leagueId, parkCode);
            var rk = fieldCode;

            notes = AppendOptionalFieldNotes(notes, r, idx);

            var entity = new TableEntity(pk, rk)
            {
                ["LeagueId"] = leagueId,
                ["FieldKey"] = normalizedFieldKey,
                ["ParkCode"] = parkCode,
                ["FieldCode"] = fieldCode,
                ["ParkName"] = parkName,
                ["FieldName"] = fieldName,
                ["DisplayName"] = displayName,
                ["Address"] = address,
                ["Notes"] = notes,
                ["IsActive"] = isActive,
                ["UpdatedUtc"] = DateTimeOffset.UtcNow
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
}
