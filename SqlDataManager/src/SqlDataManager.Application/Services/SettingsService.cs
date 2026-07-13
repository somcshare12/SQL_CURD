using SqlDataManager.Application.Abstractions;
using SqlDataManager.Contracts;

namespace SqlDataManager.Application.Services;

/// <summary>
/// Thin use-case wrapper around <see cref="ISqlDataManagerSettingsStore"/> that
/// scopes every operation to the current user. Layout preferences are stored
/// entirely separately from the database connection settings.
/// </summary>
public sealed class SettingsService(
    ISqlDataManagerSettingsStore store,
    ICurrentUser currentUser)
{
    public Task<UserSettingsDto?> GetAsync(CancellationToken ct) =>
        store.GetAsync(currentUser.UserId, ct);

    public Task SaveAsync(UserSettingsDto settings, CancellationToken ct) =>
        store.SaveAsync(currentUser.UserId, settings, ct);

    public Task ResetAsync(CancellationToken ct) =>
        store.ResetAsync(currentUser.UserId, ct);

    public Task ResetObjectAsync(string schema, string name, CancellationToken ct) =>
        store.ResetObjectAsync(currentUser.UserId, $"{schema}.{name}", ct);
}
