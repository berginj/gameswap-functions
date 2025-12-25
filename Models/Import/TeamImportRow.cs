namespace GameSwap.Functions.Models.Import;

public record TeamImportRow(
    string? division,
    string? teamId,
    string? name,
    string? primaryContactName,
    string? primaryContactEmail,
    string? primaryContactPhone
);
