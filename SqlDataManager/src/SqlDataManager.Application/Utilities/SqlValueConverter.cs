using System.Globalization;
using System.Text.Json;
using SqlDataManager.Domain.Metadata;

namespace SqlDataManager.Application.Utilities;

/// <summary>
/// Converts loosely-typed incoming values (which arrive from JSON as
/// <see cref="JsonElement"/>, strings or numbers) into the strongly-typed CLR
/// values expected by a given column. This is the single, well-tested place
/// where "the user typed 42" becomes a real <see cref="int"/> that can be bound
/// as a SQL parameter.
///
/// High-precision decimals are handled via <see cref="decimal"/> and, where the
/// caller needs to preserve exact precision, values may be supplied as strings.
/// </summary>
public static class SqlValueConverter
{
    /// <summary>
    /// Attempts to coerce <paramref name="raw"/> into the CLR type of
    /// <paramref name="column"/>. Returns false with a human-readable
    /// <paramref name="error"/> when the value is not convertible.
    /// </summary>
    public static bool TryCoerce(ColumnMetadata column, object? raw, out object? value, out string? error)
    {
        error = null;
        value = null;

        var normalized = Normalize(raw);
        if (normalized is null)
        {
            // A genuine null; the validator decides whether null is allowed.
            value = null;
            return true;
        }

        var target = Nullable.GetUnderlyingType(column.ClrType) ?? column.ClrType;

        try
        {
            if (target == typeof(string))
            {
                value = normalized as string ?? Convert.ToString(normalized, CultureInfo.InvariantCulture);
            }
            else if (target == typeof(bool))
            {
                value = normalized switch
                {
                    bool b => b,
                    string s => ParseBool(s),
                    _ => Convert.ToBoolean(normalized, CultureInfo.InvariantCulture)
                };
            }
            else if (target == typeof(Guid))
            {
                value = normalized is Guid g ? g : Guid.Parse(normalized.ToString()!);
            }
            else if (target == typeof(DateTime))
            {
                value = normalized is DateTime dt
                    ? dt
                    : DateTime.Parse(normalized.ToString()!, CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind);
            }
            else if (target == typeof(DateTimeOffset))
            {
                value = normalized is DateTimeOffset dto
                    ? dto
                    : DateTimeOffset.Parse(normalized.ToString()!, CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind);
            }
            else if (target == typeof(TimeSpan))
            {
                value = normalized is TimeSpan ts ? ts : TimeSpan.Parse(normalized.ToString()!, CultureInfo.InvariantCulture);
            }
            else if (target == typeof(byte[]))
            {
                value = normalized is byte[] bytes ? bytes : Convert.FromBase64String(normalized.ToString()!);
            }
            else if (target.IsEnum)
            {
                value = Enum.Parse(target, normalized.ToString()!, ignoreCase: true);
            }
            else
            {
                // Numeric and other IConvertible types (int, long, short, byte,
                // decimal, double, float, ...). ChangeType handles the culture.
                value = Convert.ChangeType(normalized, target, CultureInfo.InvariantCulture);
            }

            return true;
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException or ArgumentException)
        {
            error = $"The value could not be interpreted as {column.SqlType}.";
            return false;
        }
    }

    /// <summary>Turns a raw JSON element into a plain CLR primitive.</summary>
    private static object? Normalize(object? raw)
    {
        if (raw is not JsonElement element)
        {
            return raw;
        }

        return element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            // Keep numbers as decimal to preserve as much precision as possible;
            // downstream ChangeType narrows to the real column type.
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDecimal(),
            _ => element.GetRawText()
        };
    }

    private static bool ParseBool(string s) => s.Trim().ToLowerInvariant() switch
    {
        "true" or "1" or "yes" or "y" => true,
        "false" or "0" or "no" or "n" => false,
        _ => bool.Parse(s)
    };
}
