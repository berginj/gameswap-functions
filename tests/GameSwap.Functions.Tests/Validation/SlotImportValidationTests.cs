using GameSwap.Functions.Storage;
using Xunit;

namespace GameSwap.Functions.Tests.Validation;

public class SlotImportValidationTests
{
    [Fact]
    public void Rejects_missing_required_fields()
    {
        var header = new[] { "division", "offeringteamid", "gamedate", "starttime", "endtime", "fieldkey" };
        var idx = CsvMini.HeaderIndex(header);
        var row = new[] { "A", "", "2024-05-01", "09:00", "10:00", "park/field" };

        var ok = SlotImportValidation.TryParseRow(row, idx, out _, out var error);

        Assert.False(ok);
        Assert.Contains("required", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parses_valid_row()
    {
        var header = new[] { "division", "offeringteamid", "gamedate", "starttime", "endtime", "fieldkey", "status" };
        var idx = CsvMini.HeaderIndex(header);
        var row = new[] { "A", "TEAM1", "2024-05-01", "09:00", "10:00", "Park/Field 1", "Open" };

        var ok = SlotImportValidation.TryParseRow(row, idx, out var parsed, out var error);

        Assert.True(ok, error);
        Assert.Equal("park", parsed.ParkCode);
        Assert.Equal("field-1", parsed.FieldCode);
    }
}
