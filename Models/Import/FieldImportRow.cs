namespace GameSwap.Functions.Models.Import;

public record FieldImportRow(
    string? fieldKey,
    string? parkName,
    string? fieldName,
    string? displayName,
    string? address,
    string? notes,
    string? status,
    string? isActive,
    string? lights,
    string? battingCage,
    string? portableMound,
    string? fieldLockCode,
    string? fieldNotes
);
