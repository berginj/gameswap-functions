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
}
