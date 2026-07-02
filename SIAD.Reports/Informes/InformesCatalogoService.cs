using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using Npgsql;
using SIAD.Core.DTOs.Informes;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Reports;

public sealed class InformesCatalogoService : IInformesCatalogoService
{
    private static readonly InformeCatalogoSeed[] DefaultSeeds =
    [
        new(
            "partidas-contabilidad",
            "Partidas de Contabilidad",
            "Consulta web de pólizas contables con filtros, totales y detalle.",
            "Contabilidad",
            "QUERY",
            "/informes/partidas-contabilidad",
            "contabilidad.partidas",
            "bi bi-journal-text",
            10,
            false,
            false),
        new(
            ReportesWebConstants.CodigoReporteBancosTransacciones,
            "Transacciones Bancarias",
            "Plantilla base para migrar el reporte legacy de transacciones bancarias a diseño web.",
            "Contabilidad",
            ReportesWebConstants.TipoOrigenReporte,
            ReportesWebConstants.BuildViewerRoute(ReportesWebConstants.CodigoReporteBancosTransacciones),
            string.Empty,
            "bi bi-bank",
            20,
            true,
            true),
        new(
            ReportesWebConstants.CodigoReporteBalanceComprobacion,
            "Balance de comprobacion",
            "Reporte base del balance de comprobacion para pruebas de reporteria financiera.",
            "Contabilidad",
            ReportesWebConstants.TipoOrigenReporte,
            ReportesWebConstants.BuildViewerRoute(ReportesWebConstants.CodigoReporteBalanceComprobacion),
            string.Empty,
            "bi bi-calculator",
            30,
            true,
            true),
        new(
            ReportesWebConstants.CodigoReporteEstadoSituacionFinanciera,
            "Estado de situacion financiera",
            "Reporte financiero configurado por empresa a partir de con_configuracion_balance.",
            "Contabilidad",
            ReportesWebConstants.TipoOrigenReporte,
            ReportesWebConstants.BuildViewerRoute(ReportesWebConstants.CodigoReporteEstadoSituacionFinanciera),
            string.Empty,
            "bi bi-file-earmark-bar-graph",
            40,
            true,
            true),
        new(
            ReportesWebConstants.CodigoReporteEstadoResultados,
            "Estado de resultados",
            "Reporte financiero configurado por empresa a partir de con_configuracion_linea_resultado.",
            "Contabilidad",
            ReportesWebConstants.TipoOrigenReporte,
            ReportesWebConstants.BuildViewerRoute(ReportesWebConstants.CodigoReporteEstadoResultados),
            string.Empty,
            "bi bi-graph-up-arrow",
            50,
            true,
            true),
        new(
            ReportesWebConstants.CodigoReporteTransaccionesPeriodo,
            "Transacciones por periodo",
            "Total control de transacciones por periodo, agrupado por tipo de movimiento y rubro de servicio.",
            "Facturacion",
            ReportesWebConstants.TipoOrigenReporte,
            ReportesWebConstants.BuildTransaccionesPeriodoPreviewRoute(),
            ReportesWebConstants.CodigoDatasetTransaccionesPeriodo,
            "bi bi-file-earmark-spreadsheet",
            60,
            true,
            true),
        new(
            ReportesWebConstants.CodigoReporteSaldoClientesCategoria,
            "Saldo de clientes por categoria",
            "Saldo de clientes agrupado por categoria de servicio y condicion de medicion.",
            "Medicion",
            ReportesWebConstants.TipoOrigenReporte,
            ReportesWebConstants.BuildSaldoClientesCategoriaPreviewRoute(),
            ReportesWebConstants.CodigoDatasetSaldoClientesCategoria,
            "bi bi-people",
            70,
            true,
            true),
        new(
            ReportesWebConstants.CodigoReporteDesgloseFacturacion,
            "Desglose de Facturacion",
            "Desglose de facturacion por ciclos, con debitos, creditos, adulto mayor y pagos registrados.",
            "Facturacion",
            ReportesWebConstants.TipoOrigenReporte,
            ReportesWebConstants.BuildDesgloseFacturacionPreviewRoute(),
            ReportesWebConstants.CodigoDatasetDesgloseFacturacion,
            "bi bi-receipt",
            80,
            true,
            true),
        new(
            ReportesWebConstants.CodigoReporteMovimientoPeriodo,
            "Movimiento por periodo",
            "Registro de movimientos del periodo con saldo anterior, debitos, creditos y saldo acumulado.",
            "Facturacion",
            ReportesWebConstants.TipoOrigenReporte,
            ReportesWebConstants.BuildMovimientoPeriodoPreviewRoute(),
            ReportesWebConstants.CodigoDatasetMovimientoPeriodo,
            "bi bi-list-columns-reverse",
            90,
            true,
            true),
        new(
            ReportesWebConstants.CodigoReporteSaldoClientesAntiguedad,
            "Saldo de clientes segun antigüedad",
            "Clientes con saldo vencido segun dias de antiguedad, agrupados por ciclo y filtrables por estado.",
            "Cobranza",
            ReportesWebConstants.TipoOrigenReporte,
            ReportesWebConstants.BuildSaldoClientesAntiguedadPreviewRoute(),
            ReportesWebConstants.CodigoDatasetSaldoClientesAntiguedad,
            "bi bi-hourglass-split",
            100,
            true,
            true),
        new(
            ReportesWebConstants.CodigoReporteAnalisisAntiguedadCobros,
            "Analisis de antigüedad de cobros",
            "Analisis de cobros por tramos de antigüedad, filtrable hacia atrás en meses o años desde una fecha base.",
            "Cobranza",
            ReportesWebConstants.TipoOrigenReporte,
            ReportesWebConstants.BuildAnalisisAntiguedadCobrosPreviewRoute(),
            ReportesWebConstants.CodigoDatasetAnalisisAntiguedadCobros,
            "bi bi-bar-chart-steps",
            110,
            true,
            true),
        new(
            ReportesWebConstants.CodigoReporteAuxiliarLectura,
            "Auxiliar de Lectura",
            "Auxiliar PDF de lecturas por periodo, ciclo y estado pendiente.",
            "Medicion",
            ReportesWebConstants.TipoOrigenReporte,
            ReportesWebConstants.BuildAuxiliarLecturaPreviewRoute(),
            ReportesWebConstants.CodigoDatasetAuxiliarLectura,
            "bi bi-speedometer2",
            95,
            true,
            true),
        new(
            ReportesWebConstants.CodigoReporteHistorialRecibosEmitidos,
            "Historial de recibos emitidos",
            "Historial PDF de recibos emitidos por rango de fechas y usuario o cajero.",
            "Medicion",
            ReportesWebConstants.TipoOrigenReporte,
            ReportesWebConstants.BuildHistorialRecibosEmitidosPreviewRoute(),
            ReportesWebConstants.CodigoDatasetHistorialRecibosEmitidos,
            "bi bi-journal-medical",
            96,
            true,
            true),
        new(
            ReportesWebConstants.CodigoReporteSaldoClientesCiclo,
            "Saldo de clientes por ciclo",
            "Resumen totalizado por ciclos con saldo anterior, debitos, creditos, saldo actual y conteos de clientes.",
            "Cobranza",
            ReportesWebConstants.TipoOrigenReporte,
            ReportesWebConstants.BuildSaldoClientesCicloPreviewRoute(),
            ReportesWebConstants.CodigoDatasetSaldoClientesCiclo,
            "bi bi-diagram-3",
            120,
            true,
            true),
        new(
            ReportesWebConstants.CodigoReporteSaldoClientesCategoriaCobranza,
            "Saldo de clientes por categoria",
            "Resumen totalizado por categoria con saldo anterior, debitos, creditos, saldo actual y conteos de clientes.",
            "Cobranza",
            ReportesWebConstants.TipoOrigenReporte,
            ReportesWebConstants.BuildSaldoClientesCategoriaCobranzaPreviewRoute(),
            ReportesWebConstants.CodigoDatasetSaldoClientesCategoriaCobranza,
            "bi bi-tags",
            130,
            true,
            true),
        new(
            ReportesWebConstants.CodigoReporteRecaudacion,
            "Informe de recaudacion",
            "Resumen detallado de recaudaciones agrupado por medio de pago con distribucion de recuperacion e ingresos del mes.",
            "Cobranza",
            ReportesWebConstants.TipoOrigenReporte,
            ReportesWebConstants.BuildRecaudacionPreviewRoute(),
            ReportesWebConstants.CodigoDatasetRecaudacion,
            "bi bi-cash-coin",
            140,
            true,
            true),
        new(
            ReportesWebConstants.CodigoReporteSaldoClientesCategoriaDetalle,
            "Detalle de saldos de clientes por categoria",
            "Saldo de clientes detallado por categoria y ciclo, con saldo anterior, debitos, creditos y saldo actual.",
            "Cobranza",
            ReportesWebConstants.TipoOrigenReporte,
            ReportesWebConstants.BuildSaldoClientesCategoriaDetallePreviewRoute(),
            ReportesWebConstants.CodigoDatasetSaldoClientesCategoriaDetalle,
            "bi bi-card-list",
            150,
            true,
            true),
        new(
            ReportesWebConstants.CodigoReporteSaldosAguaPotableCiclo,
            "Saldos de agua potable por ciclo",
            "Saldos de agua potable agrupados por ciclo con saldo anterior, debitos, creditos, saldo actual y conteos de usuarios activos e inactivos.",
            "Medicion",
            ReportesWebConstants.TipoOrigenReporte,
            ReportesWebConstants.BuildSaldosAguaPotableCicloPreviewRoute(),
            ReportesWebConstants.CodigoDatasetSaldosAguaPotableCiclo,
            "bi bi-droplet-half",
            97,
            true,
            true),
        new(
            ReportesWebConstants.CodigoReporteSumarialTarifarioMedicion,
            "Sumarial tarifario Medicion por periodo",
            "Resumen sumarial de conexiones, consumos y valor de agua por rangos de tarifas y categorias.",
            "Medicion",
            ReportesWebConstants.TipoOrigenReporte,
            ReportesWebConstants.BuildSumarialTarifarioMedicionPreviewRoute(),
            ReportesWebConstants.CodigoDatasetSumarialTarifarioMedicion,
            "bi bi-file-earmark-bar-graph",
            160,
            true,
            true),
        new(
            ReportesWebConstants.CodigoReporteSumarialTarifasNoMedido,
            "Sumarial de tarifas clientes no medido por periodo",
            "Resumen sumarial de tarifas para clientes sin medidor del periodo.",
            "Medicion",
            ReportesWebConstants.TipoOrigenReporte,
            ReportesWebConstants.BuildSumarialTarifasNoMedidoPreviewRoute(),
            ReportesWebConstants.CodigoDatasetSumarialTarifasNoMedido,
            "bi bi-file-earmark-bar-graph-fill",
            161,
            true,
            true)
    ];

