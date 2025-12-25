using GameSwap.Functions.Functions;

namespace GameSwap.Functions.Storage;

public static class CreateSlotValidation
{
    public sealed record Payload(
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
        string Notes);

    public static bool TryValidate(CreateSlot.CreateSlotReq? body, string fallbackEmail, out Payload payload, out string error)
    {
        payload = new Payload("", "", "", "", "", "", "", "", "", "", "");
        error = "Invalid JSON body";
        if (body is null) return false;

        var division = (body.division ?? "").Trim();
        var offeringTeamId = (body.offeringTeamId ?? "").Trim();
        var offeringEmail = (body.offeringEmail ?? fallbackEmail ?? "").Trim();
        var gameDate = (body.gameDate ?? "").Trim();
        var startTime = (body.startTime ?? "").Trim();
        var endTime = (body.endTime ?? "").Trim();
        var fieldKey = (body.fieldKey ?? "").Trim();
        var gameType = string.IsNullOrWhiteSpace(body.gameType) ? "Swap" : body.gameType!.Trim();
        var notes = (body.notes ?? "").Trim();

        if (string.IsNullOrWhiteSpace(division) ||
            string.IsNullOrWhiteSpace(offeringTeamId) ||
            string.IsNullOrWhiteSpace(gameDate) ||
            string.IsNullOrWhiteSpace(startTime) ||
            string.IsNullOrWhiteSpace(endTime) ||
            string.IsNullOrWhiteSpace(fieldKey))
        {
            error = "division, offeringTeamId, gameDate, startTime, endTime, fieldKey are required";
            return false;
        }

        if (!DateOnly.TryParseExact(gameDate, "yyyy-MM-dd", out _))
        {
            error = "gameDate must be YYYY-MM-DD.";
            return false;
        }

        if (!TimeUtil.IsValidRange(startTime, endTime, out _, out _, out var timeErr))
        {
            error = timeErr;
            return false;
        }

        if (!FieldKeyParser.TryParseFieldKey(fieldKey, out var parkCode, out var fieldCode))
        {
            error = "fieldKey must be parkCode/fieldCode.";
            return false;
        }

        payload = new Payload(
            division,
            offeringTeamId,
            offeringEmail,
            gameDate,
            startTime,
            endTime,
            fieldKey,
            parkCode,
            fieldCode,
            gameType,
            notes);

        return true;
    }
}
