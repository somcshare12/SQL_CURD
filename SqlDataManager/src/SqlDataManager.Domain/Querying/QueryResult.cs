namespace SqlDataManager.Domain.Querying;

/// <summary>
/// The result of a paged query. Rows are represented as simple ordered
/// dictionaries (column name -> value) so the layer boundaries stay free of any
/// SQL-client specific types.
/// </summary>
public sealed record QueryResult
{
    public required IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }

    /// <summary>Total matching rows, when a count was requested/available.</summary>
    public long? TotalCount { get; init; }

    /// <summary>The column names present in each returned row, in order.</summary>
    public required IReadOnlyList<string> Columns { get; init; }

    /// <summary>
    /// True when the module could not guarantee a stable ordering (no key and
    /// no explicit sort), meaning page contents may shift between requests.
    /// </summary>
    public bool UnstableOrdering { get; init; }
}
