namespace SqlDataManager.Infrastructure.Sql;

/// <summary>
/// Maps SQL Server type names to the CLR type the module uses to represent and
/// validate their values. Keeping this mapping in one place means both form
/// generation (frontend hints) and value coercion (validation) agree on how a
/// column behaves.
/// </summary>
public static class SqlTypeMapper
{
    public static Type ToClrType(string sqlTypeName) => sqlTypeName.ToLowerInvariant() switch
    {
        "int" => typeof(int),
        "bigint" => typeof(long),
        "smallint" => typeof(short),
        "tinyint" => typeof(byte),
        "bit" => typeof(bool),
        "decimal" or "numeric" or "money" or "smallmoney" => typeof(decimal),
        "float" => typeof(double),
        "real" => typeof(float),
        "uniqueidentifier" => typeof(Guid),
        "date" or "datetime" or "datetime2" or "smalldatetime" => typeof(DateTime),
        "datetimeoffset" => typeof(DateTimeOffset),
        "time" => typeof(TimeSpan),
        "binary" or "varbinary" or "image" or "timestamp" or "rowversion" => typeof(byte[]),
        // char/nchar/varchar/nvarchar/text/ntext/xml and anything unknown are
        // treated as text, which is always safe to display and edit.
        _ => typeof(string)
    };

    /// <summary>True for types that can be meaningfully sorted/filtered/searched.</summary>
    public static bool IsTextType(string sqlTypeName) =>
        ToClrType(sqlTypeName) == typeof(string);

    /// <summary>Binary payloads are excluded from sorting/filtering.</summary>
    public static bool IsBinaryType(string sqlTypeName) =>
        ToClrType(sqlTypeName) == typeof(byte[]);
}
