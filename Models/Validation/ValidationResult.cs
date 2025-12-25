namespace GameSwap.Functions.Models.Validation;

public sealed class ValidationResult
{
    public IList<ValidationError> Errors { get; } = new List<ValidationError>();

    public bool IsValid => Errors.Count == 0;

    public void Add(string field, string message)
    {
        Errors.Add(new ValidationError(field, message));
    }
}

public readonly record struct ValidationError(string Field, string Message);
