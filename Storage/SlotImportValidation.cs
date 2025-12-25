namespace GameSwap.Functions.Storage;

public static class SlotImportValidation
{
    public static readonly string[] RequiredColumns =
    {
        "division",
        "offeringteamid",
        "gamedate",
        "starttime",
        "endtime",
        "fieldkey"
    };

    public sealed record Row(
        string Division,
        string OfferingTeamId,
        string OfferingEmail,
        string GameDate,
        string StartTime,
        string EndTime,
        string FieldKeyRaw,
        string ParkCode,
        string FieldCode,
        string GameType,
        string Notes,
        string Status);

    public static bool HasRequiredColumns(Dictionary<string, int> headerIndex, out List<string> missing)
    {
        missing = RequiredColumns.Where(c => !headerIndex.ContainsKey(c)).ToList();
        return missing.Count == 0;
    }

    public static bool TryParseRow(string[] row, Dictionary<string, int> headerIndex, out Row result, out string error)
    {
        result = new Row("", "", "", "", "", "", "", "", "", "", "", "");
        error = "";

        var division = CsvMini.Get(row, headerIndex, "division").Trim();
        var offeringTeamId = CsvMini.Get(row, headerIndex, "offeringteamid").Trim();
        var offeringEmail = CsvMini.Get(row, headerIndex, "offeringemail").Trim();

        var gameDate = CsvMini.Get(row, headerIndex, "gamedate").Trim();
        var startTime = CsvMini.Get(row, headerIndex, "starttime").Trim();
        var endTime = CsvMini.Get(row, headerIndex, "endtime").Trim();

        var fieldKeyRaw = CsvMini.Get(row, headerIndex, "fieldkey").Trim();
        var gameType = CsvMini.Get(row, headerIndex, "gametype").Trim();
        var notes = CsvMini.Get(row, headerIndex, "notes").Trim();
        var status = CsvMini.Get(row, headerIndex, "status").Trim();

        if (string.IsNullOrWhiteSpace(division) ||
            string.IsNullOrWhiteSpace(offeringTeamId) ||
            string.IsNullOrWhiteSpace(gameDate) ||
            string.IsNullOrWhiteSpace(startTime) ||
            string.IsNullOrWhiteSpace(endTime) ||
            string.IsNullOrWhiteSpace(fieldKeyRaw))
        {
            error = "Division, OfferingTeamId, GameDate, StartTime, EndTime, FieldKey are required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(gameType)) gameType = "Swap";
        if (string.IsNullOrWhiteSpace(status)) status = "Open";

        if (!FieldKeyParser.TryParseFieldKey(fieldKeyRaw, out var parkCode, out var fieldCode))
        {
            error = "Invalid FieldKey. Use parkCode/fieldCode.";
            return false;
        }

        result = new Row(
            division,
            offeringTeamId,
            offeringEmail,
            gameDate,
            startTime,
            endTime,
            fieldKeyRaw,
            parkCode,
            fieldCode,
            gameType,
            notes,
            status);

        return true;
    }
}
