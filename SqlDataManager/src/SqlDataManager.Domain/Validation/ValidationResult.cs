namespace SqlDataManager.Domain.Validation;

/// <summary>A single field-level validation error.</summary>
public sealed record ValidationError(string Field, string Message);

/// <summary>
/// Aggregated validation outcome. Errors are grouped by field so the API can
/// return them in the standard error contract and the UI can show each message
/// beside the relevant input.
/// </summary>
public sealed record ValidationResult
{
    private readonly List<ValidationError> _errors = [];

    public IReadOnlyList<ValidationError> Errors => _errors;
    public bool IsValid => _errors.Count == 0;

    public void Add(string field, string message) => _errors.Add(new ValidationError(field, message));

    public IReadOnlyDictionary<string, string[]> ToDictionary() =>
        _errors
            .GroupBy(e => e.Field)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray());

    public static ValidationResult Success() => new();
}
