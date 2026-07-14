namespace SqlDataManager.Infrastructure.Sql;

/// <summary>
/// Safely quotes SQL Server identifiers (schema/table/column names) using
/// bracket quoting, for example <c>[dbo].[Customers]</c>.
///
/// Identifiers can never be SQL parameters, so this is the last line of defence
/// against injection through a name. Callers must ONLY pass names that were
/// obtained from trusted metadata and matched against the permitted-object
/// cache — but even then we defensively reject obviously invalid names and
/// escape embedded closing brackets by doubling them (the documented SQL Server
/// rule), which is exactly what <see cref="QuoteName"/> does.
/// </summary>
public static class SqlIdentifier
{
    /// <summary>
    /// Quotes a single identifier part. Throws when the name is null/empty,
    /// too long, or contains a NUL character (which SQL Server never allows).
    /// </summary>
    public static string Quote(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (name.Length > 128)
        {
            throw new ArgumentException("Identifier exceeds the 128 character SQL Server limit.", nameof(name));
        }

        if (name.Contains('\0'))
        {
            throw new ArgumentException("Identifier contains an invalid NUL character.", nameof(name));
        }

        // Double any embedded closing bracket, then wrap in brackets.
        return "[" + name.Replace("]", "]]") + "]";
    }

    /// <summary>Quotes a two-part name, for example <c>[dbo].[Customers]</c>.</summary>
    public static string QuoteQualified(string schema, string name) =>
        $"{Quote(schema)}.{Quote(name)}";
}
