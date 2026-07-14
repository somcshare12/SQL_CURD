using SqlDataManager.Domain.Enums;

namespace SqlDataManager.Domain.Metadata;

/// <summary>
/// The complete metadata for a single table or view: its identity, permissions,
/// full column list and the information required to safely identify a single
/// row for update/delete operations.
/// </summary>
public sealed record ObjectMetadata
{
    public required string Schema { get; init; }
    public required string Name { get; init; }
    public required DatabaseObjectType ObjectType { get; init; }
    public string FullName => $"{Schema}.{Name}";

    public required ObjectPermissions Permissions { get; init; }

    /// <summary>All columns in ordinal order.</summary>
    public required IReadOnlyList<ColumnMetadata> Columns { get; init; }

    /// <summary>
    /// The ordered list of column names that form the stable unique key used to
    /// address a single row (primary key, else a non-null unique constraint or
    /// index). Empty when the object has no reliable key, in which case update
    /// and delete are disabled.
    /// </summary>
    public required IReadOnlyList<string> KeyColumns { get; init; }

    /// <summary>The rowversion column name, if the table has one.</summary>
    public string? RowVersionColumn { get; init; }

    /// <summary>True when a stable unique key exists (enables update/delete).</summary>
    public bool HasStableKey => KeyColumns.Count > 0;

    /// <summary>Convenience lookup of a column by (case-insensitive) name.</summary>
    public ColumnMetadata? FindColumn(string columnName) =>
        Columns.FirstOrDefault(c =>
            string.Equals(c.Name, columnName, StringComparison.OrdinalIgnoreCase));
}
