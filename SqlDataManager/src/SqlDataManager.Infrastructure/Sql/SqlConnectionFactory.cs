using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SqlDataManager.Application.Abstractions;
using SqlDataManager.Application.Configuration;

namespace SqlDataManager.Infrastructure.Sql;

/// <summary>
/// Builds and opens SQL Server connections from the module configuration. The
/// connection string is assembled here from discrete, validated options and is
/// never surfaced anywhere outside this class.
/// </summary>
public sealed class SqlConnectionFactory(IOptions<SqlDataManagerOptions> options) : IDatabaseConnectivity
{
    private readonly ConnectionOptions _connection = options.Value.Connection;

    public string DatabaseDisplayName => _connection.Database;

    /// <summary>The command timeout to apply to every command (seconds).</summary>
    public int CommandTimeoutSeconds => _connection.CommandTimeoutSeconds;

    /// <summary>Assembles the connection string from configuration.</summary>
    public string BuildConnectionString()
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = _connection.Server,
            InitialCatalog = _connection.Database,
            Encrypt = _connection.Encrypt,
            TrustServerCertificate = _connection.TrustServerCertificate,
            ConnectTimeout = _connection.ConnectionTimeoutSeconds,
            ApplicationName = _connection.ApplicationName,
            // Connection pooling is essential for the concurrent-user target.
            Pooling = true
        };

        if (string.Equals(_connection.AuthenticationType, "Windows", StringComparison.OrdinalIgnoreCase))
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.UserID = _connection.Username;
            builder.Password = _connection.Password;
        }

        return builder.ConnectionString;
    }

    /// <summary>Opens a new connection asynchronously.</summary>
    public async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(BuildConnectionString());
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    /// <summary>Lightweight connectivity probe used by the health check.</summary>
    public async Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            command.CommandTimeout = _connection.CommandTimeoutSeconds;
            _ = await command.ExecuteScalarAsync(cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
