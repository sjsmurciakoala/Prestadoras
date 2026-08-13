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

    /// <summary>
    /// Apaga la integración contable (almacén, proveedores, compras) de la empresa de prueba, dentro
    /// de la transacción del test. Para tests que verifican la MECÁNICA (kardex, existencia, costo,
    /// idempotencia, anulación) y NO la contabilidad: así no dependen de que los flags estén encendidos
    /// en la base de prueba (en el mirror lo están). Los tests que SÍ verifican pólizas (p. ej.
    /// AjusteContabilidadTests, o los de Fase 2 de compras) siembran sus cuentas y encienden el módulo
    /// que corresponda por su cuenta.
    /// </summary>
    protected async Task DesactivarIntegracionContableAsync()
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText =
            "UPDATE public.con_integracion_config SET activo_almacen = false, activo_proveedores = false, activo_compras = false WHERE company_id = @c;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        await cmd.ExecuteNonQueryAsync();
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
