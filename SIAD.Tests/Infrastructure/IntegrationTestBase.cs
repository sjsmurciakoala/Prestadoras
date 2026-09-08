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

    /// <summary>
    /// Apaga el control presupuestario de la empresa de prueba, dentro de la transacción del test.
    /// <para>
    /// <b>Por qué hace falta:</b> <c>cfg_presupuesto_control</c> es estado GLOBAL de la base de
    /// prueba, no del test. Si alguien deja el control encendido en el mirror —por una demo, un
    /// piloto o una corrida manual—, cualquier test que registre una factura o apruebe una orden
    /// contra una cuenta presupuestada empieza a fallar con «excede el presupuesto disponible»,
    /// aunque no tenga nada que ver con presupuesto.
    /// </para>
    /// <para>
    /// Lo llaman los tests que ejercitan la MECÁNICA de compras (kardex, CxP, correlativo,
    /// anulación). Los que sí prueban el control lo encienden ellos mismos después.
    /// </para>
    /// </summary>
    protected async Task DesactivarControlPresupuestarioAsync()
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = "UPDATE public.cfg_presupuesto_control SET modo = 0 WHERE company_id = @c;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Borra la configuración de aprobación por niveles de la empresa de prueba y deja los cuatro
    /// documentos en su estado de fábrica (control apagado, sin autoaprobación), dentro de la
    /// transacción del test.
    /// <para>
    /// <b>Por qué hace falta:</b> <c>cfg_aprobacion_control</c>, <c>cfg_aprobacion_nivel</c> y
    /// <c>cfg_aprobacion_aprobador</c> son estado GLOBAL de la base de prueba, igual que el control
    /// presupuestario. El 2026-09-01 se encendió la escalera de <c>COMPRAS_OC</c> desde el portal y
    /// se cargaron cuatro niveles en el mirror; desde entonces <c>AprobarAsync</c> derivaba a la
    /// firma por niveles y 89 pruebas se caían: unas con «la orden debe enviarse a aprobación antes
    /// de poder firmarse», otras al chocar con <c>uq_cfg_aprobacion_nivel</c> al sembrar sus propios
    /// tramos sobre los que ya estaban.
    /// </para>
    /// <para>
    /// Lo llaman los dos bandos: los tests que ejercitan la MECÁNICA de compras, que necesitan la
    /// escalera apagada, y los de aprobación, que parten de cero y siembran su propia configuración.
    /// </para>
    /// </summary>
    protected async Task LimpiarAprobacionPorNivelesAsync()
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
DELETE FROM public.cfg_aprobacion_aprobador WHERE company_id = @c;
DELETE FROM public.cfg_aprobacion_nivel     WHERE company_id = @c;
UPDATE public.cfg_aprobacion_control
   SET modo = 0, permite_autoaprobacion = false
 WHERE company_id = @c;";
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
