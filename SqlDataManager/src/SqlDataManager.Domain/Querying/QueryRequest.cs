namespace SqlDataManager.Domain.Querying;

/// <summary>
/// A fully-structured, validated request to read a page of rows. There is no
/// place in this object for raw SQL: paging, sorting and filtering are all
/// expressed as structured, closed-vocabulary values.
/// </summary>
public sealed record QueryRequest
{
    /// <summary>1-based page number.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Requested page size (clamped to the configured maximum).</summary>
    public int PageSize { get; init; } = 100;

    /// <summary>
    /// Optional explicit list of columns to return. When empty, all permitted
    /// (non-hidden) columns are returned.
    /// </summary>
    public IReadOnlyList<string> SelectedColumns { get; init; } = [];

    public IReadOnlyList<SortDefinition> Sort { get; init; } = [];
    public IReadOnlyList<FilterDefinition> Filters { get; init; } = [];

    /// <summary>Optional free-text search across searchable text columns.</summary>
    public string? GlobalSearch { get; init; }

    /// <summary>Whether to compute the total row count for this query.</summary>
    public bool IncludeTotalCount { get; init; }
}
