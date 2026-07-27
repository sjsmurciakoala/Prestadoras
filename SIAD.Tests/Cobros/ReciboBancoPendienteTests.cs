using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Bancos;
using SIAD.Core.DTOs.Caja;
using SIAD.Core.DTOs.Clientes;
using SIAD.Core.DTOs.Cobranza;
using SIAD.Core.DTOs.Cobros;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Caja;
using SIAD.Services.Clientes;
using SIAD.Services.Cobros;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Cobros;

/// <summary>
/// Unificación cobranza F7 H1 (2026-07-30): los recibos "para banco"
/// pendientes viven en adm_recibo_banco_pendiente (fuente de verdad para
/// listar, anular, cobrar y conciliar); la fila legacy 202/'P' queda solo
/// para la impresión hasta F7 H2. estado_id reusa adm_estado_pago
/// (2 PENDIENTE, 1 APLICADO con cobrado_pago_id, 3 ANULADO/CUBIERTO).
/// </summary>
[Collection("Postgres")]
public sealed class ReciboBancoPendienteTests : IntegrationTestBase, IAsyncLifetime
{
    private const long Empresa = 9994;   // sintética; rollback al final
    private const string Clave = "RBP-01";

    private SiadDbContext? _context;
    private AbonoService? _abonos;
    private CobroService? _motor;

    public ReciboBancoPendienteTests(PostgresFixture fixture) : base(fixture) { }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (!Fixture.Available) return;

        var options = new DbContextOptionsBuilder<SiadDbContext>()
            .UseNpgsql(Connection)
            .Options;
        _context = new SiadDbContext(options, new TestCurrentCompanyService(Empresa));
        _context.Database.UseTransaction(Transaction);

        _motor = new CobroService(
            _context,
            new StubBanTransaccionesService(),
            new TestCurrentCompanyService(Empresa),
            new StubCorteMasivoService());

        // IClientesService solo se usa al imprimir el recibo (fuera de estos
        // tests) — null! es seguro aquí.
        _abonos = new AbonoService(
            _context,
            new StubBanTransaccionesService(),
            new TestCurrentCompanyService(Empresa),
            new StubCorteMasivoService(),
            null!,
            _motor);
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    // ------------------------------------------------------------------ setup

