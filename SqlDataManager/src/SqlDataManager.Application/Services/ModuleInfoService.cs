using Microsoft.Extensions.Options;
using SqlDataManager.Application.Abstractions;
using SqlDataManager.Application.Configuration;
using SqlDataManager.Contracts;

namespace SqlDataManager.Application.Services;

/// <summary>
/// Builds the safe, credential-free module description returned by the
/// <c>/info</c> endpoint. It exposes display names, limits and feature flags —
/// never the server, database name-as-connection-string, username or password.
/// </summary>
public sealed class ModuleInfoService(
    IOptions<SqlDataManagerOptions> options,
    IDatabaseConnectivity connectivity)
{
    private readonly SqlDataManagerOptions _options = options.Value;

    public ModuleInfoDto GetInfo()
    {
        var features = new List<string> { "read", "pagination", "sorting", "filtering" };
        if (_options.Crud.EnableCreate) features.Add("create");
        if (_options.Crud.EnableUpdate) features.Add("update");
        if (_options.Crud.EnableDelete) features.Add("delete");
        if (_options.Crud.AllowBulkDelete) features.Add("bulk-delete");

        return new ModuleInfoDto
        {
            Enabled = _options.Enabled,
            DisplayName = _options.DisplayName,
            // Only the human-friendly database name is surfaced, not the DSN.
            DatabaseDisplayName = connectivity.DatabaseDisplayName,
            MaximumPageSize = _options.DataDisplay.MaximumPageSize,
            DefaultPageSize = _options.DataDisplay.DefaultPageSize,
            AllowedPageSizes = _options.DataDisplay.AllowedPageSizes,
            SupportedFeatures = features,
            RequireAuthentication = _options.Security.RequireAuthentication
        };
    }
}
