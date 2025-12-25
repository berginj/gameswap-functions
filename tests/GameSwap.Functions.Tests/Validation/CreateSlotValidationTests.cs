using GameSwap.Functions.Functions;
using GameSwap.Functions.Storage;
using Xunit;

namespace GameSwap.Functions.Tests.Validation;

public class CreateSlotValidationTests
{
    [Fact]
    public void Rejects_missing_required_fields()
    {
        var req = new CreateSlot.CreateSlotReq(
            division: "",
            offeringTeamId: "TEAM1",
            gameDate: "2024-05-01",
            startTime: "09:00",
            endTime: "10:00",
            fieldKey: "park/field",
            parkName: null,
            fieldName: null,
            offeringEmail: null,
            gameType: null,
            notes: null);

        var ok = CreateSlotValidation.TryValidate(req, "coach@example.com", out _, out var error);

        Assert.False(ok);
        Assert.Contains("required", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_invalid_date_format()
    {
        var req = new CreateSlot.CreateSlotReq(
            division: "A",
            offeringTeamId: "TEAM1",
            gameDate: "05/01/2024",
            startTime: "09:00",
            endTime: "10:00",
            fieldKey: "park/field",
            parkName: null,
            fieldName: null,
            offeringEmail: null,
            gameType: null,
            notes: null);

        var ok = CreateSlotValidation.TryValidate(req, "coach@example.com", out _, out var error);

        Assert.False(ok);
        Assert.Contains("YYYY-MM-DD", error, StringComparison.OrdinalIgnoreCase);
    }
}
