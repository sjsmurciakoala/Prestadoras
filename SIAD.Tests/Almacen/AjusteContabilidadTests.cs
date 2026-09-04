using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Services.Almacen;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Almacen;

/// <summary>
/// F1 de la integración contable del almacén (docs/plans/2026-08-05-integracion-contable-almacen-diseno.md):
/// un ajuste de inventario, con el módulo ALMACEN activo, genera su partida de doble entrada
/// por el mismo motor que Caja/Ventas (sp_con_generar_comprobante_config), dentro de la misma
/// transacción del kardex y con anulación por reverso.
/// </summary>
[Collection("Postgres")]
public sealed class AjusteContabilidadTests : IntegrationTestBase, IAsyncLifetime
{
    private const string Modulo = "ALMACEN";
    private const string DocType = "AJUSTE";

    private SiadDbContext? _context;
    private AjusteInventarioService? _service;

    public AjusteContabilidadTests(PostgresFixture fixture) : base(fixture) { }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (Fixture.Available)
        {
            var options = new DbContextOptionsBuilder<SiadDbContext>().UseNpgsql(Connection).Options;
            var company = new TestCurrentCompanyService(CompanyId);
            _context = new SiadDbContext(options, company);
            _context.Database.UseTransaction(Transaction);
            var rollup = new ArticuloRollupService(_context);
            var motor = new InventarioPostingService(_context, company, rollup);
            _service = new AjusteInventarioService(_context, company, motor);
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    // ── Arrange ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Deja la integración contable lista para ALMACEN dentro de la transacción: perfil ERSAPS
    /// (llena la matriz de cuentas), flag activo_almacen, asiento del módulo (diario + tipo) y un
    /// período contable abierto que cubra hoy. Devuelve false si la BD de pruebas no tiene diario/tipo.
    /// </summary>
    private async Task<bool> ArrangeContabilidadAsync(bool activoAlmacen = true)
    {
        await Connection.ExecuteAsync(new CommandDefinition(
            "SELECT * FROM public.sp_con_aplicar_perfil_integracion(@CompanyId, 'ERSAPS', 'test-alm')",
            new { CompanyId }, Transaction));

        await Connection.ExecuteAsync(new CommandDefinition(@"
            UPDATE public.con_integracion_config
            SET encolar_sin_periodo = false, activo_almacen = @Activo
            WHERE company_id = @CompanyId",
            new { CompanyId, Activo = activoAlmacen }, Transaction));

        var asientoOk = await Connection.ExecuteScalarAsync<bool>(new CommandDefinition(@"
            INSERT INTO public.con_integracion_asiento (company_id, module, journal_id, type_id, created_by)
            SELECT @CompanyId, @Modulo,
                   (SELECT journal_id FROM public.con_diario WHERE company_id = @CompanyId AND is_active ORDER BY journal_id LIMIT 1),
                   (SELECT type_id FROM public.con_tipo_transaccion WHERE company_id = @CompanyId ORDER BY type_id LIMIT 1),
                   'test-alm'
            ON CONFLICT (company_id, module)
            DO UPDATE SET journal_id = EXCLUDED.journal_id, type_id = EXCLUDED.type_id
            RETURNING journal_id IS NOT NULL AND type_id IS NOT NULL",
            new { CompanyId, Modulo }, Transaction));
        if (!asientoOk)
        {
            return false;
        }

        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.con_periodo_contable
                (company_id, code, name, start_date, end_date, status_id, status, created_at, created_by)
            SELECT @CompanyId, 'ALM-TEST', 'Periodo test almacen',
                   current_date - 1, current_date + 1, 0, 'OPEN', now(), 'test-alm'
            WHERE NOT EXISTS (
                SELECT 1 FROM public.con_periodo_contable p
                WHERE p.company_id = @CompanyId AND COALESCE(p.status_id, 2) = 0
                  AND current_date BETWEEN p.start_date::date AND p.end_date::date)",
            new { CompanyId }, Transaction));

