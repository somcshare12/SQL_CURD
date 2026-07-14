using SqlDataManager.Api.Middleware;

namespace SqlDataManager.Api.Registration;

/// <summary>Pipeline helpers for hosts embedding the module.</summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the module's exception-to-error-contract middleware. A parent app
    /// that already has global error handling can skip this and rely on the
    /// coded <see cref="Domain.Exceptions.SqlDataManagerException"/> instead.
    /// </summary>
    public static IApplicationBuilder UseSqlDataManagerExceptionHandling(this IApplicationBuilder app) =>
        app.UseMiddleware<ExceptionHandlingMiddleware>();
}
