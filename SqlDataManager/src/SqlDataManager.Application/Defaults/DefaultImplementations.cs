using Microsoft.Extensions.Logging;
using SqlDataManager.Application.Abstractions;

namespace SqlDataManager.Application.Defaults;

/// <summary>
/// The default current-user used in standalone mode (trusted-network operation
/// without authentication). A parent application registers its own
/// <see cref="ICurrentUser"/> to plug in real identity and policy checks.
/// </summary>
public sealed class AnonymousCurrentUser : ICurrentUser
{
    public string UserId => "anonymous";
    public bool IsAuthenticated => false;

    // When authentication is not required, every policy check passes; when it
    // *is* required, the standalone host should register a real ICurrentUser.
    public bool IsAuthorized(string policy) => true;
}

/// <summary>
/// Default audit sink: emits a structured log entry per mutation. It never logs
/// record values, only metadata about the operation. A parent app can register
/// its own writer to persist audit events elsewhere.
/// </summary>
public sealed class LoggingAuditWriter(ILogger<LoggingAuditWriter> logger) : ISqlDataManagerAuditWriter
{
    public Task WriteAsync(SqlDataManagerAuditEvent auditEvent, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "SQL Data Manager audit: {Operation} {Schema}.{Object} by {User} success={Success} rows={Rows}",
            auditEvent.Operation, auditEvent.Schema, auditEvent.ObjectName,
            auditEvent.UserId ?? "anonymous", auditEvent.Success, auditEvent.AffectedRows);

        return Task.CompletedTask;
    }
}
