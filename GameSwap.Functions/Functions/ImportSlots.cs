using System.Net;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

public class ImportSlots
{
    private readonly ILogger _logger;
    private readonly TableServiceClient _tableServiceClient;

    private const string SlotsTableName = "GameSwapSlots";
    private const string FieldsTableName = "GameSwapFields";

    public ImportSlots(ILoggerFactory loggerFactory, TableServiceClient tableServiceClient)
    {
        _logger = loggerFactory.CreateLogger<ImportSlots>();
        _tableServiceClient = tableServiceClient;
    }

    [Function("ImportSlots")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "import/slots")] HttpRequestData req)
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

            // Required columns
            var required = new[] { "division", "offeringteamid", "gamedate", "starttime", "endtime", "fieldid" };
            var missing = required.Where(c => !headerIndex.ContainsKey(c)).ToList();
            if (missing.Count > 0)
            {
                return Json(req, HttpStatusCode.BadRequest, new
                {
                    error = "Missing required columns.",
                    required = required,
                    missing
                });
            }

            var slotsTable = _tableServiceClient.GetTableClient(SlotsTableName);
            var fieldsTable = _tableServiceClient.GetTableClient(FieldsTableName);
            await slotsTable.CreateIfNotExistsAsync();
            await fieldsTable.CreateIfNotExistsAsync();

            // Build FieldId -> FieldName map (optional but helps UI)
            var fieldNameById = await LoadFieldMapAsync(fieldsTable);

            // We'll group by PartitionKey (Division) so we can batch per division
            var byDivision = new Dictionary<string, List<TableEntity>>(StringComparer.OrdinalIgnoreCase);

            var errors = new List<object>();
            var upserted = 0;

            for (int i = 1; i < rows.Count; i++)
            {
                var r = rows[i];
                if (CsvMini.IsBlankRow(r)) continue;

                string division = CsvMini.Get(r, headerIndex, "division").Trim();
                string offeringTeamId = CsvMini.Get(r, headerIndex, "offeringteamid").Trim();
                string gameDate = CsvMini.Get(r, headerIndex, "gamedate").Trim();     // YYYY-MM-DD
                string startTime = CsvMini.Get(r, headerIndex, "starttime").Trim();   // HH:mm
                string endTime = CsvMini.Get(r, headerIndex, "endtime").Trim();       // HH:mm
                string fieldId = CsvMini.Get(r, headerIndex, "fieldid").Trim();

                string gameType = CsvMini.Get(r, headerIndex, "gametype").Trim();
                string notes = CsvMini.Get(r, headerIndex, "notes").Trim();
                string status = CsvMini.Get(r, headerIndex, "status").Trim();

                if (string.IsNullOrWhiteSpace(division) ||
                    string.IsNullOrWhiteSpace(offeringTeamId) ||
                    string.IsNullOrWhiteSpace(gameDate) ||
                    string.IsNullOrWhiteSpace(startTime) ||
                    string.IsNullOrWhiteSpace(endTime) ||
                    string.IsNullOrWhiteSpace(fieldId))
                {
                    errors.Add(new { row = i + 1, error = "Division, OfferingTeamId, GameDate, StartTime, EndTime, FieldId are required." });
                    continue;
                }

                if (string.IsNullOrWhiteSpace(gameType)) gameType = "Swap";
                if (string.IsNullOrWhiteSpace(status)) status = "Open";

                // Deterministic SlotId so re-import doesn’t duplicate:
                // RowKey (SlotId) = "{OfferingTeamId}|{GameDate}|{StartTime}|{EndTime}|{FieldId}"
                var slotId = SafeKey($"{offeringTeamId}|{gameDate}|{startTime}|{endTime}|{fieldId}");

                var fieldName = fieldNameById.TryGetValue(fieldId, out var name)
                    ? name
                    : fieldId; // fallback

                var entity = new TableEntity(division, slotId)
                {
                    ["SlotId"] = slotId,
                    ["Division"] = division,
                    ["OfferingTeamId"] = offeringTeamId,
                    ["GameDate"] = gameDate,
                    ["StartTime"] = startTime,
                    ["EndTime"] = endTime,
                    ["FieldId"] = fieldId,
                    ["Field"] = fieldName,
                    ["GameType"] = gameType,
                    ["Status"] = status,
                    ["Notes"] = notes
                };

                if (!byDivision.TryGetValue(division, out var list))
                {
                    list = new List<TableEntity>();
                    byDivision[division] = list;
                }

                list.Add(entity);
            }

            // Batch upsert by division (PartitionKey)
            foreach (var kvp in byDivision)
            {
                var division = kvp.Key;
                var entities = kvp.Value;

                for (int idx = 0; idx < entities.Count; idx += 100)
                {
                    var chunk = entities.Skip(idx).Take(100);
                    var actions = chunk.Select(e => new TableTransactionAction(TableTransactionActionType.UpsertMerge, e)).ToList();
                    var result = await slotsTable.SubmitTransactionAsync(actions);
                    upserted += result.Value.Count;
                }
            }

            return Json(req, HttpStatusCode.OK, new
            {
                table = SlotsTableName,
                upserted,
                errors
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ImportSlots failed");
            return Json(req, HttpStatusCode.InternalServerError, new { error = "Internal Server Error" });
        }
    }

    private static async Task<Dictionary<string, string>> LoadFieldMapAsync(TableClient fieldsTable)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // PartitionKey = "Fields"
        await foreach (var e in fieldsTable.QueryAsync<TableEntity>(filter: "PartitionKey eq 'Fields'"))
        {
            var fieldId = e.GetString("FieldId") ?? e.RowKey;
            var fieldName = e.GetString("FieldName") ?? fieldId;
            if (!string.IsNullOrWhiteSpace(fieldId))
                map[fieldId] = fieldName;
        }

        return map;
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
