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
        report.Margins = new DXMargins(50, 50, 35, 35);
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

        const float contentWidth = 750f;
        const float descriptionWidth = 420f;
        const float amountWidth = 145f;
        const float previousAmountWidth = 145f;

        var reportHeader = new ReportHeaderBand { HeightF = 112f };
        var pageHeader = new PageHeaderBand { HeightF = 30f };
        var groupHeader = new GroupHeaderBand { HeightF = 26f, RepeatEveryPage = true };
        var detailBand = new DetailBand { HeightF = 24f };
        var groupFooter = new GroupFooterBand { HeightF = 30f };
        var reportFooter = new ReportFooterBand { HeightF = 32f };
        var pageFooter = new PageFooterBand { HeightF = 24f };

        var companyLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 0f, contentWidth, 20f),
            Font = new DXFont("Arial", 10f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleCenter
        };
        companyLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", $"?{ReportCompanyHeaderParameters.CompanyName}"));

        var titleLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 28f, contentWidth, 24f),
            Font = new DXFont("Arial", 12f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleLeft
        };
        titleLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "FormatString('BALANCE GENERAL AL {0:dd/MM/yyyy}', ?FechaCorte)"));

        var infoLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 54f, contentWidth, 16f),
            Font = new DXFont("Arial", 8f),
            ForeColor = Color.DimGray,
            TextAlignment = TextAlignment.MiddleLeft
        };
        infoLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", $"?{ReportCompanyHeaderParameters.CompanyInfoLine}"));

        var addressLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 70f, contentWidth, 16f),
            Font = new DXFont("Arial", 8f),
            ForeColor = Color.DimGray,
            TextAlignment = TextAlignment.MiddleLeft
        };
        addressLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", $"?{ReportCompanyHeaderParameters.CompanyAddress}"));

        reportHeader.Controls.AddRange([companyLabel, titleLabel, infoLabel, addressLabel]);

        var headerTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 0f, contentWidth, 28f),
            BorderWidth = 0f,
            Font = new DXFont("Arial", 9f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleCenter
        };
        var headerRow = new XRTableRow();
        headerRow.Cells.AddRange(
        [
            new XRTableCell { Text = string.Empty, WidthF = descriptionWidth, Weight = descriptionWidth },
            new XRTableCell { Text = "Ejercicio Actual", WidthF = amountWidth, Weight = amountWidth },
            new XRTableCell { Text = "Ejercicio Anterior", WidthF = previousAmountWidth, Weight = previousAmountWidth }
        ]);
        headerTable.Rows.Add(headerRow);
        pageHeader.Controls.Add(headerTable);

        groupHeader.GroupFields.Add(new GroupField("seccion_orden"));
        groupHeader.GroupFields.Add(new GroupField("seccion_nombre"));

        var sectionLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 0f, contentWidth, 24f),
            Font = new DXFont("Arial", 10f, DXFontStyle.Bold),
            Padding = new PaddingInfo(0, 0, 0, 0),
            TextAlignment = TextAlignment.MiddleLeft
        };
        sectionLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "Iif([seccion_nombre] = 'PATRIMONIO', 'PATRIMONIO Y RESERVAS', [seccion_nombre])"));
        groupHeader.Controls.Add(sectionLabel);

        var detailTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 0f, contentWidth, 24f),
            BorderWidth = 0f,
            Font = new DXFont("Arial", 9f),
            OddStyleName = "FinancialStateOddStyle"
        };
        var detailRow = new XRTableRow();
        detailRow.Cells.AddRange(
        [
            new XRTableCell
            {
                WidthF = descriptionWidth,
                Weight = descriptionWidth,
                Padding = new PaddingInfo(0, 8, 0, 0),
                TextAlignment = TextAlignment.MiddleLeft
            },
            CreateFinancialStatementAmountCell("[monto]", amountWidth),
            CreateFinancialStatementAmountCell("[monto_anterior]", previousAmountWidth)
        ]);
        detailRow.Cells[0].ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[descripcion_mostrar]"));
        detailTable.Rows.Add(detailRow);
        detailBand.Controls.Add(detailTable);

        var groupFooterTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 2f, contentWidth, 26f),
            BorderWidth = 0f,
            Font = new DXFont("Arial", 9f, DXFontStyle.Bold)
        };
        var groupFooterRow = new XRTableRow();
        var groupTotalLabel = new XRTableCell
        {
            WidthF = descriptionWidth,
            Weight = descriptionWidth,
            TextAlignment = TextAlignment.MiddleLeft
        };
        groupTotalLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "Concat('TOTAL DEL ', [seccion_nombre])"));

        var groupTotalValue = CreateFinancialStatementSummaryCell("[monto]", amountWidth, SummaryRunning.Group);
        var groupTotalPreviousValue = CreateFinancialStatementSummaryCell("[monto_anterior]", previousAmountWidth, SummaryRunning.Group);
        groupFooterRow.Cells.AddRange([groupTotalLabel, groupTotalValue, groupTotalPreviousValue]);
        groupFooterTable.Rows.Add(groupFooterRow);
        groupFooter.Controls.Add(groupFooterTable);

        var reportFooterTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 2f, contentWidth, 28f),
            BorderWidth = 0f,
            Font = new DXFont("Arial", 9f, DXFontStyle.Bold)
        };
        var reportFooterRow = new XRTableRow();
        reportFooterRow.Cells.AddRange(
        [
            new XRTableCell
            {
                Text = "TOTAL PASIVO Y PATRIMONIO",
                WidthF = descriptionWidth,
                Weight = descriptionWidth,
                TextAlignment = TextAlignment.MiddleLeft
            },
            CreateFinancialStatementSummaryCell("Iif([clase] >= 3 And [clase] <= 5, [monto], 0)", amountWidth, SummaryRunning.Report),
            CreateFinancialStatementSummaryCell("Iif([clase] >= 3 And [clase] <= 5, [monto_anterior], 0)", previousAmountWidth, SummaryRunning.Report)
        ]);
        reportFooterTable.Rows.Add(reportFooterRow);
        reportFooter.Controls.Add(reportFooterTable);

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
                BoundsF = new RectangleF(550f, 0f, 200f, 20f),
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
                BackColor = Color.White
            }
        ]);

        report.Bands.AddRange([reportHeader, pageHeader, groupHeader, detailBand, groupFooter, reportFooter, pageFooter]);
        return report;
    }

    private XtraReport CreateEstadoResultadosTemplate(string reportCode, string displayName, string? description, DatasetDefinition dataset)
    {
        var report = CreateBaseReport(reportCode, displayName);
        report.PaperKind = DevExpress.Drawing.Printing.DXPaperKind.Letter;
        report.Margins = new DXMargins(50, 50, 35, 35);
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

        const float contentWidth = 750f;
        const float descriptionWidth = 420f;
        const float amountWidth = 145f;
        const float previousAmountWidth = 145f;

        var reportHeader = new ReportHeaderBand { HeightF = 92f };
        var pageHeader = new PageHeaderBand { HeightF = 30f };
        var groupHeader = new GroupHeaderBand { HeightF = 26f, RepeatEveryPage = true };
        var detailBand = new DetailBand { HeightF = 24f };
        var pageFooter = new PageFooterBand { HeightF = 24f };

        var companyLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 0f, contentWidth, 20f),
            Font = new DXFont("Arial", 10f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleCenter
        };
        companyLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[empresa_nombre]"));

        var titleLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 24f, contentWidth, 24f),
            Font = new DXFont("Arial", 12f, DXFontStyle.Bold),
            Text = "ESTADO DE RESULTADOS",
            TextAlignment = TextAlignment.MiddleLeft
        };

        var periodLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 50f, contentWidth, 16f),
            Font = new DXFont("Arial", 8f),
            ForeColor = Color.DimGray,
            TextAlignment = TextAlignment.MiddleLeft
        };
        periodLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "FormatString('Del {0:dd/MM/yyyy} al {1:dd/MM/yyyy}', ?FechaDesde, ?FechaHasta)"));

        var infoLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 66f, contentWidth, 16f),
            Font = new DXFont("Arial", 8f),
            ForeColor = Color.DimGray,
            TextAlignment = TextAlignment.MiddleLeft
        };
        infoLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "FormatString('{0} | RTN: {1} | Tel: {2} | Email: {3}', [empresa_nombre_legal], [empresa_rtn], [empresa_telefono], [empresa_email])"));

        var addressLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 82f, contentWidth, 16f),
            Font = new DXFont("Arial", 8f),
            ForeColor = Color.DimGray,
            TextAlignment = TextAlignment.MiddleLeft
        };
        addressLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[empresa_direccion]"));

        reportHeader.Controls.AddRange([companyLabel, titleLabel, periodLabel, infoLabel, addressLabel]);

        var headerTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 0f, contentWidth, 28f),
            BorderWidth = 0f,
            Font = new DXFont("Arial", 9f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleCenter
        };
        var headerRow = new XRTableRow();
        headerRow.Cells.AddRange(
        [
            new XRTableCell { Text = string.Empty, WidthF = descriptionWidth, Weight = descriptionWidth },
            new XRTableCell { Text = "Ejercicio Actual", WidthF = amountWidth, Weight = amountWidth },
            new XRTableCell { Text = "Ejercicio Anterior", WidthF = previousAmountWidth, Weight = previousAmountWidth }
        ]);
        headerTable.Rows.Add(headerRow);
        pageHeader.Controls.Add(headerTable);

        groupHeader.GroupFields.Add(new GroupField("seccion_orden"));
        groupHeader.GroupFields.Add(new GroupField("seccion_nombre"));

        var sectionLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 0f, contentWidth, 24f),
            Font = new DXFont("Arial", 10f, DXFontStyle.Bold),
            Padding = new PaddingInfo(0, 0, 0, 0),
            TextAlignment = TextAlignment.MiddleLeft
        };
        sectionLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[seccion_nombre]"));
        groupHeader.Controls.Add(sectionLabel);

        var detailTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 0f, contentWidth, 24f),
            BorderWidth = 0f,
            Font = new DXFont("Arial", 8.5f),
            OddStyleName = "FinancialResultsOddStyle"
        };
        var detailRow = new XRTableRow();
        detailRow.Cells.AddRange(
        [
            new XRTableCell
            {
                WidthF = descriptionWidth,
                Weight = descriptionWidth,
                Padding = new PaddingInfo(0, 8, 0, 0),
                TextAlignment = TextAlignment.MiddleLeft
            },
            CreateFinancialStatementAmountCell("[monto]", amountWidth),
            CreateFinancialStatementAmountCell("[monto_anterior]", previousAmountWidth)
        ]);
        detailRow.Cells[0].ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[descripcion_mostrar]"));
        detailTable.Rows.Add(detailRow);
        detailBand.Controls.Add(detailTable);

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
                BoundsF = new RectangleF(550f, 0f, 200f, 20f),
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
                BackColor = Color.White
            }
        ]);

        report.Bands.AddRange([reportHeader, pageHeader, groupHeader, detailBand, pageFooter]);
        return report;
    }

    private XtraReport CreateEstadoFlujoEfectivoTemplate(string reportCode, string displayName, string? description, DatasetDefinition dataset)
    {
        var report = CreateBaseReport(reportCode, displayName);
        report.PaperKind = DevExpress.Drawing.Printing.DXPaperKind.Letter;
        report.Margins = new DXMargins(50, 50, 35, 35);
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

        const float contentWidth = 750f;
        const float descriptionWidth = 420f;
        const float amountWidth = 145f;
        const float previousAmountWidth = 145f;

        var reportHeader = new ReportHeaderBand { HeightF = 92f };
        var pageHeader = new PageHeaderBand { HeightF = 30f };
        var groupHeader = new GroupHeaderBand { HeightF = 26f, RepeatEveryPage = true };
        var detailBand = new DetailBand { HeightF = 24f };
        var reportFooter = new ReportFooterBand { HeightF = 40f };
        var pageFooter = new PageFooterBand { HeightF = 24f };

        var companyLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 0f, contentWidth, 20f),
            Font = new DXFont("Arial", 10f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleCenter
        };
        companyLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[empresa_nombre]"));

        var titleLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 24f, contentWidth, 24f),
            Font = new DXFont("Arial", 12f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleLeft
        };
        titleLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "FormatString('ESTADO DE FLUJOS DE EFECTIVO DEL {0:dd/MM/yyyy} AL {1:dd/MM/yyyy}', ?FechaDesde, ?FechaHasta)"));

        var infoLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 50f, contentWidth, 16f),
            Font = new DXFont("Arial", 8f),
            ForeColor = Color.DimGray,
            TextAlignment = TextAlignment.MiddleLeft
        };
        infoLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "FormatString('{0} | RTN: {1} | Tel: {2} | Email: {3}', [empresa_nombre_legal], [empresa_rtn], [empresa_telefono], [empresa_email])"));

        var addressLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 66f, contentWidth, 16f),
            Font = new DXFont("Arial", 8f),
            ForeColor = Color.DimGray,
            TextAlignment = TextAlignment.MiddleLeft
        };
        addressLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[empresa_direccion]"));

        reportHeader.Controls.AddRange([companyLabel, titleLabel, infoLabel, addressLabel]);

        var headerTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 0f, contentWidth, 28f),
            BorderWidth = 0f,
            Font = new DXFont("Arial", 9f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleCenter
        };
        var headerRow = new XRTableRow();
        headerRow.Cells.AddRange(
        [
            new XRTableCell { Text = string.Empty, WidthF = descriptionWidth, Weight = descriptionWidth },
            new XRTableCell { Text = "Ejercicio Actual", WidthF = amountWidth, Weight = amountWidth },
            new XRTableCell { Text = "Ejercicio Anterior", WidthF = previousAmountWidth, Weight = previousAmountWidth }
        ]);
        headerTable.Rows.Add(headerRow);
        pageHeader.Controls.Add(headerTable);

        groupHeader.GroupFields.Add(new GroupField("seccion_orden"));
        groupHeader.GroupFields.Add(new GroupField("seccion_nombre"));

        var sectionLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 0f, contentWidth, 24f),
            Font = new DXFont("Arial", 10f, DXFontStyle.Bold),
            BackColor = Color.Gainsboro,
            Padding = new PaddingInfo(6, 0, 0, 0),
            TextAlignment = TextAlignment.MiddleLeft
        };
        sectionLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[seccion_nombre]"));
        groupHeader.Controls.Add(sectionLabel);

        var detailTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 0f, contentWidth, 24f),
            BorderWidth = 0f,
            Font = new DXFont("Arial", 8.5f),
            OddStyleName = "FlujoEfectivoOddStyle"
        };
        var detailRow = new XRTableRow();
        detailRow.Cells.AddRange(
        [
            new XRTableCell
            {
                WidthF = descriptionWidth,
                Weight = descriptionWidth,
                Padding = new PaddingInfo(0, 8, 0, 0),
                TextAlignment = TextAlignment.MiddleLeft
            },
            CreateFinancialStatementAmountCell("[monto]", amountWidth),
            CreateFinancialStatementAmountCell("[monto_anterior]", previousAmountWidth)
        ]);
        detailRow.Cells[0].ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[descripcion_mostrar]"));
        detailRow.Cells[0].ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Font.Bold", "[mostrar_subtotal]"));
        detailRow.Cells[0].ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Font.Italic", "[tipo_linea] == 2"));
        detailTable.Rows.Add(detailRow);
        detailBand.Controls.Add(detailTable);

        reportFooter.Controls.AddRange(
        [
            new XRLabel
            {
                BoundsF = new RectangleF(0f, 4f, contentWidth, 16f),
                Font = new DXFont("Arial", 7.5f),
                ForeColor = Color.DimGray,
                Text = "(1) No incluidos en actividades de inversion.",
                TextAlignment = TextAlignment.MiddleLeft
            },
            new XRLabel
            {
                BoundsF = new RectangleF(0f, 20f, contentWidth, 16f),
                Font = new DXFont("Arial", 7.5f),
                ForeColor = Color.DimGray,
                Text = "(2) No incluidos en actividades de financiacion.",
                TextAlignment = TextAlignment.MiddleLeft
            }
        ]);

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
                BoundsF = new RectangleF(550f, 0f, 200f, 20f),
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
                Name = "FlujoEfectivoOddStyle",
                BackColor = Color.White
            }
        ]);

        report.Bands.AddRange([reportHeader, pageHeader, groupHeader, detailBand, reportFooter, pageFooter]);
        return report;
    }

    private XtraReport CreateEstadoCambiosPatrimonioTemplate(string reportCode, string displayName, string? description, DatasetDefinition dataset)
    {
        var report = CreateBaseReport(reportCode, displayName);
        report.PaperKind = DevExpress.Drawing.Printing.DXPaperKind.Letter;
        report.Margins = new DXMargins(50, 50, 35, 35);
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

        const float contentWidth = 750f;
        const float componentWidth = 270f;
        const float amountWidth = 120f;

        var reportHeader = new ReportHeaderBand { HeightF = 92f };
        var pageHeader = new PageHeaderBand { HeightF = 30f };
        var detailBand = new DetailBand { HeightF = 24f };
        var pageFooter = new PageFooterBand { HeightF = 24f };

        var companyLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 0f, contentWidth, 20f),
            Font = new DXFont("Arial", 10f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleCenter
        };
        companyLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[empresa_nombre]"));

        var titleLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 24f, contentWidth, 24f),
            Font = new DXFont("Arial", 12f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleLeft
        };
        titleLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "FormatString('ESTADO DE CAMBIOS EN EL PATRIMONIO DEL {0:dd/MM/yyyy} AL {1:dd/MM/yyyy}', ?FechaDesde, ?FechaHasta)"));

        var infoLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 50f, contentWidth, 16f),
            Font = new DXFont("Arial", 8f),
            ForeColor = Color.DimGray,
            TextAlignment = TextAlignment.MiddleLeft
        };
        infoLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "FormatString('{0} | RTN: {1} | Tel: {2} | Email: {3}', [empresa_nombre_legal], [empresa_rtn], [empresa_telefono], [empresa_email])"));

        var addressLabel = new XRLabel
        {
            BoundsF = new RectangleF(0f, 66f, contentWidth, 16f),
            Font = new DXFont("Arial", 8f),
            ForeColor = Color.DimGray,
            TextAlignment = TextAlignment.MiddleLeft
        };
        addressLabel.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[empresa_direccion]"));

        reportHeader.Controls.AddRange([companyLabel, titleLabel, infoLabel, addressLabel]);

        var headerTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 0f, contentWidth, 28f),
            BorderWidth = 0f,
            BackColor = Color.Gainsboro,
            Font = new DXFont("Arial", 9f, DXFontStyle.Bold),
            TextAlignment = TextAlignment.MiddleCenter
        };
        var headerRow = new XRTableRow();
        headerRow.Cells.AddRange(
        [
            new XRTableCell { Text = "Componente", WidthF = componentWidth, Weight = componentWidth, TextAlignment = TextAlignment.MiddleLeft, Padding = new PaddingInfo(6, 0, 0, 0) },
            new XRTableCell { Text = "Saldo Inicial", WidthF = amountWidth, Weight = amountWidth },
            new XRTableCell { Text = "Aumentos", WidthF = amountWidth, Weight = amountWidth },
            new XRTableCell { Text = "Disminuciones", WidthF = amountWidth, Weight = amountWidth },
            new XRTableCell { Text = "Saldo Final", WidthF = amountWidth, Weight = amountWidth }
        ]);
        headerTable.Rows.Add(headerRow);
        pageHeader.Controls.Add(headerTable);

        var detailTable = new XRTable
        {
            BoundsF = new RectangleF(0f, 0f, contentWidth, 24f),
            BorderWidth = 0f,
            Font = new DXFont("Arial", 8.5f)
        };
        var detailRow = new XRTableRow();
        detailRow.Cells.AddRange(
        [
            new XRTableCell
            {
                WidthF = componentWidth,
                Weight = componentWidth,
                Padding = new PaddingInfo(0, 8, 0, 0),
                TextAlignment = TextAlignment.MiddleLeft
            },
            CreateFinancialStatementAmountCell("[saldo_inicial]", amountWidth),
            CreateFinancialStatementAmountCell("[aumentos]", amountWidth),
            CreateFinancialStatementAmountCell("[disminuciones]", amountWidth),
            CreateFinancialStatementAmountCell("[saldo_final]", amountWidth)
        ]);
        detailRow.Cells[0].ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Text", "[componente]"));
        foreach (XRTableCell cell in detailRow.Cells)
        {
            cell.ExpressionBindings.Add(new ExpressionBinding("BeforePrint", "Font.Bold", "[es_total]"));
        }

        detailTable.Rows.Add(detailRow);
        detailBand.Controls.Add(detailTable);

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
                BoundsF = new RectangleF(550f, 0f, 200f, 20f),
                Font = new DXFont("Arial", 8f),
                PageInfo = PageInfo.NumberOfTotal,
                TextAlignment = TextAlignment.MiddleRight,
                TextFormatString = "Pagina {0} de {1}"
            }
        ]);

        report.Bands.AddRange([reportHeader, pageHeader, detailBand, pageFooter]);
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


