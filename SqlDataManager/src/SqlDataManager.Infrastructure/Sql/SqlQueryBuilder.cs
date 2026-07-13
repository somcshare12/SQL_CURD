using Microsoft.Data.SqlClient;
using SqlDataManager.Application.Utilities;
using SqlDataManager.Domain.Enums;
using SqlDataManager.Domain.Exceptions;
using SqlDataManager.Domain.Metadata;
using SqlDataManager.Domain.Querying;

namespace SqlDataManager.Infrastructure.Sql;

/// <summary>The generated SQL text plus its parameters and result shape.</summary>
public sealed record BuiltQuery(
    string Sql,
    IReadOnlyList<SqlParameter> Parameters,
    IReadOnlyList<ColumnMetadata> ProjectedColumns,
    bool UnstableOrdering);

/// <summary>
/// Builds safe, parameterised <c>SELECT</c> and <c>COUNT</c> statements for a
/// single object. Every identifier is bracket-quoted from trusted metadata and
/// every value is a SQL parameter — the client's structured request can never
/// contribute raw SQL text.
/// </summary>
public sealed class SqlQueryBuilder
{
    /// <summary>Builds the paged SELECT with ORDER BY / OFFSET-FETCH.</summary>
    public BuiltQuery BuildSelect(ObjectMetadata metadata, QueryRequest request, int maxPageSize)
    {
        var parameters = new List<SqlParameter>();
        var counter = new ParameterCounter();

        var projected = ResolveProjection(metadata, request);
        var columnList = string.Join(", ", projected.Select(c => SqlIdentifier.Quote(c.Name)));

        var where = BuildWhere(metadata, request, parameters, counter);
        var (orderBy, unstable) = BuildOrderBy(metadata, request);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, maxPageSize);
        var offset = (page - 1) * pageSize;

        // OFFSET/FETCH gives deterministic server-side paging and requires an
        // ORDER BY, which we always provide.
        var sql = $"""
            SELECT {columnList}
            FROM {SqlIdentifier.QuoteQualified(metadata.Schema, metadata.Name)}
            {where}
            ORDER BY {orderBy}
            OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY;
            """;

