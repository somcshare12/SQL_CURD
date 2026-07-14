using Microsoft.AspNetCore.Mvc;
using SqlDataManager.Application.Abstractions;
using SqlDataManager.Application.Mapping;
using SqlDataManager.Application.Services;
using SqlDataManager.Contracts;

namespace SqlDataManager.Api.Registration;

/// <summary>
/// Maps every module endpoint under a configurable prefix. The prefix is passed
/// in (not hard-coded), so a parent app can mount the module at, for example,
/// <c>/api/modules/sql-data-manager</c>. All handlers are thin: they map the
/// wire DTO to the domain request, call the relevant use-case service, and map
/// the result back — every rule lives in the Application layer.
/// </summary>
public static class EndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapSqlDataManagerEndpoints(
        this IEndpointRouteBuilder endpoints, string routePrefix = "/api/sql-data-manager")
    {
        var group = endpoints.MapGroup(routePrefix).WithTags("SQL Data Manager");

        // ---- Health & info -------------------------------------------------
        group.MapGet("/health", async (IDatabaseConnectivity connectivity, CancellationToken ct) =>
        {
            var ok = await connectivity.CanConnectAsync(ct);
            return Results.Json(new { status = ok ? "healthy" : "unhealthy" },
                statusCode: ok ? 200 : 503);
        });

        group.MapGet("/info", (ModuleInfoService service) => Results.Ok(service.GetInfo()));

        // ---- Object catalog ------------------------------------------------
        group.MapGet("/objects", async (MetadataService service, CancellationToken ct) =>
        {
            var objects = await service.GetVisibleObjectsAsync(ct);
            return Results.Ok(objects.Select(o => o.ToDto()).ToList());
        });

        group.MapGet("/objects/{schema}/{objectName}/metadata",
            async (string schema, string objectName, MetadataService service, CancellationToken ct) =>
            {
                var metadata = await service.GetAccessibleMetadataAsync(schema, objectName, ct);
                return Results.Ok(metadata.ToDto());
            });

        // ---- Read ----------------------------------------------------------
        group.MapPost("/objects/{schema}/{objectName}/rows/query",
            async (string schema, string objectName, [FromBody] QueryRowsRequest request,
                   RowQueryService service, CancellationToken ct) =>
            {
                var result = await service.QueryAsync(schema, objectName, request.ToDomain(), ct);
                return Results.Ok(result.ToDto());
            });

        group.MapPost("/objects/{schema}/{objectName}/rows/read",
            async (string schema, string objectName, [FromBody] ReadRecordRequest request,
                   RowQueryService service, CancellationToken ct) =>
            {
                var record = await service.ReadOneAsync(schema, objectName, request.Keys.ToDomain(), ct);
                return Results.Ok(new RecordDetailsResponse { Record = record });
            });

        // ---- Create / Update / Delete -------------------------------------
        group.MapPost("/objects/{schema}/{objectName}/rows",
            async (string schema, string objectName, [FromBody] CreateRecordRequestDto request,
                   CreateRecordService service, CancellationToken ct) =>
            {
                var result = await service.CreateAsync(schema, objectName, request.ToDomain(), ct);
                return Results.Ok(result.ToDto());
            });

        group.MapPut("/objects/{schema}/{objectName}/rows",
            async (string schema, string objectName, [FromBody] UpdateRecordRequestDto request,
                   UpdateRecordService service, CancellationToken ct) =>
            {
                var result = await service.UpdateAsync(schema, objectName, request.ToDomain(), ct);
                return Results.Ok(result.ToDto());
            });

        group.MapDelete("/objects/{schema}/{objectName}/rows",
            async (string schema, string objectName, [FromBody] DeleteRecordRequestDto request,
                   DeleteRecordService service, CancellationToken ct) =>
            {
                var result = await service.DeleteAsync(schema, objectName, request.ToDomain(), ct);
                return Results.Ok(result.ToDto());
            });

        // ---- Settings ------------------------------------------------------
        group.MapGet("/settings", async (SettingsService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(ct) ?? new UserSettingsDto()));

        group.MapPut("/settings",
            async ([FromBody] UserSettingsDto settings, SettingsService service, CancellationToken ct) =>
            {
                await service.SaveAsync(settings, ct);
                return Results.NoContent();
            });

        group.MapDelete("/settings/objects/{schema}/{objectName}",
            async (string schema, string objectName, SettingsService service, CancellationToken ct) =>
            {
                await service.ResetObjectAsync(schema, objectName, ct);
                return Results.NoContent();
            });

        group.MapDelete("/settings", async (SettingsService service, CancellationToken ct) =>
        {
            await service.ResetAsync(ct);
            return Results.NoContent();
        });

        // ---- Administrative metadata refresh ------------------------------
        group.MapPost("/metadata/refresh", (IMetadataCatalog catalog) =>
        {
            catalog.Invalidate();
            return Results.Ok(new { refreshed = true });
        });

        return endpoints;
    }
}
