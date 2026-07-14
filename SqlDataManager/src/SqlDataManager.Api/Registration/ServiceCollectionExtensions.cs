using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SqlDataManager.Application.Abstractions;
using SqlDataManager.Application.Configuration;
using SqlDataManager.Application.Defaults;
using SqlDataManager.Application.Services;
using SqlDataManager.Infrastructure.Health;
using SqlDataManager.Infrastructure.Metadata;
using SqlDataManager.Infrastructure.Repositories;
using SqlDataManager.Infrastructure.Settings;
using SqlDataManager.Infrastructure.Sql;

namespace SqlDataManager.Api.Registration;

/// <summary>
/// The one-line backend registration surface for the module. A host (standalone
/// or parent) calls <see cref="AddSqlDataManagerModule"/> to wire up every
/// service the module needs. All default implementations are registered with
/// <c>TryAdd</c> so a parent application can override identity, auditing or the
/// settings store simply by registering its own first.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSqlDataManagerModule(
        this IServiceCollection services, IConfigurationSection configurationSection)
    {
        // ---- Options -------------------------------------------------------
        services.Configure<SqlDataManagerOptions>(configurationSection);

        // ---- Cross-cutting -------------------------------------------------
        services.TryAddSingleton(TimeProvider.System);

        // ---- Infrastructure (SQL Server) ----------------------------------
        services.TryAddSingleton<SqlConnectionFactory>();
        services.TryAddSingleton<IDatabaseConnectivity>(sp => sp.GetRequiredService<SqlConnectionFactory>());
        services.TryAddSingleton<SqlQueryBuilder>();
        services.TryAddSingleton<SqlMetadataReader>();
        services.TryAddSingleton<IMetadataCatalog, CachedMetadataCatalog>();
        services.TryAddScoped<IRowRepository, RowRepository>();
        services.TryAddScoped<IMutationRepository, MutationRepository>();
        services.TryAddSingleton<ISqlDataManagerSettingsStore, JsonFileSettingsStore>();

        // ---- Parent-overridable defaults ----------------------------------
        services.TryAddScoped<ICurrentUser, AnonymousCurrentUser>();
        services.TryAddSingleton<ISqlDataManagerAuditWriter, LoggingAuditWriter>();

        // ---- Application use-case services --------------------------------
        services.TryAddScoped<ObjectAccessService>();
        services.TryAddScoped<MetadataService>();
        services.TryAddScoped<RowQueryService>();
        services.TryAddScoped<RecordValidator>();
        services.TryAddScoped<CreateRecordService>();
        services.TryAddScoped<UpdateRecordService>();
        services.TryAddScoped<DeleteRecordService>();
        services.TryAddScoped<SettingsService>();
        services.TryAddScoped<ModuleInfoService>();

        // ---- Health checks -------------------------------------------------
        services.AddHealthChecks()
            .AddCheck<ConfigurationHealthCheck>("sqldatamanager-configuration", tags: ["sqldatamanager"])
            .AddCheck<DatabaseHealthCheck>("sqldatamanager-database", tags: ["sqldatamanager"])
            .AddCheck<SettingsDirectoryHealthCheck>("sqldatamanager-settings", tags: ["sqldatamanager"]);

        return services;
    }
}
