namespace SqlDataManager.Domain.Enums;

/// <summary>
/// Identifies whether a discovered database object is a base table or a view.
/// Views are treated as read-only by default (see the access rules), while
/// tables may support the full set of CRUD operations.
/// </summary>
public enum DatabaseObjectType
{
    /// <summary>A base table (may allow create/update/delete).</summary>
    Table = 0,

    /// <summary>A view (read-only by default).</summary>
    View = 1
}
