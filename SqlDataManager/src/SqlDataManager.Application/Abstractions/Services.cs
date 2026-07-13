using SqlDataManager.Domain.Metadata;

namespace SqlDataManager.Application.Abstractions;

/// <summary>
/// Provides the identity of the current caller. In standalone mode this is an
/// anonymous stand-in; in embedded mode the parent application supplies a real
/// implementation backed by its authentication.
/// </summary>
public interface ICurrentUser
{
    string UserId { get; }
    bool IsAuthenticated { get; }

    /// <summary>Returns true when the user satisfies the named authorization policy.</summary>
    bool IsAuthorized(string policy);
}

/// <summary>
/// Per-user layout persistence. The default JSON-file implementation lives in
/// Infrastructure, but a parent app can swap in SQL Server, a profile service,
/// distributed cache, etc.
/// </summary>
public interface ISqlDataManagerSettingsStore
{
    Task<Contracts.UserSettingsDto?> GetAsync(string userId, CancellationToken cancellationToken);
    Task SaveAsync(string userId, Contracts.UserSettingsDto settings, CancellationToken cancellationToken);
    Task ResetAsync(string userId, CancellationToken cancellationToken);
    Task ResetObjectAsync(string userId, string objectFullName, CancellationToken cancellationToken);
}

/// <summary>
/// A write-only audit sink. The default implementation logs a structured event;
/// a parent app can persist audit events to its own store.
/// </summary>
public interface ISqlDataManagerAuditWriter
{
    Task WriteAsync(SqlDataManagerAuditEvent auditEvent, CancellationToken cancellationToken);
}

/// <summary>A single auditable mutation event (never contains raw values).</summary>
public sealed record SqlDataManagerAuditEvent
{
    public required string Operation { get; init; }
    public required string Schema { get; init; }
    public required string ObjectName { get; init; }
    public string? UserId { get; init; }
    public required bool Success { get; init; }
    public int AffectedRows { get; init; }
    public string? TraceId { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
