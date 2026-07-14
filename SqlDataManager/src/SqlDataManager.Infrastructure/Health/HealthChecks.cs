using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using SqlDataManager.Application.Abstractions;
using SqlDataManager.Application.Configuration;

namespace SqlDataManager.Infrastructure.Health;

/// <summary>Verifies the module configuration is coherent.</summary>
public sealed class ConfigurationHealthCheck(IOptions<SqlDataManagerOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var o = options.Value;

        if (!o.Enabled)
        {
            return Task.FromResult(HealthCheckResult.Degraded("The module is disabled."));
        }

        if (string.IsNullOrWhiteSpace(o.Connection.Server) || string.IsNullOrWhiteSpace(o.Connection.Database))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Database server/name is not configured."));
        }

        if (o.DataDisplay.DefaultPageSize > o.DataDisplay.MaximumPageSize)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "DefaultPageSize cannot exceed MaximumPageSize."));
        }

        return Task.FromResult(HealthCheckResult.Healthy("Configuration is valid."));
    }
}

/// <summary>Verifies the configured SQL Server is reachable.</summary>
public sealed class DatabaseHealthCheck(IDatabaseConnectivity connectivity) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var connected = await connectivity.CanConnectAsync(cancellationToken);
        return connected
            ? HealthCheckResult.Healthy("Database connection succeeded.")
            : HealthCheckResult.Unhealthy("Database connection failed.");
    }
}

/// <summary>Verifies the settings directory can be created / written.</summary>
public sealed class SettingsDirectoryHealthCheck(IOptions<SqlDataManagerOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var dir = options.Value.SettingsStorage.Directory;
            Directory.CreateDirectory(dir);

            // Probe write access with a throwaway file.
            var probe = Path.Combine(dir, ".health-probe");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);

            return Task.FromResult(HealthCheckResult.Healthy("Settings directory is writable."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Settings directory is not writable.", ex));
        }
    }
}
