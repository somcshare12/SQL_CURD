using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SqlDataManager.Application.Abstractions;
using SqlDataManager.Application.Configuration;
using SqlDataManager.Domain.Enums;
using SqlDataManager.Domain.Metadata;
using SqlDataManager.Domain.Querying;
using SqlDataManager.Infrastructure.Sql;

namespace SqlDataManager.Infrastructure.Repositories;

/// <summary>
/// Executes read queries against SQL Server using the <see cref="SqlQueryBuilder"/>.
/// It applies the two display safeguards required by the spec: masked columns
/// are replaced with a mask token, and very long text values are truncated to
/// the configured maximum preview length.
/// </summary>
public sealed class RowRepository(
    SqlConnectionFactory connectionFactory,
    SqlQueryBuilder queryBuilder,
    IOptions<SqlDataManagerOptions> options) : IRowRepository
{
    private const string MaskToken = "\u2022\u2022\u2022\u2022"; // ••••
    private readonly DataDisplayOptions _display = options.Value.DataDisplay;
    private readonly int _commandTimeout = options.Value.Connection.CommandTimeoutSeconds;

    public async Task<QueryResult> QueryAsync(
        ObjectMetadata metadata, QueryRequest request, int maxPageSize, CancellationToken cancellationToken)
    {
        var built = queryBuilder.BuildSelect(metadata, request, maxPageSize);

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);

        var rows = new List<IReadOnlyDictionary<string, object?>>();
        await using (var command = CreateCommand(connection, built))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(ReadRow(reader, built.ProjectedColumns));
            }
        }

        long? total = null;
        if (request.IncludeTotalCount && _display.ExactRowCountMode != RowCountMode.Disabled)
        {
            total = await CountAsync(connection, metadata, request, cancellationToken);
        }

        return new QueryResult
        {
            Rows = rows,
            Page = Math.Max(1, request.Page),
            PageSize = Math.Clamp(request.PageSize, 1, maxPageSize),
            TotalCount = total,
            Columns = built.ProjectedColumns.Select(c => c.Name).ToList(),
            UnstableOrdering = built.UnstableOrdering
        };
    }

    public async Task<IReadOnlyDictionary<string, object?>?> ReadOneAsync(
        ObjectMetadata metadata, IReadOnlyList<PreparedValue> key, CancellationToken cancellationToken)
    {
        var projected = metadata.Columns
            .Where(c => c.Sensitivity != SensitiveColumnBehavior.Hidden)
            .ToList();

        var columnList = string.Join(", ", projected.Select(c => SqlIdentifier.Quote(c.Name)));
        var parameters = new List<SqlParameter>();
        var whereParts = new List<string>();

        for (var i = 0; i < key.Count; i++)
        {
            var name = "@k" + i;
            whereParts.Add($"{SqlIdentifier.Quote(key[i].Column.Name)} = {name}");
            parameters.Add(new SqlParameter(name, key[i].Value ?? DBNull.Value));
        }

        var sql = $"""
            SELECT {columnList}
            FROM {SqlIdentifier.QuoteQualified(metadata.Schema, metadata.Name)}
            WHERE {string.Join(" AND ", whereParts)};
            """;

        await using var connection = await connectionFactory.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = _commandTimeout };
        command.Parameters.AddRange(parameters.ToArray());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadRow(reader, projected);
    }

    private async Task<long> CountAsync(
        SqlConnection connection, ObjectMetadata metadata, QueryRequest request, CancellationToken ct)
    {
        var built = queryBuilder.BuildCount(metadata, request);
        await using var command = CreateCommand(connection, built);
        var scalar = await command.ExecuteScalarAsync(ct);
        return scalar is long l ? l : Convert.ToInt64(scalar);
    }

    private SqlCommand CreateCommand(SqlConnection connection, BuiltQuery built)
    {
        var command = new SqlCommand(built.Sql, connection) { CommandTimeout = _commandTimeout };
        command.Parameters.AddRange(built.Parameters.ToArray());
        return command;
    }

    /// <summary>
    /// Converts one reader row into a column-name -> value dictionary, applying
    /// masking and long-text truncation. Binary values are summarised rather
    /// than returned raw.
    /// </summary>
    private IReadOnlyDictionary<string, object?> ReadRow(
        SqlDataReader reader, IReadOnlyList<ColumnMetadata> columns)
    {
        var row = new Dictionary<string, object?>(columns.Count, StringComparer.Ordinal);

        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];

            if (reader.IsDBNull(i))
            {
                row[column.Name] = null;
                continue;
            }

            if (column.Sensitivity == SensitiveColumnBehavior.Masked)
            {
                row[column.Name] = MaskToken;
                continue;
            }

            var value = reader.GetValue(i);
            row[column.Name] = NormaliseValue(value);
        }

        return row;
    }

    private object? NormaliseValue(object value) => value switch
    {
        // Binary/rowversion values are summarised as base64 so the JSON stays
        // small and the grid can show a short preview.
        byte[] bytes => Convert.ToBase64String(bytes),
        string s when s.Length > _display.MaximumDisplayedTextLength =>
            s[.._display.MaximumDisplayedTextLength] + "\u2026",
        DateTime dt => dt,
        _ => value
    };
}