        return new BuiltQuery(sql, parameters, projected, unstable);
    }

    /// <summary>Builds a COUNT_BIG(*) for the same filter as the SELECT.</summary>
    public BuiltQuery BuildCount(ObjectMetadata metadata, QueryRequest request)
    {
        var parameters = new List<SqlParameter>();
        var counter = new ParameterCounter();
        var where = BuildWhere(metadata, request, parameters, counter);

        var sql = $"""
            SELECT COUNT_BIG(*)
            FROM {SqlIdentifier.QuoteQualified(metadata.Schema, metadata.Name)}
            {where};
            """;

        return new BuiltQuery(sql, parameters, [], false);
    }

    // ---- projection --------------------------------------------------------

    private static IReadOnlyList<ColumnMetadata> ResolveProjection(
        ObjectMetadata metadata, QueryRequest request)
    {
        IEnumerable<ColumnMetadata> columns = request.SelectedColumns.Count > 0
            ? request.SelectedColumns.Select(name =>
                metadata.FindColumn(name) ?? throw new ColumnNotFoundException(name))
            : metadata.Columns;

        // Never return columns configured as fully hidden.
        var projected = columns
            .Where(c => c.Sensitivity != SensitiveColumnBehavior.Hidden)
            .ToList();

        if (projected.Count == 0)
        {
            // Guarantee at least one column so the grid is never empty.
            projected = metadata.Columns
                .Where(c => c.Sensitivity != SensitiveColumnBehavior.Hidden)
                .Take(1)
                .ToList();
        }

        return projected;
    }

    // ---- WHERE -------------------------------------------------------------

    private static string BuildWhere(
        ObjectMetadata metadata,
        QueryRequest request,
        List<SqlParameter> parameters,
        ParameterCounter counter)
    {
        var predicates = new List<string>();

        foreach (var filter in request.Filters)
        {
            var column = metadata.FindColumn(filter.Column)
                ?? throw new ColumnNotFoundException(filter.Column);

            predicates.Add(BuildFilterPredicate(column, filter, parameters, counter));
        }

        // Optional global search across all searchable text columns.
        if (!string.IsNullOrWhiteSpace(request.GlobalSearch))
        {
            var searchable = metadata.Columns.Where(c => c.IsSearchable).ToList();
            if (searchable.Count > 0)
            {
                var param = counter.Next();
                parameters.Add(CreateStringParameter(param, $"%{EscapeLike(request.GlobalSearch)}%"));

                var ors = searchable.Select(c =>
                    $"{SqlIdentifier.Quote(c.Name)} LIKE {param} ESCAPE '\\'");
                predicates.Add("(" + string.Join(" OR ", ors) + ")");
            }
        }

        return predicates.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", predicates);
    }

    private static string BuildFilterPredicate(
        ColumnMetadata column,
        FilterDefinition filter,
        List<SqlParameter> parameters,
        ParameterCounter counter)
    {
        var quoted = SqlIdentifier.Quote(column.Name);

        switch (filter.Operator)
        {
            case FilterOperator.IsNull:
                return $"{quoted} IS NULL";
            case FilterOperator.IsNotNull:
                return $"{quoted} IS NOT NULL";
            case FilterOperator.IsEmpty:
                return $"{quoted} = ''";
            case FilterOperator.IsNotEmpty:
                return $"{quoted} <> ''";
            case FilterOperator.IsTrue:
                return $"{quoted} = 1";
            case FilterOperator.IsFalse:
                return $"{quoted} = 0";
        }

        // LIKE-based text operators wrap the (escaped) value with wildcards.
        if (filter.Operator is FilterOperator.Contains or FilterOperator.DoesNotContain
            or FilterOperator.StartsWith or FilterOperator.EndsWith)
        {
            var text = CoerceString(filter.Value);
            var pattern = filter.Operator switch
            {
                FilterOperator.Contains or FilterOperator.DoesNotContain => $"%{EscapeLike(text)}%",
                FilterOperator.StartsWith => $"{EscapeLike(text)}%",
                FilterOperator.EndsWith => $"%{EscapeLike(text)}",
                _ => text
            };

            var param = counter.Next();
            parameters.Add(CreateStringParameter(param, pattern));
            var keyword = filter.Operator == FilterOperator.DoesNotContain ? "NOT LIKE" : "LIKE";
            return $"{quoted} {keyword} {param} ESCAPE '\\'";
        }

        // BETWEEN needs two coerced parameters.
        if (filter.Operator == FilterOperator.Between)
        {
            var p1 = counter.Next();
            var p2 = counter.Next();
            parameters.Add(CreateTypedParameter(p1, column, filter.Value));
            parameters.Add(CreateTypedParameter(p2, column, filter.SecondValue));
            return $"{quoted} BETWEEN {p1} AND {p2}";
        }

        // Simple binary comparison operators.
        var op = filter.Operator switch
        {
            FilterOperator.Equals => "=",
            FilterOperator.DoesNotEqual => "<>",
            FilterOperator.GreaterThan => ">",
            FilterOperator.GreaterThanOrEqual => ">=",
            FilterOperator.LessThan => "<",
            FilterOperator.LessThanOrEqual => "<=",
            _ => throw new SqlDataManagerException(ErrorCodes.ValidationFailed,
                $"Operator '{filter.Operator}' is not supported for column '{column.Name}'.")
        };

        var valueParam = counter.Next();
        parameters.Add(CreateTypedParameter(valueParam, column, filter.Value));
        return $"{quoted} {op} {valueParam}";
    }

    // ---- ORDER BY (§18.2 stable ordering) ----------------------------------

    private static (string OrderBy, bool Unstable) BuildOrderBy(
        ObjectMetadata metadata, QueryRequest request)
    {
        var terms = new List<string>();
        var usedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1) explicit sort from the request.
        foreach (var sort in request.Sort)
        {
            var column = metadata.FindColumn(sort.Column)
                ?? throw new ColumnNotFoundException(sort.Column);

            var direction = sort.Direction == SortDirection.Descending ? "DESC" : "ASC";
            terms.Add($"{SqlIdentifier.Quote(column.Name)} {direction}");
            usedColumns.Add(column.Name);
        }

        // 2/3) append key columns as a deterministic tiebreaker so paging is
        // stable even when the explicit sort is on a non-unique column.
        var appendedKey = false;
        foreach (var keyColumn in metadata.KeyColumns)
        {
            if (usedColumns.Add(keyColumn))
            {
                terms.Add($"{SqlIdentifier.Quote(keyColumn)} ASC");
                appendedKey = true;
            }
        }

        if (terms.Count > 0)
        {
            // Stable when we have a key to guarantee uniqueness OR the caller
            // supplied a sort on top of a keyed tiebreaker.
            var unstable = metadata.KeyColumns.Count == 0 && !appendedKey && request.Sort.Count == 0;
            return (string.Join(", ", terms), unstable || (metadata.KeyColumns.Count == 0 && request.Sort.Count == 0));
        }

        // 4) deterministic fallback: order by the first column. Without a key
        // this cannot guarantee stability, so flag it.
        var fallback = metadata.Columns[0];
        return ($"{SqlIdentifier.Quote(fallback.Name)} ASC", metadata.KeyColumns.Count == 0);
    }

    // ---- parameter helpers -------------------------------------------------

    private static string CoerceString(object? value) =>
        value?.ToString() ?? string.Empty;

    /// <summary>Escapes LIKE wildcards so user text is matched literally.</summary>
    private static string EscapeLike(string value) => value
        .Replace("\\", "\\\\")
        .Replace("%", "\\%")
        .Replace("_", "\\_")
        .Replace("[", "\\[");

    private static SqlParameter CreateStringParameter(string name, string value) =>
        new(name, System.Data.SqlDbType.NVarChar) { Value = value };

    /// <summary>Creates a parameter whose value is coerced to the column type.</summary>
    private static SqlParameter CreateTypedParameter(string name, ColumnMetadata column, object? rawValue)
    {
        if (!SqlValueConverter.TryCoerce(column, rawValue, out var value, out var error))
        {
            throw new ValidationFailedException(
                new Dictionary<string, string[]> { [column.Name] = [error ?? "Invalid filter value."] });
        }

        return new SqlParameter(name, value ?? DBNull.Value);
    }

    private sealed class ParameterCounter
    {
        private int _index;
        public string Next() => "@p" + _index++;
    }
}
