using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NSubstitute;
using Xunit;
using SIAD.Core.Constants;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Contabilidad;
using SIAD.Services.Proveedores;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Proveedores;

/// <summary>
/// Estado de cuenta del proveedor: saldo, documentos por pagar, antigüedad y libro de
/// movimientos, sobre las funciones <c>fn_prv_estado_cuenta_*</c>
/// (<c>Database/2026-08-13_prv_estado_cuenta.sql</c>).
/// <para>
/// Cada test siembra su propio proveedor con código único y corre dentro de
/// BEGIN … ROLLBACK, así que no ve ni ensucia los datos de la base de prueba.
/// </para>
/// </summary>
[Collection("Postgres")]
public class ProveedorEstadoCuentaTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private ProveedorEstadoCuentaService? _service;
    private string _cod = string.Empty;
    private int _bodegaId;

    public ProveedorEstadoCuentaTests(PostgresFixture fixture) : base(fixture) { }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (!Fixture.Available) return;

        var options = new DbContextOptionsBuilder<SiadDbContext>().UseNpgsql(Connection).Options;
        var company = new TestCurrentCompanyService(CompanyId);
        _context = new SiadDbContext(options, company);
        _context.Database.UseTransaction(Transaction);

        var formato = Substitute.For<IAccountFormatService>();
        formato.GetFormatAsync(Arg.Any<CancellationToken>()).Returns(AccountFormat.Default);

        _service = new ProveedorEstadoCuentaService(_context, company, formato);

        _cod = await SembrarProveedorAsync();
        _bodegaId = await EscalarAsync<int>(
            "SELECT id FROM public.alm_bodega WHERE company_id = @c ORDER BY id LIMIT 1",
            ("c", CompanyId));
    }

    // ---------------------------------------------------------------- Facturas de compra

    [SkippableFact]
    public async Task Factura_sin_pagos_aparece_con_su_saldo_completo()
    {
        await SembrarFacturaAsync(total: 5000m, fecha: Hoy(-40), vencimiento: Hoy(-10));

        var docs = await _service!.GetDocumentosAsync(_cod);
        var doc = Assert.Single(docs);

        Assert.Equal(OrigenDocumentoProveedor.Compra, doc.Origen);
        Assert.Equal(5000m, doc.Monto);
        Assert.Equal(0m, doc.Abonado);
        Assert.Equal(5000m, doc.Saldo);
        Assert.Equal(EstadoCompraCxp.Pendiente, doc.EstadoId);
        Assert.Equal(10, doc.DiasVencido);
        Assert.True(doc.Vencido);
    }

    [SkippableFact]
    public async Task Factura_con_abono_parcial_reporta_abonado_saldo_y_estado_parcial()
    {
        var cxpId = await SembrarFacturaAsync(total: 5000m, fecha: Hoy(-40), vencimiento: Hoy(-10));
        await SembrarAbonoFacturaAsync(cxpId, numero: 1, monto: 2000m, fecha: Hoy(-5));

        var doc = Assert.Single(await _service!.GetDocumentosAsync(_cod));

        Assert.Equal(2000m, doc.Abonado);
        Assert.Equal(3000m, doc.Saldo);
        Assert.Equal(EstadoCompraCxp.Parcial, doc.EstadoId);
        Assert.Equal("Parcial", doc.EstadoDescripcion);
    }

    [SkippableFact]
    public async Task Factura_anulada_no_aparece_ni_suma_al_saldo()
    {
        await SembrarFacturaAsync(total: 5000m, fecha: Hoy(-40), vencimiento: Hoy(-10));
        await SembrarFacturaAsync(total: 9999m, fecha: Hoy(-30), vencimiento: Hoy(-5),
                                  estadoId: EstadoCompraCxp.Anulada);

        var docs = await _service!.GetDocumentosAsync(_cod);
        var resumen = await _service.GetResumenAsync(_cod);

        Assert.Single(docs);                       // solo la vigente
        Assert.Equal(5000m, resumen!.Resumen.SaldoTotal);
    }

    [SkippableFact]
    public async Task Abono_anulado_no_resta_del_saldo()
    {
        var cxpId = await SembrarFacturaAsync(total: 5000m, fecha: Hoy(-40), vencimiento: Hoy(-10));
        await SembrarAbonoFacturaAsync(cxpId, numero: 1, monto: 2000m, fecha: Hoy(-5));
        await SembrarAbonoFacturaAsync(cxpId, numero: 2, monto: 1500m, fecha: Hoy(-3), estado: 'A');

        var doc = Assert.Single(await _service!.GetDocumentosAsync(_cod));

        Assert.Equal(2000m, doc.Abonado);          // el anulado no cuenta
        Assert.Equal(3000m, doc.Saldo);
    }

    // ---------------------------------------------------------------- Compromisos (OPD)

    [SkippableFact]
    public async Task Compromiso_con_abonos_deriva_su_saldo()
    {
        var orden = await SembrarCompromisoAsync(monto: 8000m, fecha: Hoy(-20));
        await SembrarAbonoCompromisoAsync(orden, numero: 1, monto: 3000m, fecha: Hoy(-10));

        var doc = Assert.Single(await _service!.GetDocumentosAsync(_cod));

        Assert.Equal(OrigenDocumentoProveedor.Compromiso, doc.Origen);
        Assert.Equal("Compromiso", doc.OrigenDescripcion);
        Assert.Equal(8000m, doc.Monto);
        Assert.Equal(3000m, doc.Abonado);
        Assert.Equal(5000m, doc.Saldo);
        Assert.StartsWith("OPD-", doc.NumeroDocumento);
    }

    /// <summary>
    /// ★ El caso que protege contra la deuda fantasma: los ~228 compromisos migrados de SIMAFI
    /// llegaron con status_transacc = true y CERO abonos (L 6.8M). Están saldados por definición
    /// (compat de OrdenesPagoDirectoService) y NO deben arrastrar deuda al estado de cuenta.
    /// </summary>
    [SkippableFact]
    public async Task Compromiso_legacy_procesado_sin_abonos_no_arrastra_deuda()
    {
        await SembrarCompromisoAsync(monto: 6_800_000m, fecha: Hoy(-500), statusTransacc: true);
        await SembrarCompromisoAsync(monto: 1000m, fecha: Hoy(-20));   // este sí es deuda real

        var docs = await _service!.GetDocumentosAsync(_cod);
        var resumen = await _service.GetResumenAsync(_cod);

        var doc = Assert.Single(docs);
        Assert.Equal(1000m, doc.Monto);
        Assert.Equal(1000m, resumen!.Resumen.SaldoTotal);
    }

    /// <summary>
    /// El correlativo del compromiso se rellena a 5 dígitos, pero NUNCA se trunca: los
    /// numero_orden reales llegan a 6 y con un LPAD(x,5,'0') a secas dos documentos distintos
    /// (333423 y 333426) salían ambos como "OPD-33342". Bug encontrado en el mirror.
    /// </summary>
    [SkippableFact]
    public async Task Numero_de_compromiso_largo_no_se_trunca_ni_colisiona()
    {
        await SembrarCompromisoAsync(monto: 100m, fecha: Hoy(-5), numeroOrden: 987123);
        await SembrarCompromisoAsync(monto: 200m, fecha: Hoy(-4), numeroOrden: 987126);
        await SembrarCompromisoAsync(monto: 300m, fecha: Hoy(-3), numeroOrden: 412);

        var docs = await _service!.GetDocumentosAsync(_cod);

        var numeros = new List<string>();
        foreach (var d in docs)
        {
            numeros.Add(d.NumeroDocumento);
        }

        Assert.Contains("OPD-987123", numeros);
        Assert.Contains("OPD-987126", numeros);
        Assert.Contains("OPD-00412", numeros);   // los cortos sí se rellenan a 5
        Assert.Equal(3, numeros.Count);
        Assert.Equal(numeros.Count, new HashSet<string>(numeros).Count);   // sin colisiones
    }

    [SkippableFact]
    public async Task Compromiso_anulado_queda_fuera()
    {
        await SembrarCompromisoAsync(monto: 4000m, fecha: Hoy(-20), anulado: true);

        var resumen = await _service!.GetResumenAsync(_cod);

        Assert.Empty(await _service.GetDocumentosAsync(_cod));
        Assert.Equal(0m, resumen!.Resumen.SaldoTotal);
    }

    // ---------------------------------------------------------------- Resumen y antigüedad

    [SkippableFact]
    public async Task Antiguedad_ubica_cada_documento_en_su_tramo()
    {
        await SembrarFacturaAsync(total: 100m, fecha: Hoy(-60), vencimiento: Hoy(+10)); // corriente
        await SembrarFacturaAsync(total: 200m, fecha: Hoy(-60), vencimiento: Hoy(-15)); // 1-30
        await SembrarFacturaAsync(total: 400m, fecha: Hoy(-90), vencimiento: Hoy(-45)); // 31-60
        await SembrarFacturaAsync(total: 800m, fecha: Hoy(-120), vencimiento: Hoy(-75)); // 61-90
        await SembrarFacturaAsync(total: 1600m, fecha: Hoy(-200), vencimiento: Hoy(-120)); // +90

        var r = (await _service!.GetResumenAsync(_cod))!.Resumen;

        Assert.Equal(100m, r.AntiguedadCorriente);
        Assert.Equal(200m, r.Antiguedad30);
        Assert.Equal(400m, r.Antiguedad60);
        Assert.Equal(800m, r.Antiguedad90);
        Assert.Equal(1600m, r.AntiguedadMas90);

        Assert.Equal(3100m, r.SaldoTotal);
        Assert.Equal(3000m, r.SaldoVencido);
        Assert.Equal(100m, r.SaldoPorVencer);
        Assert.Equal(5, r.DocumentosPendientes);

        // La suma de los tramos es exactamente el saldo.
        Assert.Equal(r.SaldoTotal,
            r.AntiguedadCorriente + r.Antiguedad30 + r.Antiguedad60 + r.Antiguedad90 + r.AntiguedadMas90);
    }

    [SkippableFact]
    public async Task Resumen_reporta_el_ultimo_pago_de_cualquiera_de_las_dos_ramas()
    {
        var cxpId = await SembrarFacturaAsync(total: 5000m, fecha: Hoy(-40), vencimiento: Hoy(-10));
        await SembrarAbonoFacturaAsync(cxpId, numero: 1, monto: 1200m, fecha: Hoy(-9));

        var orden = await SembrarCompromisoAsync(monto: 8000m, fecha: Hoy(-20));
        await SembrarAbonoCompromisoAsync(orden, numero: 1, monto: 777m, fecha: Hoy(-2)); // el más reciente

        var r = (await _service!.GetResumenAsync(_cod))!.Resumen;

        Assert.Equal(777m, r.UltimoPagoMonto);
        Assert.Equal(Hoy(-2), r.UltimoPagoFecha);
    }

    // ---------------------------------------------------------------- Movimientos

    [SkippableFact]
    public async Task Movimientos_cierran_en_el_saldo_del_resumen()
    {
        var cxpId = await SembrarFacturaAsync(total: 5000m, fecha: Hoy(-40), vencimiento: Hoy(-10));
        await SembrarAbonoFacturaAsync(cxpId, numero: 1, monto: 2000m, fecha: Hoy(-5));

        var orden = await SembrarCompromisoAsync(monto: 8000m, fecha: Hoy(-20));
        await SembrarAbonoCompromisoAsync(orden, numero: 1, monto: 3000m, fecha: Hoy(-3));

        var movs = await _service!.GetMovimientosAsync(_cod);
        var resumen = (await _service.GetResumenAsync(_cod))!.Resumen;

        Assert.Equal(4, movs.Count);                                  // 2 cargos + 2 abonos
        Assert.Equal(resumen.SaldoTotal, movs[^1].SaldoCorrido);      // 13000 - 5000 = 8000
        Assert.Equal(8000m, movs[^1].SaldoCorrido);

        // El primer movimiento es el cargo más antiguo (el compromiso, de hace 20 días).
        Assert.Equal(TipoMovimientoProveedor.Cargo, movs[0].Tipo);
        Assert.Equal("Cargo", movs[0].TipoDescripcion);
    }

    /// <summary>
    /// Un documento pagado del todo sale de la lista de pendientes, pero sus dos líneas siguen en
    /// el libro y netean a cero. Caso encontrado en el mirror (una factura de 267.25 ya saldada):
    /// es lo que separa "documentos(todos)" de "documentos(pendientes)" sin mover el saldo.
    /// </summary>
    [SkippableFact]
    public async Task Documento_pagado_sale_de_pendientes_pero_el_libro_sigue_cuadrando()
    {
        var pagada = await SembrarFacturaAsync(total: 267.25m, fecha: Hoy(-30), vencimiento: Hoy(-1));
        await SembrarAbonoFacturaAsync(pagada, numero: 1, monto: 267.25m, fecha: Hoy(-1));
        await SembrarFacturaAsync(total: 5000m, fecha: Hoy(-40), vencimiento: Hoy(-10));

        var pendientes = await _service!.GetDocumentosAsync(_cod, soloPendientes: true);
        var todos = await _service.GetDocumentosAsync(_cod, soloPendientes: false);
        var movs = await _service.GetMovimientosAsync(_cod);
        var resumen = (await _service.GetResumenAsync(_cod))!.Resumen;

        Assert.Single(pendientes);                 // la pagada no aparece
        Assert.Equal(2, todos.Count);              // pero sigue existiendo
        Assert.Equal(3, movs.Count);               // 2 cargos + 1 abono

        // La invariante que de verdad importa: el libro neteado == el saldo del resumen.
        decimal cargos = 0m, abonos = 0m;
        foreach (var m in movs)
        {
            cargos += m.Cargo;
            abonos += m.Abono;
        }

        Assert.Equal(5000m, resumen.SaldoTotal);
        Assert.Equal(resumen.SaldoTotal, cargos - abonos);
        Assert.Equal(resumen.SaldoTotal, movs[^1].SaldoCorrido);
    }

    [SkippableFact]
    public async Task Filtrar_movimientos_por_fecha_no_altera_el_saldo_corrido()
    {
        var cxpId = await SembrarFacturaAsync(total: 5000m, fecha: Hoy(-40), vencimiento: Hoy(-10));
        await SembrarAbonoFacturaAsync(cxpId, numero: 1, monto: 2000m, fecha: Hoy(-5));

        var completos = await _service!.GetMovimientosAsync(_cod);
        var recortados = await _service.GetMovimientosAsync(_cod, desde: Hoy(-6));

        // El abono es el único dentro del rango, y conserva su acumulado histórico (3000),
        // no el del rango (que sería -2000).
        var abono = Assert.Single(recortados);
        Assert.Equal(TipoMovimientoProveedor.Abono, abono.Tipo);
        Assert.Equal(3000m, abono.SaldoCorrido);
        Assert.Equal(completos[^1].SaldoCorrido, abono.SaldoCorrido);
    }

    // ---------------------------------------------------------------- Tenancy

    [SkippableFact]
    public async Task Documentos_de_otra_empresa_no_contaminan_el_estado_de_cuenta()
    {
        await SembrarCompromisoAsync(monto: 1000m, fecha: Hoy(-20));
        await SembrarCompromisoAsync(monto: 500_000m, fecha: Hoy(-20), companyId: CompanyId + 9000);

        var resumen = await _service!.GetResumenAsync(_cod);

        Assert.Single(await _service.GetDocumentosAsync(_cod));
        Assert.Equal(1000m, resumen!.Resumen.SaldoTotal);
    }

    [SkippableFact]
    public async Task Proveedor_inexistente_devuelve_null()
    {
        Assert.Null(await _service!.GetResumenAsync("NO-EXISTE-ZZZ"));
        Assert.Null(await _service.GetDatosImpresionAsync("NO-EXISTE-ZZZ"));
    }

    // ---------------------------------------------------------------- Impresión (F3)

    [SkippableFact]
    public async Task Datos_de_impresion_traen_empresa_proveedor_resumen_y_documentos()
    {
        var cxpId = await SembrarFacturaAsync(total: 5000m, fecha: Hoy(-40), vencimiento: Hoy(-10));
        await SembrarAbonoFacturaAsync(cxpId, numero: 1, monto: 2000m, fecha: Hoy(-5));

        var datos = await _service!.GetDatosImpresionAsync(_cod, impresoPor: "tester");

        Assert.NotNull(datos);
        Assert.Equal(_cod, datos!.Codigo);
        Assert.Equal("PROVEEDOR PRUEBA ESTADO CUENTA", datos.Nombre);
        Assert.Equal("tester", datos.ImpresoPor);
        Assert.False(string.IsNullOrWhiteSpace(datos.EmpresaNombre));   // sale de cfg_companies
        Assert.Equal(3000m, datos.Resumen.SaldoTotal);

        var item = Assert.Single(datos.Items);
        Assert.Equal(3000m, item.Saldo);
        // El reporte bindea estos textos, no las fechas crudas (DateOnly no lo formatea el motor).
        Assert.Equal(Hoy(-40).ToString("dd/MM/yyyy"), item.FechaTexto);
        Assert.Equal(Hoy(-10).ToString("dd/MM/yyyy"), item.VencimientoTexto);
        Assert.Equal("10", item.DiasTexto);
    }

    [SkippableFact]
    public async Task Impresion_con_soloPendientes_apagado_incluye_los_documentos_pagados()
    {
        var pagada = await SembrarFacturaAsync(total: 400m, fecha: Hoy(-20), vencimiento: Hoy(-2));
        await SembrarAbonoFacturaAsync(pagada, numero: 1, monto: 400m, fecha: Hoy(-1));
        await SembrarFacturaAsync(total: 1000m, fecha: Hoy(-20), vencimiento: Hoy(-2));

        var soloPendientes = await _service!.GetDatosImpresionAsync(_cod, soloPendientes: true);
        var todos = await _service.GetDatosImpresionAsync(_cod, soloPendientes: false);

        Assert.Single(soloPendientes!.Items);
        Assert.Equal(2, todos!.Items.Count);
        Assert.Equal(1000m, todos.Resumen.SaldoTotal);   // el resumen no cambia
    }

    // ================================================================ Sembrado

    private async Task<string> SembrarProveedorAsync()
    {
        var codigo = "ZEC" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        await EjecutarAsync(@"
            INSERT INTO public.prv_proveedores
                (cod_proveedor, cod_tipoproveedor, nombre, cuenta_contable, direccion,
                 fecha_creacion, usuario_creo, status, company_id)
            SELECT @cod, COALESCE((SELECT MIN(cod_tipoproveedor) FROM public.prv_tipoproveedor), 1),
                   'PROVEEDOR PRUEBA ESTADO CUENTA', '21101010941', 'Prueba',
                   now(), 'test', TRUE, @c",
            ("cod", codigo), ("c", (int)CompanyId));

        return codigo;
    }

    /// <summary>Crea la factura (alm_compra_hdr) y su cuenta por pagar; devuelve el id de la CxP.</summary>
    private async Task<int> SembrarFacturaAsync(
        decimal total, DateOnly fecha, DateOnly vencimiento, short? estadoId = null)
    {
        var estado = estadoId ?? EstadoCompraCxp.Pendiente;

        var hdrId = await EscalarAsync<int>(@"
            INSERT INTO public.alm_compra_hdr
                (company_id, numero, fecha, fecha_vencimiento, cod_proveedor, proveedor,
                 numero_factura_sar, bodega_id, sub_total, impuesto, total, observaciones, estado)
            VALUES (@c,
                    COALESCE((SELECT MAX(numero) FROM public.alm_compra_hdr WHERE company_id = @c), 0) + 1,
                    @f, @v, @cod, 'PROVEEDOR PRUEBA ESTADO CUENTA',
                    @sar, @bod, @total, 0, @total, 'Compra de prueba', 1)
            RETURNING id",
            ("c", CompanyId), ("f", fecha), ("v", vencimiento), ("cod", _cod),
            ("sar", "TEST-" + Guid.NewGuid().ToString("N")[..8]), ("bod", _bodegaId), ("total", total));

        return await EscalarAsync<int>(@"
            INSERT INTO public.alm_compra_cxp
                (company_id, compra_hdr_id, cod_proveedor, proveedor, numero_factura_sar,
                 fecha, fecha_vencimiento, condicion_pago, monto, saldo, estado_id)
            SELECT @c, @hdr, @cod, 'PROVEEDOR PRUEBA ESTADO CUENTA', h.numero_factura_sar,
                   @f, @v, 2, @total, @total, @estado
            FROM public.alm_compra_hdr h
            WHERE h.company_id = @c AND h.id = @hdr
            RETURNING id",
            ("c", CompanyId), ("hdr", hdrId), ("cod", _cod), ("f", fecha), ("v", vencimiento),
            ("total", total), ("estado", estado));
    }

    private Task SembrarAbonoFacturaAsync(
        int cxpId, int numero, decimal monto, DateOnly fecha, char estado = 'V')
        => EjecutarAsync(@"
            INSERT INTO public.alm_compra_cxp_abono
                (company_id, cxp_id, numero_abono, fecha, monto, metodo_pago, estado)
            VALUES (@c, @cxp, @n, @f, @m, 'transferencia', @e)",
            ("c", CompanyId), ("cxp", cxpId), ("n", numero), ("f", fecha), ("m", monto),
            ("e", estado.ToString()));

    /// <summary>Crea un compromiso y devuelve su numero_orden.</summary>
    private async Task<int> SembrarCompromisoAsync(
        decimal monto, DateOnly fecha, bool statusTransacc = false, bool anulado = false,
        long? companyId = null, int? numeroOrden = null)
    {
        var company = companyId ?? CompanyId;

        if (numeroOrden.HasValue)
        {
            return await EscalarAsync<int>(@"
                INSERT INTO public.prv_compromiso_hdr
                    (company_id, numero_orden, fecha, monto, concepto, cod_proveedor,
                     nombre_proveedor, status_transacc, anulado)
                VALUES (@c, @n, @f, @m, 'Compromiso de prueba', @cod,
                        'PROVEEDOR PRUEBA ESTADO CUENTA', @st, @an)
                RETURNING numero_orden",
                ("c", company), ("n", numeroOrden.Value), ("f", fecha.ToDateTime(TimeOnly.MinValue)),
                ("m", monto), ("cod", _cod), ("st", statusTransacc), ("an", anulado));
        }

        return await EscalarAsync<int>(@"
            INSERT INTO public.prv_compromiso_hdr
                (company_id, numero_orden, fecha, monto, concepto, cod_proveedor,
                 nombre_proveedor, status_transacc, anulado)
            SELECT @c,
                   COALESCE((SELECT MAX(numero_orden) FROM public.prv_compromiso_hdr), 0) + 1,
                   @f, @m, 'Compromiso de prueba', @cod,
                   'PROVEEDOR PRUEBA ESTADO CUENTA', @st, @an
            RETURNING numero_orden",
            ("c", company), ("f", fecha.ToDateTime(TimeOnly.MinValue)), ("m", monto),
            ("cod", _cod), ("st", statusTransacc), ("an", anulado));
    }

    private Task SembrarAbonoCompromisoAsync(
        int numeroOrden, int numero, decimal monto, DateOnly fecha, char estado = 'V')
        => EjecutarAsync(@"
            INSERT INTO public.prv_compromiso_abono
                (company_id, numero_orden, numero_abono, fecha, monto, metodo_pago, estado, usuario_creo)
            VALUES (@c, @o, @n, @f, @m, 'TRANSFERENCIA', @e, 'test')",
            ("c", CompanyId), ("o", numeroOrden), ("n", numero),
            ("f", fecha.ToDateTime(TimeOnly.MinValue)), ("m", monto), ("e", estado.ToString()));

    // ================================================================ Utilidades

    private static DateOnly Hoy(int dias) => DateOnly.FromDateTime(DateTime.Today).AddDays(dias);

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
