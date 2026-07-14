namespace SqlDataManager.Contracts;

// ---------------------------------------------------------------------------
// Wire DTOs. These are intentionally simple, JSON-friendly records. Enum-like
// values (filter operators, sort direction, object type) are represented as
// strings on the wire so the API stays version-tolerant and easy to consume
// from TypeScript. The API layer maps these strings to the strongly-typed
// Domain enums, validating them in the process.
// ---------------------------------------------------------------------------

/// <summary>The standard error response returned for every failed request.</summary>
public sealed record ApiErrorResponse
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public string? TraceId { get; init; }
    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; init; }
}

/// <summary>Effective CRUD permissions on the wire.</summary>
public sealed record PermissionsDto
{
    public required bool CanRead { get; init; }
    public required bool CanCreate { get; init; }
    public required bool CanUpdate { get; init; }
    public required bool CanDelete { get; init; }
}

/// <summary>Module information returned by the <c>/info</c> endpoint.</summary>
public sealed record ModuleInfoDto
{
    public required bool Enabled { get; init; }
    public required string DisplayName { get; init; }
    public required string DatabaseDisplayName { get; init; }
    public required int MaximumPageSize { get; init; }
    public required int DefaultPageSize { get; init; }
    public required IReadOnlyList<int> AllowedPageSizes { get; init; }
    public required IReadOnlyList<string> SupportedFeatures { get; init; }
    public required bool RequireAuthentication { get; init; }
}

/// <summary>A tree node (table or view) returned by <c>/objects</c>.</summary>
public sealed record DatabaseObjectDto
{
    public required string Schema { get; init; }
    public required string Name { get; init; }
    public required string ObjectType { get; init; }
    public required string FullName { get; init; }
    public required PermissionsDto Permissions { get; init; }
}

/// <summary>Full column metadata for form generation and grid rendering.</summary>
public sealed record ColumnMetadataDto
{
    public required string Name { get; init; }
    public required int OrdinalPosition { get; init; }
    public required string SqlType { get; init; }
    public required string ClrType { get; init; }
    public required bool IsNullable { get; init; }
    public int? MaxLength { get; init; }
    public int? NumericPrecision { get; init; }
    public int? NumericScale { get; init; }
    public required bool HasDefault { get; init; }
    public required bool IsIdentity { get; init; }
    public required bool IsComputed { get; init; }
    public required bool IsPrimaryKey { get; init; }
    public required bool IsRowVersion { get; init; }
    public required bool IsInsertable { get; init; }
    public required bool IsUpdatable { get; init; }
    public required bool IsFilterable { get; init; }
    public required bool IsSortable { get; init; }
    public required bool IsSearchable { get; init; }
    public required string Sensitivity { get; init; }
    public string? DisplayName { get; init; }
}

/// <summary>Complete object metadata response.</summary>
public sealed record ObjectMetadataDto
{
    public required string Schema { get; init; }
    public required string Name { get; init; }
    public required string ObjectType { get; init; }
    public required string FullName { get; init; }
    public required PermissionsDto Permissions { get; init; }
    public required IReadOnlyList<ColumnMetadataDto> Columns { get; init; }
    public required IReadOnlyList<string> KeyColumns { get; init; }
    public string? RowVersionColumn { get; init; }
    public required bool HasStableKey { get; init; }
}

// ---- Query -----------------------------------------------------------------

public sealed record SortDto
{
    public required string Column { get; init; }
    public string Direction { get; init; } = "asc";
}

public sealed record FilterDto
{
    public required string Column { get; init; }
    public required string Operator { get; init; }
    public object? Value { get; init; }
    public object? SecondValue { get; init; }
}

public sealed record QueryRowsRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 100;
    public IReadOnlyList<string> SelectedColumns { get; init; } = [];
    public IReadOnlyList<SortDto> Sort { get; init; } = [];
    public IReadOnlyList<FilterDto> Filters { get; init; } = [];
    public string? GlobalSearch { get; init; }
    public bool IncludeTotalCount { get; init; }
}

public sealed record QueryRowsResponse
{
    public required IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public long? TotalCount { get; init; }
    public required IReadOnlyList<string> Columns { get; init; }
    public bool UnstableOrdering { get; init; }
}

// ---- Mutations -------------------------------------------------------------

public sealed record RecordKeyValueDto
{
    public required string Column { get; init; }
    public object? Value { get; init; }
}

public sealed record FieldValueDto
{
    public required string Column { get; init; }
    public object? Value { get; init; }
}

public sealed record ReadRecordRequest
{
    public required IReadOnlyList<RecordKeyValueDto> Keys { get; init; }
}

public sealed record RecordDetailsResponse
{
    public required IReadOnlyDictionary<string, object?> Record { get; init; }
}

public sealed record CreateRecordRequestDto
{
    public required IReadOnlyList<FieldValueDto> Fields { get; init; }
}

public sealed record UpdateRecordRequestDto
{
    public required IReadOnlyList<RecordKeyValueDto> Keys { get; init; }
    public required IReadOnlyList<FieldValueDto> ChangedFields { get; init; }
    public string? ConcurrencyToken { get; init; }
    public IReadOnlyList<FieldValueDto> OriginalValues { get; init; } = [];
}

public sealed record DeleteRecordRequestDto
{
    public required IReadOnlyList<RecordKeyValueDto> Keys { get; init; }
    public string? ConcurrencyToken { get; init; }
}

public sealed record RecordMutationResultDto
{
    public required bool Success { get; init; }
    public required int AffectedRows { get; init; }
    public IReadOnlyDictionary<string, object?>? GeneratedKeys { get; init; }
    public IReadOnlyDictionary<string, object?>? Record { get; init; }
}

// ---- Settings --------------------------------------------------------------

/// <summary>
/// Per-user layout preferences. The shape is intentionally loose (a dictionary
/// keyed by object full name) because the frontend owns the exact contents of
/// the per-object layout; the backend simply persists it verbatim.
/// </summary>
public sealed record UserSettingsDto
{
    public string? LastSelectedObject { get; init; }
    public bool TreeCollapsed { get; init; }
    public double? TreePanelWidth { get; init; }
    public IReadOnlyList<string> ExpandedTreeNodes { get; init; } = [];
    public string? Theme { get; init; }
    public IReadOnlyList<string> RecentObjects { get; init; } = [];

    /// <summary>Per-object grid layout, keyed by fully-qualified object name.</summary>
    public IReadOnlyDictionary<string, ObjectLayoutDto> ObjectLayouts { get; init; }
        = new Dictionary<string, ObjectLayoutDto>();
}

public sealed record ObjectLayoutDto
{
    public int? PageSize { get; init; }
    public IReadOnlyList<string> HiddenColumns { get; init; } = [];
    public IReadOnlyList<string> ColumnOrder { get; init; } = [];
    public IReadOnlyDictionary<string, double> ColumnWidths { get; init; }
        = new Dictionary<string, double>();
    public IReadOnlyList<SortDto> Sort { get; init; } = [];
    public string? Density { get; init; }
}
