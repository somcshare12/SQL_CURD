namespace SqlDataManager.Domain.Enums;

/// <summary>
/// The complete closed set of filter operators the backend understands.
///
/// The frontend maps a user's chosen operator to one of these enum values, and
/// the SQL query builder maps each enum value to a fixed, safe SQL fragment
/// (for example <c>LIKE @p0</c>). Because the set is closed and every value is
/// translated by trusted server code, the client can never inject a raw
/// predicate.
/// </summary>
public enum FilterOperator
{
    // ---- Text operators ----
    Contains,
    DoesNotContain,
    StartsWith,
    EndsWith,

    // ---- Shared equality operators (text / numeric / identifier / boolean) ----
    Equals,
    DoesNotEqual,

    // ---- Numeric & date/time comparison operators ----
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Between,

    // ---- Emptiness / null operators ----
    IsEmpty,
    IsNotEmpty,
    IsNull,
    IsNotNull,

    // ---- Boolean-only operators ----
    IsTrue,
    IsFalse
}
