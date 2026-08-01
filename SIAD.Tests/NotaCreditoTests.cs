using Dapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

[Collection("Postgres")]
public sealed class NotaCreditoTests : IntegrationTestBase
{
    public NotaCreditoTests(PostgresFixture fixture) : base(fixture) { }

    [SkippableFact]
    public async Task Tablas_y_SPs_NC_ND_V3_existen()
    {
        const string sql = @"
            SELECT
                to_regclass('public.adm_nota_credito')::text           AS tabla_nc,
                to_regclass('public.adm_nota_credito_detalle')::text   AS tabla_nc_det,
                to_regclass('public.adm_nota_debito')::text            AS tabla_nd,
                to_regclass('public.adm_nota_debito_detalle')::text    AS tabla_nd_det,
                to_regclass('public.cfg_motivo_aumento')::text         AS catalogo_motivo_aumento,
                to_regproc('public.sp_adm_emitir_nota_credito')::text  AS sp_nc,
                to_regproc('public.sp_adm_emitir_nota_debito')::text   AS sp_nd";

        var row = await Connection.QueryFirstAsync<(string? tabla_nc, string? tabla_nc_det,
            string? tabla_nd, string? tabla_nd_det, string? catalogo_motivo_aumento,
            string? sp_nc, string? sp_nd)>(
                new CommandDefinition(sql, transaction: Transaction));

        Assert.False(string.IsNullOrWhiteSpace(row.tabla_nc), "adm_nota_credito no existe.");
        Assert.False(string.IsNullOrWhiteSpace(row.tabla_nc_det), "adm_nota_credito_detalle no existe.");
        Assert.False(string.IsNullOrWhiteSpace(row.tabla_nd), "adm_nota_debito no existe.");
        Assert.False(string.IsNullOrWhiteSpace(row.tabla_nd_det), "adm_nota_debito_detalle no existe.");
        Assert.False(string.IsNullOrWhiteSpace(row.catalogo_motivo_aumento), "cfg_motivo_aumento no existe.");
        Assert.False(string.IsNullOrWhiteSpace(row.sp_nc), "sp_adm_emitir_nota_credito no existe.");
        Assert.False(string.IsNullOrWhiteSpace(row.sp_nd), "sp_adm_emitir_nota_debito no existe.");
    }

    [SkippableFact]
    public async Task SP_emitir_nota_credito_rechaza_factura_inexistente()
    {
        const string sql = @"
            SELECT cai_id FROM public.adm_cai_facturacion
            WHERE company_id = @CompanyId AND tipo_documento_fiscal_id = 6
            LIMIT 1";
        var caiNcId = await Connection.ExecuteScalarAsync<long?>(
            new CommandDefinition(sql, new { CompanyId = CompanyId }, Transaction));

        Skip.If(caiNcId is null, "No hay CAI tipo NC (6) en esta company — no se puede probar.");

        var exception = await Record.ExceptionAsync(async () =>
        {
            await Connection.QueryAsync(new CommandDefinition(@"
                SELECT * FROM public.sp_adm_emitir_nota_credito(
                    p_company_id := @CompanyId,
                    p_factura_origen_id := -1,
                    p_motivo_anulacion_id := 1::smallint,
                    p_motivo_detalle := 'test inexistente',
                    p_monto_disminuir := NULL::numeric,
                    p_lineas := NULL::jsonb,
                    p_usuario_emisor := 'TEST',
                    p_cai_id := @CaiId
                )",
                new { CompanyId = CompanyId, CaiId = caiNcId }, Transaction));
        });

        Assert.NotNull(exception);
        Assert.Contains("FACTURA_NO_EXISTE", exception!.Message);
    }

    [SkippableFact]
    public async Task SP_emitir_nota_credito_rechaza_factura_ya_anulada()
    {
        const string sql = @"
            SELECT f.id
            FROM public.factura f
            WHERE f.company_id = @CompanyId AND COALESCE(f.estado, '') = 'N'
            LIMIT 1";

        var facturaAnulada = await Connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(sql, new { CompanyId = CompanyId }, Transaction));

        Skip.If(facturaAnulada is null, "No hay facturas anuladas en esta company — no se puede probar el rechazo.");

