using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Informes;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Reports;

public interface IReportesDatasetService
{
    Task<IReadOnlyList<ReporteDatasetCatalogoItemDto>> ListarAsync(long companyId, CancellationToken ct = default);

    Task<ReporteDatasetDetalleDto?> ObtenerAsync(long companyId, string codigo, CancellationToken ct = default);

    Task<ReporteDatasetDetalleDto> CrearAsync(long companyId, ReporteDatasetCreateDto dto, string actor, bool allowSql, CancellationToken ct = default);

    Task<ReporteDatasetDetalleDto> ActualizarAsync(long companyId, string codigo, ReporteDatasetCreateDto dto, string actor, bool allowSql, CancellationToken ct = default);

    Task EliminarAsync(long companyId, string codigo, CancellationToken ct = default);

    Task<ReporteDatasetPreviewResultDto> ProbarAsync(long companyId, string codigo, ReporteDatasetPreviewRequestDto request, bool allowSql, CancellationToken ct = default);
}

public sealed class ReportesDatasetService : IReportesDatasetService
{
    private const int PreviewRowLimit = 50;
    private const string LegacyObjectDatasetSourceType = "OBJECT";
    private const string LegacyBancosOriginKey = "bancos-transacciones";
    private static readonly Regex ParameterNameRegex = new("^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.Compiled);
    private static readonly Regex IdentifierRegex = new(@"^[A-Za-z][A-Za-z0-9_\.]*$", RegexOptions.Compiled);

    private static readonly DatasetSeed[] DefaultSeeds =
    [
        new(
            ReportesWebConstants.CodigoDatasetBancosTransacciones,
            "Dataset transacciones bancarias",
            "Fuente administrable para el reporte web de transacciones bancarias.",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetBancosTransacciones,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            [
                new DatasetParameterSeed("CompanyId", "p_company_id", "Empresa actual", ReportesWebConstants.DatasetParameterDataType.Int64, ReportesWebConstants.DatasetParameterValueSource.CurrentCompany, null, false, false, true, 0),
                new DatasetParameterSeed("BancoCuentaId", "p_banco_cuenta_id", "Cuenta bancaria", ReportesWebConstants.DatasetParameterDataType.Int64, ReportesWebConstants.DatasetParameterValueSource.Report, null, false, true, false, 10),
                new DatasetParameterSeed("FechaDesde", "p_fecha_desde", "Fecha desde", ReportesWebConstants.DatasetParameterDataType.Date, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, true, false, 20),
                new DatasetParameterSeed("FechaHasta", "p_fecha_hasta", "Fecha hasta", ReportesWebConstants.DatasetParameterDataType.Date, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, true, false, 30),
                new DatasetParameterSeed("IncluirAnuladas", "p_incluir_anuladas", "Incluir anuladas", ReportesWebConstants.DatasetParameterDataType.Boolean, ReportesWebConstants.DatasetParameterValueSource.Report, "false", true, false, false, 40)
            ]),
        new(
            ReportesWebConstants.CodigoDatasetBalanceComprobacion,
            "Dataset balance de comprobacion",
            "Fuente base para balance de comprobacion y reportes financieros segun estructura ERSAPS.",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetBalanceComprobacion,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            [
                new DatasetParameterSeed("CompanyId", "p_company_id", "Empresa actual", ReportesWebConstants.DatasetParameterDataType.Int64, ReportesWebConstants.DatasetParameterValueSource.CurrentCompany, null, false, false, true, 0),
                new DatasetParameterSeed("FechaDesde", "p_fecha_desde", "Fecha desde", ReportesWebConstants.DatasetParameterDataType.Date, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, false, true, 10),
                new DatasetParameterSeed("FechaHasta", "p_fecha_hasta", "Fecha hasta", ReportesWebConstants.DatasetParameterDataType.Date, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, false, true, 20),
                new DatasetParameterSeed("IncluirSinMovimiento", "p_incluir_sin_movimiento", "Incluir cuentas sin movimiento", ReportesWebConstants.DatasetParameterDataType.Boolean, ReportesWebConstants.DatasetParameterValueSource.Report, "false", true, false, false, 30)
            ]),
        new(
            ReportesWebConstants.CodigoDatasetEstadoSituacionFinanciera,
            "Dataset estado de situacion financiera",
            "Fuente configurada por empresa para el estado de situacion financiera desde con_configuracion_balance.",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetEstadoSituacionFinanciera,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            [
                new DatasetParameterSeed("CompanyId", "p_company_id", "Empresa actual", ReportesWebConstants.DatasetParameterDataType.Int64, ReportesWebConstants.DatasetParameterValueSource.CurrentCompany, null, false, false, true, 0),
                new DatasetParameterSeed("FechaCorte", "p_fecha_corte", "Fecha de corte", ReportesWebConstants.DatasetParameterDataType.Date, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, false, true, 10)
            ]),
        new(
            ReportesWebConstants.CodigoDatasetEstadoResultados,
            "Dataset estado de resultados",
            "Fuente configurada por empresa para el estado de resultados desde con_configuracion_linea_resultado.",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetEstadoResultados,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            [
                new DatasetParameterSeed("CompanyId", "p_company_id", "Empresa actual", ReportesWebConstants.DatasetParameterDataType.Int64, ReportesWebConstants.DatasetParameterValueSource.CurrentCompany, null, false, false, true, 0),
                new DatasetParameterSeed("FechaDesde", "p_fecha_desde", "Fecha desde", ReportesWebConstants.DatasetParameterDataType.Date, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, false, true, 10),
                new DatasetParameterSeed("FechaHasta", "p_fecha_hasta", "Fecha hasta", ReportesWebConstants.DatasetParameterDataType.Date, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, false, true, 20)
            ]),
        new(
            ReportesWebConstants.CodigoDatasetTransaccionesPeriodo,
            "Dataset transacciones por periodo",
            "Fuente resumida para el total control de transacciones por periodo.",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetTransaccionesPeriodo,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            [
                new DatasetParameterSeed("CompanyId", "p_company_id", "Empresa actual", ReportesWebConstants.DatasetParameterDataType.Int64, ReportesWebConstants.DatasetParameterValueSource.CurrentCompany, null, false, false, true, 0),
                new DatasetParameterSeed("FechaDesde", "p_fecha_desde", "Fecha desde", ReportesWebConstants.DatasetParameterDataType.Date, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, false, true, 10),
                new DatasetParameterSeed("FechaHasta", "p_fecha_hasta", "Fecha hasta", ReportesWebConstants.DatasetParameterDataType.Date, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, false, true, 20)
            ]),
        new(
            ReportesWebConstants.CodigoDatasetSaldoClientesCategoria,
            "Dataset saldo de clientes por categoria",
            "Fuente de saldos por categoria de servicio y condicion de medicion a fecha de corte.",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetSaldoClientesCategoria,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            [
                new DatasetParameterSeed("CompanyId", "p_company_id", "Empresa actual", ReportesWebConstants.DatasetParameterDataType.Int64, ReportesWebConstants.DatasetParameterValueSource.CurrentCompany, null, false, false, true, 0),
                new DatasetParameterSeed("FechaCorte", "p_fecha_corte", "Fecha de corte", ReportesWebConstants.DatasetParameterDataType.Date, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, false, true, 10),
                new DatasetParameterSeed("CategoriaServicioId", "p_categoria_servicio_id", "Categoria", ReportesWebConstants.DatasetParameterDataType.Int64, ReportesWebConstants.DatasetParameterValueSource.Report, "0", true, false, false, 20),
                new DatasetParameterSeed("EstadoCliente", "p_estado_cliente", "Estado del cliente", ReportesWebConstants.DatasetParameterDataType.Int64, ReportesWebConstants.DatasetParameterValueSource.Report, "0", true, false, false, 30)
            ]),
        new(
            ReportesWebConstants.CodigoDatasetDesgloseFacturacion,
            "Dataset desglose de facturacion",
            "Fuente resumida por ciclos para el reporte de desglose de facturacion.",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetDesgloseFacturacion,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            [
                new DatasetParameterSeed("CompanyId", "p_company_id", "Empresa actual", ReportesWebConstants.DatasetParameterDataType.Int64, ReportesWebConstants.DatasetParameterValueSource.CurrentCompany, null, false, false, true, 0),
                new DatasetParameterSeed("FechaDesde", "p_fecha_desde", "Fecha desde", ReportesWebConstants.DatasetParameterDataType.Date, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, false, true, 10),
                new DatasetParameterSeed("FechaHasta", "p_fecha_hasta", "Fecha hasta", ReportesWebConstants.DatasetParameterDataType.Date, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, false, true, 20)
            ]),
        new(
            ReportesWebConstants.CodigoDatasetMovimientoPeriodo,
            "Dataset movimiento por periodo",
            "Fuente de movimientos del periodo con saldo anterior y saldo acumulado.",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetMovimientoPeriodo,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            [
                new DatasetParameterSeed("CompanyId", "p_company_id", "Empresa actual", ReportesWebConstants.DatasetParameterDataType.Int64, ReportesWebConstants.DatasetParameterValueSource.CurrentCompany, null, false, false, true, 0),
                new DatasetParameterSeed("FechaDesde", "p_fecha_desde", "Fecha desde", ReportesWebConstants.DatasetParameterDataType.Date, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, false, true, 10),
                new DatasetParameterSeed("FechaHasta", "p_fecha_hasta", "Fecha hasta", ReportesWebConstants.DatasetParameterDataType.Date, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, false, true, 20)
            ]),
        new(
            ReportesWebConstants.CodigoDatasetAuxiliarLectura,
            "Dataset auxiliar de lectura",
            "Fuente detallada del auxiliar de lectura por periodo, ciclo y estado pendiente.",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetAuxiliarLectura,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            [
                new DatasetParameterSeed("CompanyId", "p_company_id", "Empresa actual", ReportesWebConstants.DatasetParameterDataType.Int64, ReportesWebConstants.DatasetParameterValueSource.CurrentCompany, null, false, false, true, 0),
                new DatasetParameterSeed("Anio", "p_anio", "Año", ReportesWebConstants.DatasetParameterDataType.Int64, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, false, true, 10),
                new DatasetParameterSeed("Mes", "p_mes", "Mes", ReportesWebConstants.DatasetParameterDataType.Int64, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, false, true, 20),
                new DatasetParameterSeed("CicloId", "p_ciclo_id", "Ciclo", ReportesWebConstants.DatasetParameterDataType.Int64, ReportesWebConstants.DatasetParameterValueSource.Report, "0", true, false, false, 30),
                new DatasetParameterSeed("SoloPendientes", "p_solo_pendientes", "Solo pendientes", ReportesWebConstants.DatasetParameterDataType.Boolean, ReportesWebConstants.DatasetParameterValueSource.Report, "false", true, false, false, 40)
            ]),
        new(
            ReportesWebConstants.CodigoDatasetSaldoClientesAntiguedad,
            "Dataset saldo de clientes segun antigüedad",
            "Fuente de clientes con saldo vencido segun antiguedad, agrupados por ciclo.",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetSaldoClientesAntiguedad,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            [
                new DatasetParameterSeed("CompanyId", "p_company_id", "Empresa actual", ReportesWebConstants.DatasetParameterDataType.Int64, ReportesWebConstants.DatasetParameterValueSource.CurrentCompany, null, false, false, true, 0),
                new DatasetParameterSeed("FechaCorte", "p_fecha_corte", "Fecha de corte", ReportesWebConstants.DatasetParameterDataType.Date, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, false, true, 10),
                new DatasetParameterSeed("DiasMinimos", "p_dias_minimos", "Dias minimos", ReportesWebConstants.DatasetParameterDataType.Int64, ReportesWebConstants.DatasetParameterValueSource.Report, "60", true, false, true, 20),
                new DatasetParameterSeed("EstadoCliente", "p_estado_cliente", "Estado del cliente", ReportesWebConstants.DatasetParameterDataType.Int64, ReportesWebConstants.DatasetParameterValueSource.Report, "0", true, false, false, 30),
                new DatasetParameterSeed("CicloId", "p_ciclo_id", "Ciclo", ReportesWebConstants.DatasetParameterDataType.Int64, ReportesWebConstants.DatasetParameterValueSource.Report, "0", true, false, false, 40)
            ]),
        new(
            ReportesWebConstants.CodigoDatasetHistorialRecibosEmitidos,
            "Dataset historial de recibos emitidos",
            "Fuente de recibos emitidos por rango de fechas y usuario o cajero.",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetHistorialRecibosEmitidos,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            [
                new DatasetParameterSeed("CompanyId", "p_company_id", "Empresa actual", ReportesWebConstants.DatasetParameterDataType.Int64, ReportesWebConstants.DatasetParameterValueSource.CurrentCompany, null, false, false, true, 0),
                new DatasetParameterSeed("FechaDesde", "p_fecha_desde", "Fecha desde", ReportesWebConstants.DatasetParameterDataType.Date, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, false, true, 10),
                new DatasetParameterSeed("FechaHasta", "p_fecha_hasta", "Fecha hasta", ReportesWebConstants.DatasetParameterDataType.Date, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, false, true, 20),
                new DatasetParameterSeed("Usuario", "p_usuario", "Usuario", ReportesWebConstants.DatasetParameterDataType.Text, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, true, false, 30)
            ]),
        new(
            ReportesWebConstants.CodigoDatasetAnalisisAntiguedadCobros,
            "Dataset analisis de antigüedad de cobros",
            "Fuente de saldos por tramos de antigüedad de cobro, filtrable por retroceso en meses o años.",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetAnalisisAntiguedadCobros,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            [
                new DatasetParameterSeed("CompanyId", "p_company_id", "Empresa actual", ReportesWebConstants.DatasetParameterDataType.Int64, ReportesWebConstants.DatasetParameterValueSource.CurrentCompany, null, false, false, true, 0),
                new DatasetParameterSeed("FechaBase", "p_fecha_base", "Fecha base", ReportesWebConstants.DatasetParameterDataType.Date, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, false, true, 10),
                new DatasetParameterSeed("RetrocesoValor", "p_retroceso_valor", "Retroceso", ReportesWebConstants.DatasetParameterDataType.Int64, ReportesWebConstants.DatasetParameterValueSource.Report, "12", true, false, true, 20),
                new DatasetParameterSeed("UnidadTiempo", "p_unidad_tiempo", "Unidad de tiempo", ReportesWebConstants.DatasetParameterDataType.Text, ReportesWebConstants.DatasetParameterValueSource.Report, "MESES", true, false, true, 30)
            ]),
        new(
            ReportesWebConstants.CodigoDatasetSaldoClientesCiclo,
            "Dataset saldo de clientes por ciclo",
            "Fuente de saldos por ciclo con movimientos de periodo y conteos de clientes.",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetSaldoClientesCiclo,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            [
                new DatasetParameterSeed("CompanyId", "p_company_id", "Empresa actual", ReportesWebConstants.DatasetParameterDataType.Int64, ReportesWebConstants.DatasetParameterValueSource.CurrentCompany, null, false, false, true, 0),
                new DatasetParameterSeed("FechaDesde", "p_fecha_desde", "Fecha desde", ReportesWebConstants.DatasetParameterDataType.Date, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, false, true, 10),
                new DatasetParameterSeed("FechaHasta", "p_fecha_hasta", "Fecha hasta", ReportesWebConstants.DatasetParameterDataType.Date, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, false, true, 20),
                new DatasetParameterSeed("CicloId", "p_ciclo_id", "Ciclo", ReportesWebConstants.DatasetParameterDataType.Int64, ReportesWebConstants.DatasetParameterValueSource.Report, "0", true, false, false, 30)
            ]),
        new(
            ReportesWebConstants.CodigoDatasetSaldoClientesCategoriaCobranza,
            "Dataset saldo de clientes por categoria",
            "Fuente resumida por categoria con saldo anterior, movimientos del periodo y desglose de medidores.",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetSaldoClientesCategoriaCobranza,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            [
                new DatasetParameterSeed("CompanyId", "p_company_id", "Empresa actual", ReportesWebConstants.DatasetParameterDataType.Int64, ReportesWebConstants.DatasetParameterValueSource.CurrentCompany, null, false, false, true, 0),
                new DatasetParameterSeed("FechaDesde", "p_fecha_desde", "Fecha desde", ReportesWebConstants.DatasetParameterDataType.Date, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, false, true, 10),
                new DatasetParameterSeed("FechaHasta", "p_fecha_hasta", "Fecha hasta", ReportesWebConstants.DatasetParameterDataType.Date, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, false, true, 20),
                new DatasetParameterSeed("CategoriaServicioId", "p_categoria_servicio_id", "Categoria", ReportesWebConstants.DatasetParameterDataType.Int64, ReportesWebConstants.DatasetParameterValueSource.Report, "0", true, false, false, 30)
            ]),
        new(
            ReportesWebConstants.CodigoDatasetRecaudacion,
            "Dataset de recaudacion",
            "Fuente de ingresos y recuperaciones por medio de pago.",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetRecaudacion,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            [
                new DatasetParameterSeed("CompanyId", "p_company_id", "Empresa actual", ReportesWebConstants.DatasetParameterDataType.Int64, ReportesWebConstants.DatasetParameterValueSource.CurrentCompany, null, false, false, true, 0),
                new DatasetParameterSeed("FechaDesde", "p_fecha_desde", "Fecha desde", ReportesWebConstants.DatasetParameterDataType.Date, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, false, true, 10),
                new DatasetParameterSeed("FechaHasta", "p_fecha_hasta", "Fecha hasta", ReportesWebConstants.DatasetParameterDataType.Date, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, false, true, 20),
                new DatasetParameterSeed("MedioPagoCodigo", "p_medio_pago_codigo", "Medio de Pago", ReportesWebConstants.DatasetParameterDataType.Text, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, true, false, 30)
            ]),
        new(
            ReportesWebConstants.CodigoDatasetSaldoClientesCategoriaDetalle,
            "Dataset saldo de clientes detallado por categoria",
            "Fuente detallada por categoria con saldo anterior, movimientos del periodo y desglose por cliente.",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetSaldoClientesCategoriaDetalle,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            [
                new DatasetParameterSeed("CompanyId", "p_company_id", "Empresa actual", ReportesWebConstants.DatasetParameterDataType.Int64, ReportesWebConstants.DatasetParameterValueSource.CurrentCompany, null, false, false, true, 0),
                new DatasetParameterSeed("FechaDesde", "p_fecha_desde", "Fecha desde", ReportesWebConstants.DatasetParameterDataType.Date, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, false, true, 10),
                new DatasetParameterSeed("FechaHasta", "p_fecha_hasta", "Fecha hasta", ReportesWebConstants.DatasetParameterDataType.Date, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, false, true, 20),
                new DatasetParameterSeed("CategoriaServicioId", "p_categoria_servicio_id", "Categoria", ReportesWebConstants.DatasetParameterDataType.Int64, ReportesWebConstants.DatasetParameterValueSource.Report, "0", true, false, false, 30)
            ]),
        new(
            ReportesWebConstants.CodigoDatasetSaldosAguaPotableCiclo,
            "Dataset saldos de agua potable por ciclo",
            "Fuente de saldos de agua potable agrupados por ciclo con movimientos del periodo y conteos de usuarios.",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetSaldosAguaPotableCiclo,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            [
                new DatasetParameterSeed("CompanyId", "p_company_id", "Empresa actual", ReportesWebConstants.DatasetParameterDataType.Int64, ReportesWebConstants.DatasetParameterValueSource.CurrentCompany, null, false, false, true, 0),
                new DatasetParameterSeed("FechaDesde", "p_fecha_desde", "Fecha desde", ReportesWebConstants.DatasetParameterDataType.Date, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, false, true, 10),
                new DatasetParameterSeed("FechaHasta", "p_fecha_hasta", "Fecha hasta", ReportesWebConstants.DatasetParameterDataType.Date, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, false, true, 20),
                new DatasetParameterSeed("CicloId", "p_ciclo_id", "Ciclo", ReportesWebConstants.DatasetParameterDataType.Int64, ReportesWebConstants.DatasetParameterValueSource.Report, "0", true, false, false, 30)
            ]),
        new(
            ReportesWebConstants.CodigoDatasetSumarialTarifarioMedicion,
            "Dataset sumarial tarifario medicion por periodo",
            "Fuente de conexiones, consumos y valor de agua agrupados por categoria y rango de tarifas.",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetSumarialTarifarioMedicion,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            [
                new DatasetParameterSeed("CompanyId", "p_company_id", "Empresa actual", ReportesWebConstants.DatasetParameterDataType.Int64, ReportesWebConstants.DatasetParameterValueSource.CurrentCompany, null, false, false, true, 0),
                new DatasetParameterSeed("FechaDesde", "p_fecha_desde", "Fecha desde", ReportesWebConstants.DatasetParameterDataType.Date, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, false, true, 10),
                new DatasetParameterSeed("FechaHasta", "p_fecha_hasta", "Fecha hasta", ReportesWebConstants.DatasetParameterDataType.Date, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, false, true, 20)
            ]),
        new(
            ReportesWebConstants.CodigoDatasetSumarialTarifasNoMedido,
            "Dataset sumarial de tarifas no medido por periodo",
            "Fuente de conexiones y valor de agua agrupados por categoria y tarifa para clientes sin medidor.",
            ReportesWebConstants.DatasetSourceType.StoredProcedure,
            ReportesWebConstants.OrigenDatasetSumarialTarifasNoMedido,
            null,
            ReportesWebConstants.DefaultReportingConnectionName,
            [
                new DatasetParameterSeed("CompanyId", "p_company_id", "Empresa actual", ReportesWebConstants.DatasetParameterDataType.Int64, ReportesWebConstants.DatasetParameterValueSource.CurrentCompany, null, false, false, true, 0),
                new DatasetParameterSeed("FechaDesde", "p_fecha_desde", "Fecha desde", ReportesWebConstants.DatasetParameterDataType.Date, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, false, true, 10),
                new DatasetParameterSeed("FechaHasta", "p_fecha_hasta", "Fecha hasta", ReportesWebConstants.DatasetParameterDataType.Date, ReportesWebConstants.DatasetParameterValueSource.Report, null, true, false, true, 20)
            ])
    ];

    private readonly SiadDbContext _context;
    private readonly IConfiguration _configuration;

    public ReportesDatasetService(SiadDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<IReadOnlyList<ReporteDatasetCatalogoItemDto>> ListarAsync(long companyId, CancellationToken ct = default)
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
            await EnsureDefaultDatasetsAsync(companyId, ct);

            var datasets = await _context.rep_catalogo_datasets
                .AsNoTracking()
                .Where(x => x.company_id == companyId && x.is_active)
                .OrderBy(x => x.nombre)
                .ToListAsync(ct);

            var counts = await _context.rep_dataset_parametros
                .AsNoTracking()
                .Where(x => x.company_id == companyId)
                .GroupBy(x => x.dataset_id)
                .Select(group => new { group.Key, Count = group.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

            return datasets
                .Select(item => BuildCatalogItem(item, counts.GetValueOrDefault(item.dataset_id)))
                .ToList();
        }
        catch (Exception ex) when (IsTableMissing(ex))
        {
            return BuildFallbackCatalog();
        }
    }

    public async Task<ReporteDatasetDetalleDto?> ObtenerAsync(long companyId, string codigo, CancellationToken ct = default)
    {
        if (companyId <= 0)
        {
            return null;
        }

        if (!await CompanyExistsAsync(companyId, ct))
        {
            return null;
        }

        var normalizedCode = NormalizeCodeOrThrow(codigo);

        try
        {
            await EnsureDefaultDatasetsAsync(companyId, ct);

            var dataset = await _context.rep_catalogo_datasets
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.company_id == companyId && x.codigo == normalizedCode, ct);

            if (dataset is null)
            {
                return BuildFallbackDetail(normalizedCode);
            }

            if (!dataset.is_active)
            {
                return null;
            }

            var parameters = await LoadParametersAsync(companyId, dataset.dataset_id, ct);
            return BuildDetailItem(dataset, parameters);
        }
        catch (Exception ex) when (IsTableMissing(ex))
        {
            return BuildFallbackDetail(normalizedCode);
        }
    }

    public async Task<ReporteDatasetDetalleDto> CrearAsync(long companyId, ReporteDatasetCreateDto dto, string actor, bool allowSql, CancellationToken ct = default)
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

        try
        {
            var codigo = NormalizeCodeOrThrow(dto.Codigo);
            var input = NormalizeInput(dto, allowSql);

            if (await _context.rep_catalogo_datasets.AnyAsync(x => x.company_id == companyId && x.codigo == codigo, ct))
            {
                throw new InvalidOperationException("Ya existe un dataset con ese código.");
            }

            var now = DateTime.UtcNow;
            var dataset = new rep_catalogo_dataset
            {
                company_id = companyId,
                codigo = codigo,
                nombre = input.Nombre,
                descripcion = input.Descripcion,
                tipo_origen = input.TipoOrigen,
                origen_clave = input.OrigenClave,
                sql_text = input.SqlText,
                connection_name = input.ConnectionName,
                is_active = true,
                created_at = now,
                created_by = actor
            };

            _context.rep_catalogo_datasets.Add(dataset);
            await _context.SaveChangesAsync(ct);

            await ReplaceParametersAsync(companyId, dataset.dataset_id, input.Parametros, actor, now, ct);

            var parameters = await LoadParametersAsync(companyId, dataset.dataset_id, ct);
            return BuildDetailItem(dataset, parameters);
        }
        catch (Exception ex) when (IsTableMissing(ex))
        {
            throw new InvalidOperationException(
                "La tabla de datasets de reportería no existe. Ejecute el script 2026-03-21_add_rep_catalogo_dataset.sql.",
                ex);
        }
    }

