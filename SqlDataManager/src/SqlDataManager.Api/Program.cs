using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Json;
using SqlDataManager.Api.Registration;
using SqlDataManager.Application.Configuration;

// ---------------------------------------------------------------------------
// Standalone host for the SQL Data Manager module.
//
// This file demonstrates the full "standalone mode": it registers the module,
// configures the cross-cutting web concerns (CORS, rate limiting, request-size
// limiting, OpenAPI, error handling) and maps the endpoints under the prefix
// taken from configuration. A parent application would perform the same two
// module calls (AddSqlDataManagerModule / MapSqlDataManagerEndpoints) inside
// its own host and skip the pieces it already owns.
// ---------------------------------------------------------------------------

var builder = WebApplication.CreateBuilder(args);

// The API route prefix is configurable; never hard-coded across the codebase.
var routePrefix = builder.Configuration["Modules:SqlDataManager:ApiRoutePrefix"]
                  ?? "/api/sql-data-manager";

// Register the module (options binding + all services + health checks).
builder.Services.AddSqlDataManagerModule(
    builder.Configuration.GetSection(SqlDataManagerOptions.SectionName));

// Emit camelCase JSON so the TypeScript client can consume it naturally.
builder.Services.Configure<JsonOptions>(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

// CORS: allow the frontend dev server origin(s). In production the SPA is
// served from the same origin, so CORS is only needed for local development.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? ["http://localhost:5173", "http://localhost:4173"];
builder.Services.AddCors(options =>
{
    options.AddPolicy("SqlDataManagerCors", policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// Rate limiting protects the database from abusive request volumes.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "global",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 240,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

// Request-size limiting: CRUD payloads are small, so cap the body size.
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 2 * 1024 * 1024);

builder.Services.AddOpenApi();

var app = builder.Build();

// Error contract first so every downstream error is shaped consistently.
app.UseSqlDataManagerExceptionHandling();

app.UseCors("SqlDataManagerCors");
app.UseRateLimiter();

// OpenAPI document at /openapi/v1.json.
app.MapOpenApi();

// Serve the built React client (if present in wwwroot) with SPA fallback so
// deep links like /sql-data-manager/tables/dbo/Customers work on refresh.
app.UseDefaultFiles();
app.UseStaticFiles();

// Map the module endpoints under the configurable prefix.
app.MapSqlDataManagerEndpoints(routePrefix);

// SPA fallback: any non-API route returns index.html when the client is built.
app.MapFallbackToFile("index.html");

app.Run();

// Exposed so the integration-test project can spin up the host in-memory.
public partial class Program;
