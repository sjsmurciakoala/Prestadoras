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
            return;
        }

        _context.rep_catalogo_informes.AddRange(pending);
        await _context.SaveChangesAsync(ct);
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