    private async Task PrepararAsync()
    {
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.cfg_company (company_id, code, commercial_name, legal_name, tax_id, country_code, currency_code, timezone, status, created_at, created_by)
            VALUES (@id, 'X994', 'Recibos', 'Empresa Recibos F7', 'RTN-R', 'HND', 'HNL', 'America/Tegucigalpa', 'A', now(), 't')
            ON CONFLICT (company_id) DO NOTHING",
            new { id = Empresa }, Transaction));
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.cliente_maestro (company_id, maestro_cliente_clave, maestro_cliente_identidad, maestro_cliente_nombre, estado)
            VALUES (@id, @clave, '0000000000000', 'CLIENTE RBP F7', true)",
            new { id = Empresa, clave = Clave }, Transaction));
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.adm_documento_secuencia
                (company_id, tipo_documento, canal_id, prefijo, longitud_padding, valor_actual, updated_by)
            VALUES (@id, 'RECIBO_PAGO', 0, 'REC-', 8, 0, 'test')
            ON CONFLICT (company_id, tipo_documento, canal_id) DO NOTHING",
            new { id = Empresa }, Transaction));
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.sesion_caja (company_id, usuario_apertura, fecha_apertura, estado)
            VALUES (@id, 'test-f7', now(), 'ABIERTA')",
            new { id = Empresa }, Transaction));
    }

    private async Task<(int facturaId, string numFactura)> CrearFacturaAsync(decimal monto, string sufijo)
    {
        var facturaId = await Connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
            INSERT INTO public.factura (company_id, numfactura, clientecodigo, tipofactura,
                ano, mes, fechaemision, estado, tipofacturacion, tipo_documento_fiscal_id, saldototal)
            VALUES (@companyId, @num, @clave, 'F', '2026', '7', current_date, 'A', 'S', 1, @monto)
            RETURNING id",
            new { companyId = Empresa, num = $"RBP-{sufijo}", clave = Clave, monto }, Transaction));
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.factura_detalle (company_id, factura_id, codigo, tiposervicio, montovalor, montovalor_saldo)
            VALUES (@companyId, @facturaId, 'AGUA_POTABLE', 'AGUA_POTABLE', @monto, @monto)",
            new { companyId = Empresa, facturaId, monto }, Transaction));
        return (facturaId, $"RBP-{sufijo}");
    }

    // ------------------------------------------------------------------ tests

    [SkippableFact]
    public async Task Generar_listar_y_anular_viven_en_la_tabla_nueva()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await PrepararAsync();
        var (facturaId, numFactura) = await CrearFacturaAsync(100m, "A");

        var generado = await _abonos!.GenerarReciboPendienteAsync(new GenerarReciboDto
        {
            ClienteClave = Clave,
            NumFactura = numFactura,
            Monto = 60m,
            Usuario = "test-f7"
        });
        Assert.True(generado.Success, generado.Message);

        // Fuente de verdad: fila en la tabla nueva, enlazada al espejo 'P'.
        var fila = await Connection.QueryFirstAsync<(long id, short estado, decimal monto, int? taIde)>(new CommandDefinition(
            "SELECT recibo_pendiente_id, estado_id, monto, transaccion_abonado_ide FROM public.adm_recibo_banco_pendiente WHERE company_id = @C AND factura_id = @F",
            new { C = Empresa, F = facturaId }, Transaction));
        Assert.Equal(2, fila.estado);
        Assert.Equal(60m, fila.monto);
        Assert.NotNull(fila.taIde);

        // El listado lee la tabla nueva y expone ambos ids.
        var listado = await _abonos.ListarRecibosPendientesPorClienteAsync(Clave);
        var pendiente = Assert.Single(listado);
        Assert.Equal(fila.id, pendiente.PendienteId);
        Assert.Equal(fila.taIde, pendiente.TransaccionId);
        Assert.Equal(60m, pendiente.Monto);

        // Control de disponible: no se puede duplicar el papel sobre el saldo.
        var exceso = await _abonos.GenerarReciboPendienteAsync(new GenerarReciboDto
        {
            ClienteClave = Clave,
            NumFactura = numFactura,
            Monto = 50m,
            Usuario = "test-f7"
        });
        Assert.False(exceso.Success);

        // Anular por PendienteId: tabla nueva 3 + espejo legacy 'A'.
        var anulado = await _abonos.AnularReciboPendienteAsync(new AnularReciboPendienteDto
        {
            PendienteId = fila.id,
            Usuario = "test-f7",
            Motivo = "prueba"
        });
        Assert.True(anulado.Success, anulado.Message);

        var (estadoNuevo, estadoLegacy) = await Connection.QueryFirstAsync<(short, string)>(new CommandDefinition(@"
            SELECT r.estado_id, t.estado
            FROM public.adm_recibo_banco_pendiente r
            JOIN public.transaccion_abonado t ON t.ide = r.transaccion_abonado_ide
            WHERE r.recibo_pendiente_id = @Id",
            new { Id = fila.id }, Transaction));
        Assert.Equal(3, estadoNuevo);
        Assert.Equal("A", estadoLegacy);
    }

    [SkippableFact]
    public async Task Cobrar_el_pendiente_lo_marca_aplicado_con_su_pago()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await PrepararAsync();
        var (facturaId, numFactura) = await CrearFacturaAsync(80m, "B");

        var generado = await _abonos!.GenerarReciboPendienteAsync(new GenerarReciboDto
        {
            ClienteClave = Clave,
            NumFactura = numFactura,
            Monto = 80m,
            Usuario = "test-f7"
        });
        Assert.True(generado.Success, generado.Message);
        var taIde = ((GenerarReciboResponseDto)generado.Data!).TransaccionId;

        var cobro = await _motor!.RegistrarCobroAsync(new CobroCrearDto
        {
            Canal = CanalCobro.Caja,
            ClienteClave = Clave,
            Usuario = "test-f7",
            FormaPago = "EFECTIVO",
            ReciboPendienteId = taIde,
            Aplicaciones = [new CobroAplicacionDto
            {
                DocumentoTipo = DocumentoCobroTipo.Factura,
                FacturaId = facturaId,
                Monto = 80m
            }]
        });
        Assert.True(cobro.Success, cobro.Message);

        var (estado, pagoId) = await Connection.QueryFirstAsync<(short, long?)>(new CommandDefinition(
            "SELECT estado_id, cobrado_pago_id FROM public.adm_recibo_banco_pendiente WHERE company_id = @C AND factura_id = @F",
            new { C = Empresa, F = facturaId }, Transaction));
        Assert.Equal(1, estado);       // APLICADO
        Assert.NotNull(pagoId);        // enlazado al documento del motor
    }

    [SkippableFact]
    public async Task Factura_saldada_por_otro_canal_cubre_el_pendiente()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        await PrepararAsync();
        var (facturaId, numFactura) = await CrearFacturaAsync(50m, "C");

        var generado = await _abonos!.GenerarReciboPendienteAsync(new GenerarReciboDto
        {
            ClienteClave = Clave,
            NumFactura = numFactura,
            Monto = 50m,
            Usuario = "test-f7"
        });
        Assert.True(generado.Success, generado.Message);

        // Cobro directo de la factura (sin usar el papel): el trigger de
        // conciliación cubre el pendiente automáticamente.
        var cobro = await _motor!.RegistrarCobroAsync(new CobroCrearDto
        {
            Canal = CanalCobro.Caja,
            ClienteClave = Clave,
            Usuario = "test-f7",
            FormaPago = "EFECTIVO",
            Aplicaciones = [new CobroAplicacionDto
            {
                DocumentoTipo = DocumentoCobroTipo.Factura,
                FacturaId = facturaId,
                Monto = 50m
            }]
        });
        Assert.True(cobro.Success, cobro.Message);

        var (estado, motivo) = await Connection.QueryFirstAsync<(short, string)>(new CommandDefinition(
            "SELECT estado_id, motivo_anulacion FROM public.adm_recibo_banco_pendiente WHERE company_id = @C AND factura_id = @F",
            new { C = Empresa, F = facturaId }, Transaction));
        Assert.Equal(3, estado);
        Assert.Contains("CUBIERTO", motivo);
    }

    // ------------------------------------------------------------------ stubs

    private sealed class TestCurrentCompanyService(long companyId) : ICurrentCompanyService
    {
        public long GetCompanyId() => companyId;
    }

    private sealed class StubBanTransaccionesService : SIAD.Services.Bancos.IBanTransaccionesService
    {
        public Task<IReadOnlyList<BanTransaccionListDto>> GetTransaccionesAsync(long companyId, long? bancoId = null, long? bancoCuentaId = null, DateOnly? fechaDesde = null, DateOnly? fechaHasta = null, bool incluirAnuladas = false, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<BanTransaccionListDto?> GetTransaccionByIdAsync(long banKardexId, long companyId, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<BanTransaccionDetalleDto?> GetTransaccionDetalleAsync(long banKardexId, long companyId, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<EstadoCuentaDto?> GetEstadoCuentaAsync(long companyId, long bancoCuentaId, DateOnly? fechaDesde = null, DateOnly? fechaHasta = null, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<(long BanKardexId, decimal SaldoResultante)> RegistrarMovimientoAsync(long bancoCuentaId, string idTipoTransaccion, DateOnly fechaMovimiento, string descripcion, string? referencia, string? sourceDocument, decimal tasaCambio, decimal monto, IReadOnlyList<BanTransaccionContraLineaDto> contraCuentas, string usuario, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<(long BanKardexIdAnulacion, decimal SaldoResultante)> AnularMovimientoAsync(long bancoCuentaId, long banKardexIdOriginal, string motivo, string usuario, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class StubCorteMasivoService : SIAD.Services.Cobranza.ICorteMasivoService
    {
        public Task<CorteMasivoHdrDto> GenerarAsync(GenerarCorteMasivoRequest request, string usuario, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<CorteMasivoHdrDto>> ListarAsync(CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<CorteMasivoDetalleDto?> ObtenerDetalleAsync(int hdrId, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<CorteMasivoDetalleDto?> ObtenerParaReimpresionAsync(int hdrId, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<int> CancelarOrdenesCorteClienteAsync(string clienteClave, string usuario, CancellationToken ct = default)
            => Task.FromResult(0);
    }
}
