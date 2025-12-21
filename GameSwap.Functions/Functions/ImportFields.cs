using System.Net;
using System.Text;
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

    private const string FieldsTableName = "GameSwapFields";

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
            await ApiGuards.RequireMemberAsync(_svc, me.UserId, leagueId);

            var csvText = await HttpUtil.ReadBodyAsStringAsync(req);
            if (string.IsNullOrWhiteSpace(csvText))
                return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = "Empty CSV body." });

            var rows = CsvMini.Parse(csvText);
            if (rows.Count < 2)
                return HttpUtil.Json(req, HttpStatusCode.BadRequest, new { error = "No CSV rows found." });

            var header = rows[0];
            var idx = CsvMini.HeaderIndex(header);

            // Required canonical columns:
            // ParkName, FieldName
            if (!idx.ContainsKey("parkname") || !idx.ContainsKey("fieldname"))
            {
                return HttpUtil.Json(req, HttpStatusCode.BadRequest, new
                {
                    error = "Missing required columns. Required: ParkName, FieldName. Optional: DisplayName, Address, Notes, IsActive"
                });
            }

            var table = _svc.GetTableClient(FieldsTableName);
            await table.CreateIfNotExistsAsync();

            int upserted = 0;
            int rejected = 0;
            int skipped = 0;

            var errors = new List<object>();
            var actions = new List<TableTransactionAction>();

            for (int i = 1; i < rows.Count; i++)
            {
                var r = rows[i];
                if (CsvMini.IsBlankRow(r)) { skipped++; continue; }

                var parkName = CsvMini.Get(r, idx, "parkname").Trim();
                var fieldName = CsvMini.Get(r, idx, "fieldname").Trim();
                var displayName = CsvMini.Get(r, idx, "displayname").Trim();
                var address = CsvMini.Get(r, idx, "address").Trim();
                var notes = CsvMini.Get(r, idx, "notes").Trim();
                var isActiveRaw = CsvMini.Get(r, idx, "isactive").Trim();

                if (string.IsNullOrWhiteSpace(parkName) || string.IsNullOrWhiteSpace(fieldName))
                {
                    rejected++;
                    errors.Add(new { row = i + 1, error = "ParkName and FieldName are required." });
                    continue;
                }

                var parkCode = Slug.Make(parkName);
                var fieldCode = Slug.Make(fieldName);

                if (string.IsNullOrWhiteSpace(parkCode) || string.IsNullOrWhiteSpace(fieldCode))
                {
                    rejected++;
                    errors.Add(new { row = i + 1, error = "Invalid ParkName/FieldName; slug became empty." });
                    continue;
                }

                if (string.IsNullOrWhiteSpace(displayName))
                    displayName = $"{parkName} > {fieldName}";

                bool isActive = string.IsNullOrWhiteSpace(isActiveRaw)
                    ? true
                    : (bool.TryParse(isActiveRaw, out var b) ? b : true);

                var pk = $"FIELD#{leagueId}#{parkCode}";
                var rk = fieldCode;

                var entity = new TableEntity(pk, rk)
                {
                    ["LeagueId"] = leagueId,
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

            return HttpUtil.Json(req, HttpStatusCode.OK, new
            {
                table = FieldsTableName,
                leagueId,
                upserted,
                rejected,
                skipped,
                errors
            });
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
            _log.LogError(ex, "ImportFields failed");
            return HttpUtil.Json(req, HttpStatusCode.InternalServerError, new { error = "Internal Server Error" });
        }
    }
}
