using DevExpress.DataAccess.ConnectionParameters;
using DevExpress.DataAccess.Sql;
using DevExpress.DataAccess.Web;
using DevExpress.DataAccess.Wizard.Services;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace SIAD.Reports;

internal static class ReportingPostgreSqlConnectionResolver
{
    public static DataConnectionParametersBase Resolve(IConfiguration configuration, string connectionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);

        var connectionString = configuration.GetConnectionString(connectionName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new KeyNotFoundException($"Connection string '{connectionName}' not found.");
        }

        if (connectionString.Contains("XpoProvider=", StringComparison.OrdinalIgnoreCase))
        {
            return new CustomStringConnectionParameters(connectionString);
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        return new PostgreSqlConnectionParameters
        {
            ServerName = builder.Host,
            PortNumber = builder.Port > 0 ? builder.Port : 5432,
            DatabaseName = builder.Database,
            UserName = builder.Username,
            Password = builder.Password
        };
    }

    public static Dictionary<string, string> GetConnectionDescriptions(IConfiguration configuration)
        => configuration
            .GetSection("ConnectionStrings")
            .GetChildren()
            .Where(section => !string.IsNullOrWhiteSpace(section.Key) && !string.IsNullOrWhiteSpace(section.Value))
            .ToDictionary(section => section.Key, section => section.Key, StringComparer.OrdinalIgnoreCase);
}

public sealed class ReportingConnectionProviderFactory : IConnectionProviderFactory
{
    private readonly IConnectionProviderService _connectionProviderService;

    public ReportingConnectionProviderFactory(IConnectionProviderService connectionProviderService)
    {
        _connectionProviderService = connectionProviderService;
    }

    public IConnectionProviderService Create() => _connectionProviderService;
}

public sealed class ReportingConnectionProviderService : IConnectionProviderService
{
    private readonly IConfiguration _configuration;

    public ReportingConnectionProviderService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public SqlDataConnection LoadConnection(string connectionName)
        => new(connectionName, ReportingPostgreSqlConnectionResolver.Resolve(_configuration, connectionName));
}

public sealed class ReportingDataSourceWizardConnectionStringsProvider : IDataSourceWizardConnectionStringsProvider
{
    private readonly IConfiguration _configuration;

    public ReportingDataSourceWizardConnectionStringsProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Dictionary<string, string> GetConnectionDescriptions()
        => ReportingPostgreSqlConnectionResolver.GetConnectionDescriptions(_configuration);

    public DataConnectionParametersBase GetDataConnectionParameters(string name)
    {
        // Keep only the connection name in saved layouts. Runtime resolution happens via IConnectionProviderService.
        return null!;
    }
}
