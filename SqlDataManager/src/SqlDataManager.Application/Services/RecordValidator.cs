using SqlDataManager.Application.Utilities;
using SqlDataManager.Domain.Metadata;
using SqlDataManager.Domain.Mutations;
using SqlDataManager.Domain.Validation;

namespace SqlDataManager.Application.Services;

/// <summary>
/// Validates create and update payloads against trusted column metadata. This
/// is the authoritative (server-side) validation; the frontend performs the
/// same checks for usability but is never trusted.
/// </summary>
public sealed class RecordValidator
{
    /// <summary>Validates a create payload and returns the coerced field values.</summary>
    public ValidationResult ValidateCreate(
        ObjectMetadata metadata,
        IReadOnlyList<FieldValue> fields,
        out IReadOnlyList<(ColumnMetadata Column, object? Value)> coerced)
    {
        var result = new ValidationResult();
        var accepted = new List<(ColumnMetadata, object?)>();
        var supplied = fields.ToDictionary(f => f.Column, f => f.Value, StringComparer.OrdinalIgnoreCase);

        // Reject any field that targets a non-insertable column outright.
        foreach (var field in fields)
        {
            var column = metadata.FindColumn(field.Column);
            if (column is null)
            {
                result.Add(field.Column, "Unknown column.");
                continue;
            }

            if (!column.IsInsertable)
            {
                result.Add(field.Column, "This column cannot be set (computed, identity or read-only).");
            }
        }

        // Validate each insertable column: required-ness, nullability, coercion,
        // and per-type constraints.
        foreach (var column in metadata.Columns.Where(c => c.IsInsertable))
        {
            var hasValue = supplied.TryGetValue(column.Name, out var raw);

            if (!hasValue)
            {
                // Omitted column: allowed only if it is nullable or has a DB
                // default (SQL Server will supply the value).
                if (!column.IsNullable && !column.HasDefault)
                {
                    result.Add(column.Name, "A value is required.");
                }
                continue;
            }

            ValidateColumnValue(column, raw, result, accepted);
        }

        coerced = accepted;
        return result;
    }

    /// <summary>Validates an update payload (only changed fields are present).</summary>
    public ValidationResult ValidateUpdate(
        ObjectMetadata metadata,
        IReadOnlyList<FieldValue> changedFields,
        out IReadOnlyList<(ColumnMetadata Column, object? Value)> coerced)
    {
        var result = new ValidationResult();
        var accepted = new List<(ColumnMetadata, object?)>();

        if (changedFields.Count == 0)
        {
            result.Add("_record", "No changes were supplied.");
        }

        foreach (var field in changedFields)
        {
            var column = metadata.FindColumn(field.Column);
            if (column is null)
            {
                result.Add(field.Column, "Unknown column.");
                continue;
            }

            if (!column.IsUpdatable)
            {
                result.Add(field.Column, "This column cannot be edited.");
                continue;
            }

            ValidateColumnValue(column, field.Value, result, accepted);
        }

        coerced = accepted;
        return result;
    }

    /// <summary>Shared per-value validation: null rules, coercion and length/precision.</summary>
    private static void ValidateColumnValue(
        ColumnMetadata column,
        object? raw,
        ValidationResult result,
        List<(ColumnMetadata, object?)> accepted)
    {
        if (!SqlValueConverter.TryCoerce(column, raw, out var value, out var coercionError))
        {
            result.Add(column.Name, coercionError ?? "Invalid value.");
            return;
        }

        if (value is null)
        {
            if (!column.IsNullable)
            {
                result.Add(column.Name, "This value cannot be null.");
                return;
            }

            accepted.Add((column, null));
            return;
        }

        // Text length check (skip -1 / MAX which has no fixed limit).
        if (value is string text && column.MaxLength is > 0 && text.Length > column.MaxLength)
        {
            result.Add(column.Name, $"The value exceeds the maximum length of {column.MaxLength}.");
            return;
        }

        accepted.Add((column, value));
    }
}
