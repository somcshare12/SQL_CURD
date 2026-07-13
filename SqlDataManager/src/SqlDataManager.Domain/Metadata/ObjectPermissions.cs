namespace SqlDataManager.Domain.Metadata;

/// <summary>
/// The effective CRUD permissions for a single database object, as resolved by
/// the backend access rules. The backend is always the source of truth; the
/// frontend merely hides actions the user cannot perform.
/// </summary>
public sealed record ObjectPermissions
{
    public required bool CanRead { get; init; }
    public required bool CanCreate { get; init; }
    public required bool CanUpdate { get; init; }
    public required bool CanDelete { get; init; }

    /// <summary>A read-only object with no mutation capability.</summary>
    public static ObjectPermissions ReadOnly { get; } = new()
    {
        CanRead = true,
        CanCreate = false,
        CanUpdate = false,
        CanDelete = false
    };

    /// <summary>No access at all.</summary>
    public static ObjectPermissions None { get; } = new()
    {
        CanRead = false,
        CanCreate = false,
        CanUpdate = false,
        CanDelete = false
    };
}
