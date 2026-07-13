using Microsoft.Data.SqlClient;
using SqlDataManager.Domain.Exceptions;

namespace SqlDataManager.Infrastructure.Sql;

/// <summary>
/// Translates raw <see cref="SqlException"/>s into the module's safe, coded
/// exceptions. This is where we make sure the browser never sees a raw SQL
/// error message or stack trace — only an understandable, stable error code.
/// </summary>
public static class SqlErrorTranslator
{
    public static SqlDataManagerException Translate(SqlException ex) => ex.Number switch
    {
        // Unique key / duplicate key violations.
        2627 or 2601 => new SqlDataManagerException(
            ErrorCodes.UniqueConstraintConflict,
            "A record with the same unique value already exists.",
            httpStatusCode: 409, innerException: ex),

        // Foreign-key constraint violations (insert/update/delete).
        547 => new SqlDataManagerException(
            ErrorCodes.ForeignKeyConflict,
            "The operation conflicts with a related record and cannot be completed.",
            httpStatusCode: 409, innerException: ex),

        // Command timeout.
        -2 => new SqlDataManagerException(
            ErrorCodes.QueryTimeout,
            "The database operation timed out. Please refine your request and try again.",
            httpStatusCode: 504, innerException: ex),

        // Anything else is an unexpected database error; details stay server-side.
        _ => new SqlDataManagerException(
            ErrorCodes.DatabaseUnavailable,
            "A database error occurred while processing the request.",
            httpStatusCode: 500, innerException: ex)
    };
}
