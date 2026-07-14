using SqlDataManager.Domain.Enums;

namespace SqlDataManager.Domain.Querying;

/// <summary>
/// A single column filter. Note that <see cref="Column"/> is validated against
/// trusted metadata before use, and <see cref="Value"/>/<see cref="SecondValue"/>
/// are always emitted as SQL parameters — never concatenated into SQL text.
/// </summary>
public sealed record FilterDefinition
{
    public required string Column { get; init; }
    public required FilterOperator Operator { get; init; }

    /// <summary>Primary comparison value (may be null for unary operators).</summary>
    public object? Value { get; init; }

    /// <summary>Second value, used only by the <c>Between</c> operator.</summary>
    public object? SecondValue { get; init; }
}
