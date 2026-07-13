using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SqlDataManager.Application.Abstractions;
using SqlDataManager.Application.Configuration;
using SqlDataManager.Domain.Exceptions;
using SqlDataManager.Domain.Mutations;
using SqlDataManager.Infrastructure.Sql;

namespace SqlDataManager.Infrastructure.Repositories;

/// <summary>
/// Executes create/update/delete against SQL Server. Each operation:
///   * runs inside a short-lived explicit transaction (opened only after the
///     client has already confirmed the action),
///   * uses parameters for every value and bracket-quoted, metadata-validated
///     identifiers for every name,
///   * targets exactly one row via the stable key (never "all columns"),
///   * enforces optimistic concurrency via rowversion when present,
///   * translates SQL errors into safe, coded exceptions.
/// </summary>
public sealed class MutationRepository(
    SqlConnectionFactory connectionFactory,
    IOptions<SqlDataManagerOptions> options) : IMutationRepository
{
    private readonly int _commandTimeout = options.Value.Connection.CommandTimeoutSeconds;

    public async Task<RecordMutationResult> CreateAsync(PreparedCreate command, CancellationToken ct)
    {
        var metadata = command.Metadata;
        var target = SqlIdentifier.QuoteQualified(metadata.Schema, metadata.Name);

        await using var connection = await connectionFactory.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);

        try
        {
            var parameters = new List<SqlParameter>();

            // An OUTPUT INSERTED clause returns the new row's key values in the
            // same statement/scope, which correctly captures identity values,
            // server-generated GUID defaults and supplied composite-key values.
            var outputClause = metadata.KeyColumns.Count > 0
                ? "OUTPUT " + string.Join(", ", metadata.KeyColumns.Select(k => "INSERTED." + SqlIdentifier.Quote(k)))
                : string.Empty;

            string insertSql;
            if (command.Values.Count == 0)
            {
                // No values supplied: let every column use its database default.
                insertSql = $"INSERT INTO {target} {outputClause} DEFAULT VALUES;";
            }
            else
            {
                var columns = string.Join(", ", command.Values.Select(v => SqlIdentifier.Quote(v.Column.Name)));
                var valueParams = command.Values.Select((_, i) => "@v" + i).ToList();
                for (var i = 0; i < command.Values.Count; i++)
                {
                    parameters.Add(new SqlParameter("@v" + i, command.Values[i].Value ?? DBNull.Value));
                }

                insertSql = $"INSERT INTO {target} ({columns}) {outputClause} VALUES ({string.Join(", ", valueParams)});";
            }

            // Capture generated key values so the UI can refresh the created row.
            var generatedKeys = new Dictionary<string, object?>(StringComparer.Ordinal);
            await using (var insert = new SqlCommand(insertSql, connection, transaction) { CommandTimeout = _commandTimeout })
            {
                insert.Parameters.AddRange(parameters.ToArray());

                if (metadata.KeyColumns.Count > 0)
                {
                    await using var reader = await insert.ExecuteReaderAsync(ct);
                    if (await reader.ReadAsync(ct))
                    {
                        foreach (var keyColumn in metadata.KeyColumns)
                        {
                            var value = reader[keyColumn];
                            generatedKeys[keyColumn] = value is DBNull ? null : value;
                        }
                    }
                }
                else
                {
                    await insert.ExecuteNonQueryAsync(ct);
                }
            }

            await transaction.CommitAsync(ct);

            return new RecordMutationResult
            {
                Success = true,
                AffectedRows = 1,
                GeneratedKeys = generatedKeys
            };
        }
        catch (SqlException ex)
        {
            await transaction.RollbackAsync(ct);
            throw SqlErrorTranslator.Translate(ex);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<RecordMutationResult> UpdateAsync(PreparedUpdate command, CancellationToken ct)
    {
        var metadata = command.Metadata;
        var target = SqlIdentifier.QuoteQualified(metadata.Schema, metadata.Name);
        var parameters = new List<SqlParameter>();

        var setClauses = new List<string>();
        for (var i = 0; i < command.Changes.Count; i++)
        {
            var change = command.Changes[i];
            setClauses.Add($"{SqlIdentifier.Quote(change.Column.Name)} = @s{i}");
            parameters.Add(new SqlParameter("@s" + i, change.Value ?? DBNull.Value));
        }

        var (whereSql, usesConcurrency) = BuildKeyWhere(command.Metadata, command.Key, command.ConcurrencyToken, parameters);

        var sql = $"UPDATE {target} SET {string.Join(", ", setClauses)} WHERE {whereSql};";

        return await ExecuteSingleRowMutationAsync(sql, parameters, usesConcurrency, ct);
    }

    public async Task<RecordMutationResult> DeleteAsync(PreparedDelete command, CancellationToken ct)
    {
        var metadata = command.Metadata;
        var target = SqlIdentifier.QuoteQualified(metadata.Schema, metadata.Name);
        var parameters = new List<SqlParameter>();

        var (whereSql, usesConcurrency) = BuildKeyWhere(metadata, command.Key, command.ConcurrencyToken, parameters);
        var sql = $"DELETE FROM {target} WHERE {whereSql};";

        return await ExecuteSingleRowMutationAsync(sql, parameters, usesConcurrency, ct);
    }

    /// <summary>
    /// Builds the key (and optional rowversion) WHERE clause. Returns whether a
    /// concurrency token participates so we can distinguish "not found" from
    /// "changed by another user" when zero rows are affected.
    /// </summary>
    private static (string Sql, bool UsesConcurrency) BuildKeyWhere(
        Domain.Metadata.ObjectMetadata metadata,
        IReadOnlyList<PreparedValue> key,
        byte[]? concurrencyToken,
        List<SqlParameter> parameters)
    {
        var parts = new List<string>();
        for (var i = 0; i < key.Count; i++)
        {
            parts.Add($"{SqlIdentifier.Quote(key[i].Column.Name)} = @k{i}");
            parameters.Add(new SqlParameter("@k" + i, key[i].Value ?? DBNull.Value));
        }

        var usesConcurrency = false;
        if (metadata.RowVersionColumn is not null && concurrencyToken is not null)
        {
            parts.Add($"{SqlIdentifier.Quote(metadata.RowVersionColumn)} = @rowversion");
            parameters.Add(new SqlParameter("@rowversion", SqlDbType.Timestamp) { Value = concurrencyToken });
            usesConcurrency = true;
        }

        return (string.Join(" AND ", parts), usesConcurrency);
    }

    /// <summary>Runs a keyed UPDATE/DELETE requiring exactly one affected row.</summary>
    private async Task<RecordMutationResult> ExecuteSingleRowMutationAsync(
        string sql, List<SqlParameter> parameters, bool usesConcurrency, CancellationToken ct)
    {
        await using var connection = await connectionFactory.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);

        try
        {
            await using var command = new SqlCommand(sql, connection, transaction) { CommandTimeout = _commandTimeout };
            command.Parameters.AddRange(parameters.ToArray());

            var affected = await command.ExecuteNonQueryAsync(ct);

            if (affected == 0)
            {
                await transaction.RollbackAsync(ct);

                // With a concurrency token, zero rows means the row changed or
                // was deleted; without one it simply was not found.
                if (usesConcurrency)
                {
                    throw new ConcurrencyConflictException();
                }

                throw new RowNotFoundException();
            }

            if (affected > 1)
            {
                // Should be impossible with a unique key, but never silently
                // over-affect rows.
                await transaction.RollbackAsync(ct);
                throw new SqlDataManagerException(ErrorCodes.UnexpectedError,
                    "The operation affected more than one row and was rolled back.", httpStatusCode: 500);
            }

            await transaction.CommitAsync(ct);
            return new RecordMutationResult { Success = true, AffectedRows = affected };
        }
        catch (SqlException ex)
        {
            await transaction.RollbackAsync(ct);
            throw SqlErrorTranslator.Translate(ex);
        }
        catch (SqlDataManagerException)
        {
            throw;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
