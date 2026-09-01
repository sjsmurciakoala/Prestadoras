using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NSubstitute;
using Xunit;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.DTOs.Presupuesto;
using SIAD.Core.DTOs.Proveedores;
using SIAD.Core.DTOs.Retenciones;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Almacen;
using SIAD.Services.Contabilidad;
using SIAD.Services.Presupuesto;
using SIAD.Services.Proveedores;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Proveedores;

/// <summary>
/// Cuentas por pagar unificadas: <c>fn_prv_cxp_documentos</c> / <c>fn_prv_cxp_resumen</c>
/// (<c>Database/2026-08-22_prv_cxp_unificada.sql</c>) y el pago en lote.
/// <para>
/// La consulta trae TODOS los proveedores de la empresa, así que cada test siembra su propio
/// proveedor con código único y consulta filtrando por él. Todo corre dentro de
/// BEGIN … ROLLBACK, así que la base queda intacta.
/// </para>
/// <para>
/// Los tests del lote sustituyen los dos servicios de pago: lo que se verifica aquí es el
/// contrato del lote (validaciones, mapeo de cada rama y totales), no la mecánica de pago, que
/// ya tiene sus propios tests en cada módulo.
/// </para>
/// </summary>
[Collection("Postgres")]
public class CuentasPorPagarTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private CuentasPorPagarService? _service;
    private ICompraCxpService _compras = null!;
    private IOrdenesPagoDirectoService _compromisos = null!;
    private ProveedorEstadoCuentaService? _estadoCuenta;
    private string _cod = string.Empty;
    private int _bodegaId;

    public CuentasPorPagarTests(PostgresFixture fixture) : base(fixture) { }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (!Fixture.Available) return;

        var options = new DbContextOptionsBuilder<SiadDbContext>().UseNpgsql(Connection).Options;
        var company = new TestCurrentCompanyService(CompanyId);
        _context = new SiadDbContext(options, company);
        _context.Database.UseTransaction(Transaction);

        _compras = Substitute.For<ICompraCxpService>();
        _compromisos = Substitute.For<IOrdenesPagoDirectoService>();
        _service = new CuentasPorPagarService(_context, company, _compras, _compromisos);

        var formato = Substitute.For<IAccountFormatService>();
        formato.GetFormatAsync(Arg.Any<CancellationToken>()).Returns(AccountFormat.Default);
        _estadoCuenta = new ProveedorEstadoCuentaService(_context, company, formato);

        _cod = await SembrarProveedorAsync();
        _bodegaId = await EscalarAsync<int>(
            "SELECT id FROM public.alm_bodega WHERE company_id = @c ORDER BY id LIMIT 1", ("c", CompanyId));
    }

    // ─────────────────────────────────────────────────────────── Listado unificado

    [SkippableFact]
    public async Task Listado_trae_las_dos_ramas_como_un_solo_conjunto()
    {
        await SembrarFacturaAsync(1000m, Hoy(-30), Hoy(+10));
        await SembrarCompromisoAsync(400m, Hoy(-20));

        var docs = await ListarDelProveedorAsync();

        Assert.Equal(2, docs.Count);
        Assert.Equal(1, ContarPorOrigen(docs, OrigenDocumentoProveedor.Compra));
        Assert.Equal(1, ContarPorOrigen(docs, OrigenDocumentoProveedor.Compromiso));
    }

    [SkippableFact]
    public async Task Compromiso_sale_sin_plazo_y_nunca_cuenta_como_vencido()
    {
        // Un compromiso viejo: con la fecha de emisión como vencimiento nacería "vencido".
        await SembrarCompromisoAsync(400m, Hoy(-120));

        var docs = await ListarDelProveedorAsync();
        var compromiso = PrimeroDeOrigen(docs, OrigenDocumentoProveedor.Compromiso);

        Assert.NotNull(compromiso);
        Assert.Null(compromiso!.FechaVencimiento);
        Assert.Null(compromiso.DiasVencido);
        Assert.False(compromiso.TienePlazo);
        Assert.False(compromiso.Vencido);
    }

    [SkippableFact]
    public async Task Factura_conserva_su_vencimiento_y_sus_dias()
    {
        await SembrarFacturaAsync(1000m, Hoy(-60), Hoy(-15));

        var docs = await ListarDelProveedorAsync();
        var factura = PrimeroDeOrigen(docs, OrigenDocumentoProveedor.Compra);

        Assert.NotNull(factura);
        Assert.Equal(Hoy(-15), factura!.FechaVencimiento);
        Assert.Equal(15, factura.DiasVencido);
        Assert.True(factura.Vencido);
    }

    [SkippableFact]
    public async Task Filtro_por_origen_deja_solo_la_rama_pedida()
    {
        await SembrarFacturaAsync(1000m, Hoy(-30), Hoy(+10));
        await SembrarCompromisoAsync(400m, Hoy(-20));

        var soloFacturas = await ListarDelProveedorAsync(f => f.Origen = OrigenDocumentoProveedor.Compra);
        var soloCompromisos = await ListarDelProveedorAsync(f => f.Origen = OrigenDocumentoProveedor.Compromiso);

        Assert.Single(soloFacturas);
        Assert.Single(soloCompromisos);
        Assert.Equal(OrigenDocumentoProveedor.Compra, soloFacturas[0].Origen);
        Assert.Equal(OrigenDocumentoProveedor.Compromiso, soloCompromisos[0].Origen);
    }

    [SkippableFact]
    public async Task Solo_vencidos_deja_fuera_al_compromiso_porque_no_tiene_plazo()
    {
        await SembrarFacturaAsync(1000m, Hoy(-60), Hoy(-15));   // vencida
        await SembrarFacturaAsync(500m, Hoy(-10), Hoy(+20));    // al día
        await SembrarCompromisoAsync(400m, Hoy(-120));          // viejo, pero sin plazo

        var vencidos = await ListarDelProveedorAsync(f => f.SoloVencidos = true);

        Assert.Single(vencidos);
        Assert.Equal(OrigenDocumentoProveedor.Compra, vencidos[0].Origen);
    }

    [SkippableFact]
    public async Task Los_saldados_solo_aparecen_cuando_se_piden()
    {
        var cxpId = await SembrarFacturaAsync(1000m, Hoy(-30), Hoy(+10));
        await SembrarAbonoFacturaAsync(cxpId, 1, 1000m, Hoy(-5));   // queda en cero

        var pendientes = await ListarDelProveedorAsync();
        var todos = await ListarDelProveedorAsync(f => f.IncluirPagados = true);

        Assert.Empty(pendientes);
        Assert.Single(todos);
        Assert.Equal(EstadoCompraCxp.Pagada, todos[0].EstadoId);
        Assert.Equal(0m, todos[0].Saldo);
    }

    [SkippableFact]
    public async Task Abono_parcial_deja_el_documento_en_estado_parcial()
    {
        var cxpId = await SembrarFacturaAsync(1000m, Hoy(-30), Hoy(+10));
        await SembrarAbonoFacturaAsync(cxpId, 1, 400m, Hoy(-5));

        var docs = await ListarDelProveedorAsync();

        Assert.Single(docs);
        Assert.Equal(400m, docs[0].Abonado);
        Assert.Equal(600m, docs[0].Saldo);
        Assert.Equal(EstadoCompraCxp.Parcial, docs[0].EstadoId);
    }

    [SkippableFact]
    public async Task Compromiso_procesado_sin_abonos_no_arrastra_deuda()
    {
        // Los ~228 migrados de SIMAFI: procesados y sin abonos, ya no se deben.
        await SembrarCompromisoAsync(900m, Hoy(-200), statusTransacc: true);

        var docs = await ListarDelProveedorAsync();

        Assert.Empty(docs);
    }

    [SkippableFact]
    public async Task Compromiso_anulado_queda_fuera()
    {
        await SembrarCompromisoAsync(900m, Hoy(-30), anulado: true);

        var docs = await ListarDelProveedorAsync();

        Assert.Empty(docs);
    }

    [SkippableFact]
    public async Task Busqueda_encuentra_por_codigo_de_proveedor()
    {
        await SembrarFacturaAsync(1000m, Hoy(-30), Hoy(+10));

        var filtro = new CxpUnificadaFilterDto { Search = _cod };
        var docs = await _service!.ListarAsync(filtro);

        Assert.NotEmpty(docs);
        foreach (var d in docs)
        {
            Assert.Equal(_cod, d.CodProveedor);
        }
    }

    // ─────────────────────────────────────────────────────────────────── Resumen

    [SkippableFact]
    public async Task Resumen_separa_compras_de_compromisos_y_el_total_cuadra()
    {
        await SembrarFacturaAsync(1000m, Hoy(-60), Hoy(-15));   // vencida
        await SembrarFacturaAsync(500m, Hoy(-10), Hoy(+20));    // al día
        await SembrarCompromisoAsync(400m, Hoy(-20));

        var resumen = await _service!.ObtenerResumenAsync(
            new CxpUnificadaFilterDto { CodProveedor = _cod });

        Assert.Equal(1900m, resumen.SaldoTotal);
        Assert.Equal(1500m, resumen.SaldoCompras);
        Assert.Equal(400m, resumen.SaldoCompromisos);
        Assert.Equal(resumen.SaldoTotal, resumen.SaldoCompras + resumen.SaldoCompromisos);
        Assert.Equal(3, resumen.DocumentosPendientes);
        Assert.Equal(2, resumen.ComprasPendientes);
        Assert.Equal(1, resumen.CompromisosPendientes);
        Assert.Equal(1000m, resumen.SaldoVencido);
        Assert.Equal(1, resumen.DocumentosVencidos);
    }

    [SkippableFact]
    public async Task Saldo_cuadra_con_el_estado_de_cuenta_del_proveedor()
    {
        await SembrarFacturaAsync(1000m, Hoy(-60), Hoy(-15));
        await SembrarCompromisoAsync(400m, Hoy(-20));

        var resumen = await _service!.ObtenerResumenAsync(
            new CxpUnificadaFilterDto { CodProveedor = _cod });
        var estado = await _estadoCuenta!.GetResumenAsync(_cod);

        Assert.NotNull(estado);
        // Las dos pantallas leen la misma función base: el saldo tiene que ser el mismo.
        Assert.Equal(estado!.Resumen.SaldoTotal, resumen.SaldoTotal);
    }

    // ──────────────────────────────────────────────────────────── Pago en lote

    [SkippableFact]
    public async Task Lote_sin_lineas_se_rechaza()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service!.PagarLoteAsync(new CxpLoteUpsertDto { MetodoPago = MetodoPagoCompra.Efectivo }, "test"));

        Assert.Contains("al menos un documento", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task Lote_rechaza_el_mismo_documento_dos_veces()
    {
        var cxpId = await SembrarFacturaAsync(1000m, Hoy(-30), Hoy(+10));

        var dto = LoteEfectivo(
            Linea(OrigenDocumentoProveedor.Compra, cxpId, 100m),
            Linea(OrigenDocumentoProveedor.Compra, cxpId, 200m));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service!.PagarLoteAsync(dto, "test"));

        Assert.Contains("dos veces", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task Lote_rechaza_un_documento_que_ya_no_esta_pendiente()
    {
        var dto = LoteEfectivo(Linea(OrigenDocumentoProveedor.Compra, 999999999, 100m));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service!.PagarLoteAsync(dto, "test"));

        Assert.Contains("ya no está pendiente", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task Lote_rechaza_un_monto_mayor_que_el_saldo()
    {
        var cxpId = await SembrarFacturaAsync(1000m, Hoy(-30), Hoy(+10));

        var dto = LoteEfectivo(Linea(OrigenDocumentoProveedor.Compra, cxpId, 1500m));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service!.PagarLoteAsync(dto, "test"));

        Assert.Contains("supera su saldo", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task Lote_bancario_exige_cuenta_del_banco()
    {
        var cxpId = await SembrarFacturaAsync(1000m, Hoy(-30), Hoy(+10));

        var dto = new CxpLoteUpsertDto { MetodoPago = MetodoPagoCompra.Transferencia };
        dto.Lineas.Add(Linea(OrigenDocumentoProveedor.Compra, cxpId, 100m));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service!.PagarLoteAsync(dto, "test"));

        Assert.Contains("banco", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task Lote_paga_cada_documento_con_el_servicio_de_su_rama()
    {
        var cxpId = await SembrarFacturaAsync(1000m, Hoy(-30), Hoy(+10));
        var numeroOrden = await SembrarCompromisoAsync(400m, Hoy(-20));

        ConfigurarPagosOk(cxpId, numeroOrden);

        var dto = LoteEfectivo(
            Linea(OrigenDocumentoProveedor.Compra, cxpId, 600m),
            Linea(OrigenDocumentoProveedor.Compromiso, numeroOrden, 400m));

        var resultado = await _service!.PagarLoteAsync(dto, "test");

        Assert.True(resultado.Success);
        Assert.Equal(2, resultado.Pagos.Count);
        Assert.Equal(1000m, resultado.TotalAplicado);
        Assert.Equal(1000m, resultado.TotalNeto);
        Assert.Equal(1, resultado.Desembolsos);   // los dos son del mismo proveedor

        await _compras.Received(1).RegistrarAbonoAsync(
            cxpId, Arg.Is<CompraCxpAbonoUpsertDto>(a => a.Monto == 600m), "test", Arg.Any<CancellationToken>());
        await _compromisos.Received(1).RegistrarAbonoAsync(
            numeroOrden, Arg.Is<AbonoCompromisoUpsertDto>(a => a.Monto == 400m), Arg.Any<CancellationToken>());
    }

    [SkippableFact]
    public async Task Compromiso_pagado_en_efectivo_va_como_contable_con_la_cuenta_elegida()
    {
        var numeroOrden = await SembrarCompromisoAsync(400m, Hoy(-20));
        ConfigurarPagosOk(0, numeroOrden);

        var dto = LoteEfectivo(Linea(OrigenDocumentoProveedor.Compromiso, numeroOrden, 400m));
        dto.CuentaContableId = 77;

        await _service!.PagarLoteAsync(dto, "test");

        // El compromiso no conoce "EFECTIVO": el lote lo traduce a CONTABLE y le pasa la
        // cuenta de contrapartida por contra-magnitud (sin retención no arma líneas).
        await _compromisos.Received(1).RegistrarAbonoAsync(
            numeroOrden,
            Arg.Is<AbonoCompromisoUpsertDto>(a =>
                a.MetodoPago == OrdenPagoDirectoMetodoPago.Contable &&
                a.CuentaContraId == 77 &&
                a.Lineas.Count == 0),
            Arg.Any<CancellationToken>());
    }

    [SkippableFact]
    public async Task Factura_con_varias_retenciones_las_pasa_todas_al_servicio_de_compras()
    {
        var cxpId = await SembrarFacturaAsync(1000m, Hoy(-30), Hoy(+10));
        ConfigurarPagosOk(cxpId, 0);

        var linea = Linea(OrigenDocumentoProveedor.Compra, cxpId, 1000m);
        linea.Retenciones.Add(Retencion(1, 900, 1000m, 12.5m, 125m));
        linea.Retenciones.Add(Retencion(2, 901, 1000m, 1m, 10m));

        await _service!.PagarLoteAsync(LoteEfectivo(linea), "test");

        // El monto que baja la deuda es el BRUTO; las dos retenciones viajan tal cual.
        await _compras.Received(1).RegistrarAbonoAsync(
            cxpId,
            Arg.Is<CompraCxpAbonoUpsertDto>(a => a.Monto == 1000m && a.Retenciones.Count == 2),
            "test", Arg.Any<CancellationToken>());
    }

    [SkippableFact]
    public async Task Compromiso_con_varias_retenciones_arma_una_linea_por_cada_una_mas_el_origen()
    {
        var numeroOrden = await SembrarCompromisoAsync(1000m, Hoy(-20));
        ConfigurarPagosOk(0, numeroOrden);

        var linea = Linea(OrigenDocumentoProveedor.Compromiso, numeroOrden, 1000m);
        linea.Retenciones.Add(Retencion(1, 900, 1000m, 12.5m, 125m));
        linea.Retenciones.Add(Retencion(2, 901, 1000m, 1m, 10m));

        await _service!.PagarLoteAsync(LoteEfectivo(linea), "test");

        // Modelo GENERAL: origen al HABER por el neto (1000 − 135) + una línea por retención.
        await _compromisos.Received(1).RegistrarAbonoAsync(
            numeroOrden,
            Arg.Is<AbonoCompromisoUpsertDto>(a =>
                a.Lineas.Count == 3 &&
                a.Lineas[0].Credito == 865m &&
                a.Lineas[1].Credito == 125m &&
                a.Lineas[2].Credito == 10m &&
                a.Retenciones.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [SkippableFact]
    public async Task Un_compromiso_cuya_retencion_se_come_el_monto_se_rechaza()
    {
        var numeroOrden = await SembrarCompromisoAsync(1000m, Hoy(-20));
        ConfigurarPagosOk(0, numeroOrden);

        var linea = Linea(OrigenDocumentoProveedor.Compromiso, numeroOrden, 100m);
        linea.Retenciones.Add(Retencion(1, 900, 100m, 100m, 100m));   // neto = 0

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service!.PagarLoteAsync(LoteEfectivo(linea), "test"));

        Assert.Contains("mayor que cero", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task Si_un_documento_falla_no_se_registra_ninguno()
    {
        var cxpId = await SembrarFacturaAsync(1000m, Hoy(-30), Hoy(+10));
        var numeroOrden = await SembrarCompromisoAsync(400m, Hoy(-20));

        ConfigurarPagosOk(cxpId, numeroOrden);
        // El segundo documento revienta: el lote entero se detiene.
        _compromisos.RegistrarAbonoAsync(numeroOrden, Arg.Any<AbonoCompromisoUpsertDto>(), Arg.Any<CancellationToken>())
            .Returns<Task<AbonoCompromisoResultadoDto>>(_ => throw new InvalidOperationException("periodo cerrado"));

        var dto = LoteEfectivo(
            Linea(OrigenDocumentoProveedor.Compra, cxpId, 600m),
            Linea(OrigenDocumentoProveedor.Compromiso, numeroOrden, 400m));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service!.PagarLoteAsync(dto, "test"));
    }

    [SkippableFact]
    public async Task Un_compromiso_que_responde_sin_exito_corta_el_lote()
    {
        var numeroOrden = await SembrarCompromisoAsync(400m, Hoy(-20));
        _compromisos.RegistrarAbonoAsync(numeroOrden, Arg.Any<AbonoCompromisoUpsertDto>(), Arg.Any<CancellationToken>())
            .Returns(new AbonoCompromisoResultadoDto { Success = false, Message = "El saldo cambio" });

        var dto = LoteEfectivo(Linea(OrigenDocumentoProveedor.Compromiso, numeroOrden, 400m));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service!.PagarLoteAsync(dto, "test"));

        Assert.Contains("El saldo cambio", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ================================================================ Utilidades

    private void ConfigurarPagosOk(int cxpId, int numeroOrden)
    {
        _compras.RegistrarAbonoAsync(cxpId, Arg.Any<CompraCxpAbonoUpsertDto>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new CompraCxpAbonoResultadoDto
            {
                Success = true,
                CxpId = cxpId,
                NumeroAbono = 1,
                Saldo = 0m,
                EstadoId = EstadoCompraCxp.Pagada
            }));

        _compromisos.RegistrarAbonoAsync(numeroOrden, Arg.Any<AbonoCompromisoUpsertDto>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new AbonoCompromisoResultadoDto
            {
                Success = true,
                NumeroOrden = numeroOrden,
                NumeroAbono = 1,
                Saldo = 0m,
                Pagado = true
            }));

        _compromisos.GetCuentasContraProcesamientoAsync(Arg.Any<CancellationToken>())
            .Returns(new List<CuentaContableLookupDto>
            {
                new() { AccountId = 55, BancoCuentaId = 9, Code = "1102-01", Description = "Banco de prueba" }
            });
    }

    private static RetencionAplicadaDto Retencion(int id, long cuentaId, decimal baseCalculo, decimal porcentaje, decimal monto)
        => new() { RetencionId = id, CuentaId = cuentaId, Base = baseCalculo, Porcentaje = porcentaje, Monto = monto };

    private static CxpLoteLineaDto Linea(short origen, long documentoId, decimal monto)
        => new() { Origen = origen, DocumentoId = documentoId, Monto = monto };

    private static CxpLoteUpsertDto LoteEfectivo(params CxpLoteLineaDto[] lineas)
    {
        var dto = new CxpLoteUpsertDto { MetodoPago = MetodoPagoCompra.Efectivo, CuentaContableId = 77 };
        foreach (var l in lineas)
        {
            dto.Lineas.Add(l);
        }

        return dto;
    }

    private async Task<List<CxpDocumentoDto>> ListarDelProveedorAsync(Action<CxpUnificadaFilterDto>? ajuste = null)
    {
        var filtro = new CxpUnificadaFilterDto { CodProveedor = _cod };
        ajuste?.Invoke(filtro);
        return new List<CxpDocumentoDto>(await _service!.ListarAsync(filtro));
    }

    /// <summary>Sin LINQ, por convención del repo.</summary>
    private static int ContarPorOrigen(List<CxpDocumentoDto> docs, short origen)
    {
        var n = 0;
        foreach (var d in docs)
        {
            if (d.Origen == origen) n++;
        }

        return n;
    }

    private static CxpDocumentoDto? PrimeroDeOrigen(List<CxpDocumentoDto> docs, short origen)
    {
        foreach (var d in docs)
        {
            if (d.Origen == origen) return d;
        }

        return null;
    }

    private static DateOnly Hoy(int dias) => DateOnly.FromDateTime(DateTime.Today).AddDays(dias);

    private async Task<string> SembrarProveedorAsync()
    {
        var codigo = "ZCX" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        await EjecutarAsync(@"
            INSERT INTO public.prv_proveedores
                (cod_proveedor, cod_tipoproveedor, nombre, cuenta_contable, direccion,
                 fecha_creacion, usuario_creo, status, company_id)
            SELECT @cod, COALESCE((SELECT MIN(cod_tipoproveedor) FROM public.prv_tipoproveedor), 1),
                   'PROVEEDOR PRUEBA CXP UNIFICADA', '21101010941', 'Prueba',
                   now(), 'test', TRUE, @c",
            ("cod", codigo), ("c", (int)CompanyId));

        return codigo;
    }

    /// <summary>Crea la factura y su cuenta por pagar; devuelve el id de la CxP.</summary>
    private async Task<int> SembrarFacturaAsync(decimal total, DateOnly fecha, DateOnly vencimiento)
    {
        var hdrId = await EscalarAsync<int>(@"
            INSERT INTO public.alm_compra_hdr
                (company_id, numero, fecha, fecha_vencimiento, cod_proveedor, proveedor,
                 numero_factura_sar, bodega_id, sub_total, impuesto, total, observaciones, estado)
            VALUES (@c,
                    COALESCE((SELECT MAX(numero) FROM public.alm_compra_hdr WHERE company_id = @c), 0) + 1,
                    @f, @v, @cod, 'PROVEEDOR PRUEBA CXP UNIFICADA',
                    @sar, @bod, @total, 0, @total, 'Compra de prueba', 1)
            RETURNING id",
            ("c", CompanyId), ("f", fecha), ("v", vencimiento), ("cod", _cod),
            ("sar", "TCX-" + Guid.NewGuid().ToString("N")[..8]), ("bod", _bodegaId), ("total", total));

        return await EscalarAsync<int>(@"
            INSERT INTO public.alm_compra_cxp
                (company_id, compra_hdr_id, cod_proveedor, proveedor, numero_factura_sar,
                 fecha, fecha_vencimiento, condicion_pago, monto, saldo, estado_id)
            SELECT @c, @hdr, @cod, 'PROVEEDOR PRUEBA CXP UNIFICADA', h.numero_factura_sar,
                   @f, @v, 2, @total, @total, @estado
            FROM public.alm_compra_hdr h
            WHERE h.company_id = @c AND h.id = @hdr
            RETURNING id",
            ("c", CompanyId), ("hdr", hdrId), ("cod", _cod), ("f", fecha), ("v", vencimiento),
            ("total", total), ("estado", EstadoCompraCxp.Pendiente));
    }

    private Task SembrarAbonoFacturaAsync(int cxpId, int numero, decimal monto, DateOnly fecha, char estado = 'V')
        => EjecutarAsync(@"
            INSERT INTO public.alm_compra_cxp_abono
                (company_id, cxp_id, numero_abono, fecha, monto, metodo_pago, estado)
            VALUES (@c, @cxp, @n, @f, @m, 'transferencia', @e)",
            ("c", CompanyId), ("cxp", cxpId), ("n", numero), ("f", fecha), ("m", monto),
            ("e", estado.ToString()));

    private async Task<int> SembrarCompromisoAsync(
        decimal monto, DateOnly fecha, bool statusTransacc = false, bool anulado = false)
        => await EscalarAsync<int>(@"
            INSERT INTO public.prv_compromiso_hdr
                (company_id, numero_orden, fecha, monto, concepto, cod_proveedor,
                 nombre_proveedor, status_transacc, anulado)
            SELECT @c,
                   COALESCE((SELECT MAX(numero_orden) FROM public.prv_compromiso_hdr), 0) + 1,
                   @f, @m, 'Compromiso de prueba', @cod,
                   'PROVEEDOR PRUEBA CXP UNIFICADA', @st, @an
            RETURNING numero_orden",
            ("c", CompanyId), ("f", fecha.ToDateTime(TimeOnly.MinValue)), ("m", monto),
            ("cod", _cod), ("st", statusTransacc), ("an", anulado));

    private async Task EjecutarAsync(string sql, params (string Nombre, object? Valor)[] parametros)
    {
        await using var cmd = NuevoComando(sql, parametros);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<T> EscalarAsync<T>(string sql, params (string Nombre, object? Valor)[] parametros)
    {
        await using var cmd = NuevoComando(sql, parametros);
        var valor = await cmd.ExecuteScalarAsync();
        return (T)Convert.ChangeType(valor!, typeof(T));
    }

    private NpgsqlCommand NuevoComando(string sql, (string Nombre, object? Valor)[] parametros)
    {
        var cmd = Connection.CreateCommand();
        cmd.Transaction = Transaction;
        cmd.CommandText = sql;
        foreach (var (nombre, valor) in parametros)
        {
            cmd.Parameters.AddWithValue(nombre, valor ?? DBNull.Value);
        }

        return cmd;
    }

    private sealed class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
