namespace GameSwap.Functions.Storage;

public static class ScheduleValidation
{
    public static bool TryValidateDate(string value, string fieldName, out string error)
    {
        error = "";
        var trimmed = (value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            error = $"{fieldName} is required.";
            return false;
        }

        if (!DateOnly.TryParseExact(trimmed, "yyyy-MM-dd", out _))
        {
            error = $"{fieldName} must be YYYY-MM-DD.";
            return false;
        }

        return true;
    }

    public static bool TryValidateOptionalDate(string? value, string fieldName, out string error)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            error = "";
            return true;
        }

        return TryValidateDate(value, fieldName, out error);
    }

    public static bool TryValidateTimeRange(string startTime, string endTime, out string error)
    {
        if (!TimeUtil.IsValidRange(startTime, endTime, out _, out _, out var timeErr))
        {
            error = timeErr;
            return false;
        }

        error = "";
        return true;
    }

    public static bool TryParseFieldKey(string raw, out string parkCode, out string fieldCode)
    {
        parkCode = "";
        fieldCode = "";
        var v = (raw ?? "").Trim().Trim('/');
        var parts = v.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2) return false;

        parkCode = Slug.Make(parts[0]);
        fieldCode = Slug.Make(parts[1]);
        return !string.IsNullOrWhiteSpace(parkCode) && !string.IsNullOrWhiteSpace(fieldCode);
    }

    public static bool LocationMatches(string? filter, params string?[] values)
    {
        if (string.IsNullOrWhiteSpace(filter)) return true;

        var needle = filter.Trim();
        return values.Any(value =>
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            return value.Contains(needle, StringComparison.OrdinalIgnoreCase);
        });
    }
}
