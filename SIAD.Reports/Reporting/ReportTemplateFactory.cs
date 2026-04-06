using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using DevExpress.DataAccess.ConnectionParameters;
using DevExpress.DataAccess;
using DevExpress.DataAccess.Sql;
using DevExpress.Drawing;
using DevExpress.XtraPrinting;
using DevExpress.XtraReports.Parameters;
using DevExpress.XtraReports.UI;
using Microsoft.Extensions.Configuration;
using SIAD.Core.Constants;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Reports;

public sealed class ReportTemplateFactory
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _currentCompanyService;
    private readonly IConfiguration _configuration;

    public ReportTemplateFactory(
        SiadDbContext context,
        ICurrentCompanyService currentCompanyService,
        IConfiguration configuration)
    {
        _context = context;
        _currentCompanyService = currentCompanyService;
        _configuration = configuration;
    }

    public byte[] CreateLayoutBytes(string reportCode, string displayName, string? description, string? datasetCode = null)
    {
        using var report = CreateTemplateReport(reportCode, displayName, description, datasetCode);
        using var stream = new MemoryStream();
        report.SaveLayoutToXml(stream);
        return stream.ToArray();
    }

    public XtraReport CreateTemplateReport(string reportCode, string displayName, string? description, string? datasetCode = null)
    {
        var normalizedReportCode = ReportesWebConstants.NormalizeCode(reportCode);
        var dataset = ResolveDataset(datasetCode);

        if (dataset is not null)
        {
            return CreateDatasetTemplate(normalizedReportCode, displayName, description, dataset);
        }

        return normalizedReportCode switch
        {
            ReportesWebConstants.CodigoReporteBancosTransacciones => CreateBancosTransaccionesTemplate(reportCode, displayName, description, BuildDefaultBancosDataset()),
            ReportesWebConstants.CodigoReporteBalanceComprobacion => CreateBalanceComprobacionTemplate(reportCode, displayName, description, BuildDefaultBalanceComprobacionDataset()),
            ReportesWebConstants.CodigoReporteEstadoSituacionFinanciera => CreateEstadoSituacionFinancieraTemplate(reportCode, displayName, description, BuildDefaultEstadoSituacionFinancieraDataset()),
            ReportesWebConstants.CodigoReporteEstadoResultados => CreateEstadoResultadosTemplate(reportCode, displayName, description, BuildDefaultEstadoResultadosDataset()),
            _ => CreateBlankTemplate(reportCode, displayName, description)
        };
    }

    private XtraReport CreateDatasetTemplate(string reportCode, string displayName, string? description, DatasetDefinition dataset)
    {
        if (IsBancosTransaccionesTemplate(reportCode, dataset) &&
            dataset.SourceType is ReportesWebConstants.DatasetSourceType.StoredProcedure
                or ReportesWebConstants.DatasetSourceType.View
                or ReportesWebConstants.DatasetSourceType.Sql)
        {
            return CreateBancosTransaccionesTemplate(reportCode, displayName, description, dataset);
        }

        if (IsBalanceComprobacionTemplate(reportCode, dataset) &&
            dataset.SourceType is ReportesWebConstants.DatasetSourceType.StoredProcedure
                or ReportesWebConstants.DatasetSourceType.View
                or ReportesWebConstants.DatasetSourceType.Sql)
        {
            return CreateBalanceComprobacionTemplate(reportCode, displayName, description, dataset);
        }

        if (IsEstadoSituacionFinancieraTemplate(reportCode, dataset) &&
            dataset.SourceType is ReportesWebConstants.DatasetSourceType.StoredProcedure
                or ReportesWebConstants.DatasetSourceType.View
                or ReportesWebConstants.DatasetSourceType.Sql)
        {
            return CreateEstadoSituacionFinancieraTemplate(reportCode, displayName, description, dataset);
        }

        if (IsEstadoResultadosTemplate(reportCode, dataset) &&
            dataset.SourceType is ReportesWebConstants.DatasetSourceType.StoredProcedure
                or ReportesWebConstants.DatasetSourceType.View
                or ReportesWebConstants.DatasetSourceType.Sql)
        {
            return CreateEstadoResultadosTemplate(reportCode, displayName, description, dataset);
        }

        return dataset.SourceType switch
        {
            ReportesWebConstants.DatasetSourceType.StoredProcedure or ReportesWebConstants.DatasetSourceType.View or ReportesWebConstants.DatasetSourceType.Sql
                => CreateRelationalBackedTemplate(reportCode, displayName, description, dataset),
            _ => CreateBlankTemplate(reportCode, displayName, BuildUnsupportedDatasetDescription(description, dataset))
        };
    }

    private static XtraReport CreateBlankTemplate(string reportCode, string displayName, string? description)
    {
        var report = CreateBaseReport(reportCode, displayName);

        var detail = new DetailBand { HeightF = 150f };
        var title = new XRLabel
        {
            BoundsF = new RectangleF(0f, 18f, 650f, 34f),
            Font = new DXFont("Arial", 18f, DXFontStyle.Bold),
            Text = displayName,
            TextAlignment = TextAlignment.MiddleCenter
        };

        var subtitle = new XRLabel
        {
            BoundsF = new RectangleF(0f, 64f, 650f, 48f),
            Font = new DXFont("Arial", 10f),
            Multiline = true,
            Text = string.IsNullOrWhiteSpace(description)
                ? "Plantilla base de reporte web. Diseñe el detalle en el editor web y publique la versión aprobada."
                : description,
            TextAlignment = TextAlignment.MiddleCenter
        };

        detail.Controls.AddRange([title, subtitle]);
        report.Bands.Add(detail);
        return report;
    }

    private XtraReport CreateRelationalBackedTemplate(string reportCode, string displayName, string? description, DatasetDefinition dataset)
    {
        var report = CreateBaseReport(reportCode, displayName);
        report.RequestParameters = dataset.Parameters.Any(x => x.Source == ReportesWebConstants.DatasetParameterValueSource.Report && x.Visible);

        foreach (var parameter in dataset.Parameters)
        {
            report.Parameters.Add(CreateReportParameter(parameter));
        }

        var queryName = string.IsNullOrWhiteSpace(dataset.Code) ? "MainQuery" : dataset.Code.Replace('-', '_');
        var dataSource = CreateRelationalDataSource(dataset, queryName);
        report.ComponentStorage.AddRange([dataSource]);
        report.DataSource = dataSource;
        report.DataMember = queryName;

        var header = new ReportHeaderBand { HeightF = 120f };
        header.Controls.AddRange(
        [
            new XRLabel
            {
                BoundsF = new RectangleF(0f, 0f, 650f, 30f),
                Font = new DXFont("Arial", 18f, DXFontStyle.Bold),
                Text = displayName,
                TextAlignment = TextAlignment.MiddleLeft
            },
            new XRLabel
            {
                BoundsF = new RectangleF(0f, 38f, 650f, 30f),
                Font = new DXFont("Arial", 9.5f),
                Multiline = true,
                Text = string.IsNullOrWhiteSpace(description)
                    ? $"Dataset {dataset.Code} ({dataset.SourceType}). Arrastre campos desde el Field List para diseñar el detalle."
                    : description,
                TextAlignment = TextAlignment.MiddleLeft
            },
            new XRLabel
            {
                BoundsF = new RectangleF(0f, 80f, 650f, 32f),
                Font = new DXFont("Arial", 9f),
                ForeColor = Color.DimGray,
                Multiline = true,
                Text = "La fuente de datos se resolvió desde el catálogo de datasets. El layout puede modificarse y publicarse sin redeploy de la aplicación.",
                TextAlignment = TextAlignment.MiddleLeft
            }
        ]);

        var detailBand = new DetailBand { HeightF = 140f };
        detailBand.Controls.Add(new XRLabel
        {
            BoundsF = new RectangleF(0f, 16f, 650f, 96f),
            Font = new DXFont("Arial", 10f),
            Multiline = true,
            Text = "Plantilla enlazada a dataset. Use el diseñador para arrastrar campos, crear tablas, grupos, expresiones y filtros. Los campos ya deberían estar disponibles en el Field List.",
            TextAlignment = TextAlignment.MiddleCenter,
            Borders = BorderSide.All,
            BorderWidth = 1f,
            BorderColor = Color.Gainsboro,
            Padding = new PaddingInfo(16, 16, 16, 16)
        });

        report.Bands.AddRange([header, detailBand]);
        return report;
    }

    private SqlDataSource CreateRelationalDataSource(DatasetDefinition dataset, string queryName)
    {
        var connectionName = string.IsNullOrWhiteSpace(dataset.ConnectionName)
            ? ReportesWebConstants.DefaultReportingConnectionName
            : dataset.ConnectionName;
        var dataSource = new SqlDataSource
        {
            Name = $"{queryName}DataSource",
            ConnectionName = connectionName,
            ConnectionParameters = ReportingPostgreSqlConnectionResolver.Resolve(_configuration, connectionName)
        };

        SqlQuery query = dataset.SourceType switch
        {
            // PostgreSQL datasets marked as STORED_PROCEDURE are implemented as set-returning
            // functions. DevExpress resolves the schema for StoredProcQuery, but later fails at
            // document generation with StoredProcNotInSchemaValidationException. CustomSqlQuery
            // keeps the same function call and avoids that validation path.
            ReportesWebConstants.DatasetSourceType.StoredProcedure => BuildStoredProcedureQuery(dataset, queryName),
            ReportesWebConstants.DatasetSourceType.View => BuildViewQuery(dataset, queryName),
            ReportesWebConstants.DatasetSourceType.Sql => BuildSqlQuery(dataset, queryName),
            _ => throw new InvalidOperationException($"El tipo de dataset {dataset.SourceType} no usa SqlDataSource.")
        };

        dataSource.Queries.Add(query);
        dataSource.RebuildResultSchema();
        dataSource.ConnectionParameters = null;
        return dataSource;
    }

    private CustomSqlQuery BuildStoredProcedureQuery(DatasetDefinition dataset, string queryName)
    {
        var query = new CustomSqlQuery
        {
            Name = queryName,
            Sql = ReportingStoredFunctionSqlHelper.BuildSelectSql(
                dataset.OriginKey!,
                dataset.Parameters.Select(parameter =>
                    ReportingStoredFunctionSqlHelper.CreateArgument(
                        string.IsNullOrWhiteSpace(parameter.QueryName)
                            ? parameter.Name
                            : parameter.QueryName,
                        ReportingStoredFunctionSqlHelper.ResolvePostgreSqlTypeName(parameter.DataType))))
        };

        foreach (var parameter in dataset.Parameters)
        {
            query.Parameters.Add(CreateQueryParameter(parameter, ReportesWebConstants.DatasetSourceType.Sql));
        }

        return query;
    }

    private CustomSqlQuery BuildViewQuery(DatasetDefinition dataset, string queryName)
    {
        var query = new CustomSqlQuery
        {
            Name = queryName,
            Sql = $"SELECT * FROM {dataset.OriginKey}"
        };

        return query;
    }

    private CustomSqlQuery BuildSqlQuery(DatasetDefinition dataset, string queryName)
    {
        var query = new CustomSqlQuery
        {
            Name = queryName,
            Sql = dataset.SqlText!
        };

        foreach (var parameter in dataset.Parameters)
        {
            query.Parameters.Add(CreateQueryParameter(parameter, dataset.SourceType));
        }

        return query;
    }

    private static QueryParameter CreateQueryParameter(DatasetParameterDefinition parameter, string datasetSourceType)
        => new()
        {
            Name = ResolveQueryParameterName(parameter, datasetSourceType),
            Type = typeof(Expression),
            Value = new Expression($"?{parameter.Name}", ResolveParameterType(parameter.DataType))
        };

    private Parameter CreateReportParameter(DatasetParameterDefinition parameter)
        => new()
        {
            Name = parameter.Name,
            Description = parameter.Label,
            Type = ResolveParameterType(parameter.DataType),
            AllowNull = parameter.AllowNull,
            Visible = parameter.Source == ReportesWebConstants.DatasetParameterValueSource.Report && parameter.Visible,
            Value = ResolveParameterDefaultValue(parameter)
        };

    private object? ResolveParameterDefaultValue(DatasetParameterDefinition parameter)
    {
        if (parameter.Source == ReportesWebConstants.DatasetParameterValueSource.CurrentCompany)
        {
            return _currentCompanyService.GetCompanyId();
        }

        if (string.IsNullOrWhiteSpace(parameter.DefaultValue))
        {
            return parameter.DataType == ReportesWebConstants.DatasetParameterDataType.Boolean ? false : null;
        }

        return parameter.DataType switch
        {
            ReportesWebConstants.DatasetParameterDataType.Text => parameter.DefaultValue,
            ReportesWebConstants.DatasetParameterDataType.Int64 => long.Parse(parameter.DefaultValue, CultureInfo.InvariantCulture),
            ReportesWebConstants.DatasetParameterDataType.Decimal => decimal.Parse(parameter.DefaultValue, CultureInfo.InvariantCulture),
            ReportesWebConstants.DatasetParameterDataType.Date => DateTime.Parse(parameter.DefaultValue, CultureInfo.InvariantCulture),
            ReportesWebConstants.DatasetParameterDataType.DateTime => DateTime.Parse(parameter.DefaultValue, CultureInfo.InvariantCulture),
            ReportesWebConstants.DatasetParameterDataType.Boolean => bool.Parse(parameter.DefaultValue),
            _ => parameter.DefaultValue
        };
    }

    private DatasetDefinition? ResolveDataset(string? datasetCode)
    {
        var companyId = _currentCompanyService.GetCompanyId();
        if (companyId <= 0 || string.IsNullOrWhiteSpace(datasetCode))
        {
            return null;
        }

        var normalizedCode = ReportesWebConstants.NormalizeCode(datasetCode);
        if (!ReportesWebConstants.IsValidCode(normalizedCode))
        {
            return null;
        }

        var dataset = _context.rep_catalogo_datasets
            .FirstOrDefault(x => x.company_id == companyId && x.codigo == normalizedCode && x.is_active);

        if (dataset is null)
        {
            return normalizedCode switch
            {
                ReportesWebConstants.CodigoDatasetBancosTransacciones => BuildDefaultBancosDataset(),
                ReportesWebConstants.CodigoDatasetBalanceComprobacion => BuildDefaultBalanceComprobacionDataset(),
                ReportesWebConstants.CodigoDatasetEstadoSituacionFinanciera => BuildDefaultEstadoSituacionFinancieraDataset(),
                ReportesWebConstants.CodigoDatasetEstadoResultados => BuildDefaultEstadoResultadosDataset(),
                _ => null
            };
        }

        var parameters = _context.rep_dataset_parametros
            .Where(x => x.company_id == companyId && x.dataset_id == dataset.dataset_id)
            .OrderBy(x => x.orden)
            .ThenBy(x => x.nombre)
            .Select(x => new DatasetParameterDefinition(
                x.nombre,
                GetDistinctOriginName(x.nombre_origen, x.nombre),
                x.etiqueta,
                x.tipo_dato,
                x.fuente_valor,
                x.valor_default,
                x.visible,
                x.permite_nulo,
                x.requerido,
                x.orden))
            .ToList();

        return new DatasetDefinition(
            dataset.codigo,
            dataset.nombre,
            dataset.tipo_origen,
            dataset.origen_clave,
            dataset.sql_text,
            dataset.connection_name,
            parameters);
    }

    private static DatasetDefinition BuildDefaultBancosDataset()
        => new(
            ReportesWebConstants.CodigoDatasetBancosTransacciones,
            "Dataset transacciones bancarias",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetBancosTransacciones,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            BuildDefaultBancosDatasetParameters());

    private static IReadOnlyList<DatasetParameterDefinition> BuildDefaultBancosDatasetParameters()
        => [
            new(
                "CompanyId",
                "p_company_id",
                "Empresa actual",
                ReportesWebConstants.DatasetParameterDataType.Int64,
                ReportesWebConstants.DatasetParameterValueSource.CurrentCompany,
                null,
                false,
                false,
                true,
                0),
            new(
                "BancoCuentaId",
                "p_banco_cuenta_id",
                "Cuenta bancaria",
                ReportesWebConstants.DatasetParameterDataType.Int64,
                ReportesWebConstants.DatasetParameterValueSource.Report,
                null,
                false,
                true,
                false,
                10),
            new(
                "FechaDesde",
                "p_fecha_desde",
                "Fecha desde",
                ReportesWebConstants.DatasetParameterDataType.Date,
                ReportesWebConstants.DatasetParameterValueSource.Report,
                null,
                true,
                true,
                false,
                20),
            new(
                "FechaHasta",
                "p_fecha_hasta",
                "Fecha hasta",
                ReportesWebConstants.DatasetParameterDataType.Date,
                ReportesWebConstants.DatasetParameterValueSource.Report,
                null,
                true,
                true,
                false,
                30),
            new(
                "IncluirAnuladas",
                "p_incluir_anuladas",
                "Incluir anuladas",
                ReportesWebConstants.DatasetParameterDataType.Boolean,
                ReportesWebConstants.DatasetParameterValueSource.Report,
                "false",
                true,
                false,
                false,
                40)
        ];

    private static DatasetDefinition BuildDefaultBalanceComprobacionDataset()
        => new(
            ReportesWebConstants.CodigoDatasetBalanceComprobacion,
            "Dataset balance de comprobacion",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetBalanceComprobacion,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            BuildDefaultBalanceComprobacionDatasetParameters());

    private static IReadOnlyList<DatasetParameterDefinition> BuildDefaultBalanceComprobacionDatasetParameters()
        => [
            new(
                "CompanyId",
                "p_company_id",
                "Empresa actual",
                ReportesWebConstants.DatasetParameterDataType.Int64,
                ReportesWebConstants.DatasetParameterValueSource.CurrentCompany,
                null,
                false,
                false,
                true,
                0),
            new(
                "FechaDesde",
                "p_fecha_desde",
                "Fecha desde",
                ReportesWebConstants.DatasetParameterDataType.Date,
                ReportesWebConstants.DatasetParameterValueSource.Report,
                null,
                true,
                false,
                true,
                10),
            new(
                "FechaHasta",
                "p_fecha_hasta",
                "Fecha hasta",
                ReportesWebConstants.DatasetParameterDataType.Date,
                ReportesWebConstants.DatasetParameterValueSource.Report,
                null,
                true,
                false,
                true,
                20),
            new(
                "IncluirSinMovimiento",
                "p_incluir_sin_movimiento",
                "Incluir cuentas sin movimiento",
                ReportesWebConstants.DatasetParameterDataType.Boolean,
                ReportesWebConstants.DatasetParameterValueSource.Report,
                "false",
                true,
                false,
                false,
                30)
        ];

    private static DatasetDefinition BuildDefaultEstadoSituacionFinancieraDataset()
        => new(
            ReportesWebConstants.CodigoDatasetEstadoSituacionFinanciera,
            "Dataset estado de situacion financiera",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetEstadoSituacionFinanciera,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            BuildDefaultEstadoSituacionFinancieraDatasetParameters());

    private static IReadOnlyList<DatasetParameterDefinition> BuildDefaultEstadoSituacionFinancieraDatasetParameters()
        => [
            new(
                "CompanyId",
                "p_company_id",
                "Empresa actual",
                ReportesWebConstants.DatasetParameterDataType.Int64,
                ReportesWebConstants.DatasetParameterValueSource.CurrentCompany,
                null,
                false,
                false,
                true,
                0),
            new(
                "FechaCorte",
                "p_fecha_corte",
                "Fecha de corte",
                ReportesWebConstants.DatasetParameterDataType.Date,
                ReportesWebConstants.DatasetParameterValueSource.Report,
                null,
                true,
                false,
                true,
                10)
        ];

    private static DatasetDefinition BuildDefaultEstadoResultadosDataset()
        => new(
            ReportesWebConstants.CodigoDatasetEstadoResultados,
            "Dataset estado de resultados",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetEstadoResultados,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            BuildDefaultEstadoResultadosDatasetParameters());

    private static IReadOnlyList<DatasetParameterDefinition> BuildDefaultEstadoResultadosDatasetParameters()
        => [
            new(
                "CompanyId",
                "p_company_id",
                "Empresa actual",
                ReportesWebConstants.DatasetParameterDataType.Int64,
                ReportesWebConstants.DatasetParameterValueSource.CurrentCompany,
                null,
                false,
                false,
                true,
                0),
            new(
                "FechaDesde",
                "p_fecha_desde",
                "Fecha desde",
                ReportesWebConstants.DatasetParameterDataType.Date,
                ReportesWebConstants.DatasetParameterValueSource.Report,
                null,
                true,
                false,
                true,
                10),
            new(
                "FechaHasta",
                "p_fecha_hasta",
                "Fecha hasta",
                ReportesWebConstants.DatasetParameterDataType.Date,
                ReportesWebConstants.DatasetParameterValueSource.Report,
                null,
                true,
                false,
                true,
                20)
        ];

    private static bool IsBancosTransaccionesTemplate(string reportCode, DatasetDefinition dataset)
        => string.Equals(ReportesWebConstants.NormalizeCode(reportCode), ReportesWebConstants.CodigoReporteBancosTransacciones, StringComparison.OrdinalIgnoreCase)
           || string.Equals(dataset.Code, ReportesWebConstants.CodigoDatasetBancosTransacciones, StringComparison.OrdinalIgnoreCase);

    private static bool IsBalanceComprobacionTemplate(string reportCode, DatasetDefinition dataset)
        => string.Equals(ReportesWebConstants.NormalizeCode(reportCode), ReportesWebConstants.CodigoReporteBalanceComprobacion, StringComparison.OrdinalIgnoreCase)
           || string.Equals(dataset.Code, ReportesWebConstants.CodigoDatasetBalanceComprobacion, StringComparison.OrdinalIgnoreCase);

    private static bool IsEstadoSituacionFinancieraTemplate(string reportCode, DatasetDefinition dataset)
        => string.Equals(ReportesWebConstants.NormalizeCode(reportCode), ReportesWebConstants.CodigoReporteEstadoSituacionFinanciera, StringComparison.OrdinalIgnoreCase)
           || string.Equals(dataset.Code, ReportesWebConstants.CodigoDatasetEstadoSituacionFinanciera, StringComparison.OrdinalIgnoreCase);

    private static bool IsEstadoResultadosTemplate(string reportCode, DatasetDefinition dataset)
        => string.Equals(ReportesWebConstants.NormalizeCode(reportCode), ReportesWebConstants.CodigoReporteEstadoResultados, StringComparison.OrdinalIgnoreCase)
           || string.Equals(dataset.Code, ReportesWebConstants.CodigoDatasetEstadoResultados, StringComparison.OrdinalIgnoreCase);

    private static string BuildUnsupportedDatasetDescription(string? originalDescription, DatasetDefinition dataset)
        => string.IsNullOrWhiteSpace(originalDescription)
            ? $"El dataset '{dataset.Code}' existe, pero el origen '{dataset.SourceType}' todavía no tiene una plantilla automática registrada."
            : $"{originalDescription} Este dataset aún no tiene una plantilla automática registrada.";

    private static Type ResolveParameterType(string dataType)
        => dataType switch
        {
            ReportesWebConstants.DatasetParameterDataType.Text => typeof(string),
            ReportesWebConstants.DatasetParameterDataType.Int64 => typeof(long),
            ReportesWebConstants.DatasetParameterDataType.Decimal => typeof(decimal),
            ReportesWebConstants.DatasetParameterDataType.Date => typeof(DateTime),
            ReportesWebConstants.DatasetParameterDataType.DateTime => typeof(DateTime),
            ReportesWebConstants.DatasetParameterDataType.Boolean => typeof(bool),
            _ => typeof(string)
        };

    private static string ResolveQueryParameterName(DatasetParameterDefinition parameter, string datasetSourceType)
        => NormalizeQueryParameterIdentifier(parameter.QueryName, parameter.Name);

    private static string NormalizeQueryParameterIdentifier(string? candidate, string fallbackName)
        => ReportingStoredFunctionSqlHelper.NormalizeParameterName(candidate, fallbackName);

    private static string? GetDistinctOriginName(string? originName, string logicalName)
    {
        if (string.IsNullOrWhiteSpace(originName))
        {
            return null;
        }

        var normalized = NormalizeQueryParameterIdentifier(originName, logicalName);
        return string.Equals(normalized, logicalName, StringComparison.OrdinalIgnoreCase)
            ? null
            : normalized;
    }

    private XtraReport CreateBancosTransaccionesTemplate(string reportCode, string displayName, string? description, DatasetDefinition dataset)
    {
        var report = CreateBaseReport(reportCode, displayName);
        report.Landscape = true;
        report.PaperKind = DevExpress.Drawing.Printing.DXPaperKind.Letter;
        report.RequestParameters = dataset.Parameters.Any(x => x.Source == ReportesWebConstants.DatasetParameterValueSource.Report && x.Visible);

        foreach (var parameter in dataset.Parameters)
        {
            var reportParameter = CreateReportParameter(parameter);
            ApplyBancosTemplateDefaults(reportParameter);
            report.Parameters.Add(reportParameter);
        }

        var queryName = string.IsNullOrWhiteSpace(dataset.Code) ? "MainQuery" : dataset.Code.Replace('-', '_');
        var dataSource = CreateRelationalDataSource(dataset, queryName);
        report.ComponentStorage.AddRange([dataSource]);
        report.DataSource = dataSource;
        report.DataMember = queryName;

        var reportHeader = new ReportHeaderBand { HeightF = 78f };
        var pageHeader = new PageHeaderBand { HeightF = 52f };
        var detailBand = new DetailBand { HeightF = 24f };
        var reportFooter = new ReportFooterBand { HeightF = 32f };
        var pageFooter = new PageFooterBand { HeightF = 24f };

        reportHeader.Controls.AddRange(
        [
            new XRLabel
            {
                BoundsF = new RectangleF(0f, 0f, 960f, 32f),
                Font = new DXFont("Arial", 18f, DXFontStyle.Bold),
                Text = displayName,
                TextAlignment = TextAlignment.MiddleLeft
            },
            new XRLabel
            {
                BoundsF = new RectangleF(0f, 38f, 960f, 34f),
                Font = new DXFont("Arial", 9f),
                Multiline = true,
                Text = string.IsNullOrWhiteSpace(description)
                    ? "Reporte web de transacciones bancarias. Ajuste los parámetros en el visor o rediseñe el layout desde el diseñador."
                    : description,
                TextAlignment = TextAlignment.MiddleLeft
            }
        ]);

        pageHeader.Controls.Add(new XRLabel
        {
            BoundsF = new RectangleF(0f, 0f, 960f, 18f),
            Font = new DXFont("Arial", 8.5f),
            ForeColor = Color.DimGray,
            Text = "Filtros activos: Fecha desde, fecha hasta e inclusión de anuladas. El company_id se resuelve desde la sesión actual.",
            TextAlignment = TextAlignment.MiddleLeft
        });

        var headerTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 22f, 960f, 28f),
            BackColor = Color.FromArgb(232, 238, 245),
            Borders = BorderSide.All,
            BorderWidth = 1f,
            Font = new DXFont("Arial", 8.5f, DXFontStyle.Bold),
            ForeColor = Color.FromArgb(39, 54, 74),
            TextAlignment = TextAlignment.MiddleCenter
        };
        var headerRow = new XRTableRow();
        headerRow.Cells.AddRange(
        [
            CreateHeaderCell("Fecha", 1.10f),
            CreateHeaderCell("Banco", 1.55f),
            CreateHeaderCell("Cuenta", 1.85f),
            CreateHeaderCell("Tipo", 1.20f),
            CreateHeaderCell("Descripción", 2.75f),
            CreateHeaderCell("Referencia", 1.55f),
            CreateHeaderCell("Monto", 1.20f),
            CreateHeaderCell("Saldo", 1.20f),
            CreateHeaderCell("Estado", 0.95f)
        ]);
        headerTable.Rows.Add(headerRow);
        pageHeader.Controls.Add(headerTable);

        var detailTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 0f, 960f, 24f),
            Borders = BorderSide.Left | BorderSide.Right | BorderSide.Bottom,
            BorderWidth = 1f,
            Font = new DXFont("Arial", 8f),
            OddStyleName = "DetailOddStyle"
        };
        var detailRow = new XRTableRow();
        detailRow.Cells.AddRange(
        [
            CreateDetailCell("[FechaMovimiento]", 1.10f, TextAlignment.MiddleCenter, "{0:dd/MM/yyyy}"),
            CreateDetailCell("[BancoNombre]", 1.55f),
            CreateDetailCell("[CuentaDisplay]", 1.85f),
            CreateDetailCell("[TipoTransaccion]", 1.20f),
            CreateDetailCell("[Descripcion]", 2.75f),
            CreateDetailCell("[Referencia]", 1.55f),
            CreateDetailCell("[Monto]", 1.20f, TextAlignment.MiddleRight, "{0:n2}"),
            CreateDetailCell("[SaldoResultante]", 1.20f, TextAlignment.MiddleRight, "{0:n2}"),
            CreateDetailCell("[Estado]", 0.95f, TextAlignment.MiddleCenter)
        ]);
        detailTable.Rows.Add(detailRow);
        detailBand.Controls.Add(detailTable);

        var totalCaption = new XRLabel
        {
            BoundsF = new RectangleF(676f, 0f, 140f, 24f),
            Font = new DXFont("Arial", 9f, DXFontStyle.Bold),
            Text = "Total monto:",
            TextAlignment = TextAlignment.MiddleRight
        };

        var totalValue = new XRLabel
        {
            BoundsF = new RectangleF(820f, 0f, 140f, 24f),
            Borders = BorderSide.Top,
            BorderWidth = 1f,
            Font = new DXFont("Arial", 9f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleRight,
            TextFormatString = "{0:n2}"
        };
        totalValue.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "sumSum([Monto])"));
        totalValue.Summary = new XRSummary
        {
            Running = SummaryRunning.Report
        };

        reportFooter.Controls.AddRange([totalCaption, totalValue]);

        pageFooter.Controls.AddRange(
        [
            new XRPageInfo
            {
                BoundsF = new RectangleF(0f, 0f, 260f, 20f),
                Font = new DXFont("Arial", 8f),
                PageInfo = PageInfo.DateTime,
                TextAlignment = TextAlignment.MiddleLeft,
                TextFormatString = "Generado: {0:dd/MM/yyyy HH:mm}"
            },
            new XRPageInfo
            {
                BoundsF = new RectangleF(760f, 0f, 200f, 20f),
                Font = new DXFont("Arial", 8f),
                PageInfo = PageInfo.NumberOfTotal,
                TextAlignment = TextAlignment.MiddleRight,
                TextFormatString = "Página {0} de {1}"
            }
        ]);

        report.StyleSheet.AddRange(
        [
            new XRControlStyle
            {
                Name = "DetailOddStyle",
                BackColor = Color.FromArgb(248, 250, 252)
            }
        ]);

        report.Bands.AddRange([reportHeader, pageHeader, detailBand, reportFooter, pageFooter]);
        return report;
    }

    private XtraReport CreateBalanceComprobacionTemplate(string reportCode, string displayName, string? description, DatasetDefinition dataset)
    {
        var report = CreateBaseReport(reportCode, displayName);
        report.Landscape = true;
        report.PaperKind = DevExpress.Drawing.Printing.DXPaperKind.Letter;
        report.RequestParameters = dataset.Parameters.Any(x => x.Source == ReportesWebConstants.DatasetParameterValueSource.Report && x.Visible);

        foreach (var parameter in dataset.Parameters)
        {
            var reportParameter = CreateReportParameter(parameter);
            ApplyBalanceComprobacionTemplateDefaults(reportParameter);
            report.Parameters.Add(reportParameter);
        }

        var queryName = string.IsNullOrWhiteSpace(dataset.Code) ? "MainQuery" : dataset.Code.Replace('-', '_');
        var dataSource = CreateRelationalDataSource(dataset, queryName);
        report.ComponentStorage.AddRange([dataSource]);
        report.DataSource = dataSource;
        report.DataMember = queryName;

        var reportHeader = new ReportHeaderBand { HeightF = 94f };
        var pageHeader = new PageHeaderBand { HeightF = 56f };
        var groupHeader = new GroupHeaderBand { HeightF = 26f, RepeatEveryPage = true };
        var detailBand = new DetailBand { HeightF = 22f };
        var reportFooter = new ReportFooterBand { HeightF = 28f };
        var pageFooter = new PageFooterBand { HeightF = 24f };

        reportHeader.Controls.AddRange(
        [
            new XRLabel
            {
                BoundsF = new RectangleF(0f, 0f, 960f, 32f),
                Font = new DXFont("Arial", 18f, DXFontStyle.Bold),
                Text = displayName,
                TextAlignment = TextAlignment.MiddleLeft
            },
            new XRLabel
            {
                BoundsF = new RectangleF(0f, 38f, 960f, 24f),
                Font = new DXFont("Arial", 9.5f),
                Text = string.IsNullOrWhiteSpace(description)
                    ? "Balance de comprobacion base para reporteria financiera conforme al manual ERSAPS."
                    : description,
                TextAlignment = TextAlignment.MiddleLeft
            },
            new XRLabel
            {
                BoundsF = new RectangleF(0f, 66f, 960f, 24f),
                Font = new DXFont("Arial", 8.5f),
                ForeColor = Color.DimGray,
                Text = "El reporte muestra saldo anterior, debitos y creditos del periodo, y saldo actual por rubro, cuenta y subcuenta.",
                TextAlignment = TextAlignment.MiddleLeft
            }
        ]);

        pageHeader.Controls.Add(new XRLabel
        {
            BoundsF = new RectangleF(0f, 0f, 960f, 18f),
            Font = new DXFont("Arial", 8.5f),
            ForeColor = Color.DimGray,
            Text = "Formato base alineado con ERSAPS. Valores expresados sin decimales para salida regulatoria.",
            TextAlignment = TextAlignment.MiddleLeft
        });

        var headerTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 22f, 960f, 34f),
            BackColor = Color.FromArgb(232, 238, 245),
            Borders = BorderSide.All,
            BorderWidth = 1f,
            Font = new DXFont("Arial", 8.25f, DXFontStyle.Bold),
            ForeColor = Color.FromArgb(39, 54, 74),
            TextAlignment = TextAlignment.MiddleCenter
        };

        var headerRow = new XRTableRow();
        headerRow.Cells.AddRange(
        [
            CreateHeaderCell("Codigo", 1.20f),
            CreateHeaderCell("Cuenta", 2.80f),
            CreateHeaderCell("Saldo anterior\ndeudor", 1.10f),
            CreateHeaderCell("Saldo anterior\nacreedor", 1.10f),
            CreateHeaderCell("Debitos", 1.05f),
            CreateHeaderCell("Creditos", 1.05f),
            CreateHeaderCell("Saldo actual\ndeudor", 1.10f),
            CreateHeaderCell("Saldo actual\nacreedor", 1.10f)
        ]);
        headerTable.Rows.Add(headerRow);
        pageHeader.Controls.Add(headerTable);

        groupHeader.GroupFields.Add(new GroupField("rubro_orden"));
        groupHeader.GroupFields.Add(new GroupField("rubro_nombre"));

        var groupLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 0f, 960f, 24f),
            BackColor = Color.FromArgb(244, 247, 250),
            Borders = BorderSide.Top | BorderSide.Bottom,
            BorderWidth = 1f,
            Font = new DXFont("Arial", 10f, DXFontStyle.Bold),
            Padding = new PaddingInfo(6, 6, 0, 0),
            TextAlignment = TextAlignment.MiddleLeft
        };
        groupLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[rubro_nombre]"));
        groupHeader.Controls.Add(groupLabel);

        var detailTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 0f, 960f, 22f),
            Borders = BorderSide.Left | BorderSide.Right | BorderSide.Bottom,
            BorderWidth = 1f,
            Font = new DXFont("Arial", 8f),
            OddStyleName = "TrialBalanceOddStyle"
        };

        var detailRow = new XRTableRow();
        detailRow.Cells.AddRange(
        [
            CreateDetailCell("[cuenta_codigo]", 1.20f),
            CreateDetailCell("[cuenta_nombre_mostrar]", 2.80f),
            CreateDetailCell("[saldo_anterior_deudor]", 1.10f, TextAlignment.MiddleRight, "{0:n0}"),
            CreateDetailCell("[saldo_anterior_acreedor]", 1.10f, TextAlignment.MiddleRight, "{0:n0}"),
            CreateDetailCell("[debitos_periodo]", 1.05f, TextAlignment.MiddleRight, "{0:n0}"),
            CreateDetailCell("[creditos_periodo]", 1.05f, TextAlignment.MiddleRight, "{0:n0}"),
            CreateDetailCell("[saldo_actual_deudor]", 1.10f, TextAlignment.MiddleRight, "{0:n0}"),
            CreateDetailCell("[saldo_actual_acreedor]", 1.10f, TextAlignment.MiddleRight, "{0:n0}")
        ]);
        detailTable.Rows.Add(detailRow);
        detailBand.Controls.Add(detailTable);

        var footerTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 0f, 960f, 26f),
            Borders = BorderSide.Top,
            BorderWidth = 1f,
            Font = new DXFont("Arial", 8.5f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleRight
        };

        var footerRow = new XRTableRow();
        footerRow.Cells.AddRange(
        [
            new XRTableCell
            {
                Text = "Totales",
                Weight = 4.00f,
                Padding = new PaddingInfo(4, 4, 0, 0),
                TextAlignment = TextAlignment.MiddleLeft
            },
            CreateSummaryCell("[saldo_anterior_deudor]", 1.10f),
            CreateSummaryCell("[saldo_anterior_acreedor]", 1.10f),
            CreateSummaryCell("[debitos_periodo]", 1.05f),
            CreateSummaryCell("[creditos_periodo]", 1.05f),
            CreateSummaryCell("[saldo_actual_deudor]", 1.10f),
            CreateSummaryCell("[saldo_actual_acreedor]", 1.10f)
        ]);
        footerTable.Rows.Add(footerRow);
        reportFooter.Controls.Add(footerTable);

        pageFooter.Controls.AddRange(
        [
            new XRPageInfo
            {
                BoundsF = new RectangleF(0f, 0f, 260f, 20f),
                Font = new DXFont("Arial", 8f),
                PageInfo = PageInfo.DateTime,
                TextAlignment = TextAlignment.MiddleLeft,
                TextFormatString = "Generado: {0:dd/MM/yyyy HH:mm}"
            },
            new XRPageInfo
            {
                BoundsF = new RectangleF(760f, 0f, 200f, 20f),
                Font = new DXFont("Arial", 8f),
                PageInfo = PageInfo.NumberOfTotal,
                TextAlignment = TextAlignment.MiddleRight,
                TextFormatString = "Pagina {0} de {1}"
            }
        ]);

        report.StyleSheet.AddRange(
        [
            new XRControlStyle
            {
                Name = "TrialBalanceOddStyle",
                BackColor = Color.FromArgb(248, 250, 252)
            }
        ]);

        report.Bands.AddRange([reportHeader, pageHeader, groupHeader, detailBand, reportFooter, pageFooter]);
        return report;
    }

    private XtraReport CreateEstadoSituacionFinancieraTemplate(string reportCode, string displayName, string? description, DatasetDefinition dataset)
    {
        var report = CreateBaseReport(reportCode, displayName);
        report.PaperKind = DevExpress.Drawing.Printing.DXPaperKind.Letter;
        report.RequestParameters = dataset.Parameters.Any(x => x.Source == ReportesWebConstants.DatasetParameterValueSource.Report && x.Visible);

        foreach (var parameter in dataset.Parameters)
        {
            var reportParameter = CreateReportParameter(parameter);
            ApplyEstadoSituacionFinancieraTemplateDefaults(reportParameter);
            report.Parameters.Add(reportParameter);
        }

        var queryName = string.IsNullOrWhiteSpace(dataset.Code) ? "MainQuery" : dataset.Code.Replace('-', '_');
        var dataSource = CreateRelationalDataSource(dataset, queryName);
        report.ComponentStorage.AddRange([dataSource]);
        report.DataSource = dataSource;
        report.DataMember = queryName;

        var reportHeader = new ReportHeaderBand { HeightF = 82f };
        var pageHeader = new PageHeaderBand { HeightF = 44f };
        var groupHeader = new GroupHeaderBand { HeightF = 24f, RepeatEveryPage = true };
        var detailBand = new DetailBand { HeightF = 22f };
        var groupFooter = new GroupFooterBand { HeightF = 24f };
        var pageFooter = new PageFooterBand { HeightF = 24f };

        reportHeader.Controls.AddRange(
        [
            new XRLabel
            {
                BoundsF = new RectangleF(0f, 0f, 650f, 30f),
                Font = new DXFont("Arial", 18f, DXFontStyle.Bold),
                Text = displayName,
                TextAlignment = TextAlignment.MiddleLeft
            },
            new XRLabel
            {
                BoundsF = new RectangleF(0f, 36f, 650f, 22f),
                Font = new DXFont("Arial", 9.5f),
                Text = string.IsNullOrWhiteSpace(description)
                    ? "Estado de situacion financiera construido desde la configuracion contable por empresa."
                    : description,
                TextAlignment = TextAlignment.MiddleLeft
            },
            new XRLabel
            {
                BoundsF = new RectangleF(0f, 60f, 650f, 18f),
                Font = new DXFont("Arial", 8.5f),
                ForeColor = Color.DimGray,
                Text = "La estructura de lineas se resuelve desde con_configuracion_balance y los saldos desde el balance de comprobacion.",
                TextAlignment = TextAlignment.MiddleLeft
            }
        ]);

        var headerTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 0f, 650f, 28f),
            BackColor = Color.FromArgb(232, 238, 245),
            Borders = BorderSide.All,
            BorderWidth = 1f,
            Font = new DXFont("Arial", 8.5f, DXFontStyle.Bold),
            ForeColor = Color.FromArgb(39, 54, 74),
            TextAlignment = TextAlignment.MiddleCenter
        };
        var headerRow = new XRTableRow();
        headerRow.Cells.AddRange(
        [
            CreateHeaderCell("Codigo", 1.20f),
            CreateHeaderCell("Descripcion", 4.10f),
            CreateHeaderCell("Monto", 1.45f)
        ]);
        headerTable.Rows.Add(headerRow);
        pageHeader.Controls.Add(headerTable);

        groupHeader.GroupFields.Add(new GroupField("seccion_orden"));
        groupHeader.GroupFields.Add(new GroupField("seccion_nombre"));

        var sectionLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 0f, 650f, 24f),
            BackColor = Color.FromArgb(244, 247, 250),
            Borders = BorderSide.Top | BorderSide.Bottom,
            BorderWidth = 1f,
            Font = new DXFont("Arial", 10f, DXFontStyle.Bold),
            Padding = new PaddingInfo(6, 6, 0, 0),
            TextAlignment = TextAlignment.MiddleLeft
        };
        sectionLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[seccion_nombre]"));
        groupHeader.Controls.Add(sectionLabel);

        var detailTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 0f, 650f, 22f),
            Borders = BorderSide.Left | BorderSide.Right | BorderSide.Bottom,
            BorderWidth = 1f,
            Font = new DXFont("Arial", 8f),
            OddStyleName = "FinancialStateOddStyle"
        };
        var detailRow = new XRTableRow();
        detailRow.Cells.AddRange(
        [
            CreateDetailCell("[codigo_cuenta]", 1.20f),
            CreateDetailCell("[descripcion_mostrar]", 4.10f),
            CreateDetailCell("[monto]", 1.45f, TextAlignment.MiddleRight, "{0:n2}")
        ]);
        detailTable.Rows.Add(detailRow);
        detailBand.Controls.Add(detailTable);

        var groupTotalLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 2f, 510f, 20f),
            Borders = BorderSide.Top,
            BorderWidth = 1f,
            Font = new DXFont("Arial", 8.5f, DXFontStyle.Bold),
            Padding = new PaddingInfo(4, 4, 0, 0),
            TextAlignment = TextAlignment.MiddleLeft
        };
        groupTotalLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "Concat('Total ', [seccion_nombre])"));

        var groupTotalValue = new XRLabel
        {
            BoundsF = new RectangleF(510f, 2f, 140f, 20f),
            Borders = BorderSide.Top,
            BorderWidth = 1f,
            Font = new DXFont("Arial", 8.5f, DXFontStyle.Bold),
            Padding = new PaddingInfo(4, 4, 0, 0),
            TextAlignment = TextAlignment.MiddleRight,
            TextFormatString = "{0:n2}"
        };
        groupTotalValue.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "sumSum([monto])"));
        groupTotalValue.Summary = new XRSummary
        {
            Running = SummaryRunning.Group
        };
        groupFooter.Controls.AddRange([groupTotalLabel, groupTotalValue]);

        pageFooter.Controls.AddRange(
        [
            new XRPageInfo
            {
                BoundsF = new RectangleF(0f, 0f, 260f, 20f),
                Font = new DXFont("Arial", 8f),
                PageInfo = PageInfo.DateTime,
                TextAlignment = TextAlignment.MiddleLeft,
                TextFormatString = "Generado: {0:dd/MM/yyyy HH:mm}"
            },
            new XRPageInfo
            {
                BoundsF = new RectangleF(450f, 0f, 200f, 20f),
                Font = new DXFont("Arial", 8f),
                PageInfo = PageInfo.NumberOfTotal,
                TextAlignment = TextAlignment.MiddleRight,
                TextFormatString = "Pagina {0} de {1}"
            }
        ]);

        report.StyleSheet.AddRange(
        [
            new XRControlStyle
            {
                Name = "FinancialStateOddStyle",
                BackColor = Color.FromArgb(248, 250, 252)
            }
        ]);

        report.Bands.AddRange([reportHeader, pageHeader, groupHeader, detailBand, groupFooter, pageFooter]);
        return report;
    }

    private XtraReport CreateEstadoResultadosTemplate(string reportCode, string displayName, string? description, DatasetDefinition dataset)
    {
        var report = CreateBaseReport(reportCode, displayName);
        report.PaperKind = DevExpress.Drawing.Printing.DXPaperKind.Letter;
        report.RequestParameters = dataset.Parameters.Any(x => x.Source == ReportesWebConstants.DatasetParameterValueSource.Report && x.Visible);

        foreach (var parameter in dataset.Parameters)
        {
            var reportParameter = CreateReportParameter(parameter);
            ApplyEstadoResultadosTemplateDefaults(reportParameter);
            report.Parameters.Add(reportParameter);
        }

        var queryName = string.IsNullOrWhiteSpace(dataset.Code) ? "MainQuery" : dataset.Code.Replace('-', '_');
        var dataSource = CreateRelationalDataSource(dataset, queryName);
        report.ComponentStorage.AddRange([dataSource]);
        report.DataSource = dataSource;
        report.DataMember = queryName;

        var reportHeader = new ReportHeaderBand { HeightF = 82f };
        var pageHeader = new PageHeaderBand { HeightF = 44f };
        var groupHeader = new GroupHeaderBand { HeightF = 24f, RepeatEveryPage = true };
        var detailBand = new DetailBand { HeightF = 22f };
        var groupFooter = new GroupFooterBand { HeightF = 24f };
        var reportFooter = new ReportFooterBand { HeightF = 24f };
        var pageFooter = new PageFooterBand { HeightF = 24f };

        reportHeader.Controls.AddRange(
        [
            new XRLabel
            {
                BoundsF = new RectangleF(0f, 0f, 650f, 30f),
                Font = new DXFont("Arial", 18f, DXFontStyle.Bold),
                Text = displayName,
                TextAlignment = TextAlignment.MiddleLeft
            },
            new XRLabel
            {
                BoundsF = new RectangleF(0f, 36f, 650f, 22f),
                Font = new DXFont("Arial", 9.5f),
                Text = string.IsNullOrWhiteSpace(description)
                    ? "Estado de resultados construido desde la configuracion contable por empresa."
                    : description,
                TextAlignment = TextAlignment.MiddleLeft
            },
            new XRLabel
            {
                BoundsF = new RectangleF(0f, 60f, 650f, 18f),
                Font = new DXFont("Arial", 8.5f),
                ForeColor = Color.DimGray,
                Text = "La estructura de lineas se resuelve desde con_configuracion_linea_resultado y los montos desde los movimientos del periodo.",
                TextAlignment = TextAlignment.MiddleLeft
            }
        ]);

        var headerTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 0f, 650f, 28f),
            BackColor = Color.FromArgb(232, 238, 245),
            Borders = BorderSide.All,
            BorderWidth = 1f,
            Font = new DXFont("Arial", 8.5f, DXFontStyle.Bold),
            ForeColor = Color.FromArgb(39, 54, 74),
            TextAlignment = TextAlignment.MiddleCenter
        };
        var headerRow = new XRTableRow();
        headerRow.Cells.AddRange(
        [
            CreateHeaderCell("Codigo", 1.20f),
            CreateHeaderCell("Descripcion", 4.10f),
            CreateHeaderCell("Monto", 1.45f)
        ]);
        headerTable.Rows.Add(headerRow);
        pageHeader.Controls.Add(headerTable);

        groupHeader.GroupFields.Add(new GroupField("seccion_orden"));
        groupHeader.GroupFields.Add(new GroupField("seccion_nombre"));

        var sectionLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 0f, 650f, 24f),
            BackColor = Color.FromArgb(244, 247, 250),
            Borders = BorderSide.Top | BorderSide.Bottom,
            BorderWidth = 1f,
            Font = new DXFont("Arial", 10f, DXFontStyle.Bold),
            Padding = new PaddingInfo(6, 6, 0, 0),
            TextAlignment = TextAlignment.MiddleLeft
        };
        sectionLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[seccion_nombre]"));
        groupHeader.Controls.Add(sectionLabel);

        var detailTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 0f, 650f, 22f),
            Borders = BorderSide.Left | BorderSide.Right | BorderSide.Bottom,
            BorderWidth = 1f,
            Font = new DXFont("Arial", 8f),
            OddStyleName = "FinancialResultsOddStyle"
        };
        var detailRow = new XRTableRow();
        detailRow.Cells.AddRange(
        [
            CreateDetailCell("[codigo_cuenta]", 1.20f),
            CreateDetailCell("[descripcion_mostrar]", 4.10f),
            CreateDetailCell("[monto]", 1.45f, TextAlignment.MiddleRight, "{0:n2}")
        ]);
        detailTable.Rows.Add(detailRow);
        detailBand.Controls.Add(detailTable);

        var groupTotalLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 2f, 510f, 20f),
            Borders = BorderSide.Top,
            BorderWidth = 1f,
            Font = new DXFont("Arial", 8.5f, DXFontStyle.Bold),
            Padding = new PaddingInfo(4, 4, 0, 0),
            TextAlignment = TextAlignment.MiddleLeft
        };
        groupTotalLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "Concat('Total ', [seccion_nombre])"));

        var groupTotalValue = new XRLabel
        {
            BoundsF = new RectangleF(510f, 2f, 140f, 20f),
            Borders = BorderSide.Top,
            BorderWidth = 1f,
            Font = new DXFont("Arial", 8.5f, DXFontStyle.Bold),
            Padding = new PaddingInfo(4, 4, 0, 0),
            TextAlignment = TextAlignment.MiddleRight,
            TextFormatString = "{0:n2}"
        };
        groupTotalValue.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "sumSum([monto])"));
        groupTotalValue.Summary = new XRSummary
        {
            Running = SummaryRunning.Group
        };
        groupFooter.Controls.AddRange([groupTotalLabel, groupTotalValue]);

        var netResultLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 2f, 510f, 20f),
            Borders = BorderSide.Top,
            BorderWidth = 1f,
            Font = new DXFont("Arial", 9f, DXFontStyle.Bold),
            Padding = new PaddingInfo(4, 4, 0, 0),
            Text = "Resultado neto",
            TextAlignment = TextAlignment.MiddleLeft
        };

        var netResultValue = new XRLabel
        {
            BoundsF = new RectangleF(510f, 2f, 140f, 20f),
            Borders = BorderSide.Top,
            BorderWidth = 1f,
            Font = new DXFont("Arial", 9f, DXFontStyle.Bold),
            Padding = new PaddingInfo(4, 4, 0, 0),
            TextAlignment = TextAlignment.MiddleRight,
            TextFormatString = "{0:n2}"
        };
        netResultValue.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "sumSum([monto_neto])"));
        netResultValue.Summary = new XRSummary
        {
            Running = SummaryRunning.Report
        };
        reportFooter.Controls.AddRange([netResultLabel, netResultValue]);

        pageFooter.Controls.AddRange(
        [
            new XRPageInfo
            {
                BoundsF = new RectangleF(0f, 0f, 260f, 20f),
                Font = new DXFont("Arial", 8f),
                PageInfo = PageInfo.DateTime,
                TextAlignment = TextAlignment.MiddleLeft,
                TextFormatString = "Generado: {0:dd/MM/yyyy HH:mm}"
            },
            new XRPageInfo
            {
                BoundsF = new RectangleF(450f, 0f, 200f, 20f),
                Font = new DXFont("Arial", 8f),
                PageInfo = PageInfo.NumberOfTotal,
                TextAlignment = TextAlignment.MiddleRight,
                TextFormatString = "Pagina {0} de {1}"
            }
        ]);

        report.StyleSheet.AddRange(
        [
            new XRControlStyle
            {
                Name = "FinancialResultsOddStyle",
                BackColor = Color.FromArgb(248, 250, 252)
            }
        ]);

        report.Bands.AddRange([reportHeader, pageHeader, groupHeader, detailBand, groupFooter, reportFooter, pageFooter]);
        return report;
    }

    private static void ApplyBancosTemplateDefaults(Parameter parameter)
    {
        parameter.Value = parameter.Name switch
        {
            "FechaDesde" => FirstDayOfCurrentMonth(),
            "FechaHasta" => DateTime.Today,
            "IncluirAnuladas" => false,
            _ => parameter.Value
        };
    }

    private static void ApplyBalanceComprobacionTemplateDefaults(Parameter parameter)
    {
        parameter.Value = parameter.Name switch
        {
            "FechaDesde" => FirstDayOfCurrentMonth(),
            "FechaHasta" => DateTime.Today,
            "IncluirSinMovimiento" => false,
            _ => parameter.Value
        };
    }

    private static void ApplyEstadoSituacionFinancieraTemplateDefaults(Parameter parameter)
    {
        parameter.Value = parameter.Name switch
        {
            "FechaCorte" => DateTime.Today,
            _ => parameter.Value
        };
    }

    private static void ApplyEstadoResultadosTemplateDefaults(Parameter parameter)
    {
        parameter.Value = parameter.Name switch
        {
            "FechaDesde" => FirstDayOfCurrentMonth(),
            "FechaHasta" => DateTime.Today,
            _ => parameter.Value
        };
    }

    private static XtraReport CreateBaseReport(string reportCode, string displayName)
    {
        var report = new XtraReport
        {
            Name = reportCode,
            DisplayName = displayName,
            Margins = new DXMargins(35, 35, 28, 28)
        };

        report.Bands.Clear();
        report.Bands.AddRange([new TopMarginBand(), new BottomMarginBand()]);
        return report;
    }

    private static Parameter CreateReportParameter(
        string name,
        string description,
        Type type,
        bool allowNull,
        bool visible,
        object? value)
        => new()
        {
            Name = name,
            Description = description,
            Type = type,
            AllowNull = allowNull,
            Visible = visible,
            Value = value
        };

    private static XRTableCell CreateHeaderCell(string text, float weight)
        => new()
        {
            Text = text,
            Weight = weight,
            Multiline = true
        };

    private static XRTableCell CreateDetailCell(
        string expression,
        float weight,
        TextAlignment alignment = TextAlignment.MiddleLeft,
        string? formatString = null)
    {
        var cell = new XRTableCell
        {
            Weight = weight,
            TextAlignment = alignment,
            Padding = new PaddingInfo(4, 4, 0, 0)
        };

        cell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", expression));
        if (!string.IsNullOrWhiteSpace(formatString))
        {
            cell.TextFormatString = formatString;
        }

        return cell;
    }

    private static XRTableCell CreateSummaryCell(string expression, float weight)
    {
        var cell = new XRTableCell
        {
            Weight = weight,
            TextAlignment = TextAlignment.MiddleRight,
            Padding = new PaddingInfo(4, 4, 0, 0),
            TextFormatString = "{0:n0}"
        };

        cell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", $"sumSum({expression})"));
        cell.Summary = new XRSummary
        {
            Running = SummaryRunning.Report
        };

        return cell;
    }

    private static DateTime FirstDayOfCurrentMonth()
    {
        var today = DateTime.Today;
        return new DateTime(today.Year, today.Month, 1);
    }

    private sealed record DatasetDefinition(
        string Code,
        string Name,
        string SourceType,
        string? OriginKey,
        string? SqlText,
        string? ConnectionName,
        IReadOnlyList<DatasetParameterDefinition> Parameters);

    private sealed record DatasetParameterDefinition(
        string Name,
        string? QueryName,
        string Label,
        string DataType,
        string Source,
        string? DefaultValue,
        bool Visible,
        bool AllowNull,
        bool Required,
        int Order);
}


