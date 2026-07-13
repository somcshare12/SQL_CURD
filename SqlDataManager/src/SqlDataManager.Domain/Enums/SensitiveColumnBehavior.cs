namespace SqlDataManager.Domain.Enums;

/// <summary>
/// How a column classified as "sensitive" (for example a national id or a
/// password hash) should be treated by the module.
/// </summary>
public enum SensitiveColumnBehavior
{
    /// <summary>Shown normally (default).</summary>
    Visible = 0,

    /// <summary>Value is partially masked (for example <c>****1234</c>).</summary>
    Masked = 1,

    /// <summary>Value is never returned to the client at all.</summary>
    Hidden = 2,

    /// <summary>Value is shown but cannot be edited.</summary>
    ReadOnly = 3
}
