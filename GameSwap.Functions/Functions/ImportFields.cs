using System.Net;
using System.Text;
using System.Text.Json;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

public class ImportFields
{
    private readonly ILogger _logger;
    private readonly TableServiceClient _tableServiceClient;

    private const string FieldsTableName = "GameSwapFields";

    public ImportFields(ILoggerFactory loggerFactory, TableServiceClient tableServiceClient)
    {
        _logger = loggerFactory.CreateLogger<ImportFields>();
        _tableServiceClient = tableServiceClient;
    }

    [Function("ImportFields")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "import/fields")] HttpRequestData req)
    {
        try
        {
            var csvText = await ReadBodyAsStringAsync(req);
            if (string.IsNullOrWhiteSpace(csvText))
                return Json(req, HttpStatusCode.BadRequest, new { error = "Empty CSV body." });

            var rows = CsvMini.Parse(csvText);
            if (rows.Count == 0)
                return Json(req, HttpStatusCode.BadRequest, new { error = "No CSV rows found." });

            var header = rows[0];
            var headerIndex = CsvMini.HeaderIndex(header);

            // Required: FieldId, FieldName
            if (!headerIndex.ContainsKey("fieldid") || !headerIndex.ContainsKey("fieldname"))
            {
                return Json(req, HttpStatusCode.BadRequest, new
                {
                    error = "Missing required columns. Required: FieldId, FieldName. Optional: Address, Location, Notes"
                });
            }

            var table = _tableServiceClient.GetTableClient(FieldsTableName);
            await table.CreateIfNotExistsAsync();

            var upserted = 0;
            var errors = new List<object>();

            // One partition key makes batching easy
            const string pk = "Fields";
            var actions = new List<TableTransactionAction>();

            for (int i = 1; i < rows.Count; i++)
            {
                var r = rows[i];
                if (CsvMini.IsBlankRow(r)) continue;

                string fieldId = CsvMini.Get(r, headerIndex, "fieldid").Trim();
                string fieldName = CsvMini.Get(r, headerIndex, "fieldname").Trim();
                string address = CsvMini.Get(r, headerIndex, "address").Trim();
                string location = CsvMini.Get(r, headerIndex, "location").Trim();
                string notes = CsvMini.Get(r, headerIndex, "notes").Trim();

                if (string.IsNullOrWhiteSpace(fieldId) || string.IsNullOrWhiteSpace(fieldName))
                {
                    errors.Add(new { row = i + 1, error = "FieldId and FieldName are required." });
                    continue;
                }

                var rk = SafeKey(fieldId);

                var entity = new TableEntity(pk, rk)
                {
                    ["FieldId"] = fieldId,
                    ["FieldName"] = fieldName,
                    ["Address"] = address,
                    ["Location"] = location,
                    ["Notes"] = notes
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

            return Json(req, HttpStatusCode.OK, new
            {
                table = FieldsTableName,
                upserted,
                errors
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ImportFields failed");
            return Json(req, HttpStatusCode.InternalServerError, new { error = "Internal Server Error" });
        }
    }

    private static async Task<string> ReadBodyAsStringAsync(HttpRequestData req)
    {
        using var reader = new StreamReader(req.Body, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    private static HttpResponseData Json(HttpRequestData req, HttpStatusCode status, object obj)
    {
        var resp = req.CreateResponse(status);
        resp.Headers.Add("Content-Type", "application/json");
        resp.WriteString(JsonSerializer.Serialize(obj));
        return resp;
    }

    // Table keys cannot contain: / \ # ?
    private static string SafeKey(string input)
    {
        var bad = new HashSet<char>(new[] { '/', '\\', '#', '?' });
        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
            sb.Append(bad.Contains(c) ? '_' : c);
        return sb.ToString();
    }
}
