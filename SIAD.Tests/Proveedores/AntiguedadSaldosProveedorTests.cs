using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NSubstitute;
using Xunit;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Proveedores;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Contabilidad;
using SIAD.Services.Proveedores;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Proveedores;

/// <summary>
/// Antigüedad de saldos del proveedor (aging de CxP) sobre <c>fn_prv_antiguedad_saldos</c>
/// (<c>Database/2026-08-14_prv_antiguedad_saldos.sql</c>).
/// <para>
/// El aging es multiempresa y multi-proveedor: la función trae TODOS los proveedores con saldo de
/// la empresa, así que cada test siembra su propio proveedor con código único y localiza SU fila
/// en el resultado con <see cref="FilaDe"/> (los otros proveedores de la base de prueba también
/// aparecen, pero no se tocan). Todo corre dentro de BEGIN … ROLLBACK.
/// </para>
/// </summary>
[Collection("Postgres")]
public class AntiguedadSaldosProveedorTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private AntiguedadSaldosProveedorService? _service;
    private ProveedorEstadoCuentaService? _estadoCuenta;   // para el cuadre
    private string _cod = string.Empty;
    private int _bodegaId;

    public AntiguedadSaldosProveedorTests(PostgresFixture fixture) : base(fixture) { }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (!Fixture.Available) return;

        var options = new DbContextOptionsBuilder<SiadDbContext>().UseNpgsql(Connection).Options;
        var company = new TestCurrentCompanyService(CompanyId);
        _context = new SiadDbContext(options, company);
        _context.Database.UseTransaction(Transaction);

        _service = new AntiguedadSaldosProveedorService(_context, company);

        var formato = Substitute.For<IAccountFormatService>();
        formato.GetFormatAsync(Arg.Any<CancellationToken>()).Returns(AccountFormat.Default);
        _estadoCuenta = new ProveedorEstadoCuentaService(_context, company, formato);

        _cod = await SembrarProveedorAsync();
        _bodegaId = await EscalarAsync<int>(
            "SELECT id FROM public.alm_bodega WHERE company_id = @c ORDER BY id LIMIT 1", ("c", CompanyId));
    }

    // ---------------------------------------------------------------- Los seis tramos

    [SkippableFact]
    public async Task Aging_reparte_el_saldo_en_los_seis_tramos_incluidos_los_dos_nuevos()
    {
        await SembrarFacturaAsync(100m, Hoy(-60), Hoy(+10));    // por vencer  (días ≤ 0)
        await SembrarFacturaAsync(200m, Hoy(-60), Hoy(-15));    // 1 – 30
        await SembrarFacturaAsync(400m, Hoy(-90), Hoy(-45));    // 31 – 60
        await SembrarFacturaAsync(800m, Hoy(-120), Hoy(-75));   // 61 – 90
        await SembrarFacturaAsync(1600m, Hoy(-200), Hoy(-105)); // 91 – 120  (nuevo)
        await SembrarFacturaAsync(3200m, Hoy(-260), Hoy(-150)); // más de 120 (nuevo)

        var fila = FilaDe(await _service!.GetAsync(), _cod);

        Assert.NotNull(fila);
        Assert.Equal(100m, fila!.PorVencer);
        Assert.Equal(200m, fila.Tramo30);
        Assert.Equal(400m, fila.Tramo60);
        Assert.Equal(800m, fila.Tramo90);
        Assert.Equal(1600m, fila.Tramo120);
        Assert.Equal(3200m, fila.TramoMas120);

        Assert.Equal(6200m, fila.Vencido);       // 200+400+800+1600+3200
        Assert.Equal(6300m, fila.SaldoTotal);    // + los 100 por vencer
        Assert.Equal(6, fila.DocumentosPendientes);

        // La invariante del aging: los seis tramos suman exactamente el saldo total.
        Assert.Equal(fila.SaldoTotal,
            fila.PorVencer + fila.Tramo30 + fila.Tramo60 + fila.Tramo90 + fila.Tramo120 + fila.TramoMas120);
    }

    // ---------------------------------------------------------------- Cuadre con estado de cuenta

    /// <summary>
    /// El aging no debe inventar ni perder un lempiro respecto al estado de cuenta del proveedor:
    /// mismos totales, y el viejo tramo «&gt; 90» del resumen se abre exactamente en 91–120 + &gt;120.
    /// Con un abono parcial de por medio para confirmar que reparte el SALDO, no el monto.
    /// </summary>
    [SkippableFact]
    public async Task Aging_cuadra_con_el_estado_de_cuenta_del_proveedor()
    {
        await SembrarFacturaAsync(1000m, Hoy(-60), Hoy(-15));       // 1 – 30
        var vieja = await SembrarFacturaAsync(3000m, Hoy(-260), Hoy(-150)); // > 120
        await SembrarAbonoFacturaAsync(vieja, numero: 1, monto: 1000m, fecha: Hoy(-100)); // saldo 2000
        await SembrarCompromisoAsync(500m, Hoy(-40));              // 31 – 60 (el OPD vence en su fecha)

        var fila = FilaDe(await _service!.GetAsync(), _cod)!;
        var rs = (await _estadoCuenta!.GetResumenAsync(_cod))!.Resumen;

        Assert.Equal(rs.SaldoTotal, fila.SaldoTotal);
        Assert.Equal(rs.SaldoVencido, fila.Vencido);
        Assert.Equal(rs.SaldoPorVencer, fila.PorVencer);
        Assert.Equal(rs.Antiguedad30, fila.Tramo30);
        Assert.Equal(rs.Antiguedad60, fila.Tramo60);
        Assert.Equal(rs.Antiguedad90, fila.Tramo90);

        // El aporte de F0: lo que el estado de cuenta agrupa en «más de 90» aquí se abre en dos.
        Assert.Equal(rs.AntiguedadMas90, fila.Tramo120 + fila.TramoMas120);
        Assert.Equal(2000m, fila.TramoMas120);   // el saldo tras el abono, no los 3000 del monto
    }

    // ---------------------------------------------------------------- Filtros

    [SkippableFact]
    public async Task Aging_solo_vencido_deja_por_vencer_en_cero()
    {
        await SembrarFacturaAsync(500m, Hoy(-60), Hoy(+20));   // por vencer
        await SembrarFacturaAsync(700m, Hoy(-60), Hoy(-15));   // vencido 1 – 30

        var conPorVencer = FilaDe(await _service!.GetAsync(incluirPorVencer: true), _cod)!;
        var soloVencido = FilaDe(await _service.GetAsync(incluirPorVencer: false), _cod)!;

        Assert.Equal(500m, conPorVencer.PorVencer);
        Assert.Equal(1200m, conPorVencer.SaldoTotal);

        Assert.Equal(0m, soloVencido.PorVencer);
        Assert.Equal(700m, soloVencido.SaldoTotal);    // solo lo vencido
        Assert.Equal(700m, soloVencido.Vencido);
    }

    [SkippableFact]
    public async Task Aging_filtra_por_origen()
    {
        await SembrarFacturaAsync(1000m, Hoy(-60), Hoy(-15));   // compra
        await SembrarCompromisoAsync(400m, Hoy(-40));           // compromiso

        var ambos = FilaDe(await _service!.GetAsync(origen: 0), _cod)!;
        var soloCompras = FilaDe(await _service.GetAsync(origen: 1), _cod)!;
        var soloCompromisos = FilaDe(await _service.GetAsync(origen: 2), _cod)!;

        Assert.Equal(1400m, ambos.SaldoTotal);
        Assert.Equal(1000m, soloCompras.SaldoTotal);
        Assert.Equal(400m, soloCompromisos.SaldoTotal);
    }

    // ---------------------------------------------------------------- Filtro por proveedor

    [SkippableFact]
    public async Task Filtrar_por_proveedor_devuelve_solo_ese_con_sus_totales()
    {
        await SembrarFacturaAsync(1000m, Hoy(-60), Hoy(-15));    // 1 – 30
        await SembrarFacturaAsync(500m, Hoy(-120), Hoy(-75));    // 61 – 90

        var soloMio = await _service!.GetAsync(codProveedor: _cod);

        // Una sola fila (la mía), pese a que la empresa tiene otros proveedores con saldo.
        var fila = Assert.Single(soloMio.Filas);
        Assert.Equal(_cod, fila.CodProveedor);
        Assert.Equal(1500m, fila.SaldoTotal);

        // Los totales del pie son los de esa fila, no los de toda la cartera.
        Assert.Equal(1, soloMio.Totales.Proveedores);
        Assert.Equal(1500m, soloMio.Totales.SaldoTotal);
        Assert.Equal(1000m, soloMio.Totales.Tramo30);
        Assert.Equal(500m, soloMio.Totales.Tramo90);
    }

    [SkippableFact]
    public async Task Filtrar_por_proveedor_inexistente_devuelve_vacio()
    {
        await SembrarFacturaAsync(1000m, Hoy(-60), Hoy(-15));

        var vacio = await _service!.GetAsync(codProveedor: "NO-EXISTE-ZZZ");

        Assert.Empty(vacio.Filas);
        Assert.Equal(0, vacio.Totales.Proveedores);
        Assert.Equal(0m, vacio.Totales.SaldoTotal);
    }

    // ---------------------------------------------------------------- Totales del pie

    [SkippableFact]
    public async Task Totales_del_pie_suman_todas_las_filas()
    {
        await SembrarFacturaAsync(1234m, Hoy(-60), Hoy(-15));

        var dto = await _service!.GetAsync();

        decimal sumaSaldo = 0m, sumaVencido = 0m, sumaMas120 = 0m;
        int sumaDocs = 0;
        foreach (var f in dto.Filas)
        {
            sumaSaldo += f.SaldoTotal;
            sumaVencido += f.Vencido;
            sumaMas120 += f.TramoMas120;
            sumaDocs += f.DocumentosPendientes;
        }

        Assert.Equal(dto.Filas.Count, dto.Totales.Proveedores);
        Assert.Equal(sumaSaldo, dto.Totales.SaldoTotal);
        Assert.Equal(sumaVencido, dto.Totales.Vencido);
        Assert.Equal(sumaMas120, dto.Totales.TramoMas120);
        Assert.Equal(sumaDocs, dto.Totales.DocumentosPendientes);
        Assert.NotNull(FilaDe(dto, _cod));   // mi proveedor está entre las filas
    }

    // ---------------------------------------------------------------- Tenancy

    [SkippableFact]
    public async Task Deuda_de_otra_empresa_no_aparece_en_el_aging_actual()
    {
        // El proveedor existe en la empresa actual, pero su única deuda está en otra empresa.
        await SembrarCompromisoAsync(999m, Hoy(-20), companyId: CompanyId + 9000);

        var dto = await _service!.GetAsync();

        Assert.Null(FilaDe(dto, _cod));   // sin saldo en la empresa actual → no lista
    }

    // ================================================================ Sembrado

    private async Task<string> SembrarProveedorAsync()
    {
        var codigo = "ZAG" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        await EjecutarAsync(@"
            INSERT INTO public.prv_proveedores
                (cod_proveedor, cod_tipoproveedor, nombre, cuenta_contable, direccion,
                 fecha_creacion, usuario_creo, status, company_id)
            SELECT @cod, COALESCE((SELECT MIN(cod_tipoproveedor) FROM public.prv_tipoproveedor), 1),
                   'PROVEEDOR PRUEBA ANTIGUEDAD', '21101010941', 'Prueba',
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
                    @f, @v, @cod, 'PROVEEDOR PRUEBA ANTIGUEDAD',
                    @sar, @bod, @total, 0, @total, 'Compra de prueba', 1)
            RETURNING id",
            ("c", CompanyId), ("f", fecha), ("v", vencimiento), ("cod", _cod),
            ("sar", "TAG-" + Guid.NewGuid().ToString("N")[..8]), ("bod", _bodegaId), ("total", total));

        return await EscalarAsync<int>(@"
            INSERT INTO public.alm_compra_cxp
                (company_id, compra_hdr_id, cod_proveedor, proveedor, numero_factura_sar,
                 fecha, fecha_vencimiento, condicion_pago, monto, saldo, estado_id)
            SELECT @c, @hdr, @cod, 'PROVEEDOR PRUEBA ANTIGUEDAD', h.numero_factura_sar,
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
        long? companyId = null)
    {
        var company = companyId ?? CompanyId;

        return await EscalarAsync<int>(@"
            INSERT INTO public.prv_compromiso_hdr
                (company_id, numero_orden, fecha, monto, concepto, cod_proveedor,
                 nombre_proveedor, status_transacc, anulado)
            SELECT @c,
                   COALESCE((SELECT MAX(numero_orden) FROM public.prv_compromiso_hdr), 0) + 1,
                   @f, @m, 'Compromiso de prueba', @cod,
                   'PROVEEDOR PRUEBA ANTIGUEDAD', @st, @an
            RETURNING numero_orden",
            ("c", company), ("f", fecha.ToDateTime(TimeOnly.MinValue)), ("m", monto),
            ("cod", _cod), ("st", statusTransacc), ("an", anulado));
    }

    // ================================================================ Utilidades

    private static DateOnly Hoy(int dias) => DateOnly.FromDateTime(DateTime.Today).AddDays(dias);

    /// <summary>Localiza la fila de un proveedor en el resultado (sin LINQ, por convención del repo).</summary>
    private static AntiguedadSaldosProveedorFilaDto? FilaDe(AntiguedadSaldosProveedorDto dto, string cod)
    {
        foreach (var f in dto.Filas)
        {
            if (f.CodProveedor == cod)
            {
                return f;
            }
        }

        return null;
    }

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
