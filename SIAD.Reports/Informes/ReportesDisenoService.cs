using Microsoft.EntityFrameworkCore;
using Npgsql;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Informes;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Reports;

public sealed class ReportesDisenoService : IReportesDisenoService
{
    private static readonly ReporteSeed[] DefaultSeeds =
    [
        new(
            ReportesWebConstants.CodigoReporteBancosTransacciones,
            "Transacciones Bancarias",
            "Plantilla base para migrar el reporte legacy de transacciones bancarias a diseño web.",
            "Contabilidad",
            "bi bi-bank",
            20,
            ReportesWebConstants.CodigoDatasetBancosTransacciones),
        new(
            ReportesWebConstants.CodigoReporteBalanceComprobacion,
            "Balance de comprobacion",
            "Plantilla base del balance de comprobacion para reporteria financiera conforme a ERSAPS.",
            "Contabilidad",
            "bi bi-calculator",
            30,
            ReportesWebConstants.CodigoDatasetBalanceComprobacion),
        new(
            ReportesWebConstants.CodigoReporteEstadoSituacionFinanciera,
            "Estado de situacion financiera",
            "Plantilla base del estado de situacion financiera usando la configuracion contable por empresa.",
            "Contabilidad",
            "bi bi-file-earmark-bar-graph",
            40,
            ReportesWebConstants.CodigoDatasetEstadoSituacionFinanciera),
        new(
            ReportesWebConstants.CodigoReporteEstadoResultados,
            "Estado de resultados",
            "Plantilla base del estado de resultados usando la configuracion contable por empresa.",
            "Contabilidad",
            "bi bi-graph-up-arrow",
            50,
            ReportesWebConstants.CodigoDatasetEstadoResultados),
        new(
            ReportesWebConstants.CodigoReporteTransaccionesPeriodo,
            "Transacciones por periodo",
            "Plantilla base del total control de transacciones por periodo.",
            "Facturacion",
            "bi bi-file-earmark-spreadsheet",
            60,
            ReportesWebConstants.CodigoDatasetTransaccionesPeriodo),
        new(
            ReportesWebConstants.CodigoReporteSaldosAguaPotableCiclo,
            "Saldos de agua potable por ciclo",
            "Plantilla base de saldos de agua potable agrupados por ciclo.",
            "Medicion",
            "bi bi-droplet-half",
            97,
            ReportesWebConstants.CodigoDatasetSaldosAguaPotableCiclo),
        new(
            ReportesWebConstants.CodigoReporteSumarialTarifarioMedicion,
            "Sumarial tarifario Medicion por periodo",
            "Plantilla base de resumen sumarial de conexiones, consumos y valor de agua por rangos de tarifas.",
            "Medicion",
            "bi bi-file-earmark-bar-graph",
            160,
            ReportesWebConstants.CodigoDatasetSumarialTarifarioMedicion),
        new(
            ReportesWebConstants.CodigoReporteSumarialTarifasNoMedido,
            "Sumarial de tarifas clientes no medido por periodo",
            "Plantilla base de resumen sumarial de tarifas para clientes sin medidor del periodo.",
            "Medicion",
            "bi bi-file-earmark-bar-graph-fill",
            161,
            ReportesWebConstants.CodigoDatasetSumarialTarifasNoMedido)
    ];

    private readonly SiadDbContext _context;
    private readonly ReportDraftRegenerationService _draftRegeneration;

    public ReportesDisenoService(
        SiadDbContext context,
        ReportDraftRegenerationService draftRegeneration)
    {
        _context = context;
        _draftRegeneration = draftRegeneration;
    }

