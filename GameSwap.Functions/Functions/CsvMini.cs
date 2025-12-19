using System.Text;

public static class CsvMini
{
    // Returns list of rows; each row is list of cells
    public static List<List<string>> Parse(string csv)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < csv.Length; i++)
        {
            char c = csv[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // escaped quote?
                    if (i + 1 < csv.Length && csv[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    cell.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    row.Add(cell.ToString());
                    cell.Clear();
                }
                else if (c == '\r')
                {
                    // ignore, handle on \n
                }
                else if (c == '\n')
                {
                    row.Add(cell.ToString());
                    cell.Clear();
                    rows.Add(row);
                    row = new List<string>();
                }
                else
                {
                    cell.Append(c);
                }
            }
        }

        // flush last cell/row
        row.Add(cell.ToString());
        if (row.Count > 1 || !string.IsNullOrWhiteSpace(row[0]))
            rows.Add(row);

        // Trim trailing empty rows
        while (rows.Count > 0 && IsBlankRow(rows[^1]))
            rows.RemoveAt(rows.Count - 1);

        return rows;
    }

    public static Dictionary<string, int> HeaderIndex(List<string> headerRow)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headerRow.Count; i++)
        {
            var key = (headerRow[i] ?? "").Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(key) && !map.ContainsKey(key))
                map[key] = i;
        }
        return map;
    }

    public static string Get(List<string> row, Dictionary<string, int> headerIndex, string column)
    {
        if (!headerIndex.TryGetValue(column.ToLowerInvariant(), out var idx))
            return "";

        if (idx < 0 || idx >= row.Count)
            return "";

        return row[idx] ?? "";
    }

    public static bool IsBlankRow(List<string> row)
    {
        for (int i = 0; i < row.Count; i++)
            if (!string.IsNullOrWhiteSpace(row[i]))
                return false;
        return true;
    }
}
