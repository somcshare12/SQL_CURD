using Microsoft.Data.SqlClient;
using SqlDataManager.Domain.Enums;
using SqlDataManager.Domain.Metadata;
using SqlDataManager.Infrastructure.Sql;

namespace SqlDataManager.Infrastructure.Metadata;

/// <summary>
/// Reads structural metadata for every table and view directly from SQL Server
/// system catalog views (<c>sys.objects</c>, <c>sys.columns</c>,
/// <c>sys.indexes</c>, ...). The result is a fully-populated
/// <see cref="ObjectMetadata"/> per object, including the chosen stable key and
/// the derived per-column capabilities (insertable/updatable/sortable/...).
///
/// The permissions on the returned metadata express only *structural
/// capability*; the Application access rules refine them per user.
/// </summary>
public sealed class SqlMetadataReader(SqlConnectionFactory connectionFactory)
{
    public async Task<IReadOnlyList<ObjectMetadata>> ReadAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var objects = await ReadObjectsAsync(connection, cancellationToken);
        var columnsByObject = await ReadColumnsAsync(connection, cancellationToken);
        var keysByObject = await ReadStableKeysAsync(connection, cancellationToken);

        var result = new List<ObjectMetadata>();

        foreach (var (objectId, info) in objects)
        {
            if (!columnsByObject.TryGetValue(objectId, out var columns) || columns.Count == 0)
            {
                continue;
            }

            columns = columns.OrderBy(c => c.OrdinalPosition).ToList();

            var keyColumns = keysByObject.TryGetValue(objectId, out var keys) ? keys : [];
            var isTable = info.Type == DatabaseObjectType.Table;

            // Mark primary-key membership and finalise per-column capabilities
            // now that we know the object's key and type.
            var finalColumns = columns
                .Select(c => FinaliseColumn(c, keyColumns, isTable))
                .ToList();

            var rowVersionColumn = finalColumns.FirstOrDefault(c => c.IsRowVersion)?.Name;

            var hasStableKey = keyColumns.Count > 0;
            var hasInsertable = finalColumns.Any(c => c.IsInsertable);
            var hasUpdatable = finalColumns.Any(c => c.IsUpdatable);

            var capability = new ObjectPermissions
            {
                CanRead = true,
                CanCreate = isTable && hasInsertable,
                CanUpdate = isTable && hasStableKey && hasUpdatable,
                CanDelete = isTable && hasStableKey
            };

            result.Add(new ObjectMetadata
            {
                Schema = info.Schema,
                Name = info.Name,
                ObjectType = info.Type,
                Permissions = capability,
                Columns = finalColumns,
                KeyColumns = keyColumns,
                RowVersionColumn = rowVersionColumn
            });
        }

