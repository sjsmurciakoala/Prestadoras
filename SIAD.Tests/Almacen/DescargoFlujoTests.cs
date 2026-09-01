using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Services.Aprobaciones;
using SIAD.Services.Almacen;
using SIAD.Data;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Almacen;

/// <summary>
/// Descargo (entrega) contra requisición y su anulación (Fase 6.3). Lo que se vigila: que el descargo
/// SÍ postee la salida (a diferencia de la requisición), que la entrega sea parcial con tope, que
/// derive el estado de la requisición, y que la anulación revierta la salida y devuelva la cantidad.
/// <para>Requiere <c>2026-08-01_alm_requisicion_descargo.sql</c> (paso 26) aplicado en <c>SIAD_TEST_DB</c>.</para>
/// </summary>
[Collection("Postgres")]
public class DescargoFlujoTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private RequisicionDocumentoService? _req;
    private DescargoDocumentoService? _desc;

    public DescargoFlujoTests(PostgresFixture fixture) : base(fixture) { }

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
            _req = new RequisicionDocumentoService(_context, company,
                new AprobacionService(_context, company, new TestCurrentUserService()));
            _desc = new DescargoDocumentoService(_context, company, motor, rollup, Substitute.For<IAlertasStockNotificador>());
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

    private async Task<int> SeedArticuloConParAsync(string codigo, int bodegaId, decimal existencia, decimal costo)
    {
        var a = new alm_articulo { codigo_articulo = codigo, descripcion = $"Artículo {codigo}", existencia = existencia, activo = true };
        _context!.alm_articulos.Add(a);
        await _context.SaveChangesAsync();
        _context.alm_articulo_bodegas.Add(new alm_articulo_bodega
        {
            articulo_id = a.id, bodega_id = bodegaId, existencia = existencia,
            costo_promedio = costo, activo = true, principal = true
        });
        await _context.SaveChangesAsync();
        return a.id;
    }

    private async Task<RequisicionDocumentoDto> RequisicionAprobadaAsync(int bodega, params (int art, decimal cant)[] lineas)
    {
        var dto = new RequisicionDocumentoDto
        {
            BodegaId = bodega, Solicitante = "Ana", Uuid = Guid.NewGuid(),
            Detalles = lineas.Select(l => new RequisicionDocumentoDetalleDto { ArticuloId = l.art, Cantidad = l.cant, PrecioUnitario = 0m }).ToList()
        };
        var req = await _req!.CrearAsync(dto, "sol");
        await _req.EnviarARevisionAsync(req.Id, "sol");
        return await _req.AprobarAsync(req.Id, "jefe");
    }

    // Toda entrega nueva exige quién recibe y el departamento (obligatorios desde el commit b817c72).
    private static DescargoDocumentoDto Entrega(int reqHdrId, params (int reqLineaId, decimal cant)[] lineas) => new()
    {
        RequisicionHdrId = reqHdrId,
        RecibidoPor = "Juan Pérez",
        Departamento = "ALM",
        Uuid = Guid.NewGuid(),
        Detalles = lineas.Select(l => new DescargoDocumentoDetalleDto { RequisicionLineaId = l.reqLineaId, Cantidad = l.cant }).ToList()
    };

    private async Task<decimal> ExistenciaAsync(int art, int bod)
        => (await _context!.alm_articulo_bodegas.AsNoTracking().FirstAsync(u => u.articulo_id == art && u.bodega_id == bod)).existencia;
    private async Task<short> EstadoReqAsync(int reqId)
        => (await _context!.alm_requisicion_hdrs.AsNoTracking().FirstAsync(h => h.id == reqId)).estado;
    private async Task<decimal> DespachadaAsync(int reqLineaId)
        => (await _context!.alm_requisicions.AsNoTracking().FirstAsync(l => l.id == reqLineaId)).cantidad_despachada;
    private async Task<string?> DepartamentoHdrAsync(int hdrId)
        => (await _context!.alm_descargo_hdrs.AsNoTracking().FirstAsync(h => h.id == hdrId)).departamento;

    // ── 1 — el descargo SÍ postea y baja la existencia ────────────────────────
    [SkippableFact]
    public async Task Entrega_Postea_BajaExistencia_ValorizaAlPromedio()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var bod = await SeedBodegaAsync("ZZDF1");
        var art = await SeedArticuloConParAsync("ZZDF1-A", bod, existencia: 100m, costo: 10m);
        var req = await RequisicionAprobadaAsync(bod, (art, 10m));
        var reqLinea = req.Detalles.Single().Id;

        var desc = await _desc!.EntregarAsync(Entrega(req.Id, (reqLinea, 4m)), "bodeguero");

        Assert.True(desc.Posteado);
        Assert.Equal(96m, await ExistenciaAsync(art, bod));   // 100 - 4
        var linea = desc.Detalles.Single();
        Assert.Equal(10m, linea.PrecioUnitario);              // salió al promedio vigente
        Assert.Equal(40m, linea.Total);
        Assert.Equal(40m, desc.Total);
    }

    // ── 2 — entrega parcial deriva el estado de la requisición ────────────────
    [SkippableFact]
    public async Task EntregaParcial_LuegoTotal_DerivaEstadoRequisicion()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var bod = await SeedBodegaAsync("ZZDF2");
        var art = await SeedArticuloConParAsync("ZZDF2-A", bod, 100m, 10m);
        var req = await RequisicionAprobadaAsync(bod, (art, 10m));
        var reqLinea = req.Detalles.Single().Id;

        await _desc!.EntregarAsync(Entrega(req.Id, (reqLinea, 4m)), "bodeguero");
        Assert.Equal(4m, await DespachadaAsync(reqLinea));
        Assert.Equal(EstadoRequisicionHdr.DespachadaParcial, await EstadoReqAsync(req.Id));

        await _desc.EntregarAsync(Entrega(req.Id, (reqLinea, 6m)), "bodeguero");
        Assert.Equal(10m, await DespachadaAsync(reqLinea));
        Assert.Equal(EstadoRequisicionHdr.DespachadaTotal, await EstadoReqAsync(req.Id));
        Assert.Equal(90m, await ExistenciaAsync(art, bod));
    }

    // ── 3 — no se puede entregar más de lo pendiente ──────────────────────────
    [SkippableFact]
    public async Task Entregar_MasDeLoPendiente_Rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var bod = await SeedBodegaAsync("ZZDF3");
        var art = await SeedArticuloConParAsync("ZZDF3-A", bod, 100m, 10m);
        var req = await RequisicionAprobadaAsync(bod, (art, 5m));
        var reqLinea = req.Detalles.Single().Id;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _desc!.EntregarAsync(Entrega(req.Id, (reqLinea, 6m)), "bodeguero"));
        Assert.Contains("pendiente", ex.Message);
        Assert.Equal(100m, await ExistenciaAsync(art, bod));   // nada a medias
    }

    // ── 4 — anular devuelve la mercadería y la cantidad a la requisición ──────
    [SkippableFact]
    public async Task Anular_RevierteSalida_YDevuelveCantidad()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var bod = await SeedBodegaAsync("ZZDF4");
        var art = await SeedArticuloConParAsync("ZZDF4-A", bod, 100m, 10m);
        var req = await RequisicionAprobadaAsync(bod, (art, 10m));
        var reqLinea = req.Detalles.Single().Id;

        var desc = await _desc!.EntregarAsync(Entrega(req.Id, (reqLinea, 10m)), "bodeguero");
        Assert.Equal(90m, await ExistenciaAsync(art, bod));
        Assert.Equal(EstadoRequisicionHdr.DespachadaTotal, await EstadoReqAsync(req.Id));

        var ok = await _desc.AnularAsync(desc.Id, "error de entrega", "bodeguero");
        Assert.True(ok);
        Assert.Equal(100m, await ExistenciaAsync(art, bod));   // mercadería devuelta a la bodega
        Assert.Equal(0m, await DespachadaAsync(reqLinea));      // cantidad devuelta a la requisición
        Assert.Equal(EstadoRequisicionHdr.Aprobada, await EstadoReqAsync(req.Id));   // vuelve a aprobada
        // Idempotente.
        Assert.True(await _desc.AnularAsync(desc.Id, "otra vez", "bodeguero"));
    }

    // ── 5 — descargo directo (sin requisición) exige motivo y postea ──────────
    [SkippableFact]
    public async Task Directo_SinMotivo_Rechaza_ConMotivo_Postea()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var bod = await SeedBodegaAsync("ZZDF5");
        var art = await SeedArticuloConParAsync("ZZDF5-A", bod, 50m, 8m);

        var sinMotivo = new DescargoDocumentoDto
        {
            BodegaId = bod, RecibidoPor = "Juan Pérez", Departamento = "ALM", Uuid = Guid.NewGuid(),
            Detalles = [new() { ArticuloId = art, Cantidad = 3m }]
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => _desc!.EntregarAsync(sinMotivo, "bodeguero"));

        var conMotivo = new DescargoDocumentoDto
        {
            BodegaId = bod, Motivo = "consumo interno", RecibidoPor = "Juan Pérez", Departamento = "ALM", Uuid = Guid.NewGuid(),
            Detalles = [new() { ArticuloId = art, Cantidad = 3m }]
        };
        var desc = await _desc!.EntregarAsync(conMotivo, "bodeguero");
        Assert.True(desc.Posteado);
        Assert.Equal(47m, await ExistenciaAsync(art, bod));
    }

    // ── 6 — salir a costo 0 se rechaza (prerrequisito del corte) ──────────────
    [SkippableFact]
    public async Task Entregar_ParConCostoCero_Rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var bod = await SeedBodegaAsync("ZZDF6");
        var art = await SeedArticuloConParAsync("ZZDF6-A", bod, existencia: 50m, costo: 0m);   // costo 0
        var req = await RequisicionAprobadaAsync(bod, (art, 5m));
        var reqLinea = req.Detalles.Single().Id;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _desc!.EntregarAsync(Entrega(req.Id, (reqLinea, 5m)), "bodeguero"));
        Assert.Equal(50m, await ExistenciaAsync(art, bod));
    }

    // ── 7 — idempotencia de la entrega ────────────────────────────────────────
    [SkippableFact]
    public async Task Entrega_MismoUuid_NoDuplica()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var bod = await SeedBodegaAsync("ZZDF7");
        var art = await SeedArticuloConParAsync("ZZDF7-A", bod, 100m, 10m);
        var req = await RequisicionAprobadaAsync(bod, (art, 10m));
        var reqLinea = req.Detalles.Single().Id;
        var dto = Entrega(req.Id, (reqLinea, 4m));

        var d1 = await _desc!.EntregarAsync(dto, "bodeguero");
        var d2 = await _desc.EntregarAsync(dto, "bodeguero");   // mismo uuid

        Assert.Equal(d1.Id, d2.Id);
        Assert.Equal(96m, await ExistenciaAsync(art, bod));      // salió una sola vez
        Assert.Equal(4m, await DespachadaAsync(reqLinea));
    }

    // ── 8 — no se despacha una requisición no aprobada ────────────────────────
    [SkippableFact]
    public async Task Entregar_RequisicionNoAprobada_Rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var bod = await SeedBodegaAsync("ZZDF8");
        var art = await SeedArticuloConParAsync("ZZDF8-A", bod, 100m, 10m);
        // Requisición en borrador (no aprobada).
        var req = await _req!.CrearAsync(new RequisicionDocumentoDto
        {
            BodegaId = bod, Solicitante = "Ana", Uuid = Guid.NewGuid(),
            Detalles = [new() { ArticuloId = art, Cantidad = 5m }]
        }, "sol");
        var reqLinea = req.Detalles.Single().Id;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _desc!.EntregarAsync(Entrega(req.Id, (reqLinea, 5m)), "bodeguero"));
        Assert.Contains("aprobada", ex.Message);
    }

    // ── 9 — el nombre del departamento se guarda completo, no truncado (regresión b817c72) ──
    // La cabecera pasó a VARCHAR(80) para guardar el NOMBRE del catálogo th_departamento; el
    // servicio llegó a truncarlo a 3 (Limpiar(...,3)), degradando "CONTABILIDAD" → "CON".
    [SkippableFact]
    public async Task Entrega_GuardaNombreDepartamentoCompleto_NoTrunca()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var bod = await SeedBodegaAsync("ZZDF9");
        var art = await SeedArticuloConParAsync("ZZDF9-A", bod, existencia: 50m, costo: 8m);

        var dto = new DescargoDocumentoDto
        {
            BodegaId = bod, Motivo = "consumo interno", RecibidoPor = "Juan Pérez",
            Departamento = "CONTABILIDAD", Uuid = Guid.NewGuid(),   // 12 chars > 3
            Detalles = [new() { ArticuloId = art, Cantidad = 3m }]
        };
        var desc = await _desc!.EntregarAsync(dto, "bodeguero");

        // Se lee la fila real: el nombre debe quedar íntegro en alm_descargo_hdr, no recortado a "CON".
        Assert.Equal("CONTABILIDAD", await DepartamentoHdrAsync(desc.Id));
    }

    private sealed class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
