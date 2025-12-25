namespace GameSwap.Functions.Storage;

public static class FieldImportValidation
{
    public static readonly string[] RequiredColumns = { "fieldkey", "parkname", "fieldname" };

    public static bool HasRequiredColumns(Dictionary<string, int> headerIndex)
        => RequiredColumns.All(headerIndex.ContainsKey);

    public static bool TryParseFieldKeyFlexible(string raw, string parkName, string fieldName,
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

    public static bool ParseIsActive(string statusRaw, string isActiveRaw)
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

    public static string AppendOptionalFieldNotes(string existingNotes, string[] row, Dictionary<string, int> headerIndex)
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
}
