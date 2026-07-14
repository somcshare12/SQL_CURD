namespace SqlDataManager.Domain.Exceptions;

/// <summary>
/// Base class for every domain/application error the module raises on purpose.
///
/// Each exception carries a stable machine-readable <see cref="Code"/> (one of
/// the codes required by the API error contract) plus an HTTP status hint. The
/// API exception middleware turns these into the consistent error response and
/// deliberately never leaks raw SQL text or stack traces to the browser.
/// </summary>
public class SqlDataManagerException : Exception
{
    public string Code { get; }
    public int HttpStatusCode { get; }
    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; }

    public SqlDataManagerException(
        string code,
        string message,
        int httpStatusCode = 400,
        IReadOnlyDictionary<string, string[]>? validationErrors = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
        HttpStatusCode = httpStatusCode;
        ValidationErrors = validationErrors;
    }
}

/// <summary>Central definitions of the stable error codes (see spec §30).</summary>
public static class ErrorCodes
{
    public const string ModuleDisabled = "MODULE_DISABLED";
    public const string DatabaseUnavailable = "DATABASE_UNAVAILABLE";
    public const string ObjectNotFound = "OBJECT_NOT_FOUND";
    public const string ObjectNotAllowed = "OBJECT_NOT_ALLOWED";
    public const string ColumnNotFound = "COLUMN_NOT_FOUND";
    public const string ReadNotAllowed = "READ_NOT_ALLOWED";
    public const string CreateNotAllowed = "CREATE_NOT_ALLOWED";
    public const string UpdateNotAllowed = "UPDATE_NOT_ALLOWED";
    public const string DeleteNotAllowed = "DELETE_NOT_ALLOWED";
    public const string RowKeyRequired = "ROW_KEY_REQUIRED";
    public const string RowNotFound = "ROW_NOT_FOUND";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string ForeignKeyConflict = "FOREIGN_KEY_CONFLICT";
    public const string UniqueConstraintConflict = "UNIQUE_CONSTRAINT_CONFLICT";
    public const string QueryTimeout = "QUERY_TIMEOUT";
    public const string PageSizeExceeded = "PAGE_SIZE_EXCEEDED";
    public const string SettingsSaveFailed = "SETTINGS_SAVE_FAILED";
    public const string UnexpectedError = "UNEXPECTED_ERROR";
}
