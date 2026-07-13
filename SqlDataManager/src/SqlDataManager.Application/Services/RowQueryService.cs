using Microsoft.Extensions.Options;
using SqlDataManager.Application.Abstractions;
using SqlDataManager.Application.Configuration;
using SqlDataManager.Domain.Enums;
using SqlDataManager.Domain.Exceptions;
using SqlDataManager.Domain.Metadata;
using SqlDataManager.Domain.Querying;

namespace SqlDataManager.Application.Services;

/// <summary>
/// Handles paged reads and single-record reads. Before touching the database it
/// re-checks permission and validates every column, filter and sort against
/// trusted metadata, so the repository only ever receives a request it knows to
/// be safe.
/// </summary>
public sealed class RowQueryService(
    MetadataService metadataService,
    IRowRepository rowRepository,
    IOptions<SqlDataManagerOptions> options)
{
    private readonly SqlDataManagerOptions _options = options.Value;

    public async Task<QueryResult> QueryAsync(
        string schema, string name, QueryRequest request, CancellationToken cancellationToken)
    {
        var metadata = await metadataService.GetAccessibleMetadataAsync(schema, name, cancellationToken);

        // canRead is guaranteed by GetAccessibleMetadataAsync, but be explicit.
        if (!metadata.Permissions.CanRead)
        {
            throw new OperationNotAllowedException(ErrorCodes.ReadNotAllowed, "Read is not permitted.");
        }

        ValidateQuery(metadata, request);

        return await rowRepository.QueryAsync(
            metadata, request, _options.DataDisplay.MaximumPageSize, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, object?>> ReadOneAsync(
        string schema, string name, RecordKey key, CancellationToken cancellationToken)
    {
        var metadata = await metadataService.GetAccessibleMetadataAsync(schema, name, cancellationToken);
        EnsureKeyMatches(metadata, key);

        var preparedKey = Utilities.KeyPreparer.Prepare(metadata, key);
        var record = await rowRepository.ReadOneAsync(metadata, preparedKey, cancellationToken)
            ?? throw new RowNotFoundException();

        return record;
    }

    /// <summary>Validates page size, selected columns, filters and sorts.</summary>
    private void ValidateQuery(ObjectMetadata metadata, QueryRequest request)
    {
        var max = _options.DataDisplay.MaximumPageSize;
        if (request.PageSize > max)
        {
            throw new PageSizeExceededException(request.PageSize, max);
        }

        foreach (var column in request.SelectedColumns)
        {
            if (metadata.FindColumn(column) is null)
            {
                throw new ColumnNotFoundException(column);
            }
        }

        foreach (var filter in request.Filters)
        {
            var column = metadata.FindColumn(filter.Column)
                ?? throw new ColumnNotFoundException(filter.Column);

            if (!column.IsFilterable)
            {
                throw new SqlDataManagerException(ErrorCodes.ValidationFailed,
                    $"Column '{filter.Column}' cannot be filtered.");
            }
        }

        foreach (var sort in request.Sort)
        {
            var column = metadata.FindColumn(sort.Column)
                ?? throw new ColumnNotFoundException(sort.Column);

            if (!column.IsSortable)
            {
                throw new SqlDataManagerException(ErrorCodes.ValidationFailed,
                    $"Column '{sort.Column}' cannot be sorted.");
            }
        }
    }

    /// <summary>
    /// Ensures the supplied key exactly matches the object's stable key columns.
    /// This prevents callers from inventing an ad-hoc key.
    /// </summary>
    internal static void EnsureKeyMatches(ObjectMetadata metadata, RecordKey key)
    {
        if (!metadata.HasStableKey)
        {
            throw new RowKeyRequiredException(
                "This object has no stable unique key, so single-record operations are not available.");
        }

        if (key.IsEmpty)
        {
            throw new RowKeyRequiredException("A record key is required.");
        }

        var provided = key.Keys.Select(k => k.Column).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var expected = metadata.KeyColumns.ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!provided.SetEquals(expected))
        {
            throw new RowKeyRequiredException(
                $"The record key must contain exactly these columns: {string.Join(", ", metadata.KeyColumns)}.");
        }
    }
}
