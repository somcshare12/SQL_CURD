using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SqlDataManager.IntegrationTests;

/// <summary>
/// Spins up the API in-memory (pointed at the Development configuration, i.e.
/// the local SQL Server demo database) and probes connectivity once. Tests are
/// skipped automatically when the database is not reachable so the suite is
/// safe to run in environments without SQL Server.
/// </summary>
public sealed class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public bool DatabaseAvailable { get; private set; }

    public static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        // Use the Development configuration, which points at the local SQL
        // Server demo database. UseSetting avoids depending on the hosting
        // extension namespace.
        builder.UseSetting("environment", "Development");
    }

    public async Task InitializeAsync()
    {
        try
        {
            var client = CreateClient();
            var response = await client.GetAsync("/api/sql-data-manager/health");
            DatabaseAvailable = response.IsSuccessStatusCode;
        }
        catch
        {
            DatabaseAvailable = false;
        }
    }

    public new Task DisposeAsync() => Task.CompletedTask;

    public async Task<T> GetJson<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }
}
