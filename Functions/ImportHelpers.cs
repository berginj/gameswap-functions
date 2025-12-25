using System.Text.Json;
using Microsoft.Azure.Functions.Worker.Http;

namespace GameSwap.Functions.Functions;

public static class ImportHelpers
{
    public record GridPayload(List<string>? headers, List<List<string>>? rows);

    public record ImportError(int row, string column, string reason, string? value = null);

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<GridPayload?> ReadGridPayloadAsync(HttpRequestData req)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<GridPayload>(req.Body, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static List<string[]> BuildRowsFromGrid(GridPayload payload)
    {
        var result = new List<string[]>();
        var header = payload.headers?.ToArray() ?? Array.Empty<string>();
        result.Add(header);
        if (payload.rows is null) return result;

        foreach (var row in payload.rows)
            result.Add(row?.ToArray() ?? Array.Empty<string>());

        return result;
    }

    public static string NormalizeColumn(string? column)
        => (column ?? string.Empty).Trim();
}
