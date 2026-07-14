using System.Diagnostics;
using System.Text.Json;
using SqlDataManager.Contracts;
using SqlDataManager.Domain.Exceptions;

namespace SqlDataManager.Api.Middleware;

/// <summary>
/// Converts every exception into the module's consistent JSON error contract.
/// Known <see cref="SqlDataManagerException"/>s carry a stable code, HTTP status
/// and optional validation errors. Any unexpected exception is logged in full
/// server-side but returned to the browser as a generic <c>UNEXPECTED_ERROR</c>
/// so raw SQL text and stack traces never leak.
/// </summary>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (SqlDataManagerException ex)
        {
            // Expected, coded errors: log at a low level (no payloads).
            logger.LogWarning("SQL Data Manager error {Code}: {Message}", ex.Code, ex.Message);
            await WriteAsync(context, ex.HttpStatusCode, new ApiErrorResponse
            {
                Code = ex.Code,
                Message = ex.Message,
                TraceId = GetTraceId(context),
                ValidationErrors = ex.ValidationErrors
            });
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client went away; nothing to return.
            logger.LogDebug("Request was cancelled by the client.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error handling {Path}.", context.Request.Path);
            await WriteAsync(context, 500, new ApiErrorResponse
            {
                Code = ErrorCodes.UnexpectedError,
                Message = "An unexpected error occurred.",
                TraceId = GetTraceId(context)
            });
        }
    }

    private static async Task WriteAsync(HttpContext context, int statusCode, ApiErrorResponse body)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(body, SerializerOptions));
    }

    private static string GetTraceId(HttpContext context) =>
        Activity.Current?.Id ?? context.TraceIdentifier;
}
