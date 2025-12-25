namespace GameSwap.Functions.Models.Validation;

internal static class ValidationExtensions
{
    public static void Required(this ValidationResult result, string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result.Add(field, "Value is required.");
        }
    }

    public static void Required(this ValidationResult result, DateOnly value, string field)
    {
        if (value == default)
        {
            result.Add(field, "Value is required.");
        }
    }

    public static void Required(this ValidationResult result, TimeOnly value, string field)
    {
        if (value == default)
        {
            result.Add(field, "Value is required.");
        }
    }

    public static void Required(this ValidationResult result, DateTimeOffset value, string field)
    {
        if (value == default)
        {
            result.Add(field, "Value is required.");
        }
    }
}