    private readonly SiadDbContext _context;

    public InformesCatalogoService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<InformeCatalogoItemDto>> ListarAsync(long companyId, CancellationToken ct = default)
    {
        if (companyId <= 0)
        {
            return Array.Empty<InformeCatalogoItemDto>();
        }

        if (!await CompanyExistsAsync(companyId, ct))
        {
            return Array.Empty<InformeCatalogoItemDto>();
        }

        try
        {
            await EnsureDefaultCatalogAsync(companyId, ct);

            return await _context.rep_catalogo_informes
                .AsNoTracking()
                .Where(x => x.company_id == companyId && x.is_active)
                .OrderBy(x => x.orden)
                .ThenBy(x => x.nombre)
                .Select(x => new InformeCatalogoItemDto(
                    x.informe_id,
                    x.codigo,
                    x.nombre,
                    x.descripcion,
                    x.categoria,
                    x.tipo_origen,
                    x.ruta,
                    x.consulta_clave,
                    x.icono_css_class,
                    x.orden,
                    x.permite_exportar,
                    x.permite_imprimir,
                    x.is_active))
                .ToListAsync(ct);
        }
        catch (Exception ex) when (IsCatalogTableMissing(ex))
        {
            return BuildFallbackCatalog();
        }
    }

    private async Task EnsureDefaultCatalogAsync(long companyId, CancellationToken ct)
    {
        if (!await CompanyExistsAsync(companyId, ct))
        {
            return;
        }

        var existingCodes = await _context.rep_catalogo_informes
            .Where(x => x.company_id == companyId)
            .Select(x => x.codigo)
            .ToListAsync(ct);

        var existingLookup = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;
        var pending = DefaultSeeds
            .Where(seed => !existingLookup.Contains(seed.Codigo))
            .Select(seed => new rep_catalogo_informe
            {
                company_id = companyId,
                codigo = seed.Codigo,
                nombre = seed.Nombre,
                descripcion = seed.Descripcion,
                categoria = seed.Categoria,
                tipo_origen = seed.TipoOrigen,
                ruta = seed.Ruta,
                consulta_clave = seed.ConsultaClave,
                icono_css_class = seed.IconoCssClass,
                orden = seed.Orden,
                permite_exportar = seed.PermiteExportar,
                permite_imprimir = seed.PermiteImprimir,
                is_active = true,
                created_at = now,
                created_by = "reporteria-bootstrap"
            })
            .ToList();

        if (pending.Count == 0)
        {
            await SyncKnownRoutesAsync(companyId, now, ct);
            return;
        }

        _context.rep_catalogo_informes.AddRange(pending);
        await _context.SaveChangesAsync(ct);
        await SyncKnownRoutesAsync(companyId, now, ct);
    }

