using SqlDataManager.Domain.Metadata;
using SqlDataManager.Domain.Querying;

namespace SqlDataManager.Application.Abstractions;

/// <summary>
/// Reads and caches the structural metadata of the database. Implemented by the
/// Infrastructure layer against SQL Server system views. The permissions on the
/// returned metadata reflect only structural *capability* (for example a view
/// can never be written to); the Application access rules refine these into the
/// effective permissions for the current user.
/// </summary>
public interface IMetadataCatalog
{
    /// <summary>All tables and views in the database (structural capability).</summary>
    Task<IReadOnlyList<ObjectMetadata>> GetAllObjectsAsync(CancellationToken cancellationToken);

    /// <summary>Full metadata for a single object, or null if it does not exist.</summary>
    Task<ObjectMetadata?> GetObjectAsync(string schema, string name, CancellationToken cancellationToken);

    /// <summary>Clears the cache so the next read re-queries SQL Server.</summary>
    void Invalidate();
}

/// <summary>Executes safe, parameterised read queries against a single object.</summary>
public interface IRowRepository
{
    Task<QueryResult> QueryAsync(
        ObjectMetadata metadata,
        QueryRequest request,
        int maxPageSize,
        CancellationToken cancellationToken);

    /// <summary>Reads exactly one row identified by its coerced stable key.</summary>
    Task<IReadOnlyDictionary<string, object?>?> ReadOneAsync(
        ObjectMetadata metadata,
        IReadOnlyList<PreparedValue> key,
        CancellationToken cancellationToken);
}

/// <summary>
/// Executes create/update/delete inside short-lived transactions. Every method
/// receives an already-validated command and uses parameters for values plus
/// metadata-validated, bracket-quoted identifiers for names.
/// </summary>
public interface IMutationRepository
{
    Task<Domain.Mutations.RecordMutationResult> CreateAsync(
        PreparedCreate command, CancellationToken cancellationToken);

    Task<Domain.Mutations.RecordMutationResult> UpdateAsync(
        PreparedUpdate command, CancellationToken cancellationToken);

    Task<Domain.Mutations.RecordMutationResult> DeleteAsync(
        PreparedDelete command, CancellationToken cancellationToken);
}

/// <summary>Reports whether the configured database is reachable (health check).</summary>
public interface IDatabaseConnectivity
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);

    /// <summary>The database display name (for the info endpoint / UI).</summary>
    string DatabaseDisplayName { get; }
}
