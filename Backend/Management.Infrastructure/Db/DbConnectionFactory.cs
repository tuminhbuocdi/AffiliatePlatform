using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace Management.Infrastructure.Db;

public class DbConnectionFactory
{
    private readonly IConfiguration _config;

    public DbConnectionFactory(IConfiguration config)
    {
        _config = config;
    }

    public IDbConnection Create(string connectionName = "Default")
    {
        var connStr = _config.GetConnectionString(connectionName);
        if (string.IsNullOrWhiteSpace(connStr))
        {
            throw new InvalidOperationException($"Missing connection string: ConnectionStrings:{connectionName}");
        }

        // Add connection pooling settings to prevent connection leaks
        var connectionStringBuilder = new SqlConnectionStringBuilder(connStr)
        {
            // Connection pooling settings
            MaxPoolSize = 100,              // Maximum connections in pool
            MinPoolSize = 5,                // Minimum connections in pool
            Pooling = true,                 // Enable connection pooling
            ConnectTimeout = 30,            // Connection timeout in seconds (Connect Timeout)
            CommandTimeout = 300,           // Command timeout in seconds (5 minutes)
            ConnectRetryCount = 3,         // Number of retry attempts
            ConnectRetryInterval = 10       // Seconds between retry attempts
        };

        return new SqlConnection(connectionStringBuilder.ToString());
    }
}