    public async Task<ReporteDatasetDetalleDto> ActualizarAsync(long companyId, string codigo, ReporteDatasetCreateDto dto, string actor, bool allowSql, CancellationToken ct = default)
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

        try
        {
            await EnsureDefaultDatasetsAsync(companyId, ct);

            var normalizedCode = NormalizeCodeOrThrow(codigo);
            var dataset = await _context.rep_catalogo_datasets
                .FirstOrDefaultAsync(x => x.company_id == companyId && x.codigo == normalizedCode && x.is_active, ct);

            if (dataset is null)
            {
                throw new InvalidOperationException("No existe un dataset registrado con el código solicitado.");
            }

            EnsureMutable(dataset);

            if (!string.IsNullOrWhiteSpace(dto.Codigo) &&
                !string.Equals(normalizedCode, NormalizeCodeOrThrow(dto.Codigo), StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("El código del dataset no puede modificarse.");
            }

            var input = NormalizeInput(dto, allowSql);
            var now = DateTime.UtcNow;

            dataset.nombre = input.Nombre;
            dataset.descripcion = input.Descripcion;
            dataset.tipo_origen = input.TipoOrigen;
            dataset.origen_clave = input.OrigenClave;
            dataset.sql_text = input.SqlText;
            dataset.connection_name = input.ConnectionName;
            dataset.updated_at = now;
            dataset.updated_by = actor;

            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            await _context.SaveChangesAsync(ct);
            await ReplaceParametersAsync(companyId, dataset.dataset_id, input.Parametros, actor, now, ct, replaceExisting: true);
            await transaction.CommitAsync(ct);

            var parameters = await LoadParametersAsync(companyId, dataset.dataset_id, ct);
            return BuildDetailItem(dataset, parameters);
        }
        catch (Exception ex) when (IsTableMissing(ex))
        {
            throw new InvalidOperationException(
                "La tabla de datasets de reportería no existe. Ejecute el script 2026-03-21_add_rep_catalogo_dataset.sql.",
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

        try
        {
            await EnsureDefaultDatasetsAsync(companyId, ct);

            var normalizedCode = NormalizeCodeOrThrow(codigo);
            var dataset = await _context.rep_catalogo_datasets
                .FirstOrDefaultAsync(x => x.company_id == companyId && x.codigo == normalizedCode && x.is_active, ct);

            if (dataset is null)
            {
                throw new InvalidOperationException("No existe un dataset registrado con el código solicitado.");
            }

            EnsureMutable(dataset);

            var reportsInUse = await _context.rep_catalogo_informes
                .AsNoTracking()
                .Where(x => x.company_id == companyId
                            && x.tipo_origen == ReportesWebConstants.TipoOrigenReporte
                            && x.consulta_clave == normalizedCode
                            && x.is_active)
                .Select(x => x.codigo)
                .ToListAsync(ct);

            if (reportsInUse.Count > 0)
            {
                throw new InvalidOperationException($"El dataset está asignado a {reportsInUse.Count} reporte(s) activo(s): {string.Join(", ", reportsInUse)}.");
            }

            if (IsDefaultSeed(normalizedCode))
            {
                dataset.is_active = false;
                dataset.updated_at = DateTime.UtcNow;
                dataset.updated_by = "reporteria-delete";
                await _context.SaveChangesAsync(ct);
                return;
            }

            var parameters = await _context.rep_dataset_parametros
                .Where(x => x.company_id == companyId && x.dataset_id == dataset.dataset_id)
                .ToListAsync(ct);

            _context.rep_dataset_parametros.RemoveRange(parameters);
            _context.rep_catalogo_datasets.Remove(dataset);
            await _context.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (IsTableMissing(ex))
        {
            throw new InvalidOperationException(
                "La tabla de datasets de reportería no existe. Ejecute el script 2026-03-21_add_rep_catalogo_dataset.sql.",
                ex);
        }
    }

    public async Task<ReporteDatasetPreviewResultDto> ProbarAsync(long companyId, string codigo, ReporteDatasetPreviewRequestDto request, bool allowSql, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (companyId <= 0)
        {
            throw new InvalidOperationException("No fue posible determinar la empresa actual.");
        }

        if (!await CompanyExistsAsync(companyId, ct))
        {
            throw new InvalidOperationException("La empresa activa no existe o ya no esta disponible.");
        }

        try
        {
            await EnsureDefaultDatasetsAsync(companyId, ct);

            var normalizedCode = NormalizeCodeOrThrow(codigo);
            var dataset = await _context.rep_catalogo_datasets
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.company_id == companyId && x.codigo == normalizedCode && x.is_active, ct);

            if (dataset is null)
            {
                throw new InvalidOperationException("No existe un dataset activo con el código solicitado.");
            }

            if (string.Equals(dataset.tipo_origen, ReportesWebConstants.DatasetSourceType.Sql, StringComparison.OrdinalIgnoreCase) && !allowSql)
            {
                throw new InvalidOperationException("Solo usuarios admin o super administrador pueden probar datasets SQL.");
            }

            var parameters = await LoadParametersAsync(companyId, dataset.dataset_id, ct);
            return await ExecutePreviewAsync(companyId, dataset, parameters, request.Parametros, ct);
        }
        catch (Exception ex) when (IsTableMissing(ex))
        {
            throw new InvalidOperationException(
                "La tabla de datasets de reportería no existe. Ejecute el script 2026-03-21_add_rep_catalogo_dataset.sql.",
                ex);
        }
    }

    private async Task EnsureDefaultDatasetsAsync(long companyId, CancellationToken ct)
    {
        if (!await CompanyExistsAsync(companyId, ct))
        {
            return;
        }

        var datasets = await _context.rep_catalogo_datasets
            .Where(x => x.company_id == companyId)
            .ToListAsync(ct);

        var datasetLookup = datasets.ToDictionary(x => x.codigo, StringComparer.OrdinalIgnoreCase);
        var legacySeedDatasetIds = new HashSet<long>();
        var now = DateTime.UtcNow;
        var changed = false;

        foreach (var seed in DefaultSeeds)
        {
            if (!datasetLookup.TryGetValue(seed.Codigo, out var dataset))
            {
                dataset = new rep_catalogo_dataset
                {
                    company_id = companyId,
                    codigo = seed.Codigo,
                    nombre = seed.Nombre,
                    descripcion = seed.Descripcion,
                    tipo_origen = seed.TipoOrigen,
                    origen_clave = seed.OrigenClave,
                    sql_text = seed.SqlText,
                    connection_name = seed.ConnectionName,
                    is_active = true,
                    created_at = now,
                    created_by = "reporteria-bootstrap"
                };

                _context.rep_catalogo_datasets.Add(dataset);
                changed = true;
                datasets.Add(dataset);
                datasetLookup[seed.Codigo] = dataset;
            }
            else
            {
                var isLegacySeedDataset = IsLegacySeedDataset(dataset, seed);
                if (isLegacySeedDataset)
                {
                    legacySeedDatasetIds.Add(dataset.dataset_id);
                }

                changed |= SynchronizeSeedDataset(dataset, seed, now, isLegacySeedDataset);
            }
        }

        if (changed)
        {
            await _context.SaveChangesAsync(ct);
        }

        if (datasets.Count == 0)
        {
            return;
        }

        var datasetIds = datasets.Select(x => x.dataset_id).Where(x => x > 0).ToArray();
        if (datasetIds.Length == 0)
        {
            datasetIds = await _context.rep_catalogo_datasets
                .Where(x => x.company_id == companyId)
                .Select(x => x.dataset_id)
                .ToArrayAsync(ct);
        }

        var existingParameters = await _context.rep_dataset_parametros
            .Where(x => x.company_id == companyId && datasetIds.Contains(x.dataset_id))
            .ToListAsync(ct);

        var hasNewParameters = false;

        foreach (var seed in DefaultSeeds)
        {
            if (!datasetLookup.TryGetValue(seed.Codigo, out var dataset))
            {
                continue;
            }

            var currentParameters = existingParameters
                .Where(x => x.dataset_id == dataset.dataset_id)
                .ToDictionary(x => x.nombre, StringComparer.OrdinalIgnoreCase);

            foreach (var parameterSeed in seed.Parametros)
            {
                if (currentParameters.TryGetValue(parameterSeed.Nombre, out var currentParameter))
                {
                    if (SynchronizeSeedParameter(currentParameter, parameterSeed, now, legacySeedDatasetIds.Contains(dataset.dataset_id)))
                    {
                        hasNewParameters = true;
                    }

                    continue;
                }

                _context.rep_dataset_parametros.Add(new rep_dataset_parametro
                {
                    company_id = companyId,
                    dataset_id = dataset.dataset_id,
                    nombre = parameterSeed.Nombre,
                    nombre_origen = parameterSeed.NombreOrigen,
                    etiqueta = parameterSeed.Etiqueta,
                    tipo_dato = parameterSeed.TipoDato,
                    fuente_valor = parameterSeed.FuenteValor,
                    valor_default = parameterSeed.ValorDefault,
                    visible = parameterSeed.Visible,
                    permite_nulo = parameterSeed.PermiteNulo,
                    requerido = parameterSeed.Requerido,
                    orden = parameterSeed.Orden,
                    created_at = now,
                    created_by = "reporteria-bootstrap"
                });

                hasNewParameters = true;
            }
        }

        if (hasNewParameters)
        {
            await _context.SaveChangesAsync(ct);
        }
    }

    private static bool IsLegacySeedDataset(rep_catalogo_dataset dataset, DatasetSeed seed)
        => string.Equals(seed.Codigo, ReportesWebConstants.CodigoDatasetBancosTransacciones, StringComparison.OrdinalIgnoreCase)
           && (string.Equals(dataset.tipo_origen, LegacyObjectDatasetSourceType, StringComparison.OrdinalIgnoreCase)
               || string.Equals(dataset.origen_clave, LegacyBancosOriginKey, StringComparison.OrdinalIgnoreCase));

    private static bool SynchronizeSeedDataset(
        rep_catalogo_dataset dataset,
        DatasetSeed seed,
        DateTime now,
        bool forceTechnicalMigration)
    {
        var changed = false;

        if (string.IsNullOrWhiteSpace(dataset.nombre))
        {
            dataset.nombre = seed.Nombre;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(dataset.descripcion))
        {
            dataset.descripcion = seed.Descripcion;
            changed = true;
        }

        if ((forceTechnicalMigration || string.IsNullOrWhiteSpace(dataset.tipo_origen)) &&
            !string.Equals(dataset.tipo_origen, seed.TipoOrigen, StringComparison.OrdinalIgnoreCase))
        {
            dataset.tipo_origen = seed.TipoOrigen;
            changed = true;
        }

        if ((forceTechnicalMigration || string.IsNullOrWhiteSpace(dataset.origen_clave)) &&
            !string.Equals(dataset.origen_clave, seed.OrigenClave, StringComparison.OrdinalIgnoreCase))
        {
            dataset.origen_clave = seed.OrigenClave;
            changed = true;
        }

        if ((forceTechnicalMigration || string.IsNullOrWhiteSpace(dataset.connection_name)) &&
            !string.Equals(dataset.connection_name, seed.ConnectionName, StringComparison.OrdinalIgnoreCase))
        {
            dataset.connection_name = seed.ConnectionName;
            changed = true;
        }

        if (forceTechnicalMigration && dataset.sql_text is not null)
        {
            dataset.sql_text = seed.SqlText;
            changed = true;
        }

        if (changed)
        {
            dataset.updated_at = now;
            dataset.updated_by = "reporteria-bootstrap";
        }

        return changed;
    }

    private static bool SynchronizeSeedParameter(
        rep_dataset_parametro parameter,
        DatasetParameterSeed seed,
        DateTime now,
        bool forceTechnicalMigration)
    {
        var changed = false;

        if ((forceTechnicalMigration || string.IsNullOrWhiteSpace(parameter.nombre_origen)) &&
            !string.Equals(parameter.nombre_origen, seed.NombreOrigen, StringComparison.OrdinalIgnoreCase))
        {
            parameter.nombre_origen = seed.NombreOrigen;
            changed = true;
        }

        if ((forceTechnicalMigration || string.IsNullOrWhiteSpace(parameter.etiqueta)) &&
            !string.Equals(parameter.etiqueta, seed.Etiqueta, StringComparison.Ordinal))
        {
            parameter.etiqueta = seed.Etiqueta;
            changed = true;
        }

        if ((forceTechnicalMigration || string.IsNullOrWhiteSpace(parameter.tipo_dato)) &&
            !string.Equals(parameter.tipo_dato, seed.TipoDato, StringComparison.OrdinalIgnoreCase))
        {
            parameter.tipo_dato = seed.TipoDato;
            changed = true;
        }

        if ((forceTechnicalMigration || string.IsNullOrWhiteSpace(parameter.fuente_valor)) &&
            !string.Equals(parameter.fuente_valor, seed.FuenteValor, StringComparison.OrdinalIgnoreCase))
        {
            parameter.fuente_valor = seed.FuenteValor;
            changed = true;
        }

        if ((forceTechnicalMigration || parameter.valor_default is null) &&
            !string.Equals(parameter.valor_default, seed.ValorDefault, StringComparison.Ordinal))
        {
            parameter.valor_default = seed.ValorDefault;
            changed = true;
        }

        if (forceTechnicalMigration && parameter.visible != seed.Visible)
        {
            parameter.visible = seed.Visible;
            changed = true;
        }

        if (forceTechnicalMigration && parameter.permite_nulo != seed.PermiteNulo)
        {
            parameter.permite_nulo = seed.PermiteNulo;
            changed = true;
        }

        if (forceTechnicalMigration && parameter.requerido != seed.Requerido)
        {
            parameter.requerido = seed.Requerido;
            changed = true;
        }

        if (forceTechnicalMigration && parameter.orden != seed.Orden)
        {
            parameter.orden = seed.Orden;
            changed = true;
        }

        if (changed)
        {
            parameter.updated_at = now;
            parameter.updated_by = "reporteria-bootstrap";
        }

        return changed;
    }

    private async Task<ReporteDatasetPreviewResultDto> ExecutePreviewAsync(
        long companyId,
        rep_catalogo_dataset dataset,
        IReadOnlyList<rep_dataset_parametro> parameters,
        IReadOnlyDictionary<string, string?> rawValues,
        CancellationToken ct)
    {
        var connectionString = ResolveConnectionString(dataset.connection_name);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandTimeout = 30;
        command.CommandType = CommandType.Text;
        command.CommandText = BuildPreviewCommandText(dataset, parameters);

        foreach (var parameter in parameters)
        {
            if (string.Equals(dataset.tipo_origen, ReportesWebConstants.DatasetSourceType.View, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = ResolveRuntimeValue(parameter, rawValues, companyId);
            var dbParameter = command.Parameters.Add(ResolveQueryParameterName(parameter), ResolveNpgsqlDbType(parameter.tipo_dato));
            dbParameter.Value = value ?? DBNull.Value;
        }

        try
        {
            var columns = new List<string>();
            var rows = new List<IReadOnlyDictionary<string, string?>>();

            await using var reader = await command.ExecuteReaderAsync(ct);
            for (var index = 0; index < reader.FieldCount; index++)
            {
                columns.Add(reader.GetName(index));
            }

            while (await reader.ReadAsync(ct))
            {
                var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                for (var index = 0; index < reader.FieldCount; index++)
                {
                    row[columns[index]] = reader.IsDBNull(index) ? null : FormatPreviewValue(reader.GetValue(index));
                }

                rows.Add(row);
            }

            return new ReporteDatasetPreviewResultDto(dataset.codigo, dataset.tipo_origen, columns, rows, PreviewRowLimit);
        }
        catch (PostgresException ex) when (string.Equals(dataset.tipo_origen, ReportesWebConstants.DatasetSourceType.StoredProcedure, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"No fue posible ejecutar el dataset STORED_PROCEDURE. En PostgreSQL el preview espera una función que retorne filas (RETURNS TABLE / SETOF). Detalle: {ex.MessageText}",
                ex);
        }
        catch (PostgresException ex)
        {
            throw new InvalidOperationException($"No fue posible ejecutar el dataset. Detalle de PostgreSQL: {ex.MessageText}", ex);
        }
    }

    private string ResolveConnectionString(string? connectionName)
    {
        var resolvedName = string.IsNullOrWhiteSpace(connectionName)
            ? ReportesWebConstants.DefaultReportingConnectionName
            : connectionName.Trim();

        var connectionString = _configuration.GetConnectionString(resolvedName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"No existe una connection string configurada con el nombre '{resolvedName}'.");
        }

        return connectionString;
    }

    private static string BuildPreviewCommandText(rep_catalogo_dataset dataset, IReadOnlyList<rep_dataset_parametro> parameters)
        => dataset.tipo_origen switch
        {
            ReportesWebConstants.DatasetSourceType.StoredProcedure => BuildStoredProcedurePreview(dataset.origen_clave!, parameters),
            ReportesWebConstants.DatasetSourceType.View => $"SELECT * FROM {dataset.origen_clave} LIMIT {PreviewRowLimit}",
            ReportesWebConstants.DatasetSourceType.Sql => $"SELECT * FROM ({dataset.sql_text}) dataset_preview LIMIT {PreviewRowLimit}",
            _ => throw new InvalidOperationException($"El tipo de dataset {dataset.tipo_origen} no soporta preview SQL.")
        };

    private static string BuildStoredProcedurePreview(string originKey, IReadOnlyList<rep_dataset_parametro> parameters)
    {
        var arguments = parameters.Count == 0
            ? string.Empty
            : string.Join(", ", parameters.Select(x => $"@{ResolveQueryParameterName(x)}"));

        return $"SELECT * FROM {originKey}({arguments}) LIMIT {PreviewRowLimit}";
    }

    private static object? ResolveRuntimeValue(rep_dataset_parametro parameter, IReadOnlyDictionary<string, string?> rawValues, long companyId)
    {
        string? rawValue = parameter.fuente_valor switch
        {
            ReportesWebConstants.DatasetParameterValueSource.CurrentCompany => companyId.ToString(CultureInfo.InvariantCulture),
            ReportesWebConstants.DatasetParameterValueSource.Fixed => parameter.valor_default,
            _ => rawValues.TryGetValue(parameter.nombre, out var value) ? value : parameter.valor_default
        };

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            if (string.Equals(parameter.tipo_dato, ReportesWebConstants.DatasetParameterDataType.Boolean, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (parameter.requerido || !parameter.permite_nulo)
            {
                throw new InvalidOperationException($"El parámetro '{parameter.etiqueta}' es obligatorio para probar el dataset.");
            }

            return null;
        }

        return ParseParameterValue(parameter.tipo_dato, rawValue.Trim(), parameter.etiqueta);
    }

    private static object ParseParameterValue(string dataType, string rawValue, string label)
        => dataType switch
        {
            ReportesWebConstants.DatasetParameterDataType.Text => rawValue,
            ReportesWebConstants.DatasetParameterDataType.Int64 => ParseInt64(rawValue, label),
            ReportesWebConstants.DatasetParameterDataType.Decimal => ParseDecimal(rawValue, label),
            ReportesWebConstants.DatasetParameterDataType.Date => ParseDate(rawValue, label).Date,
            ReportesWebConstants.DatasetParameterDataType.DateTime => ParseDate(rawValue, label),
            ReportesWebConstants.DatasetParameterDataType.Boolean => ParseBoolean(rawValue, label),
            _ => rawValue
        };

    private static long ParseInt64(string rawValue, string label)
        => long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidOperationException($"El parámetro '{label}' debe ser un entero válido.");

    private static decimal ParseDecimal(string rawValue, string label)
        => decimal.TryParse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidOperationException($"El parámetro '{label}' debe ser un decimal válido.");

    private static DateTime ParseDate(string rawValue, string label)
    {
        if (DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var invariant))
        {
            return invariant;
        }

        if (DateTime.TryParse(rawValue, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out var currentCulture))
        {
            return currentCulture;
        }

        throw new InvalidOperationException($"El parámetro '{label}' debe ser una fecha válida.");
    }

    private static bool ParseBoolean(string rawValue, string label)
    {
        if (bool.TryParse(rawValue, out var booleanValue))
        {
            return booleanValue;
        }

        return rawValue.Trim().ToLowerInvariant() switch
        {
            "1" => true,
            "0" => false,
            "si" => true,
            "sí" => true,
            "no" => false,
            _ => throw new InvalidOperationException($"El parámetro '{label}' debe ser booleano (true/false, 1/0).")
        };
    }

    private static NpgsqlDbType ResolveNpgsqlDbType(string dataType)
        => dataType switch
        {
            ReportesWebConstants.DatasetParameterDataType.Text => NpgsqlDbType.Text,
            ReportesWebConstants.DatasetParameterDataType.Int64 => NpgsqlDbType.Bigint,
            ReportesWebConstants.DatasetParameterDataType.Decimal => NpgsqlDbType.Numeric,
            ReportesWebConstants.DatasetParameterDataType.Date => NpgsqlDbType.Date,
            ReportesWebConstants.DatasetParameterDataType.DateTime => NpgsqlDbType.Timestamp,
            ReportesWebConstants.DatasetParameterDataType.Boolean => NpgsqlDbType.Boolean,
            _ => NpgsqlDbType.Text
        };

    private static string? FormatPreviewValue(object? value)
        => value switch
        {
            null => null,
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateOnly dateOnly => dateOnly.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            bool booleanValue => booleanValue ? "true" : "false",
            byte[] bytes => $"[{bytes.Length} bytes]",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };

    private async Task ReplaceParametersAsync(
        long companyId,
        long datasetId,
        IReadOnlyList<NormalizedParameter> normalizedParameters,
        string actor,
        DateTime now,
        CancellationToken ct,
        bool replaceExisting = false)
    {
        if (replaceExisting)
        {
            var currentParameters = await _context.rep_dataset_parametros
                .Where(x => x.company_id == companyId && x.dataset_id == datasetId)
                .ToListAsync(ct);

            if (currentParameters.Count > 0)
            {
                _context.rep_dataset_parametros.RemoveRange(currentParameters);
                await _context.SaveChangesAsync(ct);
            }
        }

        if (normalizedParameters.Count == 0)
        {
            return;
        }

        var entities = normalizedParameters.Select(item => new rep_dataset_parametro
        {
            company_id = companyId,
            dataset_id = datasetId,
            nombre = item.Nombre,
            nombre_origen = item.NombreOrigen,
            etiqueta = item.Etiqueta,
            tipo_dato = item.TipoDato,
            fuente_valor = item.FuenteValor,
            valor_default = item.ValorDefault,
            visible = item.Visible,
            permite_nulo = item.PermiteNulo,
            requerido = item.Requerido,
            orden = item.Orden,
            created_at = now,
            created_by = actor,
            updated_at = replaceExisting ? now : null,
            updated_by = replaceExisting ? actor : null
        });

        _context.rep_dataset_parametros.AddRange(entities);
        await _context.SaveChangesAsync(ct);
    }

    private static void EnsureMutable(rep_catalogo_dataset dataset)
    {
        _ = dataset;
    }

    private NormalizedDatasetInput NormalizeInput(ReporteDatasetCreateDto dto, bool allowSql)
    {
        var tipoOrigen = NormalizeDatasetSourceType(dto.TipoOrigen);
        var origenClave = string.IsNullOrWhiteSpace(dto.OrigenClave) ? null : dto.OrigenClave.Trim();
        var sqlText = string.IsNullOrWhiteSpace(dto.SqlText) ? null : dto.SqlText.Trim();
        var connectionName = string.IsNullOrWhiteSpace(dto.ConnectionName)
            ? ReportesWebConstants.DefaultReportingConnectionName
            : dto.ConnectionName.Trim();

        ValidateDatasetDefinition(tipoOrigen, origenClave, sqlText, allowSql);

        return new NormalizedDatasetInput(
            RequireText(dto.Nombre, "El nombre del dataset es obligatorio."),
            string.IsNullOrWhiteSpace(dto.Descripcion) ? null : dto.Descripcion.Trim(),
            tipoOrigen,
            origenClave,
            sqlText,
            connectionName,
            NormalizeParameters(dto.Parametros));
    }

    private async Task<IReadOnlyList<rep_dataset_parametro>> LoadParametersAsync(long companyId, long datasetId, CancellationToken ct)
        => await _context.rep_dataset_parametros
            .AsNoTracking()
            .Where(x => x.company_id == companyId && x.dataset_id == datasetId)
            .OrderBy(x => x.orden)
            .ThenBy(x => x.nombre)
            .ToListAsync(ct);

    private static ReporteDatasetCatalogoItemDto BuildCatalogItem(rep_catalogo_dataset dataset, int parametrosCount)
        => new(
            dataset.dataset_id,
            dataset.codigo,
            dataset.nombre,
            dataset.descripcion,
            dataset.tipo_origen,
            dataset.origen_clave,
            dataset.is_active,
            parametrosCount);

    private static ReporteDatasetDetalleDto BuildDetailItem(rep_catalogo_dataset dataset, IReadOnlyList<rep_dataset_parametro> parameters)
        => new(
            dataset.dataset_id,
            dataset.codigo,
            dataset.nombre,
            dataset.descripcion,
            dataset.tipo_origen,
            dataset.origen_clave,
            dataset.sql_text,
            dataset.connection_name,
            dataset.is_active,
            parameters.Select(BuildParameterItem).ToList());

    private static ReporteDatasetParametroDto BuildParameterItem(rep_dataset_parametro item)
        => new(
            item.dataset_parametro_id,
            item.nombre,
            GetDistinctOriginName(item.nombre_origen, item.nombre),
            item.etiqueta,
            item.tipo_dato,
            item.fuente_valor,
            item.valor_default,
            item.visible,
            item.permite_nulo,
            item.requerido,
            item.orden);

    private static IReadOnlyList<ReporteDatasetCatalogoItemDto> BuildFallbackCatalog()
        => DefaultSeeds
            .Select(seed => new ReporteDatasetCatalogoItemDto(
                0,
                seed.Codigo,
                seed.Nombre,
                seed.Descripcion,
                seed.TipoOrigen,
                seed.OrigenClave,
                true,
                seed.Parametros.Count))
            .ToList();

    private static ReporteDatasetDetalleDto? BuildFallbackDetail(string codigo)
    {
        var seed = DefaultSeeds.FirstOrDefault(x => x.Codigo == codigo);
        if (seed is null)
        {
            return null;
        }

        return new ReporteDatasetDetalleDto(
            0,
            seed.Codigo,
            seed.Nombre,
            seed.Descripcion,
            seed.TipoOrigen,
            seed.OrigenClave,
            seed.SqlText,
            seed.ConnectionName,
            true,
            seed.Parametros.Select(x => new ReporteDatasetParametroDto(
                0,
                x.Nombre,
                x.NombreOrigen,
                x.Etiqueta,
                x.TipoDato,
                x.FuenteValor,
                x.ValorDefault,
                x.Visible,
                x.PermiteNulo,
                x.Requerido,
                x.Orden)).ToList());
    }

    private static bool IsDefaultSeed(string codigo)
        => DefaultSeeds.Any(seed => string.Equals(seed.Codigo, codigo, StringComparison.OrdinalIgnoreCase));

    private Task<bool> CompanyExistsAsync(long companyId, CancellationToken ct)
        => _context.cfg_companies
            .AsNoTracking()
            .AnyAsync(x => x.company_id == companyId, ct);

    private static string NormalizeCodeOrThrow(string codigo)
    {
        var normalized = ReportesWebConstants.NormalizeCode(codigo);
        if (!ReportesWebConstants.IsValidCode(normalized))
        {
            throw new ArgumentException("El código del dataset solo admite letras, números, guion y guion bajo.");
        }

        return normalized;
    }

    private static string NormalizeDatasetSourceType(string value)
    {
        var normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!ReportesWebConstants.IsValidDatasetSourceType(normalized))
        {
            throw new ArgumentException("El tipo de dataset no es válido.");
        }

        return normalized;
    }

    private static IReadOnlyList<NormalizedParameter> NormalizeParameters(IEnumerable<ReporteDatasetParametroCreateDto> parameters)
    {
        var list = new List<NormalizedParameter>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenOrders = new HashSet<int>();

        foreach (var parameter in parameters ?? [])
        {
            var name = parameter.Nombre?.Trim() ?? string.Empty;
            if (!ParameterNameRegex.IsMatch(name))
            {
                throw new ArgumentException($"El nombre de parámetro '{parameter.Nombre}' no es válido.");
            }

            if (!seenNames.Add(name))
            {
                throw new ArgumentException($"El parámetro '{name}' está duplicado.");
            }

            if (!seenOrders.Add(parameter.Orden))
            {
                throw new ArgumentException($"El orden '{parameter.Orden}' está duplicado entre parámetros.");
            }

            var tipoDato = (parameter.TipoDato ?? string.Empty).Trim().ToUpperInvariant();
            if (!ReportesWebConstants.IsValidDatasetParameterDataType(tipoDato))
            {
                throw new ArgumentException($"El tipo de dato del parámetro '{name}' no es válido.");
            }

            var fuenteValor = (parameter.FuenteValor ?? string.Empty).Trim().ToUpperInvariant();
            if (!ReportesWebConstants.IsValidDatasetParameterValueSource(fuenteValor))
            {
                throw new ArgumentException($"La fuente del parámetro '{name}' no es válida.");
            }

            var label = string.IsNullOrWhiteSpace(parameter.Etiqueta) ? name : parameter.Etiqueta.Trim();
            var defaultValue = string.IsNullOrWhiteSpace(parameter.ValorDefault) ? null : parameter.ValorDefault.Trim();
            var originName = NormalizeOriginParameterName(parameter.NombreOrigen, name);

            if (fuenteValor == ReportesWebConstants.DatasetParameterValueSource.Fixed && string.IsNullOrWhiteSpace(defaultValue))
            {
                throw new ArgumentException($"El parámetro '{name}' requiere un valor fijo predeterminado.");
            }

            list.Add(new NormalizedParameter(
                name,
                originName,
                label,
                tipoDato,
                fuenteValor,
                defaultValue,
                parameter.Visible,
                parameter.PermiteNulo,
                parameter.Requerido,
                parameter.Orden));
        }

        return list.OrderBy(x => x.Orden).ThenBy(x => x.Nombre, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string RequireText(string? value, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(errorMessage);
        }

        return value.Trim();
    }

    private static string ResolveQueryParameterName(rep_dataset_parametro parameter)
        => NormalizeQueryParameterIdentifier(parameter.nombre_origen, parameter.nombre);

    private static string? NormalizeOriginParameterName(string? originName, string logicalName)
    {
        var normalized = NormalizeQueryParameterIdentifier(originName, logicalName);
        return string.Equals(normalized, logicalName, StringComparison.OrdinalIgnoreCase)
            ? null
            : normalized;
    }

    private static string NormalizeQueryParameterIdentifier(string? candidate, string fallbackName)
    {
        var normalized = string.IsNullOrWhiteSpace(candidate)
            ? fallbackName.Trim()
            : candidate.Trim();

        while (normalized.StartsWith("@", StringComparison.Ordinal) ||
               normalized.StartsWith(":", StringComparison.Ordinal) ||
               normalized.StartsWith("?", StringComparison.Ordinal))
        {
            normalized = normalized[1..];
        }

        if (!ParameterNameRegex.IsMatch(normalized))
        {
            throw new ArgumentException($"El nombre técnico del parámetro '{candidate ?? fallbackName}' no es válido.");
        }

        return normalized;
    }

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

    private static void ValidateDatasetDefinition(string sourceType, string? originKey, string? sqlText, bool allowSql)
    {
        switch (sourceType)
        {
            case ReportesWebConstants.DatasetSourceType.StoredProcedure:
            case ReportesWebConstants.DatasetSourceType.View:
                if (string.IsNullOrWhiteSpace(originKey) || !IdentifierRegex.IsMatch(originKey))
                {
                    throw new ArgumentException("La clave de origen del dataset debe ser un identificador válido (schema.objeto).");
                }
                break;

            case ReportesWebConstants.DatasetSourceType.Sql:
                if (!allowSql)
                {
                    throw new InvalidOperationException("Solo usuarios admin o super administrador pueden registrar datasets SQL.");
                }

                ValidateReadonlySql(sqlText);
                break;
        }
    }

    private static void ValidateReadonlySql(string? sqlText)
    {
        if (string.IsNullOrWhiteSpace(sqlText))
        {
            throw new ArgumentException("El texto SQL es obligatorio.");
        }

        var normalized = sqlText.Trim();
        var lowered = normalized.ToLowerInvariant();

        if (!(lowered.StartsWith("select ") || lowered.StartsWith("with ")))
        {
            throw new ArgumentException("El SQL libre debe iniciar con SELECT o WITH.");
        }

        var blockedTokens = new[]
        {
            ";",
            " insert ",
            " update ",
            " delete ",
            " drop ",
            " alter ",
            " truncate ",
            " create ",
            " grant ",
            " revoke ",
            " call ",
            " do ",
            " copy "
        };

        if (blockedTokens.Any(token => lowered.Contains(token, StringComparison.Ordinal)))
        {
            throw new ArgumentException("El SQL libre solo admite consultas de lectura de una sola sentencia.");
        }
    }

    private static bool IsTableMissing(Exception ex)
    {
        if (ex is PostgresException postgresException && postgresException.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            return true;
        }

        return ex.InnerException is not null && IsTableMissing(ex.InnerException);
    }

    private sealed record DatasetSeed(
        string Codigo,
        string Nombre,
        string Descripcion,
        string TipoOrigen,
        string? OrigenClave,
        string? SqlText,
        string? ConnectionName,
        IReadOnlyList<DatasetParameterSeed> Parametros);

    private sealed record DatasetParameterSeed(
        string Nombre,
        string? NombreOrigen,
        string Etiqueta,
        string TipoDato,
        string FuenteValor,
        string? ValorDefault,
        bool Visible,
        bool PermiteNulo,
        bool Requerido,
        int Orden);

    private sealed record NormalizedParameter(
        string Nombre,
        string? NombreOrigen,
        string Etiqueta,
        string TipoDato,
        string FuenteValor,
        string? ValorDefault,
        bool Visible,
        bool PermiteNulo,
        bool Requerido,
        int Orden);

    private sealed record NormalizedDatasetInput(
        string Nombre,
        string? Descripcion,
        string TipoOrigen,
        string? OrigenClave,
        string? SqlText,
        string? ConnectionName,
        IReadOnlyList<NormalizedParameter> Parametros);
}
