using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.DTOs.Presupuesto;
using SIAD.Core.Tenancy;
using SIAD.Services.Presupuesto;
using SIAD.Data;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Presupuesto;

/// <summary>
/// Motor compartido del ejecutado (fase F8): el que usan los compromisos a proveedor y los créditos
/// bancarios para consumir <c>valor_real</c> sin pasar por un compromiso previo.
/// <para>
/// Antes de F8 esta ruta <b>no tenía ninguna prueba</b>, no tomaba lock y no dejaba rastro. Estos
/// tests fijan las tres cosas que importan: que sigue validando igual con el módulo apagado, que
/// descuenta el comprometido al encenderlo, y que ahora escribe kardex.
/// </para>
/// </summary>
[Collection("Postgres")]
public class AfectacionEjecutadoTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private PresupuestoCompromisoService? _presupuesto;

    private const string Cuenta = "TEST-PST-F8";
    private const string Presupuesto = "TST-F8";   // id_presupuesto es VARCHAR(10)
    private const string Modulo = "PROVEEDORES";
    private const string DocumentoTipo = "COMPROMISO_PRV";

    public AfectacionEjecutadoTests(PostgresFixture fixture) : base(fixture) { }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (!Fixture.Available) return;

        var options = new DbContextOptionsBuilder<SiadDbContext>().UseNpgsql(Connection).Options;
        _context = new SiadDbContext(options, new TestCurrentCompanyService(CompanyId));
        _context.Database.UseTransaction(Transaction);
        _presupuesto = new PresupuestoCompromisoService(_context, new TestCurrentCompanyService(CompanyId));

        await SembrarAsync();
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    // ── No-regresión: el módulo apagado valida como siempre ──────────────────

    [SkippableFact]
    public async Task ModuloApagado_ValidaContraProyeccionMenosEjecutado_ComoAntesDeF8()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // Hay 400 comprometidos por compras, pero el módulo está apagado: la base sigue siendo
        // proyección − ejecutado = 1000. Un consumo de 800 pasa, igual que antes de F8.
        await ComprometerDesdeComprasAsync(400m);
        await FijarModoAsync(0);

        await AfectarAsync(800m, direccion: 1);

        Assert.Equal(800m, await EjecutadoAsync());
    }

    [SkippableFact]
    public async Task ModuloApagado_SigueRechazandoLoQueExcedeLaProyeccion()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await FijarModoAsync(0);

        var ex = await EsperarRechazoAsync(() => AfectarAsync(1500m, direccion: 1));

        Assert.Contains("excede el presupuesto disponible", ex.Message);
        Assert.Equal(0m, await EjecutadoAsync());
    }

    // ── Encendido: el comprometido cuenta ────────────────────────────────────

    [SkippableFact]
    public async Task ModuloEncendido_DescuentaTambienLoComprometidoPorCompras()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await ComprometerDesdeComprasAsync(400m);
        await FijarModoAsync(2);

        // Con el módulo encendido la base pasa a 1000 − 400 = 600, así que 800 ya no cabe.
        var ex = await EsperarRechazoAsync(() => AfectarAsync(800m, direccion: 1));

        Assert.Contains("Disponible: 600.00", ex.Message);
        Assert.Equal(0m, await EjecutadoAsync());

        // Lo que sí cabe en 600 pasa sin problema.
        await AfectarAsync(600m, direccion: 1);
        Assert.Equal(600m, await EjecutadoAsync());
    }

    // ── Kardex: lo que antes no dejaba rastro ────────────────────────────────

    [SkippableFact]
    public async Task Afectar_DejaRastroEnElKardex()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await AfectarAsync(250m, direccion: 1);

        var movimientos = await MovimientosAsync();
        var mov = Assert.Single(movimientos);
        Assert.Equal(14, mov.Tipo);                     // Ejecución de compromiso a proveedor
        Assert.Equal(250m, mov.Monto);
        Assert.Equal(0m, mov.EjecutadoAnterior);
        Assert.Equal(250m, mov.EjecutadoPosterior);
        Assert.Equal("tester", mov.Usuario);
    }

    [SkippableFact]
    public async Task Revertir_DevuelveElEjecutado_YQuedaEnElKardex()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await AfectarAsync(250m, direccion: 1);
        await AfectarAsync(250m, direccion: -1);

        Assert.Equal(0m, await EjecutadoAsync());
        Assert.Equal(2, (await MovimientosAsync()).Count);
    }

    /// <summary>
    /// El flujo de edición de OPD revierte el importe anterior y aplica el nuevo sobre el MISMO
    /// documento. Si los tipos 14/15 estuvieran dentro del índice de idempotencia, la segunda
    /// aplicación se descartaría en silencio y el presupuesto quedaría desincronizado.
    /// </summary>
    [SkippableFact]
    public async Task EditarDosVecesElMismoDocumento_AplicaLosDosCambios()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await AfectarAsync(100m, direccion: 1);    // alta
        await AfectarAsync(100m, direccion: -1);   // edición: revierte
        await AfectarAsync(300m, direccion: 1);    // edición: aplica el nuevo importe

        Assert.Equal(300m, await EjecutadoAsync());
        Assert.Equal(3, (await MovimientosAsync()).Count);
    }

    // ── La cabecera ya no queda inflada ──────────────────────────────────────

    [SkippableFact]
    public async Task LaCabecera_DescuentaComprometidoYEjecutado()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await ComprometerDesdeComprasAsync(400m);
        await AfectarAsync(250m, direccion: 1);

        // valor_global 20000 − comprometido 400 − ejecutado 250 = 19350.
        // Antes de F8 la cabecera se recalculaba ignorando el comprometido y daba 19750.
        Assert.Equal(19350m, await DisponibleCabeceraAsync());
    }

    [SkippableFact]
    public async Task CuentaSinMarcaDePresupuesto_SeIgnora()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await EjecutarAsync(
            "UPDATE public.con_plan_cuentas SET allows_budget = FALSE WHERE company_id = @c AND code = @code;",
            ("c", CompanyId), ("code", Cuenta));

        await AfectarAsync(99_999m, direccion: 1);

        Assert.Equal(0m, await EjecutadoAsync());
        Assert.Empty(await MovimientosAsync());
    }

    // ── Utilería ─────────────────────────────────────────────────────────────

    private Task AfectarAsync(decimal monto, short direccion)
        => _presupuesto!.AfectarEjecutadoAsync(
            Modulo, DocumentoTipo, 90001, "90001",
            DateOnly.FromDateTime(DateTime.Today), "tester", direccion, exigeAprobado: true,
            new List<(string, decimal)> { (Cuenta, monto) });

    /// <summary>Simula lo que deja una O/C aprobada, sin montar toda la orden.</summary>
    private Task ComprometerDesdeComprasAsync(decimal monto) => EjecutarAsync(@"
UPDATE public.pst_config_presupuesto_dtl
   SET valor_comprometido = @m,
       valor_disponible = GREATEST(valor_proyeccion - @m - valor_real, 0)
 WHERE company_id = @c AND id_presupuesto = @p AND con_cuenta_code = @cta;",
        ("m", monto), ("c", CompanyId), ("p", Presupuesto), ("cta", Cuenta));

    private Task FijarModoAsync(short modo) => EjecutarAsync(@"
INSERT INTO public.cfg_presupuesto_control (company_id, modulo, modo)
VALUES (@c, @mod, @m)
ON CONFLICT (company_id, modulo) DO UPDATE SET modo = EXCLUDED.modo;",
        ("c", CompanyId), ("mod", Modulo), ("m", (int)modo));

    private async Task SembrarAsync()
    {
        await EjecutarAsync(@"
INSERT INTO public.pst_config_presupuesto_hdr
       (company_id, id_presupuesto, valor_global, valor_disponible, valor_comprometido,
        rango_periodo, fecha_inicia, fecha_finaliza, estado_aprobado)
VALUES (@c, @p, 20000, 20000, 0, 12,
        make_date(EXTRACT(YEAR FROM CURRENT_DATE)::int, 1, 1),
        make_date(EXTRACT(YEAR FROM CURRENT_DATE)::int, 12, 31), TRUE)
ON CONFLICT DO NOTHING;", ("c", CompanyId), ("p", Presupuesto));

        await EjecutarAsync(@"
INSERT INTO public.con_plan_cuentas (account_id, company_id, code, name, account_type, level,
                                     allows_posting, allows_budget, status, created_at, created_by)
SELECT (SELECT COALESCE(MAX(account_id), 0) + 1 FROM public.con_plan_cuentas),
       @c, @code, 'Cuenta de prueba F8', 'GASTO', 1, TRUE, TRUE, 'A', now(), 'tester'
 WHERE NOT EXISTS (SELECT 1 FROM public.con_plan_cuentas WHERE company_id = @c AND code = @code);",
            ("c", CompanyId), ("code", Cuenta));

        await EjecutarAsync(
            "UPDATE public.con_plan_cuentas SET allows_budget = TRUE WHERE company_id = @c AND code = @code;",
            ("c", CompanyId), ("code", Cuenta));

        await EjecutarAsync(@"
INSERT INTO public.pst_config_presupuesto_dtl
       (company_id, id_presupuesto, con_cuenta_code, id_presupuesto_dtl,
        valor_proyeccion, valor_real, valor_comprometido, valor_pagado, valor_disponible)
VALUES (@c, @p, @cuenta,
        (SELECT COALESCE(MAX(id_presupuesto_dtl), 0) + 1 FROM public.pst_config_presupuesto_dtl),
        1000, 0, 0, 0, 1000)
ON CONFLICT DO NOTHING;", ("c", CompanyId), ("p", Presupuesto), ("cuenta", Cuenta));

        await FijarModoAsync(0);   // como nace en producción
    }

    private Task<decimal> EjecutadoAsync() => EscalarAsync<decimal>(
        "SELECT COALESCE(valor_real,0) FROM public.pst_config_presupuesto_dtl WHERE company_id=@c AND id_presupuesto=@p AND con_cuenta_code=@cta",
        ("c", CompanyId), ("p", Presupuesto), ("cta", Cuenta));

    private Task<decimal> DisponibleCabeceraAsync() => EscalarAsync<decimal>(
        "SELECT COALESCE(valor_disponible,0) FROM public.pst_config_presupuesto_hdr WHERE company_id=@c AND id_presupuesto=@p",
        ("c", CompanyId), ("p", Presupuesto));

    private sealed record Movimiento(short Tipo, decimal Monto, decimal EjecutadoAnterior, decimal EjecutadoPosterior, string Usuario);

    private async Task<List<Movimiento>> MovimientosAsync()
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = @"
SELECT tipo_movimiento, monto, ejecutado_anterior, ejecutado_posterior, usuario
  FROM public.pst_movimiento
 WHERE company_id = @c AND id_presupuesto = @p
 ORDER BY id;";
        cmd.Parameters.AddWithValue("c", CompanyId);
        cmd.Parameters.AddWithValue("p", Presupuesto);

        var filas = new List<Movimiento>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            filas.Add(new Movimiento(r.GetInt16(0), r.GetDecimal(1), r.GetDecimal(2), r.GetDecimal(3), r.GetString(4)));
        }
        return filas;
    }

    /// <summary>Ver CompromisoOrdenCompraTests: el rechazo aborta la transacción del test.</summary>
    private async Task<InvalidOperationException> EsperarRechazoAsync(Func<Task> accion)
    {
        await Transaction.SaveAsync("antes_del_rechazo");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(accion);
        await Transaction.RollbackAsync("antes_del_rechazo");
        _context!.ChangeTracker.Clear();
        return ex;
    }

    private async Task EjecutarAsync(string sql, params (string Nombre, object Valor)[] parametros)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = sql;
        foreach (var (nombre, valor) in parametros) cmd.Parameters.AddWithValue(nombre, valor);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<T> EscalarAsync<T>(string sql, params (string Nombre, object Valor)[] parametros)
    {
        await using var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = sql;
        foreach (var (nombre, valor) in parametros) cmd.Parameters.AddWithValue(nombre, valor);
        var leido = await cmd.ExecuteScalarAsync();
        return leido is null or DBNull ? default! : (T)Convert.ChangeType(leido, typeof(T));
    }

    private sealed class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
