using SqlDataManager.Domain.Querying;

namespace SqlDataManager.Domain.Mutations;

/// <summary>
/// A single supplied field value for a create or update operation. Keeping the
/// original value as well (for updates) lets the module submit only changed
/// fields and support original-value optimistic concurrency.
/// </summary>
public sealed record FieldValue(string Column, object? Value);

/// <summary>A structured request to insert a new record.</summary>
public sealed record CreateRecordRequest
{
    /// <summary>The field values explicitly supplied by the user.</summary>
    public required IReadOnlyList<FieldValue> Fields { get; init; }
}

/// <summary>A structured request to update exactly one existing record.</summary>
public sealed record UpdateRecordRequest
{
    /// <summary>The stable key identifying the target row.</summary>
    public required RecordKey Key { get; init; }

    /// <summary>Only the fields that actually changed.</summary>
    public required IReadOnlyList<FieldValue> ChangedFields { get; init; }

    /// <summary>
    /// The original rowversion token, base64-encoded, when the table supports
    /// rowversion-based optimistic concurrency.
    /// </summary>
    public string? ConcurrencyToken { get; init; }

    /// <summary>
    /// Original values for configured concurrency columns, used when the table
    /// does not have a rowversion but concurrency checking is still desired.
    /// </summary>
    public IReadOnlyList<FieldValue> OriginalValues { get; init; } = [];
}

/// <summary>A structured request to delete exactly one existing record.</summary>
public sealed record DeleteRecordRequest
{
    public required RecordKey Key { get; init; }
    public string? ConcurrencyToken { get; init; }
}

/// <summary>
/// The outcome of a create/update/delete. Includes generated key values on
/// create (for example an identity id) so the UI can refresh the affected row.
/// </summary>
public sealed record RecordMutationResult
{
    public required bool Success { get; init; }
    public required int AffectedRows { get; init; }

    /// <summary>Generated key values (for example identity id) after a create.</summary>
    public IReadOnlyDictionary<string, object?>? GeneratedKeys { get; init; }

    /// <summary>The full row after the mutation, when it could be read back.</summary>
    public IReadOnlyDictionary<string, object?>? Record { get; init; }
}
