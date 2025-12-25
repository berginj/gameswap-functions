namespace GameSwap.Functions.Models.Import;

public record EventImportRow(
    string? type,
    string? division,
    string? teamId,
    string? title,
    string? eventDate,
    string? startTime,
    string? endTime,
    string? location,
    string? notes,
    string? status
);
