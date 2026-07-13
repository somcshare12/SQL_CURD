using SqlDataManager.Domain.Metadata;

namespace SqlDataManager.Application.Abstractions;

/// <summary>
/// A single already-validated, already-coerced column value. The Application
/// layer produces these so the Infrastructure layer never has to guess a
/// value's type or trust an unvalidated column name — the <see cref="Column"/>
/// is a real metadata column and <see cref="Value"/> is a proper CLR value
/// ready to bind as a SQL parameter.
/// </summary>
public sealed record PreparedValue(ColumnMetadata Column, object? Value);

/// <summary>A validated INSERT command.</summary>
public sealed record PreparedCreate
{
    public required ObjectMetadata Metadata { get; init; }
    public required IReadOnlyList<PreparedValue> Values { get; init; }
}

/// <summary>A validated UPDATE command targeting exactly one row.</summary>
public sealed record PreparedUpdate
{
    public required ObjectMetadata Metadata { get; init; }
    public required IReadOnlyList<PreparedValue> Key { get; init; }
    public required IReadOnlyList<PreparedValue> Changes { get; init; }

    /// <summary>The original rowversion (decoded bytes), when applicable.</summary>
    public byte[]? ConcurrencyToken { get; init; }

    /// <summary>Original values for non-rowversion concurrency checks.</summary>
    public IReadOnlyList<PreparedValue> OriginalValues { get; init; } = [];
}

/// <summary>A validated DELETE command targeting exactly one row.</summary>
public sealed record PreparedDelete
{
    public required ObjectMetadata Metadata { get; init; }
    public required IReadOnlyList<PreparedValue> Key { get; init; }
    public byte[]? ConcurrencyToken { get; init; }
}
