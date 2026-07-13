using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SqlDataManager.Application.Abstractions;
using SqlDataManager.Application.Configuration;
using SqlDataManager.Contracts;

namespace SqlDataManager.Infrastructure.Settings;

/// <summary>
/// Stores per-user layout settings as JSON files, one file per user. Writes are
/// atomic: the payload is serialised to a temporary file, validated, then
/// swapped into place so a crash mid-write can never corrupt the live file. A
/// corrupted file is recovered from gracefully by treating it as "no settings".
///
/// This is the default <see cref="ISqlDataManagerSettingsStore"/>; a parent app
/// can replace it with a SQL Server or profile-service backed implementation.
/// </summary>
public sealed class JsonFileSettingsStore(
    IOptions<SqlDataManagerOptions> options,
    ILogger<JsonFileSettingsStore> logger) : ISqlDataManagerSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _directory = options.Value.SettingsStorage.Directory;

    public async Task<UserSettingsDto?> GetAsync(string userId, CancellationToken cancellationToken)
    {
        var path = GetPath(userId);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<UserSettingsDto>(stream, SerializerOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            // Corrupted settings must never break the app: log and start fresh.
            logger.LogWarning(ex, "Corrupted settings file for user {UserId}; ignoring.", userId);
            return null;
        }
    }

    public async Task SaveAsync(string userId, UserSettingsDto settings, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_directory);
        var path = GetPath(userId);
        var tempPath = path + ".tmp";

        try
        {
            // 1) Serialise to a temp file. 2) Validate by re-reading. 3) Swap.
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken);
            }

            // Validate the temp file parses before replacing the live file.
            await using (var verify = File.OpenRead(tempPath))
            {
                _ = await JsonSerializer.DeserializeAsync<UserSettingsDto>(verify, SerializerOptions, cancellationToken);
            }

            if (File.Exists(path))
            {
                // File.Replace keeps a backup and swaps atomically on the OS.
                File.Replace(tempPath, path, path + ".bak", ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogError(ex, "Failed to save settings for user {UserId}.", userId);
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* best effort cleanup */ }
            }

            throw new Domain.Exceptions.SqlDataManagerException(
                Domain.Exceptions.ErrorCodes.SettingsSaveFailed,
                "The layout settings could not be saved.", httpStatusCode: 500, innerException: ex);
        }
    }

    public Task ResetAsync(string userId, CancellationToken cancellationToken)
    {
        var path = GetPath(userId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public async Task ResetObjectAsync(string userId, string objectFullName, CancellationToken cancellationToken)
    {
        var settings = await GetAsync(userId, cancellationToken);
        if (settings is null || !settings.ObjectLayouts.ContainsKey(objectFullName))
        {
            return;
        }

        var layouts = settings.ObjectLayouts.ToDictionary(kv => kv.Key, kv => kv.Value);
        layouts.Remove(objectFullName);
        await SaveAsync(userId, settings with { ObjectLayouts = layouts }, cancellationToken);
    }

    /// <summary>Builds a safe file path from a sanitised user id.</summary>
    private string GetPath(string userId)
    {
        var safe = string.Concat(userId.Select(c =>
            char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));

        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = "anonymous";
        }

        return Path.Combine(_directory, safe + ".json");
    }
}
