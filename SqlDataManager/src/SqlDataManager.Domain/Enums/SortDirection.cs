namespace SqlDataManager.Domain.Enums;

/// <summary>
/// The only two sort directions the module will ever emit into a SQL
/// <c>ORDER BY</c> clause. Restricting sorting to this closed enum (rather than
/// accepting arbitrary strings) is a core safety rule: the client can never
/// inject an arbitrary sort expression.
/// </summary>
public enum SortDirection
{
    Ascending = 0,
    Descending = 1
}
