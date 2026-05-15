using Npgsql;

namespace SIAD.Tests.Infrastructure;

public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected PostgresFixture Fixture { get; }
    protected NpgsqlConnection Connection { get; private set; } = null!;
    protected NpgsqlTransaction Transaction { get; private set; } = null!;
    protected long CompanyId => Fixture.CompanyId;

    protected IntegrationTestBase(PostgresFixture fixture)
    {
        Fixture = fixture;
    }

    public Task InitializeAsync()
    {
        Skip.IfNot(Fixture.Available, $"Falta env var {PostgresFixture.ConnectionStringEnvVar}. Test salteado.");

        Connection = Fixture.OpenConnection();
        Transaction = Connection.BeginTransaction();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (Transaction is not null)
        {
            await Transaction.RollbackAsync();
            await Transaction.DisposeAsync();
        }

        if (Connection is not null)
        {
            await Connection.DisposeAsync();
        }
    }
}