    public async Task<IReadOnlyList<ReporteDisenoCatalogoItemDto>> ListarAsync(long companyId, CancellationToken ct = default)
    {
        if (companyId <= 0)
        {
            return [];
        }

        if (!await CompanyExistsAsync(companyId, ct))
        {
            return [];
        }

        try
        {
            await EnsureDefaultCatalogAsync(companyId, ct);

            var catalogo = await _context.rep_catalogo_informes
                .AsNoTracking()
                .Where(x => x.company_id == companyId
                            && x.tipo_origen == ReportesWebConstants.TipoOrigenReporte
                            && x.is_active)
                .OrderBy(x => x.orden)
                .ThenBy(x => x.nombre)
                .ToListAsync(ct);

            var informeIds = catalogo.Select(x => x.informe_id).ToArray();
            var layouts = await LoadLayoutSummaryAsync(companyId, informeIds, ct);

            return catalogo
                .Select(item =>
                {
                    layouts.TryGetValue(item.informe_id, out var summary);
                    return BuildCatalogItem(item, summary);
                })
                .ToList();
        }
        catch (Exception ex) when (IsTableMissing(ex))
        {
            return BuildFallbackCatalog();
        }
    }

    public async Task<ReporteDisenoDetalleDto?> ObtenerAsync(long companyId, string codigo, CancellationToken ct = default)
    {
        if (companyId <= 0)
        {
            return null;
        }

        if (!await CompanyExistsAsync(companyId, ct))
        {
            return null;
        }

        var normalizedCode = NormalizeOrThrow(codigo);

        try
        {
            await EnsureDefaultCatalogAsync(companyId, ct);

            var catalogo = await _context.rep_catalogo_informes
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.company_id == companyId
                         && x.tipo_origen == ReportesWebConstants.TipoOrigenReporte
                         && x.codigo == normalizedCode
                         && x.is_active,
                    ct);

            if (catalogo is null)
            {
                return BuildFallbackDetail(normalizedCode);
            }

            var summary = await LoadLayoutSummaryAsync(companyId, catalogo.informe_id, ct);
            if (summary is null || !summary.DraftVersion.HasValue)
            {
                await _draftRegeneration.EnsureDraftLayoutAsync(companyId, normalizedCode, "reporteria-bootstrap", ct);
                summary = await LoadLayoutSummaryAsync(companyId, catalogo.informe_id, ct);
            }
            var currentLayout = await LoadCurrentDesignerLayoutInfoAsync(companyId, catalogo.informe_id, ct);
            var datasetStatus = await LoadDatasetStatusAsync(companyId, catalogo.consulta_clave, ct);
            return BuildDetailItem(catalogo, summary, BuildRegenerationStatus(catalogo.consulta_clave, currentLayout, datasetStatus));
        }
        catch (Exception ex) when (IsTableMissing(ex))
        {
            return BuildFallbackDetail(normalizedCode);
        }
    }

    public async Task<ReporteDisenoDetalleDto> CrearAsync(long companyId, ReporteDisenoCreateDto dto, string actor, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (companyId <= 0)
        {
            throw new InvalidOperationException("No fue posible determinar la empresa actual.");
        }

        if (!await CompanyExistsAsync(companyId, ct))
        {
            throw new InvalidOperationException("La empresa activa no existe o ya no esta disponible.");
        }

        var codigo = NormalizeOrThrow(dto.Codigo);
        var nombre = string.IsNullOrWhiteSpace(dto.Nombre)
            ? throw new ArgumentException("El nombre del reporte es obligatorio.", nameof(dto))
            : dto.Nombre.Trim();

        var categoria = string.IsNullOrWhiteSpace(dto.Categoria) ? "General" : dto.Categoria.Trim();
        var now = DateTime.UtcNow;

        if (await _context.rep_catalogo_informes.AnyAsync(
                x => x.company_id == companyId && x.codigo == codigo,
                ct))
        {
            throw new InvalidOperationException("Ya existe un reporte con ese código.");
        }

        var catalogo = new rep_catalogo_informe
        {
            company_id = companyId,
            codigo = codigo,
            nombre = nombre,
            descripcion = string.IsNullOrWhiteSpace(dto.Descripcion) ? null : dto.Descripcion.Trim(),
            categoria = categoria,
            tipo_origen = ReportesWebConstants.TipoOrigenReporte,
            ruta = ReportesWebConstants.BuildViewerRoute(codigo),
            consulta_clave = await ResolveDatasetCodeAsync(companyId, dto.DatasetCodigo, ct),
            icono_css_class = "bi bi-file-earmark-richtext",
            orden = await GetNextSortOrderAsync(companyId, ct),
            permite_exportar = true,
            permite_imprimir = true,
            is_active = true,
            created_at = now,
            created_by = actor
        };

        _context.rep_catalogo_informes.Add(catalogo);
        await _context.SaveChangesAsync(ct);

        await _draftRegeneration.EnsureDraftLayoutAsync(companyId, codigo, actor, ct);

        var summary = await LoadLayoutSummaryAsync(companyId, catalogo.informe_id, ct);
        var currentLayout = await LoadCurrentDesignerLayoutInfoAsync(companyId, catalogo.informe_id, ct);
        var datasetStatus = await LoadDatasetStatusAsync(companyId, catalogo.consulta_clave, ct);
        return BuildDetailItem(catalogo, summary, BuildRegenerationStatus(catalogo.consulta_clave, currentLayout, datasetStatus));
    }

    public async Task<ReporteDisenoDetalleDto> PublicarAsync(long companyId, string codigo, string actor, CancellationToken ct = default)
    {
        if (companyId <= 0)
        {
            throw new InvalidOperationException("No fue posible determinar la empresa actual.");
        }

        if (!await CompanyExistsAsync(companyId, ct))
        {
            throw new InvalidOperationException("La empresa activa no existe o ya no esta disponible.");
        }

        var normalizedCode = NormalizeOrThrow(codigo);

        try
        {
            var catalogo = await _context.rep_catalogo_informes
                .FirstOrDefaultAsync(
                    x => x.company_id == companyId
                         && x.tipo_origen == ReportesWebConstants.TipoOrigenReporte
                         && x.codigo == normalizedCode
                         && x.is_active,
                    ct);

            if (catalogo is null)
            {
                throw new InvalidOperationException("El reporte solicitado no está registrado.");
            }

            var layouts = await _context.rep_reporte_layouts
                .Where(x => x.company_id == companyId && x.informe_id == catalogo.informe_id)
                .ToListAsync(ct);

            var draft = layouts
                .Where(x => x.estado == ReportesWebConstants.LayoutStatus.Draft)
                .OrderByDescending(x => x.version_num)
                .FirstOrDefault();

            if (draft is null)
            {
                if (layouts.Any(x => x.estado == ReportesWebConstants.LayoutStatus.Published))
                {
                    var publishedSummary = await LoadLayoutSummaryAsync(companyId, catalogo.informe_id, ct);
                    return BuildDetailItem(catalogo, publishedSummary);
                }

                throw new InvalidOperationException("No existe un borrador pendiente para publicar.");
            }

            var now = DateTime.UtcNow;

            await using var transaction = await _context.Database.BeginTransactionAsync(ct);

            var publishedLayouts = layouts
                .Where(x => x.estado == ReportesWebConstants.LayoutStatus.Published)
                .ToList();

            foreach (var published in publishedLayouts)
            {
                published.estado = ReportesWebConstants.LayoutStatus.Archived;
                published.updated_at = now;
                published.updated_by = actor;
            }

            if (publishedLayouts.Count > 0)
            {
                // Libera la restriccion unica parcial antes de promover el borrador.
                await _context.SaveChangesAsync(ct);
            }

            draft.estado = ReportesWebConstants.LayoutStatus.Published;
            draft.updated_at = now;
            draft.updated_by = actor;
            draft.published_at = now;
            draft.published_by = actor;

            catalogo.updated_at = now;
            catalogo.updated_by = actor;

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            var summary = await LoadLayoutSummaryAsync(companyId, catalogo.informe_id, ct);
            return BuildDetailItem(catalogo, summary);
        }
        catch (Exception ex) when (IsLayoutTableMissing(ex))
        {
            throw new InvalidOperationException(
                "La tabla de layouts de reportería no existe. Ejecute la migración o el script 2026-03-21_add_rep_reporte_layout.sql.",
                ex);
        }
    }

    public async Task EliminarAsync(long companyId, string codigo, CancellationToken ct = default)
    {
        if (companyId <= 0)
        {
            throw new InvalidOperationException("No fue posible determinar la empresa actual.");
        }

        if (!await CompanyExistsAsync(companyId, ct))
        {
            throw new InvalidOperationException("La empresa activa no existe o ya no esta disponible.");
        }

        var normalizedCode = NormalizeOrThrow(codigo);

        try
        {
            var catalogo = await _context.rep_catalogo_informes
                .FirstOrDefaultAsync(
                    x => x.company_id == companyId
                         && x.tipo_origen == ReportesWebConstants.TipoOrigenReporte
                         && x.codigo == normalizedCode
                         && x.is_active,
                    ct);

            if (catalogo is null)
            {
                throw new InvalidOperationException("No existe un reporte registrado con el código solicitado.");
            }

            var layouts = await _context.rep_reporte_layouts
                .Where(x => x.company_id == companyId && x.informe_id == catalogo.informe_id)
                .ToListAsync(ct);

            await using var transaction = await _context.Database.BeginTransactionAsync(ct);

            if (layouts.Count > 0)
            {
                _context.rep_reporte_layouts.RemoveRange(layouts);
            }

            if (IsDefaultSeed(normalizedCode))
            {
                catalogo.is_active = false;
                catalogo.updated_at = DateTime.UtcNow;
                catalogo.updated_by = "reporteria-delete";
            }
            else
            {
                _context.rep_catalogo_informes.Remove(catalogo);
            }

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (Exception ex) when (IsLayoutTableMissing(ex))
        {
            throw new InvalidOperationException(
                "La tabla de layouts de reportería no existe. Ejecute la migración o el script 2026-03-21_add_rep_reporte_layout.sql.",
                ex);
        }
    }

    private async Task EnsureDefaultCatalogAsync(long companyId, CancellationToken ct)
    {
        if (!await CompanyExistsAsync(companyId, ct))
        {
            return;
        }

        var existingItems = await _context.rep_catalogo_informes
            .Where(x => x.company_id == companyId)
            .ToListAsync(ct);

        var existingCodes = existingItems.Select(x => x.codigo).ToList();
        var lookup = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;

        var pending = DefaultSeeds
            .Where(seed => !lookup.Contains(seed.Codigo))
            .Select(seed => new rep_catalogo_informe
            {
                company_id = companyId,
                codigo = seed.Codigo,
                nombre = seed.Nombre,
                descripcion = seed.Descripcion,
                categoria = seed.Categoria,
                tipo_origen = ReportesWebConstants.TipoOrigenReporte,
                ruta = ReportesWebConstants.BuildViewerRoute(seed.Codigo),
                consulta_clave = seed.DatasetCodigo,
                icono_css_class = seed.IconoCssClass,
                orden = seed.Orden,
                permite_exportar = true,
                permite_imprimir = true,
                is_active = true,
                created_at = now,
                created_by = "reporteria-bootstrap"
            })
            .ToList();

        var seeded = false;

        if (pending.Count > 0)
        {
            _context.rep_catalogo_informes.AddRange(pending);
        }

        foreach (var existing in existingItems.Where(x => x.tipo_origen == ReportesWebConstants.TipoOrigenReporte))
        {
            var seed = DefaultSeeds.FirstOrDefault(x => string.Equals(x.Codigo, existing.codigo, StringComparison.OrdinalIgnoreCase));
            if (seed is null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(existing.consulta_clave) && !string.IsNullOrWhiteSpace(seed.DatasetCodigo))
            {
                existing.consulta_clave = seed.DatasetCodigo;
                existing.updated_at = now;
                existing.updated_by = "reporteria-bootstrap";
                seeded = true;
            }
        }

        if (pending.Count > 0 || seeded)
        {
            await _context.SaveChangesAsync(ct);
        }

        var seedCodesToInitialize = pending
            .Select(x => x.codigo)
            .Concat(existingItems
                .Where(x => x.is_active
                            && x.tipo_origen == ReportesWebConstants.TipoOrigenReporte
                            && DefaultSeeds.Any(seed => string.Equals(seed.Codigo, x.codigo, StringComparison.OrdinalIgnoreCase)))
                .Select(x => x.codigo))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var seedCode in seedCodesToInitialize)
        {
            await _draftRegeneration.EnsureDraftLayoutAsync(companyId, seedCode, "reporteria-bootstrap", ct);
        }
    }

    private async Task<int> GetNextSortOrderAsync(long companyId, CancellationToken ct)
    {
        var lastOrder = await _context.rep_catalogo_informes
            .Where(x => x.company_id == companyId)
            .Select(x => (int?)x.orden)
            .MaxAsync(ct);

        return (lastOrder ?? 0) + 10;
    }

    private async Task<Dictionary<long, LayoutSummary>> LoadLayoutSummaryAsync(long companyId, long[] informeIds, CancellationToken ct)
    {
        if (informeIds.Length == 0)
        {
            return [];
        }

        try
        {
            return await _context.rep_reporte_layouts
                .AsNoTracking()
                .Where(x => x.company_id == companyId && informeIds.Contains(x.informe_id))
                .GroupBy(x => x.informe_id)
                .Select(group => new LayoutSummary(
                    group.Key,
                    group.Where(x => x.estado == ReportesWebConstants.LayoutStatus.Draft)
                        .Select(x => (int?)x.version_num)
                        .Max(),
                    group.Where(x => x.estado == ReportesWebConstants.LayoutStatus.Published)
                        .Select(x => (int?)x.version_num)
                        .Max()))
                .ToDictionaryAsync(x => x.InformeId, ct);
        }
        catch (Exception ex) when (IsLayoutTableMissing(ex))
        {
            return [];
        }
    }

    private async Task<LayoutSummary?> LoadLayoutSummaryAsync(long companyId, long informeId, CancellationToken ct)
    {
        var summary = await LoadLayoutSummaryAsync(companyId, [informeId], ct);
        return summary.GetValueOrDefault(informeId);
    }

    private static ReporteDisenoCatalogoItemDto BuildCatalogItem(rep_catalogo_informe catalogo, LayoutSummary? summary)
    {
        var draftVersion = summary?.DraftVersion;
        var publishedVersion = summary?.PublishedVersion;
        var estado = ResolveState(catalogo.is_active, draftVersion, publishedVersion);

        return new ReporteDisenoCatalogoItemDto(
            catalogo.informe_id,
            catalogo.codigo,
            catalogo.nombre,
            catalogo.descripcion,
            catalogo.categoria,
            catalogo.consulta_clave,
            estado,
            draftVersion,
            publishedVersion,
            draftVersion.HasValue,
            publishedVersion.HasValue,
            ReportesWebConstants.BuildViewerRoute(catalogo.codigo),
            ReportesWebConstants.BuildDesignerRoute(catalogo.codigo),
            catalogo.is_active);
    }

    private static ReporteDisenoDetalleDto BuildDetailItem(rep_catalogo_informe catalogo, LayoutSummary? summary, RegenerationStatus? regenerationStatus = null)
    {
        var draftVersion = summary?.DraftVersion;
        var publishedVersion = summary?.PublishedVersion;
        var estado = ResolveState(catalogo.is_active, draftVersion, publishedVersion);

        return new ReporteDisenoDetalleDto(
            catalogo.informe_id,
            catalogo.codigo,
            catalogo.nombre,
            catalogo.descripcion,
            catalogo.categoria,
            catalogo.consulta_clave,
            estado,
            draftVersion,
            publishedVersion,
            draftVersion.HasValue,
            publishedVersion.HasValue,
            ReportesWebConstants.BuildViewerRoute(catalogo.codigo),
            ReportesWebConstants.BuildDesignerRoute(catalogo.codigo),
            ReportesWebConstants.BuildReportName(catalogo.codigo, ReportesWebConstants.LayoutMode.Published),
            ReportesWebConstants.BuildReportName(catalogo.codigo, ReportesWebConstants.LayoutMode.Draft),
            catalogo.is_active,
            regenerationStatus?.TieneDesfaseDatasetLayout ?? false,
            regenerationStatus?.PuedeRegenerarBorrador ?? false,
            regenerationStatus?.MensajeRegeneracionBorrador,
            regenerationStatus?.LayoutBaseActualizadoEnUtc,
            regenerationStatus?.DatasetActualizadoEnUtc);
    }

    private static string ResolveState(bool isActive, int? draftVersion, int? publishedVersion)
    {
        if (!isActive)
        {
            return "Inactivo";
        }

        if (draftVersion.HasValue && publishedVersion.HasValue)
        {
            return "Borrador pendiente";
        }

        if (draftVersion.HasValue)
        {
            return "Solo borrador";
        }

        if (publishedVersion.HasValue)
        {
            return "Publicado";
        }

        return "Sin layout";
    }

    private static IReadOnlyList<ReporteDisenoCatalogoItemDto> BuildFallbackCatalog()
        => DefaultSeeds
            .Select((seed, index) => new ReporteDisenoCatalogoItemDto(
                index + 1,
                seed.Codigo,
                seed.Nombre,
                seed.Descripcion,
                seed.Categoria,
                seed.DatasetCodigo,
                "Sin layout",
                null,
                null,
                false,
                false,
                ReportesWebConstants.BuildViewerRoute(seed.Codigo),
                ReportesWebConstants.BuildDesignerRoute(seed.Codigo),
                true))
            .ToList();

    private static ReporteDisenoDetalleDto? BuildFallbackDetail(string codigo)
    {
        var seed = DefaultSeeds.FirstOrDefault(x => x.Codigo == codigo);
        if (seed is null)
        {
            return null;
        }

        return new ReporteDisenoDetalleDto(
            0,
            seed.Codigo,
            seed.Nombre,
            seed.Descripcion,
            seed.Categoria,
            seed.DatasetCodigo,
            "Sin layout",
            null,
            null,
            false,
            false,
            ReportesWebConstants.BuildViewerRoute(seed.Codigo),
            ReportesWebConstants.BuildDesignerRoute(seed.Codigo),
            ReportesWebConstants.BuildReportName(seed.Codigo, ReportesWebConstants.LayoutMode.Published),
            ReportesWebConstants.BuildReportName(seed.Codigo, ReportesWebConstants.LayoutMode.Draft),
            true,
            false,
            !string.IsNullOrWhiteSpace(seed.DatasetCodigo),
            null,
            null,
            null);
    }

    private static string NormalizeOrThrow(string codigo)
    {
        var normalized = ReportesWebConstants.NormalizeCode(codigo);
        if (!ReportesWebConstants.IsValidCode(normalized))
        {
            throw new ArgumentException("El código del reporte solo admite letras, números, guion y guion bajo.");
        }

        return normalized;
    }

    private async Task<string?> ResolveDatasetCodeAsync(long companyId, string? datasetCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(datasetCode))
        {
            return null;
        }

        var normalizedDatasetCode = NormalizeOrThrow(datasetCode);

        try
        {
            var exists = await _context.rep_catalogo_datasets
                .AnyAsync(x => x.company_id == companyId && x.codigo == normalizedDatasetCode && x.is_active, ct);

            if (!exists)
            {
                throw new InvalidOperationException("El dataset seleccionado no existe o está inactivo.");
            }

            return normalizedDatasetCode;
        }
        catch (Exception ex) when (IsTableMissing(ex))
        {
            throw new InvalidOperationException(
                "La tabla de datasets de reportería no existe. Ejecute el script 2026-03-21_add_rep_catalogo_dataset.sql.",
                ex);
        }
    }

    private async Task<CurrentLayoutInfo?> LoadCurrentDesignerLayoutInfoAsync(long companyId, long informeId, CancellationToken ct)
    {
        try
        {
            var layouts = await _context.rep_reporte_layouts
                .AsNoTracking()
                .Where(x => x.company_id == companyId && x.informe_id == informeId)
                .ToListAsync(ct);

            var current = layouts
                .OrderByDescending(x => x.estado == ReportesWebConstants.LayoutStatus.Draft)
                .ThenByDescending(x => x.version_num)
                .FirstOrDefault();

            if (current is null)
            {
                return null;
            }

            return new CurrentLayoutInfo(
                current.estado,
                current.version_num,
                current.updated_at ?? current.created_at);
        }
        catch (Exception ex) when (IsLayoutTableMissing(ex))
        {
            return null;
        }
    }

    private async Task<DatasetStatus?> LoadDatasetStatusAsync(long companyId, string? datasetCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(datasetCode))
        {
            return null;
        }

        var dataset = await _context.rep_catalogo_datasets
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.company_id == companyId && x.codigo == datasetCode, ct);

        if (dataset is null || !dataset.is_active)
        {
            return new DatasetStatus(datasetCode, null, false);
        }

        return new DatasetStatus(
            datasetCode,
            dataset.updated_at ?? dataset.created_at,
            true);
    }

    private static RegenerationStatus BuildRegenerationStatus(
        string? datasetCode,
        CurrentLayoutInfo? currentLayout,
        DatasetStatus? datasetStatus)
    {
        if (string.IsNullOrWhiteSpace(datasetCode))
        {
            return new RegenerationStatus(false, false, null, currentLayout?.ActualizadoEnUtc, null);
        }

        if (datasetStatus is null)
        {
            return new RegenerationStatus(false, false, null, currentLayout?.ActualizadoEnUtc, null);
        }

        if (!datasetStatus.Exists)
        {
            return new RegenerationStatus(
                false,
                false,
                $"El dataset asignado '{datasetCode}' no existe o está inactivo. No es posible regenerar el borrador hasta corregir la asignación.",
                currentLayout?.ActualizadoEnUtc,
                datasetStatus.ActualizadoEnUtc);
        }

        if (currentLayout?.ActualizadoEnUtc is DateTime layoutUpdatedAt &&
            datasetStatus.ActualizadoEnUtc is DateTime datasetUpdatedAt &&
            datasetUpdatedAt > layoutUpdatedAt)
        {
            return new RegenerationStatus(
                true,
                true,
                $"El dataset '{datasetCode}' cambió el {FormatUtc(datasetUpdatedAt)} y el layout base actual fue generado o guardado por última vez el {FormatUtc(layoutUpdatedAt)}. Si necesitas reconstruir campos y metadatos desde el dataset actual, regenera el borrador.",
                layoutUpdatedAt,
                datasetUpdatedAt);
        }

        return new RegenerationStatus(
            false,
            true,
            null,
            currentLayout?.ActualizadoEnUtc,
            datasetStatus.ActualizadoEnUtc);
    }

    private static string FormatUtc(DateTime value)
        => value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'");

    private static bool IsTableMissing(Exception ex)
    {
        if (ex is PostgresException postgresException &&
            postgresException.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            return true;
        }

        return ex.InnerException is not null && IsTableMissing(ex.InnerException);
    }

    private static bool IsLayoutTableMissing(Exception ex) => IsTableMissing(ex);

    private static bool IsDefaultSeed(string codigo)
        => DefaultSeeds.Any(x => string.Equals(x.Codigo, codigo, StringComparison.OrdinalIgnoreCase));

    private Task<bool> CompanyExistsAsync(long companyId, CancellationToken ct)
        => _context.cfg_companies
            .AsNoTracking()
            .AnyAsync(x => x.company_id == companyId, ct);

    private sealed record ReporteSeed(
        string Codigo,
        string Nombre,
        string Descripcion,
        string Categoria,
        string IconoCssClass,
        int Orden,
        string? DatasetCodigo);

    private sealed record LayoutSummary(long InformeId, int? DraftVersion, int? PublishedVersion);
    private sealed record CurrentLayoutInfo(string Estado, int Version, DateTime ActualizadoEnUtc);
    private sealed record DatasetStatus(string Codigo, DateTime? ActualizadoEnUtc, bool Exists);
    private sealed record RegenerationStatus(
        bool TieneDesfaseDatasetLayout,
        bool PuedeRegenerarBorrador,
        string? MensajeRegeneracionBorrador,
        DateTime? LayoutBaseActualizadoEnUtc,
        DateTime? DatasetActualizadoEnUtc);
}
