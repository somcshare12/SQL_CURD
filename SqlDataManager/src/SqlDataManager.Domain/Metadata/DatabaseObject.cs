using SqlDataManager.Domain.Enums;

namespace SqlDataManager.Domain.Metadata;

/// <summary>
/// A lightweight descriptor for a table or view that is permitted for the
/// current user. This is what populates the left navigation tree. Full column
/// metadata is loaded separately (and lazily) via <see cref="ObjectMetadata"/>.
/// </summary>
public sealed record DatabaseObject
{
    public required string Schema { get; init; }
    public required string Name { get; init; }
    public required DatabaseObjectType ObjectType { get; init; }

    /// <summary>
    /// The fully-qualified, unquoted name (for example <c>dbo.Customers</c>).
    /// Handy as a stable identifier in configuration and the UI.
    /// </summary>
    public string FullName => $"{Schema}.{Name}";

    /// <summary>Effective CRUD permissions for this object.</summary>
    public required ObjectPermissions Permissions { get; init; }
}
