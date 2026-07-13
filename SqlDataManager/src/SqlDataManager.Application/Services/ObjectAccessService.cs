using Microsoft.Extensions.Options;
using SqlDataManager.Application.Abstractions;
using SqlDataManager.Application.Configuration;
using SqlDataManager.Domain.Enums;
using SqlDataManager.Domain.Metadata;

namespace SqlDataManager.Application.Services;

/// <summary>
/// Central authority for "can this object be seen / operated on?". It applies
/// the access rules in the exact precedence required by the specification:
///
///   1. Module enabled status
///   2. Allowed / denied schemas        (deny wins)
///   3. Allowed / denied objects        (deny wins)
///   4. Per-object permission overrides
///   5. SQL Server structural capability
///   6. Current-user authorization
///   7. (operation-specific validation happens later, in the use-case services)
///
/// The result is a single <see cref="ObjectPermissions"/> value that the rest
/// of the module trusts. Denied objects resolve to <see cref="ObjectPermissions.None"/>
/// which keeps them out of the tree AND blocks direct API access.
/// </summary>
public sealed class ObjectAccessService(
    IOptions<SqlDataManagerOptions> options,
    ICurrentUser currentUser)
{
    private readonly SqlDataManagerOptions _options = options.Value;

    /// <summary>
    /// True when the object passes the schema/object allow-deny gates and is at
    /// least readable. Used to decide tree visibility.
    /// </summary>
    public bool IsVisible(ObjectMetadata metadata) =>
        ResolvePermissions(metadata).CanRead;

    /// <summary>
    /// Resolves the effective permissions for the current user against a single
    /// object, combining configuration, structural capability and authorization.
    /// </summary>
    public ObjectPermissions ResolvePermissions(ObjectMetadata metadata)
    {
        // Rule 1: the whole module can be switched off.
        if (!_options.Enabled)
        {
            return ObjectPermissions.None;
        }

        // Rules 2 & 3: schema and object allow/deny gates. Deny always wins.
        if (!PassesSchemaGate(metadata.Schema) || !PassesObjectGate(metadata.FullName))
        {
            return ObjectPermissions.None;
        }

        // Rule 4: determine what the configuration *wants* to allow, starting
        // from the per-object-type default and then applying any explicit
        // per-object override.
        var access = _options.ObjectAccess;
        var isTable = metadata.ObjectType == DatabaseObjectType.Table;
        var defaultAccess = isTable ? access.DefaultTableAccess : access.DefaultViewAccess;
        var readWriteByDefault = string.Equals(defaultAccess, "ReadWrite", StringComparison.OrdinalIgnoreCase);

        bool wantRead = true;
        bool wantCreate = readWriteByDefault;
        bool wantUpdate = readWriteByDefault;
        bool wantDelete = readWriteByDefault;

        if (_options.ObjectPermissions.TryGetValue(metadata.FullName, out var perObject))
        {
            wantRead = perObject.Read;
            wantCreate = perObject.Create;
            wantUpdate = perObject.Update;
            wantDelete = perObject.Delete;
        }

        // Rule 5: intersect with what the object structurally supports. Views
        // and keyless tables can never be updated or deleted, regardless of
        // configuration.
        var capability = metadata.Permissions;

        // Global CRUD master switches also gate every write.
        var crud = _options.Crud;

        bool canRead = wantRead && capability.CanRead;
        bool canCreate = wantCreate && capability.CanCreate && crud.EnableCreate;
        bool canUpdate = wantUpdate && capability.CanUpdate && crud.EnableUpdate;
        bool canDelete = wantDelete && capability.CanDelete && crud.EnableDelete;

        // Rule 6: current-user authorization, only enforced when the module is
        // configured to require authentication.
        if (_options.Security.RequireAuthentication)
        {
            canRead &= currentUser.IsAuthorized(_options.Security.ReadPolicy);
            canCreate &= currentUser.IsAuthorized(_options.Security.CreatePolicy);
            canUpdate &= currentUser.IsAuthorized(_options.Security.UpdatePolicy);
            canDelete &= currentUser.IsAuthorized(_options.Security.DeletePolicy);
        }

        return new ObjectPermissions
        {
            CanRead = canRead,
            CanCreate = canCreate,
            CanUpdate = canUpdate,
            CanDelete = canDelete
        };
    }

    private bool PassesSchemaGate(string schema)
    {
        var access = _options.ObjectAccess;

        // Deny list wins outright.
        if (access.DeniedSchemas.Any(s => string.Equals(s, schema, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        // If an allow-list is configured, the schema must be on it.
        if (access.AllowedSchemas.Count > 0 &&
            !access.AllowedSchemas.Any(s => string.Equals(s, schema, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return true;
    }

    private bool PassesObjectGate(string fullName)
    {
        var access = _options.ObjectAccess;

        if (access.DeniedObjects.Any(o => string.Equals(o, fullName, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (access.AllowedObjects.Count > 0 &&
            !access.AllowedObjects.Any(o => string.Equals(o, fullName, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return true;
    }
}