        return result;
    }

    // ---- object list -------------------------------------------------------

    private static async Task<Dictionary<int, (string Schema, string Name, DatabaseObjectType Type)>>
        ReadObjectsAsync(SqlConnection connection, CancellationToken ct)
    {
        const string sql = """
            SELECT o.object_id, s.name AS schema_name, o.name AS object_name, o.type
            FROM sys.objects o
            INNER JOIN sys.schemas s ON s.schema_id = o.schema_id
            WHERE o.type IN ('U', 'V')
            ORDER BY s.name, o.name;
            """;

        var map = new Dictionary<int, (string, string, DatabaseObjectType)>();
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var objectId = reader.GetInt32(0);
            var schema = reader.GetString(1);
            var name = reader.GetString(2);
            var type = reader.GetString(3).Trim() == "V" ? DatabaseObjectType.View : DatabaseObjectType.Table;
            map[objectId] = (schema, name, type);
        }

        return map;
    }

    // ---- columns -----------------------------------------------------------

    private static async Task<Dictionary<int, List<ColumnMetadata>>> ReadColumnsAsync(
        SqlConnection connection, CancellationToken ct)
    {
        // system_type_id 189 == timestamp/rowversion.
        const string sql = """
            SELECT
                c.object_id,
                c.name AS column_name,
                c.column_id,
                t.name AS type_name,
                c.is_nullable,
                c.max_length,
                c.precision,
                c.scale,
                c.is_identity,
                c.is_computed,
                c.collation_name,
                CASE WHEN c.default_object_id <> 0 THEN 1 ELSE 0 END AS has_default,
                CASE WHEN c.system_type_id = 189 THEN 1 ELSE 0 END AS is_rowversion
            FROM sys.columns c
            INNER JOIN sys.types t ON t.user_type_id = c.user_type_id
            INNER JOIN sys.objects o ON o.object_id = c.object_id
            WHERE o.type IN ('U', 'V')
            ORDER BY c.object_id, c.column_id;
            """;

        var map = new Dictionary<int, List<ColumnMetadata>>();
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var objectId = reader.GetInt32(0);
            var typeName = reader.GetString(3);
            var clrType = SqlTypeMapper.ToClrType(typeName);
            var isComputed = reader.GetBoolean(9);
            var isIdentity = reader.GetBoolean(8);
            var isRowVersion = reader.GetInt32(12) == 1;
            var isBinary = SqlTypeMapper.IsBinaryType(typeName);
            var isText = SqlTypeMapper.IsTextType(typeName);

            var column = new ColumnMetadata
            {
                Name = reader.GetString(1),
                OrdinalPosition = reader.GetInt32(2),
                SqlType = typeName,
                ClrType = clrType,
                IsNullable = reader.GetBoolean(4),
                MaxLength = NormaliseMaxLength(typeName, reader.GetInt16(5)),
                NumericPrecision = reader.GetByte(6),
                NumericScale = reader.GetByte(7),
                IsIdentity = isIdentity,
                IsComputed = isComputed,
                IsRowVersion = isRowVersion,
                HasDefault = reader.GetInt32(11) == 1,
                Collation = reader.IsDBNull(10) ? null : reader.GetString(10),
                // Filtering/sorting on binary payloads is meaningless.
                IsFilterable = !isBinary,
                IsSortable = !isBinary,
                IsSearchable = isText
            };

            if (!map.TryGetValue(objectId, out var list))
            {
                list = [];
                map[objectId] = list;
            }

            list.Add(column);
        }

        return map;
    }

    /// <summary>
    /// SQL Server stores nvarchar/nchar max_length in bytes; convert to
    /// characters. -1 means MAX for var*(max) types.
    /// </summary>
    private static int? NormaliseMaxLength(string typeName, short maxLength)
    {
        if (maxLength == -1)
        {
            return -1;
        }

        var lowered = typeName.ToLowerInvariant();
        if (lowered is "nvarchar" or "nchar")
        {
            return maxLength / 2;
        }

        return maxLength;
    }

    // ---- stable key selection (§17) ----------------------------------------

    private static async Task<Dictionary<int, IReadOnlyList<string>>> ReadStableKeysAsync(
        SqlConnection connection, CancellationToken ct)
    {
        // Read every unique index/constraint together with whether all of its
        // key columns are non-nullable. We then pick, per object, the best key
        // using the required priority: primary key, then a non-null unique
        // constraint, then a non-null unique index.
        const string sql = """
            SELECT
                i.object_id,
                i.index_id,
                i.is_primary_key,
                i.is_unique_constraint,
                i.is_unique,
                ic.key_ordinal,
                c.name AS column_name,
                c.is_nullable
            FROM sys.indexes i
            INNER JOIN sys.index_columns ic
                ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            INNER JOIN sys.columns c
                ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            INNER JOIN sys.objects o ON o.object_id = i.object_id
            WHERE o.type = 'U'
              AND i.is_unique = 1
              AND ic.is_included_column = 0
            ORDER BY i.object_id, i.index_id, ic.key_ordinal;
            """;

        // Gather candidate indexes grouped by (object, index).
        var candidates = new Dictionary<(int ObjectId, int IndexId), IndexCandidate>();

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var objectId = reader.GetInt32(0);
            var indexId = reader.GetInt32(1);
            var isPrimaryKey = reader.GetBoolean(2);
            var isUniqueConstraint = reader.GetBoolean(3);
            var columnName = reader.GetString(6);
            var isNullable = reader.GetBoolean(7);

            var mapKey = (objectId, indexId);
            if (!candidates.TryGetValue(mapKey, out var candidate))
            {
                candidate = new IndexCandidate(objectId, isPrimaryKey, isUniqueConstraint);
                candidates[mapKey] = candidate;
            }

            candidate.Columns.Add(columnName);
            if (isNullable)
            {
                candidate.HasNullableColumn = true;
            }
        }

        // Pick the best candidate per object.
        var result = new Dictionary<int, IReadOnlyList<string>>();
        foreach (var group in candidates.Values.GroupBy(c => c.ObjectId))
        {
            var best = SelectBestKey(group);
            if (best is not null)
            {
                result[group.Key] = best.Columns;
            }
        }

        return result;
    }

    private static IndexCandidate? SelectBestKey(IEnumerable<IndexCandidate> candidates)
    {
        var list = candidates.ToList();

        // 1) Primary key always wins.
        var pk = list.FirstOrDefault(c => c.IsPrimaryKey);
        if (pk is not null)
        {
            return pk;
        }

        // 2) Non-null unique constraint, fewest columns for determinism.
        var uniqueConstraint = list
            .Where(c => c.IsUniqueConstraint && !c.HasNullableColumn)
            .OrderBy(c => c.Columns.Count)
            .FirstOrDefault();
        if (uniqueConstraint is not null)
        {
            return uniqueConstraint;
        }

        // 3) Non-null unique index.
        return list
            .Where(c => !c.HasNullableColumn)
            .OrderBy(c => c.Columns.Count)
            .FirstOrDefault();
    }

    private sealed class IndexCandidate(int objectId, bool isPrimaryKey, bool isUniqueConstraint)
    {
        public int ObjectId { get; } = objectId;
        public bool IsPrimaryKey { get; } = isPrimaryKey;
        public bool IsUniqueConstraint { get; } = isUniqueConstraint;
        public bool HasNullableColumn { get; set; }
        public List<string> Columns { get; } = [];
    }

    // ---- per-column capability finalisation --------------------------------

    private static ColumnMetadata FinaliseColumn(
        ColumnMetadata column, IReadOnlyList<string> keyColumns, bool isTable)
    {
        var isPrimaryKey = keyColumns.Contains(column.Name, StringComparer.OrdinalIgnoreCase);

        // Computed, rowversion and identity columns are never insertable.
        var isInsertable = isTable && !column.IsComputed && !column.IsRowVersion && !column.IsIdentity;

        // Additionally, primary-key columns are not updatable by default.
        var isUpdatable = isInsertable && !isPrimaryKey;

        return column with
        {
            IsPrimaryKey = isPrimaryKey,
            IsInsertable = isInsertable,
            IsUpdatable = isUpdatable
        };
    }
}
