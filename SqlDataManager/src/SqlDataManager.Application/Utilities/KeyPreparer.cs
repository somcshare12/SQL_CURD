using SqlDataManager.Application.Abstractions;
using SqlDataManager.Domain.Exceptions;
using SqlDataManager.Domain.Metadata;
using SqlDataManager.Domain.Querying;

namespace SqlDataManager.Application.Utilities;

/// <summary>
/// Turns a loosely-typed <see cref="RecordKey"/> into a list of validated,
/// coerced <see cref="PreparedValue"/> entries, ordered to match the object's
/// declared key columns. Every mutation and single-row read goes through here,
/// which guarantees that the key always maps onto the real primary/unique key.
/// </summary>
public static class KeyPreparer
{
    public static IReadOnlyList<PreparedValue> Prepare(ObjectMetadata metadata, RecordKey key)
    {
        if (!metadata.HasStableKey)
        {
            throw new RowKeyRequiredException(
                "This object has no stable unique key, so it cannot be addressed by key.");
        }

        var supplied = key.Keys.ToDictionary(k => k.Column, k => k.Value, StringComparer.OrdinalIgnoreCase);
        var prepared = new List<PreparedValue>(metadata.KeyColumns.Count);

        foreach (var keyColumn in metadata.KeyColumns)
        {
            if (!supplied.TryGetValue(keyColumn, out var raw))
            {
                throw new RowKeyRequiredException($"The key column '{keyColumn}' is missing.");
            }

            var column = metadata.FindColumn(keyColumn)
                ?? throw new ColumnNotFoundException(keyColumn);

            if (!SqlValueConverter.TryCoerce(column, raw, out var value, out var error))
            {
                throw new ValidationFailedException(
                    new Dictionary<string, string[]> { [keyColumn] = [error ?? "Invalid key value."] });
            }

            if (value is null)
            {
                throw new RowKeyRequiredException($"The key column '{keyColumn}' cannot be null.");
            }

            prepared.Add(new PreparedValue(column, value));
        }

        return prepared;
    }
}