    private async Task SyncKnownRoutesAsync(long companyId, DateTime now, CancellationToken ct)
    {
        var items = await _context.rep_catalogo_informes
            .Where(x =>
                x.company_id == companyId &&
                (x.codigo == ReportesWebConstants.CodigoReporteTransaccionesPeriodo ||
                 x.codigo == ReportesWebConstants.CodigoReporteSaldoClientesCategoria ||
                 x.codigo == ReportesWebConstants.CodigoReporteDesgloseFacturacion ||
                 x.codigo == ReportesWebConstants.CodigoReporteMovimientoPeriodo ||
                 x.codigo == ReportesWebConstants.CodigoReporteAuxiliarLectura ||
                 x.codigo == ReportesWebConstants.CodigoReporteHistorialRecibosEmitidos ||
                 x.codigo == ReportesWebConstants.CodigoReporteSaldoClientesAntiguedad ||
                 x.codigo == ReportesWebConstants.CodigoReporteAnalisisAntiguedadCobros ||
                 x.codigo == ReportesWebConstants.CodigoReporteSaldoClientesCiclo ||
                 x.codigo == ReportesWebConstants.CodigoReporteSaldoClientesCategoriaCobranza ||
                 x.codigo == ReportesWebConstants.CodigoReporteRecaudacion ||
                 x.codigo == ReportesWebConstants.CodigoReporteSaldoClientesCategoriaDetalle ||
                 x.codigo == ReportesWebConstants.CodigoReporteSaldosAguaPotableCiclo ||
                 x.codigo == ReportesWebConstants.CodigoReporteSumarialTarifarioMedicion ||
                 x.codigo == ReportesWebConstants.CodigoReporteSumarialTarifasNoMedido))
            .ToListAsync(ct);

        if (items.Count == 0)
        {
            return;
        }

        var routeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ReportesWebConstants.CodigoReporteTransaccionesPeriodo] =
                ReportesWebConstants.BuildTransaccionesPeriodoPreviewRoute(),
            [ReportesWebConstants.CodigoReporteSaldoClientesCategoria] =
                ReportesWebConstants.BuildSaldoClientesCategoriaPreviewRoute(),
            [ReportesWebConstants.CodigoReporteDesgloseFacturacion] =
                ReportesWebConstants.BuildDesgloseFacturacionPreviewRoute(),
            [ReportesWebConstants.CodigoReporteMovimientoPeriodo] =
                ReportesWebConstants.BuildMovimientoPeriodoPreviewRoute(),
            [ReportesWebConstants.CodigoReporteAuxiliarLectura] =
                ReportesWebConstants.BuildAuxiliarLecturaPreviewRoute(),
            [ReportesWebConstants.CodigoReporteHistorialRecibosEmitidos] =
                ReportesWebConstants.BuildHistorialRecibosEmitidosPreviewRoute(),
            [ReportesWebConstants.CodigoReporteSaldoClientesAntiguedad] =
                ReportesWebConstants.BuildSaldoClientesAntiguedadPreviewRoute(),
            [ReportesWebConstants.CodigoReporteAnalisisAntiguedadCobros] =
                ReportesWebConstants.BuildAnalisisAntiguedadCobrosPreviewRoute(),
            [ReportesWebConstants.CodigoReporteSaldoClientesCiclo] =
                ReportesWebConstants.BuildSaldoClientesCicloPreviewRoute(),
            [ReportesWebConstants.CodigoReporteSaldoClientesCategoriaCobranza] =
                ReportesWebConstants.BuildSaldoClientesCategoriaCobranzaPreviewRoute(),
            [ReportesWebConstants.CodigoReporteRecaudacion] =
                ReportesWebConstants.BuildRecaudacionPreviewRoute(),
            [ReportesWebConstants.CodigoReporteSaldoClientesCategoriaDetalle] =
                ReportesWebConstants.BuildSaldoClientesCategoriaDetallePreviewRoute(),
            [ReportesWebConstants.CodigoReporteSaldosAguaPotableCiclo] =
                ReportesWebConstants.BuildSaldosAguaPotableCicloPreviewRoute(),
            [ReportesWebConstants.CodigoReporteSumarialTarifarioMedicion] =
                ReportesWebConstants.BuildSumarialTarifarioMedicionPreviewRoute(),
            [ReportesWebConstants.CodigoReporteSumarialTarifasNoMedido] =
                ReportesWebConstants.BuildSumarialTarifasNoMedidoPreviewRoute()
        };

        var changed = false;
        foreach (var item in items)
        {
            if (!routeMap.TryGetValue(item.codigo, out var expectedRoute))
            {
                continue;
            }

            if (string.Equals(item.ruta, expectedRoute, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            item.ruta = expectedRoute;
            item.updated_at = now;
            item.updated_by = "reporteria-bootstrap";
            changed = true;
        }

        if (changed)
        {
            await _context.SaveChangesAsync(ct);
        }
    }

    private Task<bool> CompanyExistsAsync(long companyId, CancellationToken ct)
        => _context.cfg_companies
            .AsNoTracking()
            .AnyAsync(x => x.company_id == companyId, ct);

    private sealed record InformeCatalogoSeed(
        string Codigo,
        string Nombre,
        string Descripcion,
        string Categoria,
        string TipoOrigen,
        string Ruta,
        string ConsultaClave,
        string IconoCssClass,
        int Orden,
        bool PermiteExportar,
        bool PermiteImprimir);

    private static bool IsCatalogTableMissing(Exception ex)
    {
        if (ex is PostgresException postgresException &&
            postgresException.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            return true;
        }

        return ex.InnerException is not null && IsCatalogTableMissing(ex.InnerException);
    }

    private static IReadOnlyList<InformeCatalogoItemDto> BuildFallbackCatalog() =>
        DefaultSeeds
            .Select((seed, index) => new InformeCatalogoItemDto(
                index + 1,
                seed.Codigo,
                seed.Nombre,
                seed.Descripcion,
                seed.Categoria,
                seed.TipoOrigen,
                seed.Ruta,
                seed.ConsultaClave,
                seed.IconoCssClass,
                seed.Orden,
                seed.PermiteExportar,
                seed.PermiteImprimir,
                true))
            .ToList();
}
