using SqlDataManager.Domain.Metadata;
using Xunit;

namespace SqlDataManager.ArchitectureTests;

/// <summary>
/// Guards the dependency-direction rules of the architecture. These tests fail
/// the build if someone accidentally makes the Domain (or Application) layer
/// depend on infrastructure concerns such as ASP.NET Core or SQL Server.
/// </summary>
public class LayeringTests
{
    [Theory]
    [InlineData("Microsoft.Data.SqlClient")]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("System.Data.SqlClient")]
    public void Domain_does_not_reference_infrastructure(string forbidden)
    {
        var referenced = typeof(ObjectMetadata).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty);

        Assert.DoesNotContain(referenced, name => name.StartsWith(forbidden, System.StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("Microsoft.Data.SqlClient")]
    [InlineData("Microsoft.AspNetCore")]
    public void Application_does_not_reference_data_provider_or_web(string forbidden)
    {
        var referenced = typeof(Application.Services.MetadataService).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty);

        Assert.DoesNotContain(referenced, name => name.StartsWith(forbidden, System.StringComparison.OrdinalIgnoreCase));
    }
}
