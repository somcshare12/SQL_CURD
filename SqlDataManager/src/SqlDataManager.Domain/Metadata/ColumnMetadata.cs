using SqlDataManager.Domain.Enums;

namespace SqlDataManager.Domain.Metadata;

/// <summary>
/// Describes a single column of a table or view exactly as discovered from SQL
/// Server system metadata. Every capability the module needs in order to build
/// safe SQL and to generate data-entry forms is captured here.
///
/// This is an immutable record: metadata is read once (and cached) and never
/// mutated afterwards.
/// </summary>
public sealed record ColumnMetadata
{
    /// <summary>The column name exactly as stored in SQL Server.</summary>
    public required string Name { get; init; }

    /// <summary>1-based ordinal position of the column within the object.</summary>
    public required int OrdinalPosition { get; init; }

    /// <summary>The raw SQL Server type name, for example <c>nvarchar</c>.</summary>
    public required string SqlType { get; init; }

    /// <summary>
    /// The CLR type that most naturally represents this column's values, for
    /// example <see cref="string"/>, <see cref="int"/> or <see cref="Guid"/>.
    /// Used to drive both validation and form control selection.
    /// </summary>
    public required Type ClrType { get; init; }

    /// <summary>Whether the column accepts <c>NULL</c>.</summary>
    public required bool IsNullable { get; init; }

    /// <summary>Maximum length for character/binary types (-1 = MAX).</summary>
    public int? MaxLength { get; init; }

    /// <summary>Numeric precision for decimal/numeric types.</summary>
    public int? NumericPrecision { get; init; }

    /// <summary>Numeric scale for decimal/numeric types.</summary>
    public int? NumericScale { get; init; }

    /// <summary>Whether the column has a database default constraint.</summary>
    public bool HasDefault { get; init; }

    /// <summary>The default constraint definition, when available.</summary>
    public string? DefaultDefinition { get; init; }

    /// <summary>Whether the column is an IDENTITY column.</summary>
    public bool IsIdentity { get; init; }

    /// <summary>Whether the column is computed (never insertable/updatable).</summary>
    public bool IsComputed { get; init; }

    /// <summary>Whether the column participates in the primary key.</summary>
    public bool IsPrimaryKey { get; init; }

    /// <summary>Whether the column is a <c>rowversion</c>/<c>timestamp</c>.</summary>
    public bool IsRowVersion { get; init; }

    /// <summary>Collation name for character columns, when applicable.</summary>
    public string? Collation { get; init; }

    /// <summary>
    /// The classification (if any) configured for this column. Drives masking,
    /// hiding and exclusion from confirmation dialogs / logs.
    /// </summary>
    public SensitiveColumnBehavior Sensitivity { get; init; } = SensitiveColumnBehavior.Visible;

    // ---- Derived capabilities ----------------------------------------------
    // These express, in one place, whether the module may include the column in
    // a particular operation. The metadata reader computes them from the raw
    // facts above so the rest of the codebase can rely on simple booleans.

    /// <summary>
    /// True when a value for this column may be supplied on INSERT. Computed and
    /// row-version columns are never insertable; identity columns are only
    /// insertable when identity-insert is explicitly enabled.
    /// </summary>
    public bool IsInsertable { get; init; } = true;

    /// <summary>
    /// True when this column may be changed on UPDATE. Computed, row-version,
    /// identity and (by default) primary-key columns are not updatable.
    /// </summary>
    public bool IsUpdatable { get; init; } = true;

    /// <summary>Whether the column may appear in a filter predicate.</summary>
    public bool IsFilterable { get; init; } = true;

    /// <summary>Whether the column may be used for sorting.</summary>
    public bool IsSortable { get; init; } = true;

    /// <summary>Whether the column participates in global text search.</summary>
    public bool IsSearchable { get; init; } = true;

    /// <summary>Optional friendly display name for the UI.</summary>
    public string? DisplayName { get; init; }
}
