using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Services.Almacen;
using SIAD.Data;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Almacen;

/// <summary>
/// Flujo de la requisición-documento (Fase 6.2). La regla que se vigila sobre todo: la requisición
/// es una SOLICITUD y <b>NUNCA genera asientos en el kardex</b> (la salida la produce el descargo).
/// <para>Requiere <c>2026-08-01_alm_requisicion_descargo.sql</c> (paso 26) aplicado en <c>SIAD_TEST_DB</c>.</para>
/// </summary>
[Collection("Postgres")]
public class RequisicionFlujoTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private RequisicionDocumentoService? _service;

    public RequisicionFlujoTests(PostgresFixture fixture) : base(fixture) { }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (Fixture.Available)
        {
            var options = new DbContextOptionsBuilder<SiadDbContext>().UseNpgsql(Connection).Options;
            var company = new TestCurrentCompanyService(CompanyId);
            _context = new SiadDbContext(options, company);
            _context.Database.UseTransaction(Transaction);
            _service = new RequisicionDocumentoService(_context, company);
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    // ── Seeds ────────────────────────────────────────────────────────────────

    private async Task<int> SeedBodegaAsync(string codigo)
    {
        var b = new alm_bodega { codigo = codigo, nombre = $"Bodega {codigo}", activo = true };
        _context!.alm_bodegas.Add(b);
        await _context.SaveChangesAsync();
        return b.id;
    }

    // Crear una requisición exige que cada artículo esté ASIGNADO a la bodega: una fila activa en
    // alm_articulo_bodega (aunque la existencia sea 0 — la requisición es solicitud, no salida).
    private async Task<int> SeedArticuloAsync(string codigo, int bodegaId)
    {
        var a = new alm_articulo { codigo_articulo = codigo, descripcion = $"Artículo {codigo}", existencia = 0m, activo = true };
        _context!.alm_articulos.Add(a);
        await _context.SaveChangesAsync();
        _context.alm_articulo_bodegas.Add(new alm_articulo_bodega
        {
            articulo_id = a.id, bodega_id = bodegaId, existencia = 0m,
            costo_promedio = 0m, activo = true, principal = true
        });
        await _context.SaveChangesAsync();
        return a.id;
    }

    private static RequisicionDocumentoDto Nueva(int bodega, string solicitante = "Juan Pérez",
        params (int art, decimal cant, decimal precio)[] lineas) => new()
    {
        BodegaId = bodega,
        Solicitante = solicitante,
        Departamento = "OPE",
        Aplicacion = "consumo",
        Uuid = Guid.NewGuid(),
        Detalles = lineas.Select(l => new RequisicionDocumentoDetalleDto
        {
            ArticuloId = l.art, Cantidad = l.cant, PrecioUnitario = l.precio, Descripcion = "renglón"
        }).ToList()
    };

    private Task<int> AsientosDeAsync(int articuloId)
        => _context!.alm_kardexs.AsNoTracking().CountAsync(k => k.articulo_id == articuloId);

    // ── 1 — la regla estructural: crear NO postea ─────────────────────────────
    [SkippableFact]
    public async Task Crear_QuedaEnBorrador_YNoGeneraAsientos()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var bod = await SeedBodegaAsync("ZZRQ1");
        var a1 = await SeedArticuloAsync("ZZRQ1-A", bod);
        var a2 = await SeedArticuloAsync("ZZRQ1-B", bod);

        var doc = await _service!.CrearAsync(Nueva(bod, "Ana", (a1, 5m, 10m), (a2, 3m, 20m)), "tester");

        Assert.Equal(EstadoRequisicionHdr.Borrador, doc.Estado);
        Assert.True(doc.Numero > 0);
        Assert.Equal(2, doc.Detalles.Count);
        Assert.Equal(110m, doc.Total);   // 5*10 + 3*20
        // La requisición NUNCA asienta: 0 movimientos de kardex para ambos artículos.
        Assert.Equal(0, await AsientosDeAsync(a1));
        Assert.Equal(0, await AsientosDeAsync(a2));

        // Las líneas viven en la plana con origen SIAD, sin postear, enlazadas a la cabecera.
        var lineas = await _context!.alm_requisicions.AsNoTracking()
            .Where(l => l.requisicion_hdr_id == doc.Id).ToListAsync();
        Assert.Equal(2, lineas.Count);
        Assert.All(lineas, l => Assert.Equal(OrigenDocumento.Siad, l.origen));
        Assert.All(lineas, l => Assert.False(l.posteado));
        Assert.All(lineas, l => Assert.Equal(0m, l.cantidad_despachada));
    }

    // ── 2 — guardas de alta ───────────────────────────────────────────────────
    [SkippableFact]
    public async Task Crear_SinRenglones_Rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var bod = await SeedBodegaAsync("ZZRQ2");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service!.CrearAsync(Nueva(bod, "Ana"), "tester"));
        Assert.Contains("al menos un renglón", ex.Message);
    }

    [SkippableFact]
    public async Task Crear_SinSolicitante_Rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var bod = await SeedBodegaAsync("ZZRQ2b");
        var a1 = await SeedArticuloAsync("ZZRQ2b-A", bod);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service!.CrearAsync(Nueva(bod, "  ", (a1, 1m, 1m)), "tester"));
        Assert.Contains("solicitante", ex.Message);
    }

    // ── 3 — editar borrador reemplaza renglones ───────────────────────────────
    [SkippableFact]
    public async Task EditarBorrador_ReemplazaRenglones()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var bod = await SeedBodegaAsync("ZZRQ3");
        var a1 = await SeedArticuloAsync("ZZRQ3-A", bod);
        var a2 = await SeedArticuloAsync("ZZRQ3-B", bod);
        var doc = await _service!.CrearAsync(Nueva(bod, "Ana", (a1, 5m, 10m)), "tester");

        var editar = new RequisicionDocumentoDto
        {
            BodegaId = bod, Solicitante = "Ana",
            Detalles = [new() { ArticuloId = a2, Cantidad = 7m, PrecioUnitario = 4m }]
        };
        var actualizado = await _service.ActualizarAsync(doc.Id, editar, "tester");

        Assert.Single(actualizado.Detalles);
        Assert.Equal(a2, actualizado.Detalles[0].ArticuloId);
        Assert.Equal(28m, actualizado.Total);
        // No quedaron líneas huérfanas del renglón viejo.
        Assert.Equal(1, await _context!.alm_requisicions.AsNoTracking().CountAsync(l => l.requisicion_hdr_id == doc.Id));
    }

    // ── 4 — enviar + aprobar sella al aprobador ───────────────────────────────
    [SkippableFact]
    public async Task EnviarYAprobar_SellaAprobador()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var bod = await SeedBodegaAsync("ZZRQ4");
        var a1 = await SeedArticuloAsync("ZZRQ4-A", bod);
        var doc = await _service!.CrearAsync(Nueva(bod, "Ana", (a1, 5m, 10m)), "solicitante1");

        var enrev = await _service.EnviarARevisionAsync(doc.Id, "solicitante1");
        Assert.Equal(EstadoRequisicionHdr.EnRevision, enrev.Estado);

        var aprob = await _service.AprobarAsync(doc.Id, "jefe1");
        Assert.Equal(EstadoRequisicionHdr.Aprobada, aprob.Estado);
        Assert.False(string.IsNullOrWhiteSpace(aprob.AprobadoPor));
        Assert.NotNull(aprob.FechaAprobacion);
        Assert.Equal(0, await AsientosDeAsync(a1));   // aprobar tampoco postea
    }

    // ── 5 — rechazar exige motivo y sella ─────────────────────────────────────
    [SkippableFact]
    public async Task Rechazar_ExigeMotivo_YSella()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var bod = await SeedBodegaAsync("ZZRQ5");
        var a1 = await SeedArticuloAsync("ZZRQ5-A", bod);
        var doc = await _service!.CrearAsync(Nueva(bod, "Ana", (a1, 5m, 10m)), "solicitante1");
        await _service.EnviarARevisionAsync(doc.Id, "solicitante1");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.RechazarAsync(doc.Id, "  ", "jefe1"));

        var rech = await _service.RechazarAsync(doc.Id, "no hay presupuesto", "jefe1");
        Assert.Equal(EstadoRequisicionHdr.Rechazada, rech.Estado);
        Assert.Equal("no hay presupuesto", rech.MotivoRechazo);
        Assert.False(string.IsNullOrWhiteSpace(rech.RechazadoPor));
    }

    // ── 6 — aprobar fuera de revisión rechaza ─────────────────────────────────
    [SkippableFact]
    public async Task Aprobar_SiNoEstaEnRevision_Rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var bod = await SeedBodegaAsync("ZZRQ6");
        var a1 = await SeedArticuloAsync("ZZRQ6-A", bod);
        var doc = await _service!.CrearAsync(Nueva(bod, "Ana", (a1, 5m, 10m)), "tester");   // Borrador

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.AprobarAsync(doc.Id, "jefe1"));
        Assert.Contains("en revisión", ex.Message);
    }

    // ── 7 — anular sin despacho / con despacho ────────────────────────────────
    [SkippableFact]
    public async Task Anular_SinDespacho_Anula_ConDespacho_Rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var bod = await SeedBodegaAsync("ZZRQ7");
        var a1 = await SeedArticuloAsync("ZZRQ7-A", bod);
        var doc = await _service!.CrearAsync(Nueva(bod, "Ana", (a1, 5m, 10m)), "tester");

        // Simula una entrega parcial escribiendo cantidad_despachada (aún no hay servicio de descargo).
        await _context!.alm_requisicions.Where(l => l.requisicion_hdr_id == doc.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.cantidad_despachada, 2m));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.AnularAsync(doc.Id, "error", "tester"));
        Assert.Contains("entregas", ex.Message);

        // Sin despacho sí anula.
        await _context.alm_requisicions.Where(l => l.requisicion_hdr_id == doc.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.cantidad_despachada, 0m));
        Assert.True(await _service.AnularAsync(doc.Id, "error de captura", "tester"));
        Assert.Equal(EstadoRequisicionHdr.Anulada, (await _service.GetByIdAsync(doc.Id))!.Estado);
        // Idempotente.
        Assert.True(await _service.AnularAsync(doc.Id, "otra vez", "tester"));
    }

    // ── 8 — idempotencia del alta ─────────────────────────────────────────────
    [SkippableFact]
    public async Task Crear_MismoUuid_NoDuplica()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var bod = await SeedBodegaAsync("ZZRQ8");
        var a1 = await SeedArticuloAsync("ZZRQ8-A", bod);
        var dto = Nueva(bod, "Ana", (a1, 5m, 10m));

        var d1 = await _service!.CrearAsync(dto, "tester");
        var d2 = await _service.CrearAsync(dto, "tester");   // mismo uuid

        Assert.Equal(d1.Id, d2.Id);
        Assert.Equal(1, await _context!.alm_requisicion_hdrs.AsNoTracking().CountAsync(h => h.uuid == dto.Uuid!.Value));
    }

    // ── 9 — el correlativo continúa (numero positivo, secuencial) ─────────────
    [SkippableFact]
    public async Task Correlativo_Continua_Secuencial()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var bod = await SeedBodegaAsync("ZZRQ9");
        var a1 = await SeedArticuloAsync("ZZRQ9-A", bod);
        var d1 = await _service!.CrearAsync(Nueva(bod, "Ana", (a1, 1m, 1m)), "tester");
        var d2 = await _service.CrearAsync(Nueva(bod, "Ana", (a1, 1m, 1m)), "tester");

        Assert.True(d1.Numero > 0);
        Assert.Equal(d1.Numero + 1, d2.Numero);
    }

    private sealed class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