        var caiNcId = await Connection.ExecuteScalarAsync<long?>(
            new CommandDefinition(@"
                SELECT cai_id FROM public.adm_cai_facturacion
                WHERE company_id = @CompanyId AND tipo_documento_fiscal_id = 6
                LIMIT 1",
                new { CompanyId = CompanyId }, Transaction));

        Skip.If(caiNcId is null, "No hay CAI tipo NC para esta company.");

        var exception = await Record.ExceptionAsync(async () =>
        {
            await Connection.QueryAsync(new CommandDefinition(@"
                SELECT * FROM public.sp_adm_emitir_nota_credito(
                    p_company_id := @CompanyId,
                    p_factura_origen_id := @FacturaId,
                    p_motivo_anulacion_id := 1::smallint,
                    p_motivo_detalle := 'test rechazo',
                    p_monto_disminuir := NULL::numeric,
                    p_lineas := NULL::jsonb,
                    p_usuario_emisor := 'TEST',
                    p_cai_id := @CaiId
                )",
                new { CompanyId = CompanyId, FacturaId = facturaAnulada, CaiId = caiNcId }, Transaction));
        });

        Assert.NotNull(exception);
        Assert.Contains("FACTURA_YA_ANULADA", exception!.Message);
    }

    // ------------------------------------------------------------------------
    // Unificación cobranza F7 H2a (2026-07-30): la NC PARCIAL aplica al
    // DOCUMENTO — rebaja montovalor_saldo por derrame FIFO y deja la factura
    // en 'B' (o 'C' si la cubre). Antes solo escribía el crédito espejo y,
    // tras el corte, una NC parcial no bajaba la deuda del cliente.
    // ------------------------------------------------------------------------

    [SkippableFact]
    public async Task NC_parcial_rebaja_el_saldo_del_documento()
    {
        var caiNcId = await Connection.ExecuteScalarAsync<long?>(
            new CommandDefinition(@"
                SELECT cai_id FROM public.adm_cai_facturacion
                WHERE company_id = @CompanyId AND tipo_documento_fiscal_id = 6
                LIMIT 1",
                new { CompanyId = CompanyId }, Transaction));
        Skip.If(caiNcId is null, "No hay CAI tipo NC para esta company.");

        // Factura pendiente con saldo suficiente para una NC parcial de 10.
        var factura = await Connection.QueryFirstOrDefaultAsync<(int id, decimal saldo)>(
            new CommandDefinition(@"
                SELECT f.id,
                       (SELECT COALESCE(SUM(COALESCE(d.montovalor_saldo, d.montovalor, 0)), 0)
                        FROM public.factura_detalle d WHERE d.factura_id = f.id) AS saldo
                FROM public.factura f
                WHERE f.company_id = @CompanyId AND f.estado = 'A'
                  AND EXISTS (SELECT 1 FROM public.factura_detalle d
                              WHERE d.factura_id = f.id
                                AND COALESCE(d.montovalor_saldo, d.montovalor, 0) > 15)
                ORDER BY f.id
                LIMIT 1",
                new { CompanyId = CompanyId }, Transaction));
        Skip.If(factura.id == 0, "No hay factura pendiente con saldo > 15 en esta BD.");

        await Connection.QueryAsync(new CommandDefinition(@"
            SELECT * FROM public.sp_adm_emitir_nota_credito(
                p_company_id := @CompanyId,
                p_factura_origen_id := @FacturaId,
                p_motivo_anulacion_id := 1::smallint,
                p_motivo_detalle := 'NC parcial F7',
                p_monto_disminuir := 10.00::numeric,
                p_lineas := NULL::jsonb,
                p_usuario_emisor := 'TEST-F7',
                p_cai_id := @CaiId
            )",
            new { CompanyId = CompanyId, FacturaId = factura.id, CaiId = caiNcId }, Transaction));

        var (estado, saldoNuevo) = await Connection.QueryFirstAsync<(string, decimal)>(
            new CommandDefinition(@"
                SELECT f.estado,
                       (SELECT COALESCE(SUM(COALESCE(d.montovalor_saldo, d.montovalor, 0)), 0)
                        FROM public.factura_detalle d WHERE d.factura_id = f.id)
                FROM public.factura f WHERE f.id = @Id",
                new { Id = factura.id }, Transaction));

        Assert.Equal("B", estado);
        Assert.Equal(factura.saldo - 10.00m, saldoNuevo);
    }

    // ------------------------------------------------------------------------
    // Pruebas operativas jul-2026: la VISTA PREVIA emite con el mismo SP dentro
    // de un SAVEPOINT y revierte — devuelve el documento completo (con la clave
    // del cliente) y NO deja rastro: ni fila en adm_nota_credito ni correlativo
    // consumido en el CAI.
    // ------------------------------------------------------------------------

    [SkippableFact]
    public async Task Vista_previa_de_nc_no_persiste_ni_consume_correlativo()
    {
        var caiNcId = await Connection.ExecuteScalarAsync<long?>(
            new CommandDefinition(@"
                SELECT cai_id FROM public.adm_cai_facturacion
                WHERE company_id = @CompanyId AND tipo_documento_fiscal_id = 6
                LIMIT 1",
                new { CompanyId = CompanyId }, Transaction));
        Skip.If(caiNcId is null, "No hay CAI tipo NC para esta company.");

        var facturaId = await Connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(@"
                SELECT f.id FROM public.factura f
                WHERE f.company_id = @CompanyId AND f.estado = 'A'
                  AND EXISTS (SELECT 1 FROM public.factura_detalle d
                              WHERE d.factura_id = f.id
                                AND COALESCE(d.montovalor_saldo, d.montovalor, 0) > 15)
                ORDER BY f.id LIMIT 1",
                new { CompanyId = CompanyId }, Transaction));
        Skip.If(facturaId is null, "No hay factura pendiente con saldo > 15 en esta BD.");

        var antes = await Connection.QueryFirstAsync<(long notas, long correlativo)>(
            new CommandDefinition(@"
                SELECT (SELECT count(*) FROM public.adm_nota_credito WHERE company_id = @CompanyId),
                       (SELECT correlativo_actual FROM public.adm_cai_facturacion WHERE cai_id = @CaiId)",
                new { CompanyId = CompanyId, CaiId = caiNcId }, Transaction));

        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<SIAD.Data.SiadDbContext>()
            .UseNpgsql((Npgsql.NpgsqlConnection)Connection)
            .Options;
        using var context = new SIAD.Data.SiadDbContext(options, new CompanyFija(CompanyId));
        context.Database.UseTransaction(Transaction);
        var service = new SIAD.Services.NotasCreditoDebito.NotasCreditoDebitoService(context, new CompanyFija(CompanyId));

        var (nota, error) = await service.GenerarVistaPreviaCreditoAsync(
            new SIAD.Core.DTOs.NotasCreditoDebito.EmitirNotaCreditoRequestDto
            {
                FacturaOrigenId = facturaId.Value,
                MotivoAnulacionId = 1,
                MotivoDetalle = "vista previa test",
                MontoDisminuir = 10.00m,
                CaiId = caiNcId.Value,
                Usuario = "TEST-PREVIEW"
            });

        Assert.Null(error);
        Assert.NotNull(nota);
        Assert.True(nota!.EsVistaPrevia);
        Assert.Equal("NC", nota.TipoNota);
        Assert.False(string.IsNullOrWhiteSpace(nota.ClienteClave));   // Cuenta No. del cliente
        Assert.NotEmpty(nota.Lineas);
        Assert.True(nota.Total > 0);

        var despues = await Connection.QueryFirstAsync<(long notas, long correlativo)>(
            new CommandDefinition(@"
                SELECT (SELECT count(*) FROM public.adm_nota_credito WHERE company_id = @CompanyId),
                       (SELECT correlativo_actual FROM public.adm_cai_facturacion WHERE cai_id = @CaiId)",
                new { CompanyId = CompanyId, CaiId = caiNcId }, Transaction));

        Assert.Equal(antes.notas, despues.notas);              // sin fila nueva
        Assert.Equal(antes.correlativo, despues.correlativo);  // sin correlativo consumido
    }

    private sealed class CompanyFija : SIAD.Core.Tenancy.ICurrentCompanyService
    {
        private readonly long _companyId;
        public CompanyFija(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
