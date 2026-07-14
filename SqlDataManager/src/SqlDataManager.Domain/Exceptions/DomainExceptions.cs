namespace SqlDataManager.Domain.Exceptions;

/// <summary>Raised when the module is disabled by configuration.</summary>
public sealed class ModuleDisabledException()
    : SqlDataManagerException(ErrorCodes.ModuleDisabled,
        "The SQL Data Manager module is disabled.", httpStatusCode: 503);

/// <summary>Raised when a requested object does not exist in metadata.</summary>
public sealed class ObjectNotFoundException(string schema, string name)
    : SqlDataManagerException(ErrorCodes.ObjectNotFound,
        $"The object '{schema}.{name}' was not found.", httpStatusCode: 404);

/// <summary>
/// Raised when an object exists but the caller is not permitted to access it.
/// Deliberately looks the same as "not found" to avoid leaking existence.
/// </summary>
public sealed class ObjectNotAllowedException(string schema, string name)
    : SqlDataManagerException(ErrorCodes.ObjectNotAllowed,
        $"The object '{schema}.{name}' is not accessible.", httpStatusCode: 404);

/// <summary>Raised when a referenced column is not part of trusted metadata.</summary>
public sealed class ColumnNotFoundException(string column)
    : SqlDataManagerException(ErrorCodes.ColumnNotFound,
        $"The column '{column}' does not exist on this object.", httpStatusCode: 400);

/// <summary>Raised when an operation is not permitted for the object/user.</summary>
public sealed class OperationNotAllowedException(string code, string message)
    : SqlDataManagerException(code, message, httpStatusCode: 403);

/// <summary>Raised when update/delete is attempted without a stable key.</summary>
public sealed class RowKeyRequiredException(string message)
    : SqlDataManagerException(ErrorCodes.RowKeyRequired, message, httpStatusCode: 400);

/// <summary>Raised when the targeted row cannot be found.</summary>
public sealed class RowNotFoundException()
    : SqlDataManagerException(ErrorCodes.RowNotFound,
        "The requested record was not found.", httpStatusCode: 404);

/// <summary>
/// Raised when an optimistic-concurrency check fails (zero rows affected).
/// Mapped to HTTP 409 so the client can offer reload/cancel.
/// </summary>
public sealed class ConcurrencyConflictException()
    : SqlDataManagerException(ErrorCodes.ConcurrencyConflict,
        "This record was changed by another user. Reload and try again.",
        httpStatusCode: 409);

/// <summary>Raised when structured validation fails.</summary>
public sealed class ValidationFailedException(IReadOnlyDictionary<string, string[]> errors)
    : SqlDataManagerException(ErrorCodes.ValidationFailed,
        "One or more values are invalid.", httpStatusCode: 400, validationErrors: errors);

/// <summary>Raised when a request exceeds the configured maximum page size.</summary>
public sealed class PageSizeExceededException(int requested, int maximum)
    : SqlDataManagerException(ErrorCodes.PageSizeExceeded,
        $"Requested page size {requested} exceeds the maximum of {maximum}.",
        httpStatusCode: 400);
