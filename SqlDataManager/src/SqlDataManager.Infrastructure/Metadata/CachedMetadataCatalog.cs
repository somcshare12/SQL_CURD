using Microsoft.Extensions.Options;
using SqlDataManager.Application.Abstractions;
using SqlDataManager.Application.Configuration;
using SqlDataManager.Domain.Metadata;

namespace SqlDataManager.Infrastructure.Metadata;

/// <summary>
/// Caches the result of <see cref="SqlMetadataReader"/> for a configurable
/// period so the module does not hit the system catalog on every request.
/// Metadata rarely changes, and an administrative refresh endpoint can clear
/// the cache on demand via <see cref="Invalidate"/>.
///
/// A single <see cref="SemaphoreSlim"/> guards the reload so concurrent callers
/// share one database round-trip (the "cache stampede" guard).
/// </summary>
public sealed class CachedMetadataCatalog(
    SqlMetadataReader reader,
    IOptions<SqlDataManagerOptions> options,
    TimeProvider timeProvider) : IMetadataCatalog, IDisposable
{
    private readonly TimeSpan _ttl = TimeSpan.FromSeconds(options.Value.Connection.MetadataCacheSeconds);
    private readonly SemaphoreSlim _gate = new(1, 1);

    private IReadOnlyList<ObjectMetadata>? _cache;
    private DateTimeOffset _loadedAt;

    public async Task<IReadOnlyList<ObjectMetadata>> GetAllObjectsAsync(CancellationToken cancellationToken)
    {
        if (IsFresh())
        {
            return _cache!;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (IsFresh())
            {
                return _cache!;
            }

            _cache = await reader.ReadAllAsync(cancellationToken);
            _loadedAt = timeProvider.GetUtcNow();
            return _cache;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ObjectMetadata?> GetObjectAsync(string schema, string name, CancellationToken cancellationToken)
    {
        var all = await GetAllObjectsAsync(cancellationToken);
        return all.FirstOrDefault(o =>
            string.Equals(o.Schema, schema, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public void Invalidate()
    {
        _cache = null;
        _loadedAt = default;
    }

    private bool IsFresh() =>
        _cache is not null && timeProvider.GetUtcNow() - _loadedAt < _ttl;

    public void Dispose() => _gate.Dispose();
}
