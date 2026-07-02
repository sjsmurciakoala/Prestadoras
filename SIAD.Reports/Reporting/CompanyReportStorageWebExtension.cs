using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using DevExpress.DataAccess.Sql;
using DevExpress.XtraReports;
using DevExpress.XtraReports.Parameters;
using DevExpress.XtraReports.UI;
using DevExpress.XtraReports.Web.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Npgsql;
using SIAD.Core.Constants;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Reports;

public sealed class CompanyReportStorageWebExtension : ReportStorageWebExtension
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _currentCompanyService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ReportTemplateFactory _templateFactory;
    private readonly IConfiguration _configuration;

    public CompanyReportStorageWebExtension(
        SiadDbContext context,
        ICurrentCompanyService currentCompanyService,
        IHttpContextAccessor httpContextAccessor,
        ReportTemplateFactory templateFactory,
        IConfiguration configuration)
    {
        _context = context;
        _currentCompanyService = currentCompanyService;
        _httpContextAccessor = httpContextAccessor;
        _templateFactory = templateFactory;
        _configuration = configuration;
    }

    public override bool CanSetData(string url)
        => CurrentCompanyId > 0 && IsValidUrl(url);

    public override bool IsValidUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        var request = ParseRequest(url);
        return ReportesWebConstants.IsValidCode(request.Codigo);
    }

    public override byte[] GetData(string url)
    {
        var request = ParseRequest(url);
        var companyId = RequireCompanyId();

        var catalogo = _context.rep_catalogo_informes
            .FirstOrDefault(
                x => x.company_id == companyId
                     && x.tipo_origen == ReportesWebConstants.TipoOrigenReporte
                     && x.codigo == request.Codigo);

        if (catalogo is null)
        {
            throw new InvalidOperationException($"El reporte '{request.Codigo}' no está registrado en el catálogo de reportería.");
        }

        try
        {
            var layout = ResolveLayout(companyId, catalogo.informe_id, request.Mode);
            
            if (layout is null && request.Mode == ReportesWebConstants.LayoutMode.Published)
            {
                layout = ResolveLayout(companyId, catalogo.informe_id, ReportesWebConstants.LayoutMode.Draft);
            }

            if (layout is null)
            {
                var defaultLayoutBytes = _templateFactory.CreateLayoutBytes(
                    catalogo.codigo,
                    catalogo.nombre,
                    catalogo.descripcion,
                    catalogo.consulta_clave);
                
                if (defaultLayoutBytes.Length > 0)
                {
                    var defaultXml = Encoding.UTF8.GetString(defaultLayoutBytes);
                    var now = DateTime.UtcNow;
                    var actor = ResolveActor();

                    var nextVersion = (_context.rep_reporte_layouts
                        .Where(x => x.company_id == companyId && x.informe_id == catalogo.informe_id)
                        .Select(x => (int?)x.version_num)
                        .Max() ?? 0) + 1;

                    var draft = new rep_reporte_layout
                    {
                        company_id = companyId,
                        informe_id = catalogo.informe_id,
                        version_num = nextVersion,
                        estado = ReportesWebConstants.LayoutStatus.Draft,
                        layout_xml = defaultXml,
                        created_at = now,
                        created_by = actor
                    };
                    _context.rep_reporte_layouts.Add(draft);

                    var published = new rep_reporte_layout
                    {
                        company_id = companyId,
                        informe_id = catalogo.informe_id,
                        version_num = nextVersion + 1,
                        estado = ReportesWebConstants.LayoutStatus.Published,
                        layout_xml = defaultXml,
                        created_at = now,
                        created_by = actor
                    };
                    _context.rep_reporte_layouts.Add(published);
                    _context.SaveChanges();

                    layout = request.Mode == ReportesWebConstants.LayoutMode.Draft ? draft : published;
                }
            }

            if (layout is not null && !string.IsNullOrWhiteSpace(layout.layout_xml))
            {
                var layoutXml = EnsureCompatibleLayoutXml(catalogo, layout);
                return PrepareLayoutData(companyId, layoutXml, request.Parameters);
            }
        }
        catch (Exception ex) when (IsLayoutTableMissing(ex))
        {
            throw new InvalidOperationException(
                "La tabla de layouts de reportería no existe. Ejecute la migración o el script 2026-03-21_add_rep_reporte_layout.sql.",
                ex);
        }

        throw new InvalidOperationException(BuildMissingLayoutMessage(catalogo.codigo, request.Mode));
    }

    public override Dictionary<string, string> GetUrls()
    {
        if (CurrentCompanyId <= 0)
        {
            return [];
        }

        return _context.rep_catalogo_informes
            .Where(x => x.company_id == CurrentCompanyId && x.tipo_origen == ReportesWebConstants.TipoOrigenReporte && x.is_active)
            .OrderBy(x => x.orden)
            .ThenBy(x => x.nombre)
            .ToDictionary(x => x.codigo, x => x.nombre);
    }

    public override void SetData(XtraReport report, string url)
    {
        ArgumentNullException.ThrowIfNull(report);

        var request = ParseRequest(url);
        var companyId = RequireCompanyId();
        var actor = ResolveActor();
        var catalogo = FindOrCreateCatalog(companyId, request.Codigo, actor);
        var now = DateTime.UtcNow;
        ReportCompanyHeaderParameters.Apply(report, ResolveHeaderCompany(companyId));
        var layoutXml = SerializeLayout(report);

        try
        {
            var draft = _context.rep_reporte_layouts
                .FirstOrDefault(x =>
                    x.company_id == companyId &&
                    x.informe_id == catalogo.informe_id &&
                    x.estado == ReportesWebConstants.LayoutStatus.Draft);

            if (draft is null)
            {
                var nextVersion = (_context.rep_reporte_layouts
                    .Where(x => x.company_id == companyId && x.informe_id == catalogo.informe_id)
                    .Select(x => (int?)x.version_num)
                    .Max() ?? 0) + 1;

                draft = new rep_reporte_layout
                {
                    company_id = companyId,
                    informe_id = catalogo.informe_id,
                    version_num = nextVersion,
                    estado = ReportesWebConstants.LayoutStatus.Draft,
                    layout_xml = layoutXml,
                    created_at = now,
                    created_by = actor
                };

                _context.rep_reporte_layouts.Add(draft);
            }
            else
            {
                draft.layout_xml = layoutXml;
                draft.updated_at = now;
                draft.updated_by = actor;
            }

            catalogo.updated_at = now;
            catalogo.updated_by = actor;

            _context.SaveChanges();
        }
        catch (Exception ex) when (IsLayoutTableMissing(ex))
        {
            throw new InvalidOperationException(
                "La tabla de layouts de reportería no existe. Ejecute la migración o el script 2026-03-21_add_rep_reporte_layout.sql.",
                ex);
        }
    }

    public override string SetNewData(XtraReport report, string defaultUrl)
    {
        var codigo = ReportesWebConstants.NormalizeCode(defaultUrl);
        if (!ReportesWebConstants.IsValidCode(codigo))
        {
            codigo = $"reporte-{DateTime.UtcNow:yyyyMMddHHmmss}";
        }

        SetData(report, ReportesWebConstants.BuildReportName(codigo, ReportesWebConstants.LayoutMode.Draft));
        return codigo;
    }

    private long CurrentCompanyId => _currentCompanyService.GetCompanyId();

    private long RequireCompanyId()
    {
        var companyId = CurrentCompanyId;
        if (companyId <= 0)
        {
            throw new InvalidOperationException("No fue posible resolver la empresa actual para reportería.");
        }

        return companyId;
    }

    private string ResolveActor()
        => _httpContextAccessor.HttpContext?.User?.Identity?.Name?.Trim() switch
        {
            { Length: > 0 } name => name,
            _ => "report-designer"
        };

    private cfg_company? ResolveHeaderCompany(long companyId)
        => companyId > 0
            ? _context.cfg_companies.FirstOrDefault(x => x.company_id == companyId)
            : null;

    private byte[] PrepareLayoutData(
        long companyId,
        string layoutXml,
        IReadOnlyDictionary<string, string?> requestParameters)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(layoutXml));
        using var report = XtraReport.FromXmlStream(input, true);

        ReportCompanyHeaderParameters.Apply(report, ResolveHeaderCompany(companyId));

        using var refreshed = new MemoryStream();
        report.SaveLayoutToXml(refreshed);
        var refreshedXml = Encoding.UTF8.GetString(refreshed.ToArray());

        return requestParameters.Count > 0
            ? ApplyRequestParameters(refreshedXml, requestParameters)
            : Encoding.UTF8.GetBytes(refreshedXml);
    }

    private rep_catalogo_informe FindOrCreateCatalog(long companyId, string codigo, string actor)
    {
        var catalogo = _context.rep_catalogo_informes
            .FirstOrDefault(x => x.company_id == companyId && x.codigo == codigo);

        if (catalogo is not null)
        {
            if (!catalogo.is_active)
            {
                throw new InvalidOperationException("El reporte solicitado esta inactivo o fue eliminado.");
            }

            return catalogo;
        }

        catalogo = new rep_catalogo_informe
        {
            company_id = companyId,
            codigo = codigo,
            nombre = codigo,
            descripcion = "Reporte creado desde el diseñador web.",
            categoria = "General",
            tipo_origen = ReportesWebConstants.TipoOrigenReporte,
            ruta = ReportesWebConstants.BuildViewerRoute(codigo),
            icono_css_class = "bi bi-file-earmark-richtext",
            orden = (_context.rep_catalogo_informes
                .Where(x => x.company_id == companyId)
                .Select(x => (int?)x.orden)
                .Max() ?? 0) + 10,
            permite_exportar = true,
            permite_imprimir = true,
            is_active = true,
            created_at = DateTime.UtcNow,
            created_by = actor
        };

        _context.rep_catalogo_informes.Add(catalogo);
        _context.SaveChanges();
        return catalogo;
    }

    private rep_reporte_layout? ResolveLayout(long companyId, long informeId, string mode)
    {
        if (mode == ReportesWebConstants.LayoutMode.Draft)
        {
            return _context.rep_reporte_layouts
                .Where(x => x.company_id == companyId && x.informe_id == informeId)
                .OrderByDescending(x => x.estado == ReportesWebConstants.LayoutStatus.Draft)
                .ThenByDescending(x => x.version_num)
                .FirstOrDefault();
        }

        return _context.rep_reporte_layouts
            .Where(x =>
                x.company_id == companyId &&
                x.informe_id == informeId &&
                x.estado == ReportesWebConstants.LayoutStatus.Published)
            .OrderByDescending(x => x.version_num)
            .FirstOrDefault();
    }

    private string EnsureCompatibleLayoutXml(rep_catalogo_informe catalogo, rep_reporte_layout layout)
    {
        var layoutXml = layout.layout_xml!;
        if (IsLegacyBancosObjectLayout(catalogo.codigo, layoutXml))
        {
            var now = DateTime.UtcNow;
            var migratedLayoutXml = Encoding.UTF8.GetString(
                _templateFactory.CreateLayoutBytes(
                    catalogo.codigo,
                    catalogo.nombre,
                    catalogo.descripcion,
                    catalogo.consulta_clave));

            layout.layout_xml = migratedLayoutXml;
            layout.updated_at = now;
            layout.updated_by = "reporteria-bootstrap";
            catalogo.updated_at = now;
            catalogo.updated_by = "reporteria-bootstrap";
            _context.SaveChanges();

            return migratedLayoutXml;
        }

        if (!TryMigrateStoredFunctionQueries(catalogo, layoutXml, out var migratedStoredFunctionLayoutXml))
        {
            return layoutXml;
        }

        var migrationTime = DateTime.UtcNow;
        layout.layout_xml = migratedStoredFunctionLayoutXml;
        layout.updated_at = migrationTime;
        layout.updated_by = "reporteria-compat";
        catalogo.updated_at = migrationTime;
        catalogo.updated_by = "reporteria-compat";
        _context.SaveChanges();

        return migratedStoredFunctionLayoutXml;
    }

    private static string SerializeLayout(XtraReport report)
    {
        ClearSerializedConnectionParameters(report);
        using var stream = new MemoryStream();
        report.SaveLayoutToXml(stream);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void ClearSerializedConnectionParameters(XtraReport report)
    {
        foreach (var dataSource in DataSourceManager.GetDataSources<SqlDataSource>(report, includeSubReports: true))
        {
            if (!string.IsNullOrWhiteSpace(dataSource.ConnectionName))
            {
                dataSource.ConnectionParameters = null;
            }
        }
    }

    private static ReportRequest ParseRequest(string url)
    {
        var raw = url?.Trim() ?? string.Empty;
        var parts = raw.Split('?', 2, StringSplitOptions.TrimEntries);
        var codigo = ReportesWebConstants.NormalizeCode(parts[0]);
        var mode = ReportesWebConstants.LayoutMode.Published;
        var parameters = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (parts.Length > 1)
        {
            var query = QueryHelpers.ParseQuery(parts[1]);
            foreach (var entry in query)
            {
                if (string.Equals(entry.Key, "mode", StringComparison.OrdinalIgnoreCase))
                {
                    var candidate = entry.Value.ToString();
                    if (string.Equals(candidate, ReportesWebConstants.LayoutMode.Draft, StringComparison.OrdinalIgnoreCase))
                    {
                        mode = ReportesWebConstants.LayoutMode.Draft;
                    }

                    continue;
                }

                parameters[entry.Key] = entry.Value.ToString();
            }
        }

        return new ReportRequest(codigo, mode, parameters);
    }

    private static byte[] ApplyRequestParameters(string layoutXml, IReadOnlyDictionary<string, string?> requestParameters)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(layoutXml));
        using var report = XtraReport.FromXmlStream(input, true);

        foreach (Parameter parameter in report.Parameters)
        {
            parameter.Visible = false;

            if (!requestParameters.TryGetValue(parameter.Name, out var rawValue))
            {
                continue;
            }

            parameter.Value = ConvertParameterValue(rawValue, parameter.Type);
        }

        report.RequestParameters = false;

        using var output = new MemoryStream();
        report.SaveLayoutToXml(output);
        return output.ToArray();
    }

    private static object? ConvertParameterValue(string? rawValue, Type parameterType)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var targetType = Nullable.GetUnderlyingType(parameterType) ?? parameterType;

        if (targetType == typeof(string))
        {
            return rawValue;
        }

        if (targetType == typeof(DateTime))
        {
            return DateTime.Parse(rawValue, CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(bool))
        {
            return string.Equals(rawValue, "1", StringComparison.OrdinalIgnoreCase)
                || bool.Parse(rawValue);
        }

        if (targetType == typeof(long))
        {
            return long.Parse(rawValue, CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(int))
        {
            return int.Parse(rawValue, CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(decimal))
        {
            return decimal.Parse(rawValue, CultureInfo.InvariantCulture);
        }

        if (targetType == typeof(double))
        {
            return double.Parse(rawValue, CultureInfo.InvariantCulture);
        }

        if (targetType.IsEnum)
        {
            return Enum.Parse(targetType, rawValue, ignoreCase: true);
        }

        return Convert.ChangeType(rawValue, targetType, CultureInfo.InvariantCulture);
    }

    private static bool IsLayoutTableMissing(Exception ex)
    {
        if (ex is PostgresException postgresException &&
            postgresException.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            return true;
        }

        return ex.InnerException is not null && IsLayoutTableMissing(ex.InnerException);
    }

    private static string BuildMissingLayoutMessage(string codigo, string mode)
        => mode == ReportesWebConstants.LayoutMode.Draft
            ? $"El reporte '{codigo}' no tiene un layout borrador persistido en la base de datos. Cree o regenere un borrador antes de abrir el diseñador."
            : $"El reporte '{codigo}' no tiene una versión publicada persistida en la base de datos. Publique un borrador antes de abrir el visor.";

    private static bool IsLegacyBancosObjectLayout(string codigo, string layoutXml)
        => string.Equals(codigo, ReportesWebConstants.CodigoReporteBancosTransacciones, StringComparison.OrdinalIgnoreCase)
           && (layoutXml.Contains("BancosTransaccionesObjectDataSource", StringComparison.OrdinalIgnoreCase)
               || layoutXml.Contains("BancosTransaccionesReportDataSource", StringComparison.OrdinalIgnoreCase)
               || layoutXml.Contains("ObjectDataSource", StringComparison.OrdinalIgnoreCase));

    private bool TryMigrateStoredFunctionQueries(rep_catalogo_informe catalogo, string layoutXml, out string migratedLayoutXml)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(layoutXml));
        using var report = XtraReport.FromXmlStream(stream, true);

        var storedFunctionDefinition = TryResolveStoredFunctionLayoutDefinition(catalogo);
        var changed = false;
        foreach (var dataSource in DataSourceManager.GetDataSources<SqlDataSource>(report, includeSubReports: true))
        {
            changed |= RewriteStoredFunctionQueries(dataSource, storedFunctionDefinition);
        }

        if (!changed)
        {
            migratedLayoutXml = layoutXml;
            return false;
        }

        migratedLayoutXml = SerializeLayout(report);
        return true;
    }

    private StoredFunctionLayoutDefinition? TryResolveStoredFunctionLayoutDefinition(rep_catalogo_informe catalogo)
    {
        if (string.IsNullOrWhiteSpace(catalogo.consulta_clave))
        {
            return null;
        }

        var datasetCode = ReportesWebConstants.NormalizeCode(catalogo.consulta_clave);
        if (!ReportesWebConstants.IsValidCode(datasetCode))
        {
            return null;
        }

        var dataset = _context.rep_catalogo_datasets
            .FirstOrDefault(x =>
                x.company_id == catalogo.company_id &&
                x.codigo == datasetCode &&
                x.is_active &&
                x.tipo_origen == ReportesWebConstants.DatasetSourceType.StoredProcedure &&
                !string.IsNullOrWhiteSpace(x.origen_clave));

        if (dataset is null)
        {
            return null;
        }

        var arguments = _context.rep_dataset_parametros
            .Where(x => x.company_id == dataset.company_id && x.dataset_id == dataset.dataset_id)
            .OrderBy(x => x.orden)
            .ThenBy(x => x.nombre)
            .Select(x => ReportingStoredFunctionSqlHelper.CreateArgument(
                string.IsNullOrWhiteSpace(x.nombre_origen) ? x.nombre : x.nombre_origen,
                ReportingStoredFunctionSqlHelper.ResolvePostgreSqlTypeName(x.tipo_dato)))
            .ToList();

        return new StoredFunctionLayoutDefinition(dataset.origen_clave!, arguments);
    }

    private bool RewriteStoredFunctionQueries(
        SqlDataSource dataSource,
        StoredFunctionLayoutDefinition? storedFunctionDefinition)
    {
        var changed = false;
        var storedProcQueries = dataSource.Queries
            .OfType<StoredProcQuery>()
            .ToList();

        var customSqlQueries = dataSource.Queries
            .OfType<CustomSqlQuery>()
            .ToList();

        if (storedProcQueries.Count == 0 && customSqlQueries.Count == 0)
        {
            return false;
        }

        var connectionName = string.IsNullOrWhiteSpace(dataSource.ConnectionName)
            ? ReportesWebConstants.DefaultReportingConnectionName
            : dataSource.ConnectionName;

        string? expectedStoredFunctionSql = null;
        if (storedFunctionDefinition is not null)
        {
            expectedStoredFunctionSql = ReportingStoredFunctionSqlHelper.BuildSelectSql(
                storedFunctionDefinition.RoutineName,
                storedFunctionDefinition.Arguments);
        }

        foreach (var storedProcQuery in storedProcQueries)
        {
            var replacementSql =
                storedFunctionDefinition is not null &&
                IsSameRoutineName(storedProcQuery.StoredProcName, storedFunctionDefinition.RoutineName)
                    ? expectedStoredFunctionSql!
                    : ReportingStoredFunctionSqlHelper.BuildSelectSql(
                        storedProcQuery.StoredProcName,
                        storedProcQuery.Parameters.Cast<QueryParameter>().Select(ReportingStoredFunctionSqlHelper.CreateArgument));

            var customSqlQuery = new CustomSqlQuery
            {
                Name = storedProcQuery.Name,
                Sql = replacementSql
            };

            foreach (QueryParameter parameter in storedProcQuery.Parameters)
            {
                customSqlQuery.Parameters.Add(ReportingStoredFunctionSqlHelper.CloneAsCustomSqlParameter(parameter));
            }

            var index = dataSource.Queries.IndexOf(storedProcQuery);
            dataSource.Queries.Remove(storedProcQuery);
            dataSource.Queries.Insert(index, customSqlQuery);
            changed = true;
        }

        if (storedFunctionDefinition is not null && expectedStoredFunctionSql is not null)
        {
            foreach (var customSqlQuery in customSqlQueries)
            {
                if (!IsStoredFunctionCustomSqlQuery(customSqlQuery.Sql, storedFunctionDefinition.RoutineName))
                {
                    continue;
                }

                if (string.Equals(
                    NormalizeSql(customSqlQuery.Sql),
                    NormalizeSql(expectedStoredFunctionSql),
                    StringComparison.Ordinal))
                {
                    continue;
                }

                customSqlQuery.Sql = expectedStoredFunctionSql;
                changed = true;
            }
        }

        if (!changed)
        {
            return false;
        }

        dataSource.ConnectionParameters = ReportingPostgreSqlConnectionResolver.Resolve(_configuration, connectionName);
        dataSource.RebuildResultSchema();
        dataSource.ConnectionParameters = null;
        return true;
    }

    private static bool IsSameRoutineName(string? left, string? right)
        => string.Equals(NormalizeRoutineName(left), NormalizeRoutineName(right), StringComparison.Ordinal);

    private static bool IsStoredFunctionCustomSqlQuery(string? sql, string routineName)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return false;
        }

        var normalizedSql = NormalizeSql(sql);
        var normalizedRoutineName = NormalizeRoutineName(routineName);
        return normalizedSql.Contains($"from {normalizedRoutineName}(", StringComparison.Ordinal);
    }

    private static string NormalizeRoutineName(string? routineName)
        => string.IsNullOrWhiteSpace(routineName)
            ? string.Empty
            : routineName.Trim().Trim('"').ToLowerInvariant();

    private static string NormalizeSql(string? sql)
        => string.IsNullOrWhiteSpace(sql)
            ? string.Empty
            : Regex.Replace(sql.Trim(), "\\s+", " ").ToLowerInvariant();

    private sealed record StoredFunctionLayoutDefinition(
        string RoutineName,
        IReadOnlyList<ReportingStoredFunctionSqlHelper.StoredFunctionArgument> Arguments);

    private sealed record ReportRequest(string Codigo, string Mode, IReadOnlyDictionary<string, string?> Parameters);
}
