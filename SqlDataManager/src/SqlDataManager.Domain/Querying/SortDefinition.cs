using SqlDataManager.Domain.Enums;

namespace SqlDataManager.Domain.Querying;

/// <summary>
/// A single ORDER BY component. The column is validated against trusted
/// metadata and the direction is a closed enum, so no arbitrary sort expression
/// can ever reach SQL Server.
/// </summary>
public sealed record SortDefinition
{
    public required string Column { get; init; }
    public required SortDirection Direction { get; init; }
}
