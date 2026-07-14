using Microsoft.Extensions.Options;
using SqlDataManager.Application.Abstractions;
using SqlDataManager.Application.Configuration;
using SqlDataManager.Domain.Enums;
using SqlDataManager.Domain.Exceptions;
using SqlDataManager.Domain.Metadata;

namespace SqlDataManager.Application.Services;

/// <summary>
/// Exposes the database catalog to the rest of the module with access rules and
/// sensitive-column configuration already applied. This is the *only* place the
/// upper layers should obtain metadata from, so security decisions are made
/// consistently in one spot.
/// </summary>
public sealed class MetadataService(
    IMetadataCatalog catalog,
    ObjectAccessService accessService,
    IOptions<SqlDataManagerOptions> options)
{
    private readonly SqlDataManagerOptions _options = options.Value;

    /// <summary>
    /// Returns every object the current user may at least read, each carrying
    /// its effective permissions. Denied objects are filtered out entirely.
    /// </summary>
    public async Task<IReadOnlyList<DatabaseObject>> GetVisibleObjectsAsync(CancellationToken cancellationToken)
    {
        var all = await catalog.GetAllObjectsAsync(cancellationToken);
        var visible = new List<DatabaseObject>();

        foreach (var metadata in all)
        {
            var permissions = accessService.ResolvePermissions(metadata);
            if (!permissions.CanRead)
            {
                continue;
            }

            visible.Add(new DatabaseObject
            {
                Schema = metadata.Schema,
                Name = metadata.Name,
                ObjectType = metadata.ObjectType,
                Permissions = permissions
            });
        }

        return visible
            .OrderBy(o => o.ObjectType)
            .ThenBy(o => o.Schema, StringComparer.OrdinalIgnoreCase)
            .ThenBy(o => o.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Returns full metadata for one object with effective permissions and
    /// sensitive-column behaviour applied. Throws when the object does not exist
    /// or is not accessible (both look the same to avoid leaking existence).
    /// </summary>
    public async Task<ObjectMetadata> GetAccessibleMetadataAsync(
        string schema, string name, CancellationToken cancellationToken)
    {
        var metadata = await catalog.GetObjectAsync(schema, name, cancellationToken)
            ?? throw new ObjectNotAllowedException(schema, name);

        var permissions = accessService.ResolvePermissions(metadata);
        if (!permissions.CanRead)
        {
            // Not readable == not accessible, indistinguishable from "not found".
            throw new ObjectNotAllowedException(schema, name);
        }

        var columns = ApplySensitivity(metadata.FullName, metadata.Columns);

        return metadata with
        {
            Permissions = permissions,
            Columns = columns
        };
    }

    /// <summary>
    /// Applies the configured sensitive-column behaviour: hidden columns are
    /// removed from search, masked columns are tagged, etc. The actual masking
    /// of returned values happens in the row-reading layer using these tags.
    /// </summary>
    private IReadOnlyList<ColumnMetadata> ApplySensitivity(
        string fullName, IReadOnlyList<ColumnMetadata> columns)
    {
        if (!_options.ObjectSensitivity.TryGetValue(fullName, out var sensitivity)
            || sensitivity.SensitiveColumns.Count == 0)
        {
            return columns;
        }

        return columns
            .Select(column =>
            {
                if (!sensitivity.SensitiveColumns.TryGetValue(column.Name, out var raw))
                {
                    return column;
                }

                var behavior = Enum.TryParse<SensitiveColumnBehavior>(raw, ignoreCase: true, out var parsed)
                    ? parsed
                    : SensitiveColumnBehavior.Masked;

                return column with
                {
                    Sensitivity = behavior,
                    // Hidden values must never be searched; read-only/hidden
                    // values must never be edited.
                    IsSearchable = column.IsSearchable && behavior != SensitiveColumnBehavior.Hidden,
                    IsUpdatable = column.IsUpdatable &&
                                  behavior is not (SensitiveColumnBehavior.Hidden or SensitiveColumnBehavior.ReadOnly)
                };
            })
            .ToList();
    }
}
