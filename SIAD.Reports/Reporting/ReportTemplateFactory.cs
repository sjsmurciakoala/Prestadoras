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
using DevExpress.XtraReports.UI.CrossTab;
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
            ReportesWebConstants.CodigoReporteEstadoFlujoEfectivo => CreateEstadoFlujoEfectivoTemplate(reportCode, displayName, description, BuildDefaultEstadoFlujoEfectivoDataset()),
            ReportesWebConstants.CodigoReporteEstadoCambiosPatrimonio => CreateEstadoCambiosPatrimonioTemplate(reportCode, displayName, description, BuildDefaultEstadoCambiosPatrimonioDataset()),
            ReportesWebConstants.CodigoReporteTransaccionesPeriodo => CreateTransaccionesPeriodoTemplate(reportCode, displayName, description, BuildDefaultTransaccionesPeriodoDataset()),
            ReportesWebConstants.CodigoReporteSaldosAguaPotableCiclo => CreateSaldosAguaPotableCicloTemplate(reportCode, displayName, description, BuildDefaultSaldosAguaPotableCicloDataset()),
            ReportesWebConstants.CodigoReporteSaldosAlcantarilladoSanitarioCiclo => CreateSaldosAguaPotableCicloTemplate(reportCode, displayName, description, BuildDefaultSaldosAlcantarilladoSanitarioCicloDataset()),
            ReportesWebConstants.CodigoReporteSumarialTarifarioMedicion => CreateSumarialTarifarioMedicionTemplate(reportCode, displayName, description, BuildDefaultSumarialTarifarioMedicionDataset()),
            ReportesWebConstants.CodigoReporteSumarialTarifasNoMedido => CreateSumarialTarifasNoMedidoTemplate(reportCode, displayName, description, BuildDefaultSumarialTarifasNoMedidoDataset()),
            ReportesWebConstants.CodigoReporteSaldoClientesCategoriaCobranza => CreateSaldoClientesCategoriaCobranzaTemplate(reportCode, displayName, description, BuildDefaultSaldoClientesCategoriaCobranzaDataset()),
            ReportesWebConstants.CodigoReporteSaldoClientesCategoria => CreateRelationalBackedTemplate(reportCode, displayName, description, BuildDefaultSaldoClientesCategoriaDataset()),
            ReportesWebConstants.CodigoReporteDesgloseFacturacion => CreateRelationalBackedTemplate(reportCode, displayName, description, BuildDefaultDesgloseFacturacionDataset()),
            ReportesWebConstants.CodigoReporteMovimientoPeriodo => CreateRelationalBackedTemplate(reportCode, displayName, description, BuildDefaultMovimientoPeriodoDataset()),
            ReportesWebConstants.CodigoReporteAuxiliarLectura => CreateRelationalBackedTemplate(reportCode, displayName, description, BuildDefaultAuxiliarLecturaDataset()),
            ReportesWebConstants.CodigoReporteHistorialRecibosEmitidos => CreateRelationalBackedTemplate(reportCode, displayName, description, BuildDefaultHistorialRecibosEmitidosDataset()),
            ReportesWebConstants.CodigoReporteSaldoClientesAntiguedad => CreateRelationalBackedTemplate(reportCode, displayName, description, BuildDefaultSaldoClientesAntiguedadDataset()),
            ReportesWebConstants.CodigoReporteAnalisisAntiguedadCobros => CreateRelationalBackedTemplate(reportCode, displayName, description, BuildDefaultAnalisisAntiguedadCobrosDataset()),
            ReportesWebConstants.CodigoReporteRecaudacion => CreateRelationalBackedTemplate(reportCode, displayName, description, BuildDefaultRecaudacionDataset()),
            ReportesWebConstants.CodigoReporteSaldoClientesCategoriaDetalle => CreateRelationalBackedTemplate(reportCode, displayName, description, BuildDefaultSaldoClientesCategoriaDetalleDataset()),
            ReportesWebConstants.CodigoReporteFacturaTicket => CreateFacturaTicketTemplate(reportCode, displayName, description, BuildDefaultFacturaTicketDataset()),
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

        if (IsPresupuestoComparativoTemplate(reportCode, dataset) &&
            dataset.SourceType is ReportesWebConstants.DatasetSourceType.StoredProcedure
                or ReportesWebConstants.DatasetSourceType.View
                or ReportesWebConstants.DatasetSourceType.Sql)
        {
            return CreatePresupuestoComparativoTemplate(reportCode, displayName, description, dataset);
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

        if (IsEstadoFlujoEfectivoTemplate(reportCode, dataset) &&
            dataset.SourceType is ReportesWebConstants.DatasetSourceType.StoredProcedure
                or ReportesWebConstants.DatasetSourceType.View
                or ReportesWebConstants.DatasetSourceType.Sql)
        {
            return CreateEstadoFlujoEfectivoTemplate(reportCode, displayName, description, dataset);
        }

        if (IsEstadoCambiosPatrimonioTemplate(reportCode, dataset) &&
            dataset.SourceType is ReportesWebConstants.DatasetSourceType.StoredProcedure
                or ReportesWebConstants.DatasetSourceType.View
                or ReportesWebConstants.DatasetSourceType.Sql)
        {
            return CreateEstadoCambiosPatrimonioTemplate(reportCode, displayName, description, dataset);
        }

        if (IsTransaccionesPeriodoTemplate(reportCode, dataset) &&
            dataset.SourceType is ReportesWebConstants.DatasetSourceType.StoredProcedure
                or ReportesWebConstants.DatasetSourceType.View
                or ReportesWebConstants.DatasetSourceType.Sql)
        {
            return CreateTransaccionesPeriodoTemplate(reportCode, displayName, description, dataset);
        }

        if (IsSaldosAguaPotableCicloTemplate(reportCode, dataset) &&
            dataset.SourceType is ReportesWebConstants.DatasetSourceType.StoredProcedure
                or ReportesWebConstants.DatasetSourceType.View
                or ReportesWebConstants.DatasetSourceType.Sql)
        {
            return CreateSaldosAguaPotableCicloTemplate(reportCode, displayName, description, dataset);
        }

        if (IsSaldosAlcantarilladoSanitarioCicloTemplate(reportCode, dataset) &&
            dataset.SourceType is ReportesWebConstants.DatasetSourceType.StoredProcedure
                or ReportesWebConstants.DatasetSourceType.View
                or ReportesWebConstants.DatasetSourceType.Sql)
        {
            return CreateSaldosAguaPotableCicloTemplate(reportCode, displayName, description, dataset);
        }

        if (IsSumarialTarifarioMedicionTemplate(reportCode, dataset) &&
            dataset.SourceType is ReportesWebConstants.DatasetSourceType.StoredProcedure
                or ReportesWebConstants.DatasetSourceType.View
                or ReportesWebConstants.DatasetSourceType.Sql)
        {
            return CreateSumarialTarifarioMedicionTemplate(reportCode, displayName, description, dataset);
        }

        if (IsSumarialTarifasNoMedidoTemplate(reportCode, dataset) &&
            dataset.SourceType is ReportesWebConstants.DatasetSourceType.StoredProcedure
                or ReportesWebConstants.DatasetSourceType.View
                or ReportesWebConstants.DatasetSourceType.Sql)
        {
            return CreateSumarialTarifasNoMedidoTemplate(reportCode, displayName, description, dataset);
        }

        if (IsSaldoClientesCategoriaCobranzaTemplate(reportCode, dataset) &&
            dataset.SourceType is ReportesWebConstants.DatasetSourceType.StoredProcedure
                or ReportesWebConstants.DatasetSourceType.View
                or ReportesWebConstants.DatasetSourceType.Sql)
        {
            return CreateSaldoClientesCategoriaCobranzaTemplate(reportCode, displayName, description, dataset);
        }

        if (IsFacturaTicketTemplate(reportCode, dataset) &&
            dataset.SourceType is ReportesWebConstants.DatasetSourceType.StoredProcedure
                or ReportesWebConstants.DatasetSourceType.View
                or ReportesWebConstants.DatasetSourceType.Sql)
        {
            return CreateFacturaTicketTemplate(reportCode, displayName, description, dataset);
        }

        return dataset.SourceType switch
        {
            ReportesWebConstants.DatasetSourceType.StoredProcedure or ReportesWebConstants.DatasetSourceType.View or ReportesWebConstants.DatasetSourceType.Sql
                => CreateRelationalBackedTemplate(reportCode, displayName, description, dataset),
            _ => CreateBlankTemplate(reportCode, displayName, BuildUnsupportedDatasetDescription(description, dataset))
        };
    }

    private XtraReport CreateBlankTemplate(string reportCode, string displayName, string? description)
    {
        var report = CreateBaseReport(reportCode, displayName);

        var header = ReportCompanyHeaderParameters.CreateHeaderBand(
            650f,
            displayName,
            string.IsNullOrWhiteSpace(description)
                ? "Plantilla base de reporte web. Disene el detalle en el editor web y publique la version aprobada."
                : description);

        var detail = new DetailBand { HeightF = 80f };

        var subtitle = new XRLabel
        {
            BoundsF = new RectangleF(0f, 12f, 650f, 48f),
            Font = new DXFont("Arial", 10f),
            Multiline = true,
            Text = string.IsNullOrWhiteSpace(description)
                ? "Plantilla base de reporte web. Diseñe el detalle en el editor web y publique la versión aprobada."
                : description,
            TextAlignment = TextAlignment.MiddleCenter
        };

        subtitle.Text = "Use el disenador para agregar el cuerpo del reporte. Los parametros ocultos HeaderCompanyName, HeaderCompanyInfoLine y HeaderCompanyAddress quedan disponibles para encabezados dinamicos por empresa.";

        detail.Controls.Add(subtitle);
        report.Bands.AddRange([header, detail]);
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

        ReportCompanyHeaderParameters.PrependTo(
            header,
            650f,
            displayName,
            "Encabezado dinamico resuelto desde la empresa actual.");

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
                ReportesWebConstants.CodigoDatasetEstadoFlujoEfectivo => BuildDefaultEstadoFlujoEfectivoDataset(),
                ReportesWebConstants.CodigoDatasetEstadoCambiosPatrimonio => BuildDefaultEstadoCambiosPatrimonioDataset(),
                ReportesWebConstants.CodigoDatasetTransaccionesPeriodo => BuildDefaultTransaccionesPeriodoDataset(),
                ReportesWebConstants.CodigoDatasetSaldoClientesCategoria => BuildDefaultSaldoClientesCategoriaDataset(),
                ReportesWebConstants.CodigoDatasetDesgloseFacturacion => BuildDefaultDesgloseFacturacionDataset(),
                ReportesWebConstants.CodigoDatasetMovimientoPeriodo => BuildDefaultMovimientoPeriodoDataset(),
                ReportesWebConstants.CodigoDatasetAuxiliarLectura => BuildDefaultAuxiliarLecturaDataset(),
                ReportesWebConstants.CodigoDatasetHistorialRecibosEmitidos => BuildDefaultHistorialRecibosEmitidosDataset(),
                ReportesWebConstants.CodigoDatasetSaldoClientesAntiguedad => BuildDefaultSaldoClientesAntiguedadDataset(),
                ReportesWebConstants.CodigoDatasetAnalisisAntiguedadCobros => BuildDefaultAnalisisAntiguedadCobrosDataset(),
                ReportesWebConstants.CodigoDatasetSaldoClientesCiclo => BuildDefaultSaldoClientesCicloDataset(),
                ReportesWebConstants.CodigoDatasetSaldoClientesCategoriaCobranza => BuildDefaultSaldoClientesCategoriaCobranzaDataset(),
                ReportesWebConstants.CodigoDatasetRecaudacion => BuildDefaultRecaudacionDataset(),
                ReportesWebConstants.CodigoDatasetSaldoClientesCategoriaDetalle => BuildDefaultSaldoClientesCategoriaDetalleDataset(),
                ReportesWebConstants.CodigoDatasetSaldosAguaPotableCiclo => BuildDefaultSaldosAguaPotableCicloDataset(),
                ReportesWebConstants.CodigoDatasetSaldosAlcantarilladoSanitarioCiclo => BuildDefaultSaldosAlcantarilladoSanitarioCicloDataset(),
                ReportesWebConstants.CodigoDatasetSumarialTarifarioMedicion => BuildDefaultSumarialTarifarioMedicionDataset(),
                ReportesWebConstants.CodigoDatasetSumarialTarifasNoMedido => BuildDefaultSumarialTarifasNoMedidoDataset(),
                ReportesWebConstants.CodigoDatasetFacturaTicket => BuildDefaultFacturaTicketDataset(),
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

    private static DatasetDefinition BuildDefaultEstadoFlujoEfectivoDataset()
        => new(
            ReportesWebConstants.CodigoDatasetEstadoFlujoEfectivo,
            "Dataset estado de flujos de efectivo",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetEstadoFlujoEfectivo,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            BuildDefaultEstadoFlujoEfectivoDatasetParameters());

    private static IReadOnlyList<DatasetParameterDefinition> BuildDefaultEstadoFlujoEfectivoDatasetParameters()
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

    private static DatasetDefinition BuildDefaultEstadoCambiosPatrimonioDataset()
        => new(
            ReportesWebConstants.CodigoDatasetEstadoCambiosPatrimonio,
            "Dataset estado de cambios en el patrimonio",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetEstadoCambiosPatrimonio,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            BuildDefaultEstadoCambiosPatrimonioDatasetParameters());

    private static IReadOnlyList<DatasetParameterDefinition> BuildDefaultEstadoCambiosPatrimonioDatasetParameters()
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

    private static DatasetDefinition BuildDefaultTransaccionesPeriodoDataset()
        => new(
            ReportesWebConstants.CodigoDatasetTransaccionesPeriodo,
            "Dataset transacciones por periodo",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetTransaccionesPeriodo,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            BuildDefaultTransaccionesPeriodoDatasetParameters());

    private static IReadOnlyList<DatasetParameterDefinition> BuildDefaultTransaccionesPeriodoDatasetParameters()
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

    private static DatasetDefinition BuildDefaultSaldoClientesCategoriaDataset()
        => new(
            ReportesWebConstants.CodigoDatasetSaldoClientesCategoria,
            "Dataset saldo de clientes por categoria",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetSaldoClientesCategoria,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            BuildDefaultSaldoClientesCategoriaDatasetParameters());

    private static IReadOnlyList<DatasetParameterDefinition> BuildDefaultSaldoClientesCategoriaDatasetParameters()
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
                10),
            new(
                "CategoriaServicioId",
                "p_categoria_servicio_id",
                "Categoria",
                ReportesWebConstants.DatasetParameterDataType.Int64,
                ReportesWebConstants.DatasetParameterValueSource.Report,
                "0",
                true,
                false,
                false,
                20),
            new(
                "EstadoCliente",
                "p_estado_cliente",
                "Estado del cliente",
                ReportesWebConstants.DatasetParameterDataType.Int64,
                ReportesWebConstants.DatasetParameterValueSource.Report,
                "0",
                true,
                false,
                false,
                30)
        ];

    private static DatasetDefinition BuildDefaultDesgloseFacturacionDataset()
        => new(
            ReportesWebConstants.CodigoDatasetDesgloseFacturacion,
            "Dataset desglose de facturacion",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetDesgloseFacturacion,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            BuildDefaultDesgloseFacturacionDatasetParameters());

    private static IReadOnlyList<DatasetParameterDefinition> BuildDefaultDesgloseFacturacionDatasetParameters()
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

    private static DatasetDefinition BuildDefaultMovimientoPeriodoDataset()
        => new(
            ReportesWebConstants.CodigoDatasetMovimientoPeriodo,
            "Dataset movimiento por periodo",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetMovimientoPeriodo,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            BuildDefaultMovimientoPeriodoDatasetParameters());

    private static IReadOnlyList<DatasetParameterDefinition> BuildDefaultMovimientoPeriodoDatasetParameters()
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

    private static DatasetDefinition BuildDefaultAuxiliarLecturaDataset()
        => new(
            ReportesWebConstants.CodigoDatasetAuxiliarLectura,
            "Dataset auxiliar de lectura",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetAuxiliarLectura,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            BuildDefaultAuxiliarLecturaDatasetParameters());

    private static IReadOnlyList<DatasetParameterDefinition> BuildDefaultAuxiliarLecturaDatasetParameters()
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
                "Anio",
                "p_anio",
                "Año",
                ReportesWebConstants.DatasetParameterDataType.Int64,
                ReportesWebConstants.DatasetParameterValueSource.Report,
                null,
                true,
                false,
                true,
                10),
            new(
                "Mes",
                "p_mes",
                "Mes",
                ReportesWebConstants.DatasetParameterDataType.Int64,
                ReportesWebConstants.DatasetParameterValueSource.Report,
                null,
                true,
                false,
                true,
                20),
            new(
                "CicloId",
                "p_ciclo_id",
                "Ciclo",
                ReportesWebConstants.DatasetParameterDataType.Int64,
                ReportesWebConstants.DatasetParameterValueSource.Report,
                "0",
                true,
                false,
                false,
                30),
            new(
                "SoloPendientes",
                "p_solo_pendientes",
                "Solo pendientes",
                ReportesWebConstants.DatasetParameterDataType.Boolean,
                ReportesWebConstants.DatasetParameterValueSource.Report,
                "false",
                true,
                false,
                false,
                40)
        ];

    private static DatasetDefinition BuildDefaultHistorialRecibosEmitidosDataset()
        => new(
            ReportesWebConstants.CodigoDatasetHistorialRecibosEmitidos,
            "Dataset historial de recibos emitidos",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetHistorialRecibosEmitidos,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            BuildDefaultHistorialRecibosEmitidosDatasetParameters());

    private static IReadOnlyList<DatasetParameterDefinition> BuildDefaultHistorialRecibosEmitidosDatasetParameters()
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
                "Usuario",
                "p_usuario",
                "Usuario",
                ReportesWebConstants.DatasetParameterDataType.Text,
                ReportesWebConstants.DatasetParameterValueSource.Report,
                null,
                true,
                true,
                false,
                30)
        ];

    private static DatasetDefinition BuildDefaultSaldoClientesAntiguedadDataset()
        => new(
            ReportesWebConstants.CodigoDatasetSaldoClientesAntiguedad,
            "Dataset saldo de clientes segun antigüedad",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetSaldoClientesAntiguedad,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            BuildDefaultSaldoClientesAntiguedadDatasetParameters());

    private static IReadOnlyList<DatasetParameterDefinition> BuildDefaultSaldoClientesAntiguedadDatasetParameters()
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
                10),
            new(
                "DiasMinimos",
                "p_dias_minimos",
                "Dias minimos",
                ReportesWebConstants.DatasetParameterDataType.Int64,
                ReportesWebConstants.DatasetParameterValueSource.Report,
                "60",
                true,
                false,
                true,
                20),
            new(
                "EstadoCliente",
                "p_estado_cliente",
                "Estado del cliente",
                ReportesWebConstants.DatasetParameterDataType.Int64,
                ReportesWebConstants.DatasetParameterValueSource.Report,
                "0",
                true,
                false,
                false,
                30),
            new(
                "CicloId",
                "p_ciclo_id",
                "Ciclo",
                ReportesWebConstants.DatasetParameterDataType.Int64,
                ReportesWebConstants.DatasetParameterValueSource.Report,
                "0",
                true,
                false,
                false,
                40)
        ];

    private static DatasetDefinition BuildDefaultAnalisisAntiguedadCobrosDataset()
        => new(
            ReportesWebConstants.CodigoDatasetAnalisisAntiguedadCobros,
            "Dataset analisis de antigüedad de cobros",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetAnalisisAntiguedadCobros,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            BuildDefaultAnalisisAntiguedadCobrosDatasetParameters());

    private static IReadOnlyList<DatasetParameterDefinition> BuildDefaultAnalisisAntiguedadCobrosDatasetParameters()
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
                "FechaBase",
                "p_fecha_base",
                "Fecha base",
                ReportesWebConstants.DatasetParameterDataType.Date,
                ReportesWebConstants.DatasetParameterValueSource.Report,
                null,
                true,
                false,
                true,
                10),
            new(
                "RetrocesoValor",
                "p_retroceso_valor",
                "Retroceso",
                ReportesWebConstants.DatasetParameterDataType.Int64,
                ReportesWebConstants.DatasetParameterValueSource.Report,
                "12",
                true,
                false,
                true,
                20),
            new(
                "UnidadTiempo",
                "p_unidad_tiempo",
                "Unidad de tiempo",
                ReportesWebConstants.DatasetParameterDataType.Text,
                ReportesWebConstants.DatasetParameterValueSource.Report,
                "MESES",
                true,
                false,
                true,
                30)
        ];

    private static DatasetDefinition BuildDefaultSaldoClientesCicloDataset()
        => new(
            ReportesWebConstants.CodigoDatasetSaldoClientesCiclo,
            "Dataset saldo de clientes por ciclo",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetSaldoClientesCiclo,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            BuildDefaultSaldoClientesCicloDatasetParameters());

    private static IReadOnlyList<DatasetParameterDefinition> BuildDefaultSaldoClientesCicloDatasetParameters()
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

    private static DatasetDefinition BuildDefaultSaldoClientesCategoriaCobranzaDataset()
        => new(
            ReportesWebConstants.CodigoDatasetSaldoClientesCategoriaCobranza,
            "Dataset saldo de clientes por categoria",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetSaldoClientesCategoriaCobranza,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            BuildDefaultSaldoClientesCategoriaCobranzaDatasetParameters());

    private static IReadOnlyList<DatasetParameterDefinition> BuildDefaultSaldoClientesCategoriaCobranzaDatasetParameters()
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
                "CategoriaServicioId",
                "p_categoria_servicio_id",
                "Categoria",
                ReportesWebConstants.DatasetParameterDataType.Int64,
                ReportesWebConstants.DatasetParameterValueSource.Report,
                "0",
                true,
                false,
                false,
                30)
        ];

    private static DatasetDefinition BuildDefaultRecaudacionDataset()
        => new(
            ReportesWebConstants.CodigoDatasetRecaudacion,
            "Dataset de recaudacion",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetRecaudacion,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            BuildDefaultRecaudacionDatasetParameters());

    private static IReadOnlyList<DatasetParameterDefinition> BuildDefaultRecaudacionDatasetParameters()
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
                "MedioPagoCodigo",
                "p_medio_pago_codigo",
                "Medio de Pago",
                ReportesWebConstants.DatasetParameterDataType.Text,
                ReportesWebConstants.DatasetParameterValueSource.Report,
                null,
                true,
                true,
                false,
                30)
        ];

    private static DatasetDefinition BuildDefaultSaldoClientesCategoriaDetalleDataset()
        => new(
            ReportesWebConstants.CodigoDatasetSaldoClientesCategoriaDetalle,
            "Dataset saldo de clientes detallado por categoria",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetSaldoClientesCategoriaDetalle,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            BuildDefaultSaldoClientesCategoriaDetalleDatasetParameters());

    private static IReadOnlyList<DatasetParameterDefinition> BuildDefaultSaldoClientesCategoriaDetalleDatasetParameters()
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
                "CategoriaServicioId",
                "p_categoria_servicio_id",
                "Categoria",
                ReportesWebConstants.DatasetParameterDataType.Int64,
                ReportesWebConstants.DatasetParameterValueSource.Report,
                "0",
                true,
                false,
                false,
                30)
        ];

    private static DatasetDefinition BuildDefaultSaldosAguaPotableCicloDataset()
        => new(
            ReportesWebConstants.CodigoDatasetSaldosAguaPotableCiclo,
            "Dataset saldos de agua potable por ciclo",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetSaldosAguaPotableCiclo,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            BuildDefaultSaldosAguaPotableCicloDatasetParameters());

    private static IReadOnlyList<DatasetParameterDefinition> BuildDefaultSaldosAguaPotableCicloDatasetParameters()
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
                "CicloId",
                "p_ciclo_id",
                "Ciclo",
                ReportesWebConstants.DatasetParameterDataType.Int64,
                ReportesWebConstants.DatasetParameterValueSource.Report,
                "0",
                true,
                false,
                false,
                30)
        ];

    private static DatasetDefinition BuildDefaultSaldosAlcantarilladoSanitarioCicloDataset()
        => new(
            ReportesWebConstants.CodigoDatasetSaldosAlcantarilladoSanitarioCiclo,
            "Dataset saldos de alcantarillado sanitario por ciclo",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetSaldosAlcantarilladoSanitarioCiclo,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            BuildDefaultSaldosAguaPotableCicloDatasetParameters());

    private static DatasetDefinition BuildDefaultSumarialTarifarioMedicionDataset()
        => new(
            ReportesWebConstants.CodigoDatasetSumarialTarifarioMedicion,
            "Dataset sumarial tarifario medicion por periodo",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetSumarialTarifarioMedicion,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            BuildDefaultSumarialTarifarioMedicionDatasetParameters());

    private static IReadOnlyList<DatasetParameterDefinition> BuildDefaultSumarialTarifarioMedicionDatasetParameters()
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

    private static DatasetDefinition BuildDefaultSumarialTarifasNoMedidoDataset()
        => new(
            ReportesWebConstants.CodigoDatasetSumarialTarifasNoMedido,
            "Dataset sumarial de tarifas no medido por periodo",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetSumarialTarifasNoMedido,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            BuildDefaultSumarialTarifasNoMedidoDatasetParameters());

    private static IReadOnlyList<DatasetParameterDefinition> BuildDefaultSumarialTarifasNoMedidoDatasetParameters()
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

    private static bool IsSumarialTarifasNoMedidoTemplate(string reportCode, DatasetDefinition dataset)
        => string.Equals(ReportesWebConstants.NormalizeCode(reportCode), ReportesWebConstants.CodigoReporteSumarialTarifasNoMedido, StringComparison.OrdinalIgnoreCase)
           || string.Equals(dataset.Code, ReportesWebConstants.CodigoDatasetSumarialTarifasNoMedido, StringComparison.OrdinalIgnoreCase);

    private static bool IsSumarialTarifarioMedicionTemplate(string reportCode, DatasetDefinition dataset)
        => string.Equals(ReportesWebConstants.NormalizeCode(reportCode), ReportesWebConstants.CodigoReporteSumarialTarifarioMedicion, StringComparison.OrdinalIgnoreCase)
           || string.Equals(dataset.Code, ReportesWebConstants.CodigoDatasetSumarialTarifarioMedicion, StringComparison.OrdinalIgnoreCase);

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

    private static bool IsEstadoFlujoEfectivoTemplate(string reportCode, DatasetDefinition dataset)
        => string.Equals(ReportesWebConstants.NormalizeCode(reportCode), ReportesWebConstants.CodigoReporteEstadoFlujoEfectivo, StringComparison.OrdinalIgnoreCase)
           || string.Equals(dataset.Code, ReportesWebConstants.CodigoDatasetEstadoFlujoEfectivo, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Comparativo de presupuesto: por cada cuenta, lo presupuestado y lo ejecutado del ejercicio
    /// y lo presupuestado del siguiente, con sus diferencias.
    ///
    /// Las diferencias y los porcentajes se calculan aqui y no en la consulta: los tres importes
    /// ya vienen en la misma fila, asi que pedirle a la base que reste seria tener la misma regla
    /// escrita en dos sitios.
    /// </summary>
    private XtraReport CreatePresupuestoComparativoTemplate(string reportCode, string displayName, string? description, DatasetDefinition dataset)
    {
        var report = CreateBaseReport(reportCode, displayName);
        report.PaperKind = DevExpress.Drawing.Printing.DXPaperKind.Letter;

        // Apaisado: son siete columnas de cifras y en vertical no caben.
        report.Landscape = true;
        report.Margins = new DXMargins(40, 40, 78, 58);
        report.RequestParameters = dataset.Parameters.Any(x => x.Source == ReportesWebConstants.DatasetParameterValueSource.Report && x.Visible);

        foreach (var parameter in dataset.Parameters)
        {
            var reportParameter = CreateReportParameter(parameter);
            ApplyPresupuestoComparativoTemplateDefaults(reportParameter);
            report.Parameters.Add(reportParameter);
        }

        var queryName = string.IsNullOrWhiteSpace(dataset.Code) ? "MainQuery" : dataset.Code.Replace('-', '_');
        var dataSource = CreateRelationalDataSource(dataset, queryName);
        report.ComponentStorage.AddRange([dataSource]);
        report.DataSource = dataSource;
        report.DataMember = queryName;

        EstadoFinancieroLayout.AplicarMembrete(report, ResolveCurrentCompany());

        const float contentWidth = 980f;
        const float descriptionWidth = 320f;
        const float amountWidth = 110f;
        const float percentWidth = 60f;

        var reportHeader = EstadoFinancieroLayout.CrearEncabezado("PRESUPUESTO");

        // Cabecera en dos niveles: el ejercicio en curso -presupuestado, ejecutado y su
        // diferencia- y el siguiente.
        var pageHeader = new PageHeaderBand { HeightF = 34f };
        var x = descriptionWidth;

        foreach (var (titulo, ancho) in new (string, float)[]
        {
            ("Presupuestado", amountWidth),
            ("Ejecutado", amountWidth),
            ("DIFERENCIA", amountWidth),
            ("%", percentWidth),
            ("Presupuestado", amountWidth),
            ("DIFERENCIA", amountWidth),
            ("%", percentWidth),
        })
        {
            pageHeader.Controls.Add(new XRLabel
            {
                BoundsF = new RectangleF(x, 0f, ancho, 15f),
                Font = new DXFont("Arial", 8.5f, DXFontStyle.Bold),
                Text = titulo,
                TextAlignment = TextAlignment.MiddleRight,
                Padding = new PaddingInfo(0, 6, 0, 0),
            });
            x += ancho;
        }

        // Debajo del rotulo, el anio al que pertenece cada bloque.
        var anioBase = new XRLabel
        {
            BoundsF = new RectangleF(descriptionWidth, 16f, amountWidth * 3 + percentWidth, 15f),
            Font = new DXFont("Arial", 9f, DXFontStyle.Bold | DXFontStyle.Underline),
            TextAlignment = TextAlignment.MiddleCenter,
        };
        anioBase.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[anio_base]"));

        var anioSiguiente = new XRLabel
        {
            BoundsF = new RectangleF(descriptionWidth + amountWidth * 3 + percentWidth, 16f,
                                     amountWidth * 2 + percentWidth, 15f),
            Font = new DXFont("Arial", 9f, DXFontStyle.Bold | DXFontStyle.Underline),
            TextAlignment = TextAlignment.MiddleCenter,
        };
        anioSiguiente.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[anio_siguiente]"));

        pageHeader.Controls.AddRange([anioBase, anioSiguiente]);

        var groupHeader = new GroupHeaderBand { HeightF = 20f, RepeatEveryPage = true };
        groupHeader.GroupFields.Add(new GroupField("seccion_orden"));
        groupHeader.GroupFields.Add(new GroupField("seccion_nombre"));

        var sectionLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 5f, contentWidth, 15f),
            Font = new DXFont("Arial", 9.5f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleLeft
        };
        sectionLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[seccion_nombre]"));
        groupHeader.Controls.Add(sectionLabel);

        var detailBand = new DetailBand { HeightF = 15f };
        var detailTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 2f, contentWidth, 13f),
            BorderWidth = 0f,
            Borders = BorderSide.None,
            Font = new DXFont("Arial", 8.5f)
        };

        const string diferenciaBase = "[ejecutado_base] - [presupuestado_base]";
        const string diferenciaSiguiente = "[presupuestado_siguiente] - [presupuestado_base]";

        var detailRow = new XRTableRow();
        detailRow.Cells.AddRange(
        [
            EstadoFinancieroLayout.CeldaConcepto("[cuenta_nombre]", descriptionWidth),
            EstadoFinancieroLayout.CeldaImporte("[presupuestado_base]", amountWidth),
            EstadoFinancieroLayout.CeldaImporte("[ejecutado_base]", amountWidth),
            EstadoFinancieroLayout.CeldaImporte(diferenciaBase, amountWidth),
            EstadoFinancieroLayout.CeldaPorcentaje(
                EstadoFinancieroLayout.ExpresionVariacionPorcentual("[ejecutado_base]", "[presupuestado_base]"),
                percentWidth),
            EstadoFinancieroLayout.CeldaImporte("[presupuestado_siguiente]", amountWidth),
            EstadoFinancieroLayout.CeldaImporte(diferenciaSiguiente, amountWidth),
            EstadoFinancieroLayout.CeldaPorcentaje(
                EstadoFinancieroLayout.ExpresionVariacionPorcentual("[presupuestado_siguiente]", "[presupuestado_base]"),
                percentWidth)
        ]);
        detailTable.Rows.Add(detailRow);
        detailBand.Controls.Add(detailTable);

        // Cada seccion cierra con su suma; el reporte la calcula agrupando.
        var groupFooter = EstadoFinancieroLayout.CrearPieDeGrupo(
            "'Suman los ' + Lower([seccion_nombre])",
            descriptionWidth, amountWidth, conVariacion: false,
            "[presupuestado_base]", "[ejecutado_base]");

        report.Bands.AddRange([reportHeader, pageHeader, groupHeader, detailBand, groupFooter]);
        return report;
    }

    /// <summary>
    /// Sin anio, el comparativo sale en blanco: el parametro llega en cero y no hay ejercicio que
    /// buscar. Por defecto, el anio en curso.
    /// </summary>
    private static void ApplyPresupuestoComparativoTemplateDefaults(Parameter parameter)
    {
        if (parameter is null)
        {
            return;
        }

        if (string.Equals(parameter.Name, "AnioBase", StringComparison.OrdinalIgnoreCase))
        {
            parameter.Value = (long)DateTime.Today.Year;
        }
    }

    private static bool IsPresupuestoComparativoTemplate(string reportCode, DatasetDefinition dataset)
        => string.Equals(ReportesWebConstants.NormalizeCode(reportCode), ReportesWebConstants.CodigoReportePresupuestoComparativo, StringComparison.OrdinalIgnoreCase)
           || string.Equals(dataset.Code, ReportesWebConstants.CodigoDatasetPresupuestoComparativo, StringComparison.OrdinalIgnoreCase);

    private static bool IsEstadoCambiosPatrimonioTemplate(string reportCode, DatasetDefinition dataset)
        => string.Equals(ReportesWebConstants.NormalizeCode(reportCode), ReportesWebConstants.CodigoReporteEstadoCambiosPatrimonio, StringComparison.OrdinalIgnoreCase)
           || string.Equals(dataset.Code, ReportesWebConstants.CodigoDatasetEstadoCambiosPatrimonio, StringComparison.OrdinalIgnoreCase);

    private static bool IsTransaccionesPeriodoTemplate(string reportCode, DatasetDefinition dataset)
        => string.Equals(ReportesWebConstants.NormalizeCode(reportCode), ReportesWebConstants.CodigoReporteTransaccionesPeriodo, StringComparison.OrdinalIgnoreCase)
           || string.Equals(dataset.Code, ReportesWebConstants.CodigoDatasetTransaccionesPeriodo, StringComparison.OrdinalIgnoreCase);

    private static bool IsSaldosAguaPotableCicloTemplate(string reportCode, DatasetDefinition dataset)
        => string.Equals(ReportesWebConstants.NormalizeCode(reportCode), ReportesWebConstants.CodigoReporteSaldosAguaPotableCiclo, StringComparison.OrdinalIgnoreCase)
           || string.Equals(dataset.Code, ReportesWebConstants.CodigoDatasetSaldosAguaPotableCiclo, StringComparison.OrdinalIgnoreCase);

    private static bool IsSaldosAlcantarilladoSanitarioCicloTemplate(string reportCode, DatasetDefinition dataset)
        => string.Equals(ReportesWebConstants.NormalizeCode(reportCode), ReportesWebConstants.CodigoReporteSaldosAlcantarilladoSanitarioCiclo, StringComparison.OrdinalIgnoreCase)
           || string.Equals(dataset.Code, ReportesWebConstants.CodigoDatasetSaldosAlcantarilladoSanitarioCiclo, StringComparison.OrdinalIgnoreCase);

    private static bool IsSaldoClientesCategoriaCobranzaTemplate(string reportCode, DatasetDefinition dataset)
        => string.Equals(ReportesWebConstants.NormalizeCode(reportCode), ReportesWebConstants.CodigoReporteSaldoClientesCategoriaCobranza, StringComparison.OrdinalIgnoreCase)
           || string.Equals(dataset.Code, ReportesWebConstants.CodigoDatasetSaldoClientesCategoriaCobranza, StringComparison.OrdinalIgnoreCase);

    private static bool IsFacturaTicketTemplate(string reportCode, DatasetDefinition dataset)
        => string.Equals(ReportesWebConstants.NormalizeCode(reportCode), ReportesWebConstants.CodigoReporteFacturaTicket, StringComparison.OrdinalIgnoreCase)
           || string.Equals(dataset.Code, ReportesWebConstants.CodigoDatasetFacturaTicket, StringComparison.OrdinalIgnoreCase);

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

    private XtraReport CreateTransaccionesPeriodoTemplate(string reportCode, string displayName, string? description, DatasetDefinition dataset)
    {
        var report = CreateBaseReport(reportCode, displayName);
        report.Landscape = true;
        report.PaperKind = DevExpress.Drawing.Printing.DXPaperKind.Letter;
        report.RequestParameters = dataset.Parameters.Any(x => x.Source == ReportesWebConstants.DatasetParameterValueSource.Report && x.Visible);

        foreach (var parameter in dataset.Parameters)
        {
            var reportParameter = CreateReportParameter(parameter);
            ApplyTransaccionesPeriodoTemplateDefaults(reportParameter);
            report.Parameters.Add(reportParameter);
        }

        var queryName = string.IsNullOrWhiteSpace(dataset.Code) ? "MainQuery" : dataset.Code.Replace('-', '_');
        var dataSource = CreateRelationalDataSource(dataset, queryName);
        report.ComponentStorage.AddRange([dataSource]);
        report.DataSource = dataSource;
        report.DataMember = queryName;

        var reportHeader = new ReportHeaderBand { HeightF = 100f };
        var pageHeader = new PageHeaderBand { HeightF = 38f };
        var detailBand = new DetailBand { HeightF = 36f };

        var companyLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 14f, 960f, 24f),
            Font = new DXFont("Times New Roman", 13f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleCenter
        };
        companyLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[empresa_nombre]"));

        var titleLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 38f, 960f, 22f),
            Font = new DXFont("Times New Roman", 12.5f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleCenter
        };
        titleLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[periodo_titulo]"));

        var generatedDateLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 60f, 960f, 20f),
            Font = new DXFont("Times New Roman", 11f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleCenter
        };
        generatedDateLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[fecha_reporte_texto]"));

        reportHeader.Controls.AddRange(
        [
            companyLabel,
            titleLabel,
            generatedDateLabel,
            new XRLabel
            {
                BoundsF = new RectangleF(845f, 8f, 44f, 18f),
                Font = new DXFont("Times New Roman", 10f, DXFontStyle.Bold),
                Text = "PAG.",
                TextAlignment = TextAlignment.MiddleLeft
            },
            new XRPageInfo
            {
                BoundsF = new RectangleF(930f, 8f, 30f, 18f),
                Font = new DXFont("Times New Roman", 10f),
                PageInfo = PageInfo.Number,
                TextAlignment = TextAlignment.MiddleRight
            }
        ]);

        var headerTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 0f, 960f, 36f),
            Borders = BorderSide.Top | BorderSide.Bottom,
            BorderWidth = 1f,
            Font = new DXFont("Times New Roman", 8f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleCenter
        };
        var headerRow = new XRTableRow();
        headerRow.Cells.AddRange(
        [
            CreateHeaderCell(string.Empty, 2.00f),
            CreateHeaderCell("AGUA POTABLE", 1.15f),
            CreateHeaderCell("ALCANTARILLADO\nSANITARIO", 1.25f),
            CreateHeaderCell("AMBIENTAL", 1.05f),
            CreateHeaderCell("TASA ERSAP", 1.05f),
            CreateHeaderCell("CONVENIO", 1.05f),
            CreateHeaderCell("GESTION LEGAL", 1.05f),
            CreateHeaderCell("OTROS CARGOS", 1.20f),
            CreateHeaderCell("TOTAL", 1.05f)
        ]);
        headerTable.Rows.Add(headerRow);
        pageHeader.Controls.Add(headerTable);

        var detailTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 0f, 960f, 34f),
            BorderWidth = 0f,
            Font = new DXFont("Times New Roman", 10.5f),
            TextAlignment = TextAlignment.MiddleLeft
        };
        var detailRow = new XRTableRow();
        detailRow.Cells.AddRange(
        [
            CreateDetailCell("[concepto]", 2.00f),
            CreateDetailCell("[agua_potable]", 1.15f, TextAlignment.MiddleRight, "{0:n2}"),
            CreateDetailCell("[alcantarillado_sanitario]", 1.25f, TextAlignment.MiddleRight, "{0:n2}"),
            CreateDetailCell("[ambiental]", 1.05f, TextAlignment.MiddleRight, "{0:n2}"),
            CreateDetailCell("[tasa_ersap]", 1.05f, TextAlignment.MiddleRight, "{0:n2}"),
            CreateDetailCell("[convenio]", 1.05f, TextAlignment.MiddleRight, "{0:n2}"),
            CreateDetailCell("[gestion_legal]", 1.05f, TextAlignment.MiddleRight, "{0:n2}"),
            CreateDetailCell("[otros_cargos]", 1.20f, TextAlignment.MiddleRight, "{0:n2}"),
            CreateDetailCell("[total]", 1.05f, TextAlignment.MiddleRight, "{0:n2}")
        ]);
        detailTable.Rows.Add(detailRow);
        detailBand.Controls.Add(detailTable);

        report.Bands.AddRange([reportHeader, pageHeader, detailBand]);
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

        var reportHeader = new ReportHeaderBand { HeightF = 110f };
        var pageHeader = new PageHeaderBand { HeightF = 56f };
        var groupHeader = new GroupHeaderBand { HeightF = 26f, RepeatEveryPage = true };
        var detailBand = new DetailBand { HeightF = 22f };
        var reportFooter = new ReportFooterBand { HeightF = 28f };
        var pageFooter = new PageFooterBand { HeightF = 24f };

        var companyLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 0f, 960f, 20f),
            Font = new DXFont("Arial", 10f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleCenter
        };
        companyLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[empresa_nombre]"));

        var titleLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 24f, 960f, 26f),
            Font = new DXFont("Arial", 14f, DXFontStyle.Bold),
            Text = "BALANCE DE COMPROBACION",
            TextAlignment = TextAlignment.MiddleLeft
        };

        var periodLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 52f, 960f, 16f),
            Font = new DXFont("Arial", 8f),
            ForeColor = Color.DimGray,
            TextAlignment = TextAlignment.MiddleLeft
        };
        periodLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "FormatString('Del {0:dd/MM/yyyy} al {1:dd/MM/yyyy}', ?FechaDesde, ?FechaHasta)"));

        var infoLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 68f, 960f, 16f),
            Font = new DXFont("Arial", 8f),
            ForeColor = Color.DimGray,
            TextAlignment = TextAlignment.MiddleLeft
        };
        infoLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "FormatString('{0} | RTN: {1} | Tel: {2} | Email: {3}', [empresa_nombre_legal], [empresa_rtn], [empresa_telefono], [empresa_email])"));

        var addressLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 84f, 960f, 16f),
            Font = new DXFont("Arial", 8f),
            ForeColor = Color.DimGray,
            TextAlignment = TextAlignment.MiddleLeft
        };
        addressLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[empresa_direccion]"));

        reportHeader.Controls.AddRange([companyLabel, titleLabel, periodLabel, infoLabel, addressLabel]);

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

        // Los margenes superior e inferior los ocupa el membrete.
        report.Margins = new DXMargins(50, 50, 78, 58);
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

        EstadoFinancieroLayout.AplicarMembrete(report, ResolveCurrentCompany());

        const float contentWidth = EstadoFinancieroLayout.AnchoContenido;
        const float descriptionWidth = 350f;
        const float amountWidth = 100f;

        var reportHeader = EstadoFinancieroLayout.CrearEncabezado("BALANCE GENERAL");

        // El balance se corta a una FECHA, no a un rango: el anio sale de FechaCorte.
        var pageHeader = EstadoFinancieroLayout.CrearCabeceraAgrupada(
            descriptionWidth,
            amountWidth,
            ("AL 31 DE DICIEMBRE",
             [
                 "FormatString('{0:yyyy}', ?FechaCorte)",
                 "FormatString('{0:yyyy}', AddYears(?FechaCorte, -1))"
             ]),
            ("VARIACION", ["'RELATIVA'", "'PORCENTUAL'"]));

        // Dos niveles de agrupacion, como el juego impreso: la seccion (ACTIVO, PASIVO,
        // PATRIMONIO) y dentro de ella la clase (corriente / no corriente). Cada uno cierra con
        // su suma, que calcula el propio reporte: el SP entrega cuentas, no lineas de total.
        // Level 0 es el grupo mas CERCANO al detalle: la seccion, que es la externa, lleva el mayor.
        var seccionHeader = new GroupHeaderBand { HeightF = 20f, RepeatEveryPage = true, Level = 1 };
        seccionHeader.GroupFields.Add(new GroupField("seccion_orden"));
        seccionHeader.GroupFields.Add(new GroupField("seccion_nombre"));

        var seccionLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 5f, contentWidth, 15f),
            Font = new DXFont("Arial", 10f, DXFontStyle.Bold),
            Padding = new PaddingInfo(0, 0, 0, 0),
            TextAlignment = TextAlignment.MiddleLeft
        };
        seccionLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[seccion_nombre]"));
        seccionHeader.Controls.Add(seccionLabel);

        var claseHeader = new GroupHeaderBand { HeightF = 18f, RepeatEveryPage = true, Level = 0 };
        claseHeader.GroupFields.Add(new GroupField("clase"));

        var claseLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 3f, contentWidth, 15f),
            Font = new DXFont("Arial", 9f, DXFontStyle.Bold),
            Padding = new PaddingInfo(8, 0, 0, 0),
            TextAlignment = TextAlignment.MiddleLeft
        };
        claseLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[clase_nombre]"));
        claseHeader.Controls.Add(claseLabel);

        var detailBand = new DetailBand { HeightF = 15f };

        // "Suma el activo corriente" y luego "Suma el activo": el rotulo sale del nombre que da
        // la base, no de una lista escrita aqui.
        var claseFooter = EstadoFinancieroLayout.CrearPieDeGrupo(
            "'Suma el ' + Lower([clase_nombre])",
            descriptionWidth, amountWidth, conVariacion: true, "[monto]", "[monto_anterior]");
        claseFooter.Level = 0;

        var seccionFooter = EstadoFinancieroLayout.CrearPieDeGrupo(
            "'Suma el ' + Lower([seccion_nombre])",
            descriptionWidth, amountWidth, conVariacion: true, "[monto]", "[monto_anterior]");
        seccionFooter.Level = 1;

        var detailTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 2f, contentWidth, 13f),
            BorderWidth = 0f,
            Borders = BorderSide.None,
            Font = new DXFont("Arial", 9f)
        };

        var detailRow = new XRTableRow();
        detailRow.Cells.AddRange(
        [
            EstadoFinancieroLayout.CeldaConcepto("[descripcion_mostrar]", descriptionWidth, "[nivel_cuenta] - 1"),
            EstadoFinancieroLayout.CeldaImporte("[monto]", amountWidth),
            EstadoFinancieroLayout.CeldaImporte("[monto_anterior]", amountWidth),
            EstadoFinancieroLayout.CeldaImporte(
                EstadoFinancieroLayout.ExpresionVariacionRelativa("[monto]", "[monto_anterior]"), amountWidth),
            EstadoFinancieroLayout.CeldaPorcentaje(
                EstadoFinancieroLayout.ExpresionVariacionPorcentual("[monto]", "[monto_anterior]"), amountWidth)
        ]);
        detailTable.Rows.Add(detailRow);
        detailBand.Controls.Add(detailTable);

        report.Bands.AddRange(
        [
            reportHeader, pageHeader,
            seccionHeader, claseHeader,
            detailBand,
            claseFooter, seccionFooter
        ]);
        return report;
    }

    private XtraReport CreateEstadoResultadosTemplate(string reportCode, string displayName, string? description, DatasetDefinition dataset)
    {
        var report = CreateBaseReport(reportCode, displayName);
        report.PaperKind = DevExpress.Drawing.Printing.DXPaperKind.Letter;

        // Los margenes superior e inferior los ocupa el membrete.
        report.Margins = new DXMargins(50, 50, 78, 58);
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

        EstadoFinancieroLayout.AplicarMembrete(report, ResolveCurrentCompany());

        const float contentWidth = EstadoFinancieroLayout.AnchoContenido;
        const float descriptionWidth = 350f;
        const float amountWidth = 100f;

        var reportHeader = EstadoFinancieroLayout.CrearEncabezado("ESTADO DE RESULTADOS");

        // Dos niveles, como el juego impreso: el corte a la izquierda y la variacion a la derecha.
        var pageHeader = EstadoFinancieroLayout.CrearCabeceraAgrupada(
            descriptionWidth,
            amountWidth,
            ("AL 31 DE DICIEMBRE",
             [
                 "FormatString('{0:yyyy}', ?FechaHasta)",
                 "FormatString('{0:yyyy}', AddYears(?FechaHasta, -1))"
             ]),
            ("VARIACION", ["'RELATIVA'", "'PORCENTUAL'"]));

        var groupHeader = new GroupHeaderBand { HeightF = 22f, RepeatEveryPage = true };
        var detailBand = new DetailBand { HeightF = 15f };

        groupHeader.GroupFields.Add(new GroupField("seccion_orden"));
        groupHeader.GroupFields.Add(new GroupField("seccion_nombre"));

        var sectionLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 6f, contentWidth, 15f),
            Font = new DXFont("Arial", 9.5f, DXFontStyle.Bold),
            Padding = new PaddingInfo(0, 0, 0, 0),
            TextAlignment = TextAlignment.MiddleLeft
        };
        sectionLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[seccion_nombre]"));
        groupHeader.Controls.Add(sectionLabel);

        // La linea del total cubre las cuatro columnas de cifras.
        detailBand.Controls.Add(EstadoFinancieroLayout.LineaSobreTotal(
            descriptionWidth, amountWidth, 4, "[mostrar_subtotal]"));

        var detailTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 2f, contentWidth, 13f),
            BorderWidth = 0f,
            Borders = BorderSide.None,
            Font = new DXFont("Arial", 9f)
        };

        var detailRow = new XRTableRow();
        detailRow.Cells.AddRange(
        [
            EstadoFinancieroLayout.CeldaConcepto("[descripcion_mostrar]", descriptionWidth, "[nivel_indentacion]"),
            EstadoFinancieroLayout.CeldaImporte("[monto]", amountWidth),
            EstadoFinancieroLayout.CeldaImporte("[monto_anterior]", amountWidth),
            EstadoFinancieroLayout.CeldaImporte(
                EstadoFinancieroLayout.ExpresionVariacionRelativa("[monto]", "[monto_anterior]"), amountWidth),
            EstadoFinancieroLayout.CeldaPorcentaje(
                EstadoFinancieroLayout.ExpresionVariacionPorcentual("[monto]", "[monto_anterior]"), amountWidth)
        ]);
        EstadoFinancieroLayout.MarcarComoTotal(detailRow, "[mostrar_subtotal]");
        detailTable.Rows.Add(detailRow);
        detailBand.Controls.Add(detailTable);

        report.Bands.AddRange([reportHeader, pageHeader, groupHeader, detailBand]);
        return report;
    }

    private XtraReport CreateEstadoFlujoEfectivoTemplate(string reportCode, string displayName, string? description, DatasetDefinition dataset)
    {
        var report = CreateBaseReport(reportCode, displayName);
        report.PaperKind = DevExpress.Drawing.Printing.DXPaperKind.Letter;

        // Los margenes superior e inferior los ocupa el membrete; el ancho es el del contenido.
        report.Margins = new DXMargins(50, 50, 78, 58);
        report.RequestParameters = dataset.Parameters.Any(x => x.Source == ReportesWebConstants.DatasetParameterValueSource.Report && x.Visible);

        foreach (var parameter in dataset.Parameters)
        {
            var reportParameter = CreateReportParameter(parameter);
            ApplyEstadoFlujoEfectivoTemplateDefaults(reportParameter);
            report.Parameters.Add(reportParameter);
        }

        var queryName = string.IsNullOrWhiteSpace(dataset.Code) ? "MainQuery" : dataset.Code.Replace('-', '_');
        var dataSource = CreateRelationalDataSource(dataset, queryName);
        report.ComponentStorage.AddRange([dataSource]);
        report.DataSource = dataSource;
        report.DataMember = queryName;

        EstadoFinancieroLayout.AplicarMembrete(report, ResolveCurrentCompany());

        const float contentWidth = EstadoFinancieroLayout.AnchoContenido;
        const float descriptionWidth = 460f;
        const float amountWidth = 145f;

        var reportHeader = EstadoFinancieroLayout.CrearEncabezado("ESTADO DE FLUJO DE EFECTIVO");

        // Los anios salen de la fecha del reporte, no de una constante: el encabezado tiene que
        // seguir siendo cierto el anio que viene.
        var pageHeader = EstadoFinancieroLayout.CrearCabeceraColumnas(
            descriptionWidth,
            amountWidth,
            "FormatString('{0:yyyy}', ?FechaHasta)",
            "FormatString('{0:yyyy}', AddYears(?FechaHasta, -1))");

        var groupHeader = new GroupHeaderBand { HeightF = 22f, RepeatEveryPage = true };
        var detailBand = new DetailBand { HeightF = 15f };
        var reportFooter = new ReportFooterBand { HeightF = 40f };

        groupHeader.GroupFields.Add(new GroupField("seccion_orden"));
        groupHeader.GroupFields.Add(new GroupField("seccion_nombre"));

        var sectionLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 6f, contentWidth, 15f),
            Font = new DXFont("Arial", 9.5f, DXFontStyle.Bold),
            Padding = new PaddingInfo(0, 0, 0, 0),
            TextAlignment = TextAlignment.MiddleLeft
        };
        sectionLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[seccion_nombre]"));
        groupHeader.Controls.Add(sectionLabel);

        // La linea del total va en su propio control: enlazar Visible es fiable, enlazar Borders
        // obligaria a producir el enum desde texto.
        detailBand.Controls.Add(EstadoFinancieroLayout.LineaSobreTotal(
            descriptionWidth, amountWidth, 2, "[mostrar_subtotal]"));

        var detailTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 2f, contentWidth, 13f),
            BorderWidth = 0f,
            Borders = BorderSide.None,
            Font = new DXFont("Arial", 9f)
        };

        var detailRow = new XRTableRow();
        detailRow.Cells.AddRange(
        [
            EstadoFinancieroLayout.CeldaConcepto("[descripcion_mostrar]", descriptionWidth, "[nivel_indentacion]"),
            EstadoFinancieroLayout.CeldaImporte("[monto]", amountWidth),
            EstadoFinancieroLayout.CeldaImporte("[monto_anterior]", amountWidth)
        ]);
        EstadoFinancieroLayout.MarcarComoTotal(detailRow, "[mostrar_subtotal]");
        detailRow.Cells[0].ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Font.Italic", "[tipo_linea] == 2"));
        detailTable.Rows.Add(detailRow);
        detailBand.Controls.Add(detailTable);

        reportFooter.Controls.AddRange(
        [
            new XRLabel
            {
                BoundsF = new RectangleF(0f, 8f, contentWidth, 14f),
                Font = new DXFont("Arial", 7.5f),
                ForeColor = Color.DimGray,
                Text = "(1) No incluidos en actividades de inversion.",
                TextAlignment = TextAlignment.MiddleLeft
            },
            new XRLabel
            {
                BoundsF = new RectangleF(0f, 22f, contentWidth, 14f),
                Font = new DXFont("Arial", 7.5f),
                ForeColor = Color.DimGray,
                Text = "(2) No incluidos en actividades de financiacion.",
                TextAlignment = TextAlignment.MiddleLeft
            }
        ]);

        report.Bands.AddRange([reportHeader, pageHeader, groupHeader, detailBand, reportFooter]);
        return report;
    }

    private XtraReport CreateEstadoCambiosPatrimonioTemplate(string reportCode, string displayName, string? description, DatasetDefinition dataset)
    {
        var report = CreateBaseReport(reportCode, displayName);
        report.PaperKind = DevExpress.Drawing.Printing.DXPaperKind.Letter;

        // Apaisado: la matriz lleva una columna por componente del patrimonio mas el total, y en
        // vertical no caben con cifras de nueve digitos.
        report.Landscape = true;
        report.Margins = new DXMargins(40, 40, 78, 58);
        report.RequestParameters = dataset.Parameters.Any(x => x.Source == ReportesWebConstants.DatasetParameterValueSource.Report && x.Visible);

        foreach (var parameter in dataset.Parameters)
        {
            var reportParameter = CreateReportParameter(parameter);
            ApplyEstadoCambiosPatrimonioTemplateDefaults(reportParameter);
            report.Parameters.Add(reportParameter);
        }

        var queryName = string.IsNullOrWhiteSpace(dataset.Code) ? "MainQuery" : dataset.Code.Replace('-', '_');
        var dataSource = CreateRelationalDataSource(dataset, queryName);
        report.ComponentStorage.AddRange([dataSource]);
        report.DataSource = dataSource;
        report.DataMember = queryName;

        EstadoFinancieroLayout.AplicarMembrete(report, ResolveCurrentCompany());

        var reportHeader = EstadoFinancieroLayout.CrearEncabezado("ESTADO DE CAMBIOS EN EL PATRIMONIO");

        // Una tabla de referencias cruzadas y no una tabla normal: las columnas son los
        // componentes del patrimonio, que salen de la configuracion contable de cada empresa.
        // Fijarlas en el codigo obligaria a tocarlo cada vez que una empresa configure los suyos.
        var matriz = new XRCrossTab
        {
            BoundsF = new RectangleF(0f, 110f, 980f, 20f),
            DataSource = dataSource,
            DataMember = queryName,
            Font = new DXFont("Arial", 8.5f)
        };

        // La tabla trae su propia hoja de estilos y pisa la fuente del control: sin esto sale con
        // la serif por defecto, que no es la del resto del juego. Los estilos se ASIGNAN
        // completos; mutarles una propiedad no tiene efecto.
        matriz.CrossTabStyles.GeneralStyle = new XRControlStyle
        {
            Name = "PatrimonioGeneral",
            Font = new DXFont("Arial", 8.5f),
            Padding = new PaddingInfo(4, 4, 1, 1),
        };
        matriz.CrossTabStyles.HeaderAreaStyle = new XRControlStyle
        {
            Name = "PatrimonioEncabezado",
            Font = new DXFont("Arial", 8.5f, DXFontStyle.Bold),
        };
        matriz.CrossTabStyles.DataAreaStyle = new XRControlStyle
        {
            Name = "PatrimonioDatos",
            Font = new DXFont("Arial", 8.5f),
            TextAlignment = TextAlignment.MiddleRight,
        };
        matriz.CrossTabStyles.TotalAreaStyle = new XRControlStyle
        {
            Name = "PatrimonioTotales",
            Font = new DXFont("Arial", 8.5f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleRight,
        };

        // Sin orden propio: las filas se imprimen en el orden en que las entrega la consulta, que
        // es el del estado -saldo de apertura, movimientos, saldo de cierre-. Ordenadas por
        // nombre saldrian alfabeticas, que no significa nada en un estado financiero.
        matriz.RowFields.Add(new CrossTabRowField
        {
            FieldName = "fila_nombre",
            SortOrder = XRColumnSortOrder.None
        });
        matriz.ColumnFields.Add(new CrossTabColumnField { FieldName = "componente" });
        matriz.DataFields.Add(new CrossTabDataField { FieldName = "monto" });

        // Un estilo solo surte efecto si esta registrado en la hoja del reporte; asignarlo a la
        // matriz sin registrarlo lo deja sin aplicar y todo sale con la serif por defecto.
        report.StyleSheet.AddRange(
        [
            matriz.CrossTabStyles.GeneralStyle,
            matriz.CrossTabStyles.HeaderAreaStyle,
            matriz.CrossTabStyles.DataAreaStyle,
            matriz.CrossTabStyles.TotalAreaStyle,
        ]);

        // Los encabezados se repiten si la matriz parte de pagina.
        matriz.PrintOptions.RepeatColumnHeaders = true;
        matriz.PrintOptions.RepeatRowHeaders = true;

        matriz.GenerateLayout();

        // Retoques sobre las celdas ya generadas:
        //
        //  - la esquina superior izquierda trae el nombre del campo, que no le dice nada a nadie;
        //  - el total de COLUMNA es el patrimonio total y merece su nombre;
        //  - el total de FILA suma saldos con movimientos y no significa nada, asi que se oculta.
        var filaTotalGeneral = -1;
        foreach (var celda in matriz.Cells)
        {
            if (celda is XRCrossTabCell c
                && c.ColumnIndex == 0
                && string.Equals(c.Text?.Trim(), "Grand Total", StringComparison.OrdinalIgnoreCase))
            {
                filaTotalGeneral = c.RowIndex;
            }
        }

        foreach (var celda in matriz.Cells)
        {
            if (celda is not XRCrossTabCell c)
            {
                continue;
            }

            // La fuente se pone en la celda y no solo en el estilo: registrado o no, el estilo de
            // la matriz no llega a las celdas ya generadas y todo salia con la serif por defecto.
            c.Font = c.RowIndex == 0 || c.ColumnIndex == 0
                ? new DXFont("Arial", 8.5f, DXFontStyle.Bold)
                : new DXFont("Arial", 8.5f);

            if (c.RowIndex == 0 && c.ColumnIndex == 0)
            {
                c.Text = string.Empty;
            }
            else if (filaTotalGeneral >= 0 && c.RowIndex == filaTotalGeneral)
            {
                c.Visible = false;
            }
            else if (string.Equals(c.Text?.Trim(), "Grand Total", StringComparison.OrdinalIgnoreCase))
            {
                c.Text = "Total patrimonio";
            }
        }

        // Los negativos entre parentesis y sin decimales, igual que el resto del juego.
        foreach (var celda in matriz.Cells)
        {
            if (celda is XRCrossTabCell dato && dato.DataLevel >= 0)
            {
                dato.TextFormatString = EstadoFinancieroLayout.FormatoMonto;
            }
        }

        // La matriz va en el ENCABEZADO, no en el detalle. El detalle se imprime una vez por
        // fila del origen y la matriz ya resume todas: puesta ahi, el estado salia repetido
        // tantas veces como filas tuviera la consulta.
        // La primera columna lleva los nombres de los movimientos -"Saldo al 31/12/2024"- y con el
        // ancho por defecto salen cortados.
        var primera = true;
        foreach (var columna in matriz.ColumnDefinitions)
        {
            // La primera lleva los nombres de los movimientos -"Saldo al 31/12/2024"- y con el
            // ancho por defecto salen cortados; las demas crecen segun la cifra.
            if (primera)
            {
                columna.Width = 190;
                primera = false;
                continue;
            }

            columna.AutoWidthMode = AutoSizeMode.GrowOnly;
        }

        reportHeader.HeightF = 110f + matriz.HeightF + 20f;
        reportHeader.Controls.Add(matriz);

        report.Bands.Add(reportHeader);
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

    private static void ApplyEstadoFlujoEfectivoTemplateDefaults(Parameter parameter)
    {
        parameter.Value = parameter.Name switch
        {
            "FechaDesde" => new DateTime(DateTime.Today.Year, 1, 1),
            "FechaHasta" => DateTime.Today,
            _ => parameter.Value
        };
    }

    private static void ApplyEstadoCambiosPatrimonioTemplateDefaults(Parameter parameter)
    {
        parameter.Value = parameter.Name switch
        {
            "FechaDesde" => new DateTime(DateTime.Today.Year, 1, 1),
            "FechaHasta" => DateTime.Today,
            _ => parameter.Value
        };
    }

    private static void ApplyTransaccionesPeriodoTemplateDefaults(Parameter parameter)
    {
        parameter.Value = parameter.Name switch
        {
            "FechaDesde" => FirstDayOfCurrentMonth(),
            "FechaHasta" => DateTime.Today,
            _ => parameter.Value
        };
    }

    private XtraReport CreateBaseReport(string reportCode, string displayName)
    {
        var report = new XtraReport
        {
            Name = reportCode,
            DisplayName = displayName,
            Margins = new DXMargins(35, 35, 28, 28)
        };

        report.Bands.Clear();
        report.Bands.AddRange([new TopMarginBand(), new BottomMarginBand()]);
        ReportCompanyHeaderParameters.Apply(report, ResolveCurrentCompany());
        return report;
    }

    private cfg_company? ResolveCurrentCompany()
    {
        var companyId = _currentCompanyService.GetCompanyId();
        return companyId > 0
            ? _context.cfg_companies.FirstOrDefault(x => x.company_id == companyId)
            : null;
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

    private static XRTableCell CreateSummaryCell(string expression, float weight, string formatString = "{0:n0}")
    {
        var cell = new XRTableCell
        {
            Weight = weight,
            TextAlignment = TextAlignment.MiddleRight,
            Padding = new PaddingInfo(4, 4, 0, 0),
            TextFormatString = formatString
        };

        cell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", $"sumSum({expression})"));
        cell.Summary = new XRSummary
        {
            Running = SummaryRunning.Report
        };

        return cell;
    }

    private static XRTableCell CreateFinancialStatementAmountCell(string expression, float weight)
    {
        var cell = new XRTableCell
        {
            Weight = weight,
            Borders = BorderSide.Bottom,
            BorderWidth = 1f,
            TextAlignment = TextAlignment.MiddleRight,
            Padding = new PaddingInfo(4, 4, 0, 0),
            TextFormatString = "{0:n2}"
        };

        cell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", expression));
        return cell;
    }

    private static XRTableCell CreateFinancialStatementSummaryCell(string expression, float weight, SummaryRunning running)
    {
        var cell = new XRTableCell
        {
            Weight = weight,
            Borders = BorderSide.Bottom,
            BorderWidth = 1.5f,
            TextAlignment = TextAlignment.MiddleRight,
            Padding = new PaddingInfo(4, 4, 0, 0),
            TextFormatString = "{0:n2}"
        };

        cell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", $"sumSum({expression})"));
        cell.Summary = new XRSummary
        {
            Running = running
        };

        return cell;
    }

    private static DateTime FirstDayOfCurrentMonth()
    {
        var today = DateTime.Today;
        return new DateTime(today.Year, today.Month, 1);
    }

    private XtraReport CreateSaldosAguaPotableCicloTemplate(string reportCode, string displayName, string? description, DatasetDefinition dataset)
    {
        var report = CreateBaseReport(reportCode, displayName);
        report.Landscape = true;
        report.PaperKind = DevExpress.Drawing.Printing.DXPaperKind.Letter;
        report.RequestParameters = dataset.Parameters.Any(x => x.Source == ReportesWebConstants.DatasetParameterValueSource.Report && x.Visible);

        foreach (var parameter in dataset.Parameters)
        {
            var reportParameter = CreateReportParameter(parameter);
            report.Parameters.Add(reportParameter);
        }

        var queryName = string.IsNullOrWhiteSpace(dataset.Code) ? "MainQuery" : dataset.Code.Replace('-', '_');
        var dataSource = CreateRelationalDataSource(dataset, queryName);
        report.ComponentStorage.AddRange([dataSource]);
        report.DataSource = dataSource;
        report.DataMember = queryName;

        var reportHeader = new ReportHeaderBand { HeightF = 100f };
        var pageHeader = new PageHeaderBand { HeightF = 38f };
        var detailBand = new DetailBand { HeightF = 36f };
        var reportFooter = new ReportFooterBand { HeightF = 32f };

        var companyLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 14f, 960f, 24f),
            Font = new DXFont("Times New Roman", 13f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleCenter
        };
        companyLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[empresa_nombre]"));

        var titleLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 38f, 960f, 22f),
            Font = new DXFont("Times New Roman", 12.5f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleCenter
        };
        titleLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[periodo_titulo]"));

        var generatedDateLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 60f, 960f, 20f),
            Font = new DXFont("Times New Roman", 11f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleCenter
        };
        generatedDateLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[fecha_reporte_texto]"));

        reportHeader.Controls.AddRange(
        [
            companyLabel,
            titleLabel,
            generatedDateLabel,
            new XRLabel
            {
                BoundsF = new RectangleF(845f, 8f, 44f, 18f),
                Font = new DXFont("Times New Roman", 10f, DXFontStyle.Bold),
                Text = "PAG.",
                TextAlignment = TextAlignment.MiddleLeft
            },
            new XRPageInfo
            {
                BoundsF = new RectangleF(930f, 8f, 30f, 18f),
                Font = new DXFont("Times New Roman", 10f),
                PageInfo = PageInfo.Number,
                TextAlignment = TextAlignment.MiddleRight
            }
        ]);

        var headerTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 0f, 960f, 36f),
            Borders = BorderSide.Top | BorderSide.Bottom,
            BorderWidth = 1f,
            Font = new DXFont("Times New Roman", 8f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleCenter
        };
        var headerRow = new XRTableRow();
        headerRow.Cells.AddRange(
        [
            CreateHeaderCell("Ciclo", 0.80f),
            CreateHeaderCell("Saldo\nAnterior", 1.10f),
            CreateHeaderCell("Debitos", 1.10f),
            CreateHeaderCell("Creditos", 1.10f),
            CreateHeaderCell("Saldo\nActual", 1.10f),
            CreateHeaderCell("Total\nUsuarios", 1.00f),
            CreateHeaderCell("Con\nMedidor", 1.00f),
            CreateHeaderCell("Sin\nMedidor", 1.00f),
            CreateHeaderCell("Activos", 0.90f),
            CreateHeaderCell("Inactivos", 0.90f)
        ]);
        headerTable.Rows.Add(headerRow);
        pageHeader.Controls.Add(headerTable);

        var detailTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 0f, 960f, 34f),
            BorderWidth = 0f,
            Font = new DXFont("Times New Roman", 10f),
            TextAlignment = TextAlignment.MiddleLeft
        };
        var detailRow = new XRTableRow();
        detailRow.Cells.AddRange(
        [
            CreateDetailCell("[ciclo]", 0.80f, TextAlignment.MiddleLeft),
            CreateDetailCell("[saldo_anterior]", 1.10f, TextAlignment.MiddleRight, "{0:n2}"),
            CreateDetailCell("[debitos]", 1.10f, TextAlignment.MiddleRight, "{0:n2}"),
            CreateDetailCell("[creditos]", 1.10f, TextAlignment.MiddleRight, "{0:n2}"),
            CreateDetailCell("[saldo_actual]", 1.10f, TextAlignment.MiddleRight, "{0:n2}"),
            CreateDetailCell("[total_usuarios]", 1.00f, TextAlignment.MiddleRight, "{0:n0}"),
            CreateDetailCell("[con_medidor]", 1.00f, TextAlignment.MiddleRight, "{0:n0}"),
            CreateDetailCell("[sin_medidor]", 1.00f, TextAlignment.MiddleRight, "{0:n0}"),
            CreateDetailCell("[activos]", 0.90f, TextAlignment.MiddleRight, "{0:n0}"),
            CreateDetailCell("[inactivos]", 0.90f, TextAlignment.MiddleRight, "{0:n0}")
        ]);
        detailTable.Rows.Add(detailRow);
        detailBand.Controls.Add(detailTable);

        var footerTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 0f, 960f, 32f),
            Borders = BorderSide.Top | BorderSide.Bottom,
            BorderWidth = 1f,
            Font = new DXFont("Times New Roman", 9f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleRight
        };
        var footerRow = new XRTableRow();
        footerRow.Cells.AddRange(
        [
            new XRTableCell { Text = "Total", Weight = 0.80f, TextAlignment = TextAlignment.MiddleLeft },
            CreateSummaryCell("[saldo_anterior]", 1.10f, "{0:n2}"),
            CreateSummaryCell("[debitos]", 1.10f, "{0:n2}"),
            CreateSummaryCell("[creditos]", 1.10f, "{0:n2}"),
            CreateSummaryCell("[saldo_actual]", 1.10f, "{0:n2}"),
            CreateSummaryCell("[total_usuarios]", 1.00f, "{0:n0}"),
            CreateSummaryCell("[con_medidor]", 1.00f, "{0:n0}"),
            CreateSummaryCell("[sin_medidor]", 1.00f, "{0:n0}"),
            CreateSummaryCell("[activos]", 0.90f, "{0:n0}"),
            CreateSummaryCell("[inactivos]", 0.90f, "{0:n0}")
        ]);
        footerTable.Rows.Add(footerRow);
        reportFooter.Controls.Add(footerTable);

        report.Bands.AddRange([reportHeader, pageHeader, detailBand, reportFooter]);
        return report;
    }

    private XtraReport CreateSumarialTarifarioMedicionTemplate(string reportCode, string displayName, string? description, DatasetDefinition dataset)
    {
        var report = CreateBaseReport(reportCode, displayName);
        report.Landscape = true;
        report.PaperKind = DevExpress.Drawing.Printing.DXPaperKind.Letter;
        report.Margins = new DXMargins(35, 35, 28, 28);
        report.RequestParameters = dataset.Parameters.Any(x => x.Source == ReportesWebConstants.DatasetParameterValueSource.Report && x.Visible);

        foreach (var parameter in dataset.Parameters)
        {
            var reportParameter = CreateReportParameter(parameter);
            report.Parameters.Add(reportParameter);
        }

        var queryName = string.IsNullOrWhiteSpace(dataset.Code) ? "MainQuery" : dataset.Code.Replace('-', '_');
        var dataSource = CreateRelationalDataSource(dataset, queryName);
        report.ComponentStorage.AddRange([dataSource]);
        report.DataSource = dataSource;
        report.DataMember = queryName;

        var reportHeader = new ReportHeaderBand { HeightF = 95f };
        var pageHeader = new PageHeaderBand { HeightF = 0f };
        
        var companyLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 10f, 960f, 22f),
            Font = new DXFont("Times New Roman", 13f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleCenter
        };
        companyLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[empresa_nombre]"));

        var titleLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 32f, 960f, 22f),
            Font = new DXFont("Times New Roman", 12.5f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleCenter
        };
        titleLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[periodo_titulo]"));

        var generatedDateLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 54f, 960f, 18f),
            Font = new DXFont("Times New Roman", 10.5f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleCenter
        };
        generatedDateLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[fecha_reporte_texto]"));

        reportHeader.Controls.AddRange(
        [
            companyLabel,
            titleLabel,
            generatedDateLabel,
            new XRLabel
            {
                BoundsF = new RectangleF(845f, 6f, 44f, 18f),
                Font = new DXFont("Times New Roman", 10f, DXFontStyle.Bold),
                Text = "PAG.",
                TextAlignment = TextAlignment.MiddleLeft
            },
            new XRPageInfo
            {
                BoundsF = new RectangleF(930f, 6f, 30f, 18f),
                Font = new DXFont("Times New Roman", 10f),
                PageInfo = PageInfo.Number,
                TextAlignment = TextAlignment.MiddleRight
            }
        ]);

        var groupHeader = new GroupHeaderBand { HeightF = 55f };
        groupHeader.GroupFields.Add(new GroupField("categoria_nombre", XRColumnSortOrder.Ascending));
        groupHeader.KeepTogether = true;

        var categoryLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 5f, 960f, 22f),
            Font = new DXFont("Times New Roman", 11f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleLeft
        };
        categoryLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[categoria_nombre]"));
        groupHeader.Controls.Add(categoryLabel);

        var headerTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 27f, 960f, 28f),
            Borders = BorderSide.Top | BorderSide.Bottom,
            BorderWidth = 1f,
            Font = new DXFont("Times New Roman", 8.5f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleCenter
        };
        var headerRow = new XRTableRow();
        headerRow.Cells.AddRange(
        [
            CreateHeaderCell("Código", 1.0f),
            CreateHeaderCell("Rango Min", 1.0f),
            CreateHeaderCell("Rango Max", 1.0f),
            CreateHeaderCell("Conexiones", 1.20f),
            CreateHeaderCell("Consumo M3", 1.20f),
            CreateHeaderCell("Valor Agua", 1.30f)
        ]);
        headerTable.Rows.Add(headerRow);
        groupHeader.Controls.Add(headerTable);

        var detailBand = new DetailBand { HeightF = 26f };
        var detailTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 0f, 960f, 26f),
            BorderWidth = 0f,
            Font = new DXFont("Times New Roman", 9.5f),
            TextAlignment = TextAlignment.MiddleLeft
        };
        var detailRow = new XRTableRow();
        
        var codeCell = CreateDetailCell("[codigo_tarifa]", 1.0f, TextAlignment.MiddleCenter);
        
        var minCell = new XRTableCell { Weight = 1.0f, TextAlignment = TextAlignment.MiddleRight, Padding = new PaddingInfo(4, 4, 0, 0) };
        minCell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "Iif([codigo_tarifa] = 'M' Or [codigo_tarifa] = '', null, [rango_minimo])"));
        minCell.TextFormatString = "{0:n0}";

        var maxCell = new XRTableCell { Weight = 1.0f, TextAlignment = TextAlignment.MiddleRight, Padding = new PaddingInfo(4, 4, 0, 0) };
        maxCell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "Iif([codigo_tarifa] = 'M' Or [codigo_tarifa] = '' Or [rango_maximo] >= 9999999, null, [rango_maximo])"));
        maxCell.TextFormatString = "{0:n0}";

        var conexCell = CreateDetailCell("[conexiones]", 1.20f, TextAlignment.MiddleRight, "{0:n0}");
        var consCell = CreateDetailCell("[consumo_m3]", 1.20f, TextAlignment.MiddleRight, "{0:n0}");
        var valorCell = CreateDetailCell("[valor_agua]", 1.30f, TextAlignment.MiddleRight, "{0:n2}");

        detailRow.Cells.AddRange([codeCell, minCell, maxCell, conexCell, consCell, valorCell]);
        detailTable.Rows.Add(detailRow);
        detailBand.Controls.Add(detailTable);

        var groupFooter = new GroupFooterBand { HeightF = 32f };
        var footerTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 0f, 960f, 32f),
            Borders = BorderSide.Top | BorderSide.Bottom,
            BorderWidth = 1f,
            Font = new DXFont("Times New Roman", 9f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleRight
        };
        var footerRow = new XRTableRow();
        
        var totalLabelCell = new XRTableCell { Weight = 3.0f, TextAlignment = TextAlignment.MiddleLeft, Padding = new PaddingInfo(4, 4, 0, 0) };
        totalLabelCell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "Concat('Total ', [categoria_nombre])"));

        var sumConexCell = new XRTableCell { Weight = 1.20f, TextAlignment = TextAlignment.MiddleRight, Padding = new PaddingInfo(4, 4, 0, 0), TextFormatString = "{0:n0}" };
        sumConexCell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "sumSum([conexiones])"));
        sumConexCell.Summary = new XRSummary { Running = SummaryRunning.Group };

        var sumConsCell = new XRTableCell { Weight = 1.20f, TextAlignment = TextAlignment.MiddleRight, Padding = new PaddingInfo(4, 4, 0, 0), TextFormatString = "{0:n0}" };
        sumConsCell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "sumSum([consumo_m3])"));
        sumConsCell.Summary = new XRSummary { Running = SummaryRunning.Group };

        var sumValorCell = new XRTableCell { Weight = 1.30f, TextAlignment = TextAlignment.MiddleRight, Padding = new PaddingInfo(4, 4, 0, 0), TextFormatString = "{0:n2}" };
        sumValorCell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "sumSum([valor_agua])"));
        sumValorCell.Summary = new XRSummary { Running = SummaryRunning.Group };

        footerRow.Cells.AddRange([totalLabelCell, sumConexCell, sumConsCell, sumValorCell]);
        footerTable.Rows.Add(footerRow);
        groupFooter.Controls.Add(footerTable);

        report.Bands.AddRange([reportHeader, pageHeader, groupHeader, detailBand, groupFooter]);
        return report;
    }

    private XtraReport CreateSumarialTarifasNoMedidoTemplate(string reportCode, string displayName, string? description, DatasetDefinition dataset)
    {
        var report = CreateBaseReport(reportCode, displayName);
        report.Landscape = true;
        report.PaperKind = DevExpress.Drawing.Printing.DXPaperKind.Letter;
        report.Margins = new DXMargins(35, 35, 28, 28);
        report.RequestParameters = dataset.Parameters.Any(x => x.Source == ReportesWebConstants.DatasetParameterValueSource.Report && x.Visible);

        foreach (var parameter in dataset.Parameters)
        {
            var reportParameter = CreateReportParameter(parameter);
            report.Parameters.Add(reportParameter);
        }

        var queryName = string.IsNullOrWhiteSpace(dataset.Code) ? "MainQuery" : dataset.Code.Replace('-', '_');
        var dataSource = CreateRelationalDataSource(dataset, queryName);
        report.ComponentStorage.AddRange([dataSource]);
        report.DataSource = dataSource;
        report.DataMember = queryName;

        var reportHeader = new ReportHeaderBand { HeightF = 95f };
        var pageHeader = new PageHeaderBand { HeightF = 0f };
        
        var companyLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 10f, 960f, 22f),
            Font = new DXFont("Times New Roman", 13f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleCenter
        };
        companyLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[empresa_nombre]"));

        var titleLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 32f, 960f, 22f),
            Font = new DXFont("Times New Roman", 12.5f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleCenter
        };
        titleLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[periodo_titulo]"));

        var generatedDateLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 54f, 960f, 18f),
            Font = new DXFont("Times New Roman", 10.5f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleCenter
        };
        generatedDateLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[fecha_reporte_texto]"));

        reportHeader.Controls.AddRange(
        [
            companyLabel,
            titleLabel,
            generatedDateLabel,
            new XRLabel
            {
                BoundsF = new RectangleF(845f, 6f, 44f, 18f),
                Font = new DXFont("Times New Roman", 10f, DXFontStyle.Bold),
                Text = "PAG.",
                TextAlignment = TextAlignment.MiddleLeft
            },
            new XRPageInfo
            {
                BoundsF = new RectangleF(930f, 6f, 30f, 18f),
                Font = new DXFont("Times New Roman", 10f),
                PageInfo = PageInfo.Number,
                TextAlignment = TextAlignment.MiddleRight
            }
        ]);

        var groupHeader = new GroupHeaderBand { HeightF = 55f };
        groupHeader.GroupFields.Add(new GroupField("categoria_nombre", XRColumnSortOrder.Ascending));
        groupHeader.KeepTogether = true;

        var categoryLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 5f, 960f, 22f),
            Font = new DXFont("Times New Roman", 11f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleLeft
        };
        categoryLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[categoria_nombre]"));
        groupHeader.Controls.Add(categoryLabel);

        var headerTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 27f, 960f, 28f),
            Borders = BorderSide.Top | BorderSide.Bottom,
            BorderWidth = 1f,
            Font = new DXFont("Times New Roman", 8.5f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleCenter
        };
        var headerRow = new XRTableRow();
        headerRow.Cells.AddRange(
        [
            CreateHeaderCell("Código", 1.0f),
            CreateHeaderCell("Descripción", 2.50f),
            CreateHeaderCell("No. Clientes", 1.20f),
            CreateHeaderCell("Valor Agua", 1.30f)
        ]);
        headerTable.Rows.Add(headerRow);
        groupHeader.Controls.Add(headerTable);

        var detailBand = new DetailBand { HeightF = 26f };
        var detailTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 0f, 960f, 26f),
            BorderWidth = 0f,
            Font = new DXFont("Times New Roman", 9.5f),
            TextAlignment = TextAlignment.MiddleLeft
        };
        var detailRow = new XRTableRow();
        
        var codeCell = CreateDetailCell("[codigo_tarifa]", 1.0f, TextAlignment.MiddleCenter);
        var descCell = CreateDetailCell("[descripcion_tarifa]", 2.50f, TextAlignment.MiddleLeft);
        var conexCell = CreateDetailCell("[clientes]", 1.20f, TextAlignment.MiddleRight, "{0:n0}");
        var valorCell = CreateDetailCell("[valor_agua]", 1.30f, TextAlignment.MiddleRight, "{0:n2}");

        detailRow.Cells.AddRange([codeCell, descCell, conexCell, valorCell]);
        detailTable.Rows.Add(detailRow);
        detailBand.Controls.Add(detailTable);

        var groupFooter = new GroupFooterBand { HeightF = 32f };
        var footerTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 0f, 960f, 32f),
            Borders = BorderSide.Top | BorderSide.Bottom,
            BorderWidth = 1f,
            Font = new DXFont("Times New Roman", 9f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleRight
        };
        var footerRow = new XRTableRow();
        
        var totalLabelCell = new XRTableCell { Weight = 3.50f, TextAlignment = TextAlignment.MiddleLeft, Padding = new PaddingInfo(4, 4, 0, 0) };
        totalLabelCell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "Concat('Total ', [categoria_nombre])"));

        var sumConexCell = new XRTableCell { Weight = 1.20f, TextAlignment = TextAlignment.MiddleRight, Padding = new PaddingInfo(4, 4, 0, 0), TextFormatString = "{0:n0}" };
        sumConexCell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "sumSum([clientes])"));
        sumConexCell.Summary = new XRSummary { Running = SummaryRunning.Group };

        var sumValorCell = new XRTableCell { Weight = 1.30f, TextAlignment = TextAlignment.MiddleRight, Padding = new PaddingInfo(4, 4, 0, 0), TextFormatString = "{0:n2}" };
        sumValorCell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "sumSum([valor_agua])"));
        sumValorCell.Summary = new XRSummary { Running = SummaryRunning.Group };

        footerRow.Cells.AddRange([totalLabelCell, sumConexCell, sumValorCell]);
        footerTable.Rows.Add(footerRow);
        groupFooter.Controls.Add(footerTable);

        report.Bands.AddRange([reportHeader, pageHeader, groupHeader, detailBand, groupFooter]);
        return report;
    }

    private XtraReport CreateSaldoClientesCategoriaCobranzaTemplate(string reportCode, string displayName, string? description, DatasetDefinition dataset)
    {
        var report = CreateBaseReport(reportCode, displayName);
        report.Landscape = true;
        report.PaperKind = DevExpress.Drawing.Printing.DXPaperKind.Letter;
        report.Margins = new DXMargins(35, 35, 28, 28);
        report.RequestParameters = dataset.Parameters.Any(x => x.Source == ReportesWebConstants.DatasetParameterValueSource.Report && x.Visible);

        foreach (var parameter in dataset.Parameters)
        {
            var reportParameter = CreateReportParameter(parameter);
            report.Parameters.Add(reportParameter);
        }

        var queryName = string.IsNullOrWhiteSpace(dataset.Code) ? "MainQuery" : dataset.Code.Replace('-', '_');
        var dataSource = CreateRelationalDataSource(dataset, queryName);
        report.ComponentStorage.AddRange([dataSource]);
        report.DataSource = dataSource;
        report.DataMember = queryName;

        var reportHeader = new ReportHeaderBand { HeightF = 100f };
        var pageHeader = new PageHeaderBand { HeightF = 60f };
        var detailBand = new DetailBand { HeightF = 26f };
        var reportFooter = new ReportFooterBand { HeightF = 32f };

        var companyLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 14f, 1030f, 24f),
            Font = new DXFont("Times New Roman", 13f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleCenter
        };
        companyLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[empresa_nombre]"));

        var titleLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 38f, 1030f, 22f),
            Font = new DXFont("Times New Roman", 12.5f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleCenter
        };
        titleLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[periodo_titulo]"));

        var generatedDateLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 60f, 1030f, 20f),
            Font = new DXFont("Times New Roman", 11f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleCenter
        };
        generatedDateLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[fecha_reporte_texto]"));

        reportHeader.Controls.AddRange(
        [
            companyLabel,
            titleLabel,
            generatedDateLabel,
            new XRLabel
            {
                BoundsF = new RectangleF(915f, 8f, 44f, 18f),
                Font = new DXFont("Times New Roman", 10f, DXFontStyle.Bold),
                Text = "PAG.",
                TextAlignment = TextAlignment.MiddleLeft
            },
            new XRPageInfo
            {
                BoundsF = new RectangleF(1000f, 8f, 30f, 18f),
                Font = new DXFont("Times New Roman", 10f),
                PageInfo = PageInfo.Number,
                TextAlignment = TextAlignment.MiddleRight
            }
        ]);

        // Major headers row (row 1)
        var headerTable1 = new XRTable
        {
            BoundsF = new RectangleF(0f, 0f, 1030f, 26f),
            Borders = BorderSide.Top | BorderSide.Bottom,
            BorderWidth = 1f,
            Font = new DXFont("Times New Roman", 9.5f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleCenter
        };
        var headerRow1 = new XRTableRow();
        headerRow1.Cells.AddRange(
        [
            new XRTableCell { Text = "", Weight = 2.30f },
            new XRTableCell { Text = "CLIENTES CON MEDIDOR", Weight = 3.60f, Borders = BorderSide.Top | BorderSide.Bottom | BorderSide.Right },
            new XRTableCell { Text = "CLIENTES SIN MEDIDOR", Weight = 2.90f, Borders = BorderSide.Top | BorderSide.Bottom | BorderSide.Right },
            new XRTableCell { Text = "TOTAL ACUEDUCTO", Weight = 2.90f }
        ]);
        headerTable1.Rows.Add(headerRow1);

        // Sub-headers row (row 2)
        var headerTable2 = new XRTable
        {
            BoundsF = new RectangleF(0f, 26f, 1030f, 34f),
            Borders = BorderSide.Bottom,
            BorderWidth = 1f,
            Font = new DXFont("Times New Roman", 8.5f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleCenter
        };
        var headerRow2 = new XRTableRow();
        headerRow2.Cells.AddRange(
        [
            CreateHeaderCell("Cate\ngoria", 0.50f),
            CreateHeaderCell("Descripcion Categoria", 1.80f),
            // Con Medidor
            CreateHeaderCell("Cantidad", 0.70f),
            CreateHeaderCell("Facturacion\nMes", 1.00f),
            CreateHeaderCell("Saldo\nAcumulado", 1.20f),
            CreateHeaderCell("Consumo\nM3", 0.70f),
            // Sin Medidor
            CreateHeaderCell("Cantidad", 0.70f),
            CreateHeaderCell("Facturacion\nMes", 1.00f),
            CreateHeaderCell("Saldo\nAcumulado", 1.20f),
            // Total
            CreateHeaderCell("Cantidad", 0.70f),
            CreateHeaderCell("Facturacion\nMes", 1.00f),
            CreateHeaderCell("Saldo\nAcumulado", 1.20f)
        ]);
        headerTable2.Rows.Add(headerRow2);
        
        pageHeader.Controls.AddRange([headerTable1, headerTable2]);

        var detailTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 0f, 1030f, 26f),
            BorderWidth = 0f,
            Font = new DXFont("Times New Roman", 9.5f),
            TextAlignment = TextAlignment.MiddleLeft
        };
        var detailRow = new XRTableRow();
        detailRow.Cells.AddRange(
        [
            CreateDetailCell("[categoria_orden]", 0.50f, TextAlignment.MiddleLeft),
            CreateDetailCell("[categoria]", 1.80f, TextAlignment.MiddleLeft),
            
            CreateDetailCell("[cant_con_medidor]", 0.70f, TextAlignment.MiddleRight, "{0:n0}"),
            CreateDetailCell("[facturacion_con_medidor]", 1.00f, TextAlignment.MiddleRight, "{0:n2}"),
            CreateDetailCell("[saldo_con_medidor]", 1.20f, TextAlignment.MiddleRight, "{0:n2}"),
            CreateDetailCell("[consumo_con_medidor]", 0.70f, TextAlignment.MiddleRight, "{0:n0}"),

            CreateDetailCell("[cant_sin_medidor]", 0.70f, TextAlignment.MiddleRight, "{0:n0}"),
            CreateDetailCell("[facturacion_sin_medidor]", 1.00f, TextAlignment.MiddleRight, "{0:n2}"),
            CreateDetailCell("[saldo_sin_medidor]", 1.20f, TextAlignment.MiddleRight, "{0:n2}"),

            CreateDetailCell("[cant_total]", 0.70f, TextAlignment.MiddleRight, "{0:n0}"),
            CreateDetailCell("[facturacion_total]", 1.00f, TextAlignment.MiddleRight, "{0:n2}"),
            CreateDetailCell("[saldo_total]", 1.20f, TextAlignment.MiddleRight, "{0:n2}")
        ]);
        detailTable.Rows.Add(detailRow);
        detailBand.Controls.Add(detailTable);

        var footerTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 0f, 1030f, 32f),
            Borders = BorderSide.Top | BorderSide.Bottom,
            BorderWidth = 1f,
            Font = new DXFont("Times New Roman", 9f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleRight
        };
        var footerRow = new XRTableRow();
        footerRow.Cells.AddRange(
        [
            new XRTableCell { Text = "", Weight = 0.50f },
            new XRTableCell { Text = "TOTAL", Weight = 1.80f, TextAlignment = TextAlignment.MiddleLeft },
            
            CreateSummaryCell("[cant_con_medidor]", 0.70f, "{0:n0}"),
            CreateSummaryCell("[facturacion_con_medidor]", 1.00f, "{0:n2}"),
            CreateSummaryCell("[saldo_con_medidor]", 1.20f, "{0:n2}"),
            CreateSummaryCell("[consumo_con_medidor]", 0.70f, "{0:n0}"),

            CreateSummaryCell("[cant_sin_medidor]", 0.70f, "{0:n0}"),
            CreateSummaryCell("[facturacion_sin_medidor]", 1.00f, "{0:n2}"),
            CreateSummaryCell("[saldo_sin_medidor]", 1.20f, "{0:n2}"),

            CreateSummaryCell("[cant_total]", 0.70f, "{0:n0}"),
            CreateSummaryCell("[facturacion_total]", 1.00f, "{0:n2}"),
            CreateSummaryCell("[saldo_total]", 1.20f, "{0:n2}")
        ]);
        footerTable.Rows.Add(footerRow);
        reportFooter.Controls.Add(footerTable);

        report.Bands.AddRange([reportHeader, pageHeader, detailBand, reportFooter]);
        return report;
    }

    private static DatasetDefinition BuildDefaultFacturaTicketDataset()
        => new(
            ReportesWebConstants.CodigoDatasetFacturaTicket,
            "Dataset factura (ticket)",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetFacturaTicket,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            BuildDefaultFacturaTicketDatasetParameters());

    private static IReadOnlyList<DatasetParameterDefinition> BuildDefaultFacturaTicketDatasetParameters()
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
                "FacturaId",
                "p_factura_id",
                "Factura (id interno)",
                ReportesWebConstants.DatasetParameterDataType.Int64,
                ReportesWebConstants.DatasetParameterValueSource.Report,
                null,
                true,
                false,
                true,
                10)
        ];

    /// <summary>
    /// Plantilla inicial del formato de factura en ticket (ancho 315, estilo del
    /// ticket de la app de lectores y del recibo de caja). Como el resto de
    /// plantillas, es solo el borrador de arranque: el diseño se edita en el
    /// designer web y la versión publicada vive en rep_reporte_layout.
    /// </summary>
    private XtraReport CreateFacturaTicketTemplate(string reportCode, string displayName, string? description, DatasetDefinition dataset)
    {
        const float contentWidth = 275f;

        var report = CreateBaseReport(reportCode, displayName);
        report.PaperKind = DevExpress.Drawing.Printing.DXPaperKind.Custom;
        report.PageWidth = 315;
        report.PageHeight = 2000;
        report.Margins = new DXMargins(20, 20, 10, 10);
        report.RequestParameters = dataset.Parameters.Any(x => x.Source == ReportesWebConstants.DatasetParameterValueSource.Report && x.Visible);

        foreach (var parameter in dataset.Parameters)
        {
            report.Parameters.Add(CreateReportParameter(parameter));
        }

        var queryName = dataset.Code.Replace('-', '_');
        var dataSource = CreateRelationalDataSource(dataset, queryName);
        report.ComponentStorage.AddRange([dataSource]);
        report.DataSource = dataSource;
        report.DataMember = queryName;

        static XRLabel TicketCentered(string expression, float y, float fontSize, bool bold = false, float height = 14f)
        {
            var label = new XRLabel
            {
                BoundsF = new RectangleF(0f, y, contentWidth, height),
                Font = new DXFont("Courier New", fontSize, bold ? DXFontStyle.Bold : DXFontStyle.Regular),
                TextAlignment = TextAlignment.MiddleCenter,
                WordWrap = true,
                Multiline = true,
                CanGrow = true
            };
            label.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", expression));
            return label;
        }

        static XRLine TicketDashed(float y)
            => new()
            {
                BoundsF = new RectangleF(0f, y, contentWidth, 4f),
                LineStyle = DevExpress.Drawing.DXDashStyle.Dash,
                ForeColor = Color.Gray
            };

        static float TicketRow(Band band, string label, string valueExpression, float y)
        {
            band.Controls.Add(new XRLabel
            {
                BoundsF = new RectangleF(0f, y, 112f, 14f),
                Font = new DXFont("Courier New", 8f),
                Text = label,
                ForeColor = Color.DimGray
            });
            var value = new XRLabel
            {
                BoundsF = new RectangleF(114f, y, 161f, 14f),
                Font = new DXFont("Courier New", 8f, DXFontStyle.Bold),
                TextAlignment = TextAlignment.MiddleRight,
                WordWrap = true,
                Multiline = true,
                CanGrow = true
            };
            value.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", valueExpression));
            band.Controls.Add(value);
            return y + 14f;
        }

        // Cabecera: emisor + bloque fiscal SAR + cliente + lectura.
        var header = new ReportHeaderBand();
        float y = 4f;

        header.Controls.Add(TicketCentered("[empresa_nombre]", y, 10f, bold: true)); y += 16f;
        header.Controls.Add(TicketCentered("Concat('RTN: ', [empresa_rtn])", y, 8f)); y += 14f;
        header.Controls.Add(TicketCentered("[empresa_direccion]", y, 7f, height: 24f)); y += 26f;
        header.Controls.Add(TicketCentered("Iif(IsNullOrEmpty([empresa_telefono]), '', Concat('Tel: ', [empresa_telefono]))", y, 7f)); y += 12f;

        header.Controls.Add(TicketDashed(y)); y += 10f;
        var tituloFactura = TicketCentered("'FACTURA'", y, 11f, bold: true); y += 18f;
        header.Controls.Add(tituloFactura);
        header.Controls.Add(TicketCentered("[numero_factura]", y, 10f, bold: true)); y += 16f;

        header.Controls.Add(TicketCentered("Iif(IsNullOrEmpty([codigo_cai]), '', Concat('CAI: ', [codigo_cai]))", y, 7f, height: 24f)); y += 26f;
        header.Controls.Add(TicketCentered("Iif(IsNullOrEmpty([rango_autorizado]), '', Concat('Rango autorizado: ', [rango_autorizado]))", y, 7f, height: 24f)); y += 26f;
        header.Controls.Add(TicketCentered("Iif(IsNull([fecha_limite_emision]), '', FormatString('Fecha limite de emision: {0:dd/MM/yyyy}', [fecha_limite_emision]))", y, 7f)); y += 12f;

        header.Controls.Add(TicketDashed(y)); y += 10f;

        y = TicketRow(header, "Fecha Emision:", "FormatString('{0:dd/MM/yyyy}', [fecha_emision])", y);
        y = TicketRow(header, "Fecha Vence :", "FormatString('{0:dd/MM/yyyy}', [fecha_vence])", y);
        y = TicketRow(header, "Periodo     :", "[periodo]", y);
        y = TicketRow(header, "Cuenta No.  :", "[cliente_clave]", y);
        y = TicketRow(header, "Cliente     :", "[cliente_nombre]", y);
        y = TicketRow(header, "RTN Cliente :", "[cliente_rtn]", y);

        var direccionCliente = new XRLabel
        {
            BoundsF = new RectangleF(0f, y, contentWidth, 26f),
            Font = new DXFont("Courier New", 8f),
            WordWrap = true,
            Multiline = true,
            CanGrow = true
        };
        direccionCliente.ExpressionBindings.Add(new ExpressionBinding(
            "BeforePrint", "Text",
            "Iif(IsNullOrEmpty([cliente_direccion]), '', Concat('Direccion: ', [cliente_direccion]))"));
        header.Controls.Add(direccionCliente);
        y += 30f;

        header.Controls.Add(TicketDashed(y)); y += 10f;

        y = TicketRow(header, "Medidor     :", "[medidor]", y);
        y = TicketRow(header, "Lect. Anter.:", "FormatString('{0:n2}', [lectura_anterior])", y);
        y = TicketRow(header, "Lect. Actual:", "FormatString('{0:n2}', [lectura_actual])", y);
        y = TicketRow(header, "Consumo m3  :", "FormatString('{0:n2}', [consumo])", y);
        y = TicketRow(header, "Condicion   :", "[condicion]", y);
        y = TicketRow(header, "Fecha Lect. :", "FormatString('{0:dd/MM/yyyy}', [fecha_lectura])", y);

        header.Controls.Add(TicketDashed(y)); y += 10f;
        header.HeightF = y;

        // Detalle: una fila por linea de servicio.
        var detailBand = new DetailBand { HeightF = 20f };
        var detailTable = new XRTable { BoundsF = new RectangleF(0f, 0f, contentWidth, 20f) };
        detailTable.BeginInit();
        var detailRow = new XRTableRow();

        var celdaDescripcion = new XRTableCell
        {
            WidthF = 150f,
            TextAlignment = TextAlignment.MiddleLeft,
            Font = new DXFont("Courier New", 8f),
            Borders = BorderSide.All,
            BorderWidth = 0.5f,
            BorderColor = Color.LightGray
        };
        celdaDescripcion.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[linea_descripcion]"));

        var celdaMoneda = new XRTableCell
        {
            WidthF = 30f,
            TextAlignment = TextAlignment.MiddleCenter,
            Font = new DXFont("Courier New", 8f),
            Borders = BorderSide.All,
            BorderWidth = 0.5f,
            BorderColor = Color.LightGray
        };
        celdaMoneda.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[linea_moneda]"));

        var celdaMonto = new XRTableCell
        {
            WidthF = 95f,
            TextAlignment = TextAlignment.MiddleRight,
            Font = new DXFont("Courier New", 8f),
            TextFormatString = "{0:n2}",
            Borders = BorderSide.All,
            BorderWidth = 0.5f,
            BorderColor = Color.LightGray
        };
        celdaMonto.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[linea_monto]"));

        detailRow.Cells.AddRange([celdaDescripcion, celdaMoneda, celdaMonto]);
        detailTable.Rows.Add(detailRow);
        detailTable.EndInit();
        detailBand.Controls.Add(detailTable);

        // Pie: total y leyendas.
        var footer = new ReportFooterBand();
        float fy = 0f;

        footer.Controls.Add(new XRLabel
        {
            BoundsF = new RectangleF(0f, fy, 180f, 18f),
            Font = new DXFont("Courier New", 9f, DXFontStyle.Bold),
            Text = "Total L.:",
            TextAlignment = TextAlignment.MiddleRight
        });
        var totalLabel = new XRLabel
        {
            BoundsF = new RectangleF(180f, fy, 95f, 18f),
            Font = new DXFont("Courier New", 9f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleRight
        };
        totalLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "FormatString('{0:n2}', [total])"));
        footer.Controls.Add(totalLabel);
        fy += 22f;

        footer.Controls.Add(TicketDashed(fy)); fy += 10f;
        fy = TicketRow(footer, "Lector      :", "[lector]", fy) + 2f;
        footer.Controls.Add(TicketDashed(fy)); fy += 10f;

        var leyenda1 = TicketCentered("'La factura es beneficio de todos.'", fy, 8f, bold: true); fy += 14f;
        var leyenda2 = TicketCentered("'¡Exijala!'", fy, 8f, bold: true); fy += 16f;
        var leyenda3 = TicketCentered("'Original: Cliente'", fy, 7f); fy += 14f;
        footer.Controls.AddRange([leyenda1, leyenda2, leyenda3]);
        footer.HeightF = fy + 4f;

        report.Bands.AddRange([header, detailBand, footer]);
        return report;
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


