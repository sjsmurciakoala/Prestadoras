using Npgsql;

namespace SIAD.Tests.Infrastructure;

public sealed class PostgresFixture : IDisposable
{
    public const string ConnectionStringEnvVar = "SIAD_TEST_DB";
    public const string CompanyIdEnvVar = "SIAD_TEST_COMPANY_ID";

    public string? ConnectionString { get; }
    public long CompanyId { get; }
    public bool Available => !string.IsNullOrWhiteSpace(ConnectionString);

    public PostgresFixture()
    {
        ConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvVar);
        CompanyId = long.TryParse(Environment.GetEnvironmentVariable(CompanyIdEnvVar), out var id) ? id : 2L;

        if (Available)
        {
            using var probe = new NpgsqlConnection(ConnectionString);
            probe.Open();
            using var cmd = new NpgsqlCommand("SELECT 1", probe);
            cmd.ExecuteScalar();
        }
    }

    public NpgsqlConnection OpenConnection()
    {
        if (!Available)
        {
            throw new InvalidOperationException(
                $"Falta env var {ConnectionStringEnvVar}. Configura una cadena Npgsql apuntando a una BD de prueba.");
        }

        var conn = new NpgsqlConnection(ConnectionString);
        conn.Open();
        return conn;
    }

    public void Dispose()
    {
    }
}

[CollectionDefinition("Postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
}