        return true;
    }

    private async Task<List<(long AccountId, string Code)>> CuentasPosteablesAsync(int n) =>
        (await Connection.QueryAsync<(long AccountId, string Code)>(new CommandDefinition(
            "SELECT account_id, code FROM public.con_plan_cuentas WHERE company_id = @CompanyId AND allows_posting ORDER BY account_id LIMIT @N",
            new { CompanyId, N = n }, Transaction))).ToList();

    /// <summary>Tipo con cuentas + artículo del tipo + bodega + ubicación con existencia/costo.</summary>
    private async Task<(int ArticuloId, int BodegaId)> SeedArticuloConTipoAsync(
        string ctaInventario, string ctaAjustes, decimal existencia, decimal costo)
    {
        var tipo = new alm_tipo_articulo
        {
            codigo = "TALM",
            nombre = "Tipo almacen test",
            cuenta_inventario = ctaInventario,
            cuenta_ajustes = ctaAjustes,
            maneja_inventario = true,
            activo = true
        };
        _context!.alm_tipo_articulos.Add(tipo);
        await _context.SaveChangesAsync();

        var bodega = new alm_bodega { codigo = "BALM", nombre = "Bodega alm test", activo = true };
        _context.alm_bodegas.Add(bodega);

        var articulo = new alm_articulo
        {
            codigo_articulo = "AALM01",
            descripcion = "Articulo alm test",
            tipo_articulo_id = tipo.id,
            existencia = existencia
        };
        _context.alm_articulos.Add(articulo);
        await _context.SaveChangesAsync();

        var fila = new alm_articulo_bodega
        {
            articulo_id = articulo.id,
            bodega_id = bodega.id,
            existencia = existencia,
            costo_promedio = costo,
            activo = true,
            principal = true
        };
        _context.alm_articulo_bodegas.Add(fila);
        await _context.SaveChangesAsync();

        return (articulo.id, bodega.id);
    }

    // ── Test ─────────────────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task AjusteEntrada_ConIntegracionActiva_GeneraPartidaBalanceada()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        Skip.IfNot(await ArrangeContabilidadAsync(), "Falta diario/tipo en la BD de pruebas.");

        var cuentas = await CuentasPosteablesAsync(2);
        Skip.If(cuentas.Count < 2, "La BD de pruebas no tiene 2 cuentas posteables.");
        var (ctaInvId, ctaInv) = cuentas[0];
        var (ctaAjId, ctaAj) = cuentas[1];

        var (articuloId, bodegaId) = await SeedArticuloConTipoAsync(ctaInv, ctaAj, existencia: 0m, costo: 0m);

        var dto = new AjusteInventarioDto
        {
            ArticuloId = articuloId,
            BodegaId = bodegaId,
            Clase = ClaseAjusteInventario.Entrada,
            Cantidad = 10m,
            CostoUnitario = 12m,
            Motivo = "Ajuste de entrada test",
            Fecha = DateOnly.FromDateTime(DateTime.Today)
        };

        var resultado = await _service!.CrearYPostearAsync(dto, "tester");

        var partida = await Connection.QueryFirstOrDefaultAsync<(long PolizaId, short Status, decimal Debe, decimal Haber)>(
            new CommandDefinition(@"
                SELECT h.poliza_id, h.status,
                       (SELECT COALESCE(SUM(d.debit_amount), 0) FROM public.con_partida_dtl d WHERE d.poliza_id = h.poliza_id),
                       (SELECT COALESCE(SUM(d.credit_amount), 0) FROM public.con_partida_dtl d WHERE d.poliza_id = h.poliza_id)
                FROM public.con_partida_hdr h
                WHERE h.company_id = @CompanyId AND h.module = @Modulo
                  AND h.document_type = @DocType AND h.document_id = @DocId",
            new { CompanyId, Modulo, DocType, DocId = (long)resultado.Id }, Transaction));

        Assert.NotEqual(0L, partida.PolizaId);        // se generó la partida
        Assert.Equal(1, partida.Status);              // posteada por el motor
        Assert.Equal(120m, partida.Debe);             // 10 u × 12
        Assert.Equal(partida.Debe, partida.Haber);    // balanceada

        // Entrada: Debe = cuenta de inventario; Haber = cuenta de ajustes.
        var debeCuenta = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT account_id FROM public.con_partida_dtl WHERE poliza_id = @P AND debit_amount > 0",
            new { P = partida.PolizaId }, Transaction));
        var haberCuenta = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT account_id FROM public.con_partida_dtl WHERE poliza_id = @P AND credit_amount > 0",
            new { P = partida.PolizaId }, Transaction));
        Assert.Equal(ctaInvId, debeCuenta);
        Assert.Equal(ctaAjId, haberCuenta);
    }

    [SkippableFact]
    public async Task AjusteSalida_ConIntegracionActiva_DebeAjustesHaberInventario()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        Skip.IfNot(await ArrangeContabilidadAsync(), "Falta diario/tipo en la BD de pruebas.");

        var cuentas = await CuentasPosteablesAsync(2);
        Skip.If(cuentas.Count < 2, "La BD de pruebas no tiene 2 cuentas posteables.");
        var (ctaInvId, ctaInv) = cuentas[0];
        var (ctaAjId, ctaAj) = cuentas[1];

        // Existencia y costo previos: la salida se valoriza al promedio vigente (5).
        var (articuloId, bodegaId) = await SeedArticuloConTipoAsync(ctaInv, ctaAj, existencia: 20m, costo: 5m);

        var dto = new AjusteInventarioDto
        {
            ArticuloId = articuloId,
            BodegaId = bodegaId,
            Clase = ClaseAjusteInventario.Salida,
            Cantidad = 4m,
            Motivo = "Ajuste de salida test",
            Fecha = DateOnly.FromDateTime(DateTime.Today)
        };

        var resultado = await _service!.CrearYPostearAsync(dto, "tester");

        var partida = await Connection.QueryFirstOrDefaultAsync<(long PolizaId, short Status, decimal Debe, decimal Haber)>(
            new CommandDefinition(@"
                SELECT h.poliza_id, h.status,
                       (SELECT COALESCE(SUM(d.debit_amount), 0) FROM public.con_partida_dtl d WHERE d.poliza_id = h.poliza_id),
                       (SELECT COALESCE(SUM(d.credit_amount), 0) FROM public.con_partida_dtl d WHERE d.poliza_id = h.poliza_id)
                FROM public.con_partida_hdr h
                WHERE h.company_id = @CompanyId AND h.module = @Modulo
                  AND h.document_type = @DocType AND h.document_id = @DocId",
            new { CompanyId, Modulo, DocType, DocId = (long)resultado.Id }, Transaction));

        Assert.NotEqual(0L, partida.PolizaId);
        Assert.Equal(1, partida.Status);
        Assert.Equal(20m, partida.Debe);              // 4 u × 5 (promedio vigente)
        Assert.Equal(partida.Debe, partida.Haber);

        // Salida: Debe = cuenta de ajustes; Haber = cuenta de inventario (espejo de la entrada).
        var debeCuenta = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT account_id FROM public.con_partida_dtl WHERE poliza_id = @P AND debit_amount > 0",
            new { P = partida.PolizaId }, Transaction));
        var haberCuenta = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT account_id FROM public.con_partida_dtl WHERE poliza_id = @P AND credit_amount > 0",
            new { P = partida.PolizaId }, Transaction));
        Assert.Equal(ctaAjId, debeCuenta);
        Assert.Equal(ctaInvId, haberCuenta);
    }

    [SkippableFact]
    public async Task ModuloInactivo_PosteaKardexPeroNoGeneraPartida()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        Skip.IfNot(await ArrangeContabilidadAsync(activoAlmacen: false), "Falta diario/tipo en la BD de pruebas.");

        var cuentas = await CuentasPosteablesAsync(2);
        Skip.If(cuentas.Count < 2, "La BD de pruebas no tiene 2 cuentas posteables.");
        var (articuloId, bodegaId) = await SeedArticuloConTipoAsync(cuentas[0].Code, cuentas[1].Code, existencia: 0m, costo: 0m);

        var dto = new AjusteInventarioDto
        {
            ArticuloId = articuloId,
            BodegaId = bodegaId,
            Clase = ClaseAjusteInventario.Entrada,
            Cantidad = 10m,
            CostoUnitario = 12m,
            Motivo = "Ajuste con modulo apagado",
            Fecha = DateOnly.FromDateTime(DateTime.Today)
        };

        var resultado = await _service!.CrearYPostearAsync(dto, "tester");

        Assert.True(resultado.Posteado);  // el ajuste SÍ se aplicó al kardex

        var partidas = await Connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
            SELECT COUNT(*) FROM public.con_partida_hdr
            WHERE company_id = @CompanyId AND module = @Modulo
              AND document_type = @DocType AND document_id = @DocId",
            new { CompanyId, Modulo, DocType, DocId = (long)resultado.Id }, Transaction));
        Assert.Equal(0, partidas);        // pero NO generó partida (módulo apagado)
    }

    [SkippableFact]
    public async Task AjusteValor_SubeCosto_DebeInventarioHaberAjustesPorElDelta()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        Skip.IfNot(await ArrangeContabilidadAsync(), "Falta diario/tipo en la BD de pruebas.");

        var cuentas = await CuentasPosteablesAsync(2);
        Skip.If(cuentas.Count < 2, "La BD de pruebas no tiene 2 cuentas posteables.");
        var (ctaInvId, ctaInv) = cuentas[0];
        var (ctaAjId, ctaAj) = cuentas[1];

        // 10 u a costo 5 (valor 50). Se corrige el costo a 8 → valor 80 → delta +30.
        var (articuloId, bodegaId) = await SeedArticuloConTipoAsync(ctaInv, ctaAj, existencia: 10m, costo: 5m);

        var dto = new AjusteInventarioDto
        {
            ArticuloId = articuloId,
            BodegaId = bodegaId,
            Clase = ClaseAjusteInventario.Valor,
            Cantidad = 0m,
            CostoUnitario = 8m,
            Motivo = "Ajuste de valor test",
            Fecha = DateOnly.FromDateTime(DateTime.Today)
        };

        var resultado = await _service!.CrearYPostearAsync(dto, "tester");

        var partida = await Connection.QueryFirstOrDefaultAsync<(long PolizaId, short Status, decimal Debe, decimal Haber)>(
            new CommandDefinition(@"
                SELECT h.poliza_id, h.status,
                       (SELECT COALESCE(SUM(d.debit_amount), 0) FROM public.con_partida_dtl d WHERE d.poliza_id = h.poliza_id),
                       (SELECT COALESCE(SUM(d.credit_amount), 0) FROM public.con_partida_dtl d WHERE d.poliza_id = h.poliza_id)
                FROM public.con_partida_hdr h
                WHERE h.company_id = @CompanyId AND h.module = @Modulo
                  AND h.document_type = @DocType AND h.document_id = @DocId",
            new { CompanyId, Modulo, DocType, DocId = (long)resultado.Id }, Transaction));

        Assert.NotEqual(0L, partida.PolizaId);
        Assert.Equal(1, partida.Status);
        Assert.Equal(30m, partida.Debe);              // 10 u × (8 − 5)
        Assert.Equal(partida.Debe, partida.Haber);

        // Sube el valor del inventario: Debe = inventario; Haber = ajustes.
        var debeCuenta = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT account_id FROM public.con_partida_dtl WHERE poliza_id = @P AND debit_amount > 0",
            new { P = partida.PolizaId }, Transaction));
        var haberCuenta = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT account_id FROM public.con_partida_dtl WHERE poliza_id = @P AND credit_amount > 0",
            new { P = partida.PolizaId }, Transaction));
        Assert.Equal(ctaInvId, debeCuenta);
        Assert.Equal(ctaAjId, haberCuenta);
    }

    private sealed class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
