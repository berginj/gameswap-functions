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

            var header = rows[0];
            if (header.Length > 0 && header[0] != null)
                header[0] = header[0].TrimStart('\uFEFF'); // strip BOM

            var idx = CsvMini.HeaderIndex(header);

            if (!FieldImportValidation.HasRequiredColumns(idx))
            {
                // Helpful debug: show what the importer thought the header row was
                var headerPreview = string.Join(",", header.Select(x => (x ?? "").Trim()).Take(12));
                return ApiResponses.Error(req, HttpStatusCode.BadRequest, "BAD_REQUEST",
                    "Missing required columns. Required: fieldKey, parkName, fieldName. Optional: displayName, address, notes, status (Active/Inactive).",
                    new { headerPreview });
            }

            var table = await TableClients.GetTableAsync(_svc, Constants.Tables.Fields);

            int upserted = 0, rejected = 0, skipped = 0;
            var errors = new List<object>();
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

                if (string.IsNullOrWhiteSpace(fieldKeyRaw) || string.IsNullOrWhiteSpace(parkName) || string.IsNullOrWhiteSpace(fieldName))
                {
                    rejected++;
                    errors.Add(new { row = i + 1, fieldKey = fieldKeyRaw, error = "fieldKey, parkName, fieldName are required." });
                    continue;
                }

                if (!FieldImportValidation.TryParseFieldKeyFlexible(fieldKeyRaw, parkName, fieldName, out var parkCode, out var fieldCode, out var normalizedFieldKey))
                {
                    rejected++;
                    errors.Add(new { row = i + 1, fieldKey = fieldKeyRaw, error = "Invalid fieldKey. Use parkCode/fieldCode or parkCode_fieldCode, or valid parkName/fieldName." });
                    continue;
                }

                if (string.IsNullOrWhiteSpace(displayName))
                    displayName = $"{parkName} > {fieldName}";

                var isActive = FieldImportValidation.ParseIsActive(statusRaw, isActiveRaw);

                var pk = Constants.Pk.Fields(leagueId, parkCode);
                var rk = fieldCode;

                notes = FieldImportValidation.AppendOptionalFieldNotes(notes, r, idx);

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

    private static async Task<string> ReadCsvTextAsync(HttpRequestData req)
    {
        // Read body ONCE so we can sniff it even if Content-Type is wrong.
        using var ms = new MemoryStream();
        await req.Body.CopyToAsync(ms);
        var bodyBytes = ms.ToArray();
        if (bodyBytes.Length == 0) return "";

        var ct = GetHeader(req, "Content-Type");

        // Detect multipart either by Content-Type OR by body signature
        var looksMultipart =
            (!string.IsNullOrWhiteSpace(ct) && ct.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase)) ||
            BodyLooksMultipart(bodyBytes);

        if (looksMultipart)
        {
            var bytes = await MultipartFormData.ReadFirstFileBytesAsync(bodyBytes, ct, preferName: "file");
            var csv = Encoding.UTF8.GetString(bytes ?? Array.Empty<byte>());
            return NormalizeNewlines(StripBom(csv));
        }

        // Raw CSV text
        return NormalizeNewlines(StripBom(Encoding.UTF8.GetString(bodyBytes)));
    }

    private static bool BodyLooksMultipart(byte[] body)
    {
        // Cheap heuristic: multipart bodies contain Content-Disposition and boundary-like starts
        var s = Encoding.UTF8.GetString(body, 0, Math.Min(body.Length, 4096));
        return s.StartsWith("--") && s.Contains("Content-Disposition:", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetHeader(HttpRequestData req, string name)
        => req.Headers.TryGetValues(name, out var vals) ? (vals.FirstOrDefault() ?? "") : "";

    private static string StripBom(string s) => (s ?? "").TrimStart('\uFEFF');

    private static string NormalizeNewlines(string s)
        => (s ?? "").Replace("\r\n", "\n").Replace("\r", "\n");

    private static class MultipartFormData
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
