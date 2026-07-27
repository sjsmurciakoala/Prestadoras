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
using SIAD.Core.DTOs.Cobranza;
using SIAD.Core.DTOs.Cobros;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Cobros;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Cobros;

/// <summary>
/// Unificación cobranza F7 H2b (2026-07-30): la NOTA DE DÉBITO es un documento
/// cobrable por el motor único (documento_tipo = 3). Nace con saldo_pendiente
/// = total_nota, entra al saldo del cliente (sp_obtener_cliente_saldo v6), se
/// cobra en caja rebajando su saldo vivo y el reverso lo restituye. El estado
/// FISCAL (cfg_estado_documento_fiscal) no cambia por cobros.
/// </summary>
[Collection("Postgres")]
public sealed class NotaDebitoCobrableTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private CobroService? _motor;

    public NotaDebitoCobrableTests(PostgresFixture fixture) : base(fixture) { }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (!Fixture.Available) return;

        var options = new DbContextOptionsBuilder<SiadDbContext>()
            .UseNpgsql(Connection)
            .Options;
        _context = new SiadDbContext(options, new TestCurrentCompanyService(CompanyId));
        _context.Database.UseTransaction(Transaction);

        _motor = new CobroService(
            _context,
            new StubBanTransaccionesService(),
            new TestCurrentCompanyService(CompanyId),
            new StubCorteMasivoService());
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    private Task<decimal?> SaldoAsync(string clave) =>
        Connection.ExecuteScalarAsync<decimal?>(new CommandDefinition(
            "SELECT saldo_actual FROM public.sp_obtener_cliente_saldo(@C, @Clave)",
            new { C = CompanyId, Clave = clave }, Transaction));

    [SkippableFact]
    public async Task ND_nace_cobrable_se_cobra_en_caja_y_el_reverso_la_restituye()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var caiNdId = await Connection.ExecuteScalarAsync<long?>(new CommandDefinition(@"
            SELECT cai_id FROM public.adm_cai_facturacion
            WHERE company_id = @C AND tipo_documento_fiscal_id = 7
            LIMIT 1", new { C = CompanyId }, Transaction));
        Skip.If(caiNdId is null, "No hay CAI tipo ND (7) en esta company.");

        var factura = await Connection.QueryFirstOrDefaultAsync<(int id, string clave)>(new CommandDefinition(@"
            SELECT f.id, f.clientecodigo FROM public.factura f
            WHERE f.company_id = @C AND f.estado = 'A' AND f.clientecodigo IS NOT NULL
            ORDER BY f.id LIMIT 1", new { C = CompanyId }, Transaction));
        Skip.If(factura.id == 0, "No hay factura activa en esta BD.");

        var saldoAntes = await SaldoAsync(factura.clave) ?? 0m;

        // 1) Emitir ND de 25.00: nace con saldo vivo y sube el saldo del cliente.
        var notaId = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(@"
            SELECT nota_debito_id FROM public.sp_adm_emitir_nota_debito(
                p_company_id := @C,
                p_factura_origen_id := @F,
                p_motivo_aumento_id := 1::smallint,
                p_motivo_detalle := 'ND cobrable F7',
                p_monto_aumentar := 25.00::numeric,
                p_lineas := NULL::jsonb,
                p_usuario_emisor := 'TEST-F7',
                p_cai_id := @Cai)",
            new { C = CompanyId, F = factura.id, Cai = caiNdId }, Transaction));

        var saldoVivo = await Connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            "SELECT saldo_pendiente FROM public.adm_nota_debito WHERE nota_debito_id = @Id",
            new { Id = notaId }, Transaction));
        Assert.Equal(25.00m, saldoVivo);
        Assert.Equal(saldoAntes + 25.00m, await SaldoAsync(factura.clave));

        // 2) Cobrarla en caja por el motor (documento_tipo = 3).
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.sesion_caja (company_id, usuario_apertura, fecha_apertura, estado)
            VALUES (@C, 'test-f7nd', now(), 'ABIERTA')", new { C = CompanyId }, Transaction));

        var cobro = await _motor!.RegistrarCobroAsync(new CobroCrearDto
        {
            Canal = CanalCobro.Caja,
            ClienteClave = factura.clave,
            Usuario = "test-f7nd",
            FormaPago = "EFECTIVO",
            Aplicaciones = [new CobroAplicacionDto
            {
                DocumentoTipo = DocumentoCobroTipo.NotaDebito,
                NotaDebitoId = notaId,
                Monto = 25.00m
            }]
        });
        Assert.True(cobro.Success, cobro.Message);

        var (saldoTrasCobro, estadoFiscal) = await Connection.QueryFirstAsync<(decimal, short)>(new CommandDefinition(
            "SELECT saldo_pendiente, estado_id FROM public.adm_nota_debito WHERE nota_debito_id = @Id",
            new { Id = notaId }, Transaction));
        Assert.Equal(0m, saldoTrasCobro);
        Assert.Equal(1, estadoFiscal);   // el estado FISCAL no cambia por cobros
        Assert.Equal(saldoAntes, await SaldoAsync(factura.clave));

        // La aplicación del motor referencia la ND (documento_tipo 3).
        var aplicacion = await Connection.QueryFirstAsync<(short tipo, long? ndId)>(new CommandDefinition(@"
            SELECT a.documento_tipo, a.nota_debito_id
            FROM public.adm_pago_aplicacion a
            WHERE a.company_id = @C AND a.nota_debito_id = @Id",
            new { C = CompanyId, Id = notaId }, Transaction));
        Assert.Equal(3, aplicacion.tipo);

        // 3) Reversar: el saldo vivo vuelve.
        var pagoId = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(@"
            SELECT a.pago_id FROM public.adm_pago_aplicacion a WHERE a.nota_debito_id = @Id",
            new { Id = notaId }, Transaction));
        var reverso = await _motor.ReversarCobroAsync(new CobroReversoDto
        {
            PagoId = pagoId,
            Usuario = "test-f7nd",
            Motivo = "prueba ND F7"
        });
        Assert.True(reverso.Success, reverso.Message);

        Assert.Equal(25.00m, await Connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
            "SELECT saldo_pendiente FROM public.adm_nota_debito WHERE nota_debito_id = @Id",
            new { Id = notaId }, Transaction)));
        Assert.Equal(saldoAntes + 25.00m, await SaldoAsync(factura.clave));
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
            => throw new NotSupportedException("El test de ND cubre solo EFECTIVO.");
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
