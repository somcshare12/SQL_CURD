namespace SqlDataManager.Domain.Enums;

/// <summary>
/// Controls how (and whether) the backend computes the total number of rows for
/// a query. Counting every row of a very large table is expensive, so the
/// module lets the caller/configuration choose a strategy.
/// </summary>
public enum RowCountMode
{
    /// <summary>Run an exact <c>COUNT_BIG(*)</c> for the filtered set.</summary>
    Exact = 0,

    /// <summary>Use SQL Server statistics for a fast approximate count.</summary>
    Estimated = 1,

    /// <summary>Only count when the client explicitly asks for it.</summary>
    OnDemand = 2,

    /// <summary>Never compute a total count.</summary>
    Disabled = 3
}
