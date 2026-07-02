using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Informes;
using SIAD.Core.Tenancy;
using SIAD.Reports;
using apc.Security;
using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace apc.Controllers.Informes;

[ApiController]
[Route("api/informes")]
[ModuleAuthorize(PermissionModules.Reporteria)]
public sealed class InformesController : ControllerBase
{
    private readonly IInformesCatalogoService _catalogoService;
    private readonly IInformesConsultaService _consultaService;
    private readonly ICurrentCompanyService _currentCompany;
    private readonly IConfiguration _configuration;

    public InformesController(
        IInformesCatalogoService catalogoService,
        IInformesConsultaService consultaService,
        ICurrentCompanyService currentCompany,
        IConfiguration configuration)
    {
        _catalogoService = catalogoService;
        _consultaService = consultaService;
        _currentCompany = currentCompany;
        _configuration = configuration;
    }

    [HttpGet("catalogo")]
    public async Task<IActionResult> GetCatalogo(CancellationToken ct)
    {
        var companyId = _currentCompany.GetCompanyId();
        var items = await _catalogoService.ListarAsync(companyId, ct);
        return Ok(items);
    }

    [HttpGet("catalogos/categorias-servicio")]
    public async Task<IActionResult> GetCategoriasServicio(CancellationToken ct)
    {
        var items = await _consultaService.ListarCategoriasServicioAsync(ct);
        return Ok(items);
    }

    [HttpGet("catalogos/ciclos")]
    public async Task<IActionResult> GetCiclos(CancellationToken ct)
    {
        var items = await _consultaService.ListarCiclosAsync(ct);
        return Ok(items);
    }

    [HttpGet("catalogos/usuarios-recibos")]
    public async Task<IActionResult> GetUsuariosRecibos(CancellationToken ct)
    {
        var items = await _consultaService.ListarUsuariosRecibosAsync(ct);
        return Ok(items);
    }

    [HttpGet("consultas/partidas-contabilidad")]
    public async Task<IActionResult> ConsultarPartidas([FromQuery] PartidasInformeFiltroDto filtro, CancellationToken ct)
    {
        var companyId = _currentCompany.GetCompanyId();
        var resultado = await _consultaService.ConsultarPartidasAsync(companyId, filtro, ct);
        return Ok(resultado);
    }

    [HttpGet("reportes/transacciones-periodo/pdf")]
    public IActionResult GetReporteTransaccionesPeriodoPdf(
        [FromQuery] DateOnly? fechaDesde = null,
        [FromQuery] DateOnly? fechaHasta = null)
    {
        var companyId = _currentCompany.GetCompanyId();
        if (companyId <= 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Compania invalida",
                Detail = "No fue posible determinar la empresa actual."
            });
        }

        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var desde = fechaDesde ?? new DateOnly(hoy.Year, hoy.Month, 1);
        var hasta = fechaHasta ?? hoy;

        if (desde > hasta)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Parametros invalidos",
                Detail = "La fecha desde no puede ser mayor que la fecha hasta."
            });
        }

        if (companyId > int.MaxValue)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Parametros invalidos",
                Detail = "El ID de la empresa excede el rango soportado por el reporte."
            });
        }

        var reporte = new Rpt_Dev_Transacciones_Periodo();
        ReportingRuntimeBootstrap.ConfigureSqlDataSources(reporte, _configuration);
        reporte.RequestParameters = false;
        reporte.Parameters["p_Compania_ID"].Value = (int)companyId;
        reporte.Parameters["p_Compania_ID"].Visible = false;
        reporte.Parameters["p_Fecha_Inicio"].Value = desde;
        reporte.Parameters["p_Fecha_Inicio"].Visible = false;
        reporte.Parameters["p_Fecha_Fin"].Value = hasta;
        reporte.Parameters["p_Fecha_Fin"].Visible = false;

        using var stream = new MemoryStream();
        reporte.ExportToPdf(stream);

        Response.Headers.ContentDisposition =
            $"inline; filename=transacciones-periodo-{desde.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}-{hasta.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.pdf";

        return File(stream.ToArray(), "application/pdf");
    }

    [HttpGet("reportes/saldo-clientes-categoria/pdf")]
    public IActionResult GetReporteSaldoClientesCategoriaPdf(
        [FromQuery] DateOnly? fechaCorte = null,
        [FromQuery] int categoriaServicioId = 0,
        [FromQuery] int estadoCliente = 0)
    {
        var companyId = _currentCompany.GetCompanyId();
        if (companyId <= 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Compania invalida",
                Detail = "No fue posible determinar la empresa actual."
            });
        }

        if (companyId > int.MaxValue)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Parametros invalidos",
                Detail = "El ID de la empresa excede el rango soportado por el reporte."
            });
        }

        var corte = fechaCorte ?? DateOnly.FromDateTime(DateTime.Today);

        var reporte = new Rpt_Dev_Saldo_Clientes_Categoria();
        ReportingRuntimeBootstrap.ConfigureSqlDataSources(reporte, _configuration);
        reporte.RequestParameters = false;
        reporte.Parameters["p_Compania_ID"].Value = (int)companyId;
        reporte.Parameters["p_Compania_ID"].Visible = false;
        reporte.Parameters["p_Fecha_Corte"].Value = corte;
        reporte.Parameters["p_Fecha_Corte"].Visible = false;
        reporte.Parameters["p_Categoria_Servicio_ID"].Value = categoriaServicioId;
        reporte.Parameters["p_Categoria_Servicio_ID"].Visible = false;
        reporte.Parameters["p_Estado_Cliente"].Value = estadoCliente;
        reporte.Parameters["p_Estado_Cliente"].Visible = false;

        using var stream = new MemoryStream();
        reporte.ExportToPdf(stream);

        Response.Headers.ContentDisposition =
            $"inline; filename=saldo-clientes-categoria-{corte.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.pdf";

        return File(stream.ToArray(), "application/pdf");
    }

    [HttpGet("reportes/desglose-facturacion/pdf")]
    public IActionResult GetReporteDesgloseFacturacionPdf(
        [FromQuery] DateOnly? fechaDesde = null,
        [FromQuery] DateOnly? fechaHasta = null)
    {
        var companyId = _currentCompany.GetCompanyId();
        if (companyId <= 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Compania invalida",
                Detail = "No fue posible determinar la empresa actual."
            });
        }

        if (companyId > int.MaxValue)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Parametros invalidos",
                Detail = "El ID de la empresa excede el rango soportado por el reporte."
            });
        }

        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var desde = fechaDesde ?? new DateOnly(hoy.Year, hoy.Month, 1);
        var hasta = fechaHasta ?? hoy;

        if (desde > hasta)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Parametros invalidos",
                Detail = "La fecha desde no puede ser mayor que la fecha hasta."
            });
        }

        var reporte = new Rpt_Dev_Desglose_Facturacion();
        ReportingRuntimeBootstrap.ConfigureSqlDataSources(reporte, _configuration);
        reporte.RequestParameters = false;
        reporte.Parameters["p_Compania_ID"].Value = (int)companyId;
        reporte.Parameters["p_Compania_ID"].Visible = false;
        reporte.Parameters["p_Fecha_Inicio"].Value = desde;
        reporte.Parameters["p_Fecha_Inicio"].Visible = false;
        reporte.Parameters["p_Fecha_Fin"].Value = hasta;
        reporte.Parameters["p_Fecha_Fin"].Visible = false;

        using var stream = new MemoryStream();
        reporte.ExportToPdf(stream);

        Response.Headers.ContentDisposition =
            $"inline; filename=desglose-facturacion-{desde.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}-{hasta.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.pdf";

        return File(stream.ToArray(), "application/pdf");
    }

    [HttpGet("reportes/movimiento-periodo/pdf")]
    public IActionResult GetReporteMovimientoPeriodoPdf(
        [FromQuery] DateOnly? fechaDesde = null,
        [FromQuery] DateOnly? fechaHasta = null)
    {
        var companyId = _currentCompany.GetCompanyId();
        if (companyId <= 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Compania invalida",
                Detail = "No fue posible determinar la empresa actual."
            });
        }

        if (companyId > int.MaxValue)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Parametros invalidos",
                Detail = "El ID de la empresa excede el rango soportado por el reporte."
            });
        }

        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var desde = fechaDesde ?? new DateOnly(hoy.Year, hoy.Month, 1);
        var hasta = fechaHasta ?? hoy;

        if (desde > hasta)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Parametros invalidos",
                Detail = "La fecha desde no puede ser mayor que la fecha hasta."
            });
        }

        var reporte = new Rpt_Dev_Movimiento_Periodo();
        ReportingRuntimeBootstrap.ConfigureSqlDataSources(reporte, _configuration);
        reporte.RequestParameters = false;
        reporte.Parameters["p_Compania_ID"].Value = (int)companyId;
        reporte.Parameters["p_Compania_ID"].Visible = false;
        reporte.Parameters["p_Fecha_Inicio"].Value = desde;
        reporte.Parameters["p_Fecha_Inicio"].Visible = false;
        reporte.Parameters["p_Fecha_Fin"].Value = hasta;
        reporte.Parameters["p_Fecha_Fin"].Visible = false;

        using var stream = new MemoryStream();
        reporte.ExportToPdf(stream);

        Response.Headers.ContentDisposition =
            $"inline; filename=movimiento-periodo-{desde.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}-{hasta.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.pdf";

        return File(stream.ToArray(), "application/pdf");
    }

    [HttpGet("reportes/auxiliar-lectura/pdf")]
    public IActionResult GetReporteAuxiliarLecturaPdf(
        [FromQuery] int? anio = null,
        [FromQuery] int? mes = null,
        [FromQuery] int cicloId = 0,
        [FromQuery] bool soloPendientes = false)
    {
        var companyId = _currentCompany.GetCompanyId();
        if (companyId <= 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Compania invalida",
                Detail = "No fue posible determinar la empresa actual."
            });
        }

        if (companyId > int.MaxValue)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Parametros invalidos",
                Detail = "El ID de la empresa excede el rango soportado por el reporte."
            });
        }

        var hoy = DateTime.Today;
        var periodoAnio = anio ?? hoy.Year;
        var periodoMes = mes ?? hoy.Month;

        if (periodoAnio < 2000 || periodoAnio > 2099)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Parametros invalidos",
                Detail = "El año del periodo no es valido."
            });
        }

        if (periodoMes < 1 || periodoMes > 12)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Parametros invalidos",
                Detail = "El mes del periodo no es valido."
            });
        }

        var reporte = new Rpt_Dev_Auxiliar_Lectura();
        ReportingRuntimeBootstrap.ConfigureSqlDataSources(reporte, _configuration);
        reporte.RequestParameters = false;
        reporte.Parameters["p_Compania_ID"].Value = (int)companyId;
        reporte.Parameters["p_Compania_ID"].Visible = false;
        reporte.Parameters["p_Anio"].Value = periodoAnio;
        reporte.Parameters["p_Anio"].Visible = false;
        reporte.Parameters["p_Mes"].Value = periodoMes;
        reporte.Parameters["p_Mes"].Visible = false;
        reporte.Parameters["p_Ciclo_ID"].Value = cicloId;
        reporte.Parameters["p_Ciclo_ID"].Visible = false;
        reporte.Parameters["p_Solo_Pendientes"].Value = soloPendientes;
        reporte.Parameters["p_Solo_Pendientes"].Visible = false;

        using var stream = new MemoryStream();
        reporte.ExportToPdf(stream);

        Response.Headers.ContentDisposition =
            $"inline; filename=auxiliar-lectura-{periodoAnio.ToString(CultureInfo.InvariantCulture)}{periodoMes.ToString("00", CultureInfo.InvariantCulture)}.pdf";

        return File(stream.ToArray(), "application/pdf");
    }

    [HttpGet("reportes/historial-recibos-emitidos/pdf")]
    public IActionResult GetReporteHistorialRecibosEmitidosPdf(
        [FromQuery] DateOnly? fechaDesde = null,
        [FromQuery] DateOnly? fechaHasta = null,
        [FromQuery] string? usuario = null)
    {
        var companyId = _currentCompany.GetCompanyId();
        if (companyId <= 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Compania invalida",
                Detail = "No fue posible determinar la empresa actual."
            });
        }

        if (companyId > int.MaxValue)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Parametros invalidos",
                Detail = "El ID de la empresa excede el rango soportado por el reporte."
            });
        }

        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var desde = fechaDesde ?? hoy;
        var hasta = fechaHasta ?? hoy;
        var usuarioNormalizado = string.IsNullOrWhiteSpace(usuario) ? string.Empty : usuario.Trim();

        if (desde > hasta)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Parametros invalidos",
                Detail = "La fecha desde no puede ser mayor que la fecha hasta."
            });
        }

        var reporte = new Rpt_Dev_Historial_Recibos_Emitidos();
        ReportingRuntimeBootstrap.ConfigureSqlDataSources(reporte, _configuration);
        reporte.RequestParameters = false;
        reporte.Parameters["p_Compania_ID"].Value = (int)companyId;
        reporte.Parameters["p_Compania_ID"].Visible = false;
        reporte.Parameters["p_Fecha_Desde"].Value = desde;
        reporte.Parameters["p_Fecha_Desde"].Visible = false;
        reporte.Parameters["p_Fecha_Hasta"].Value = hasta;
        reporte.Parameters["p_Fecha_Hasta"].Visible = false;
        reporte.Parameters["p_Usuario"].Value = usuarioNormalizado;
        reporte.Parameters["p_Usuario"].Visible = false;

        using var stream = new MemoryStream();
        reporte.ExportToPdf(stream);

        Response.Headers.ContentDisposition =
            $"inline; filename=historial-recibos-emitidos-{desde.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}-{hasta.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.pdf";

        return File(stream.ToArray(), "application/pdf");
    }

    [HttpGet("reportes/saldo-clientes-antiguedad/pdf")]
    public IActionResult GetReporteSaldoClientesAntiguedadPdf(
        [FromQuery] DateOnly? fechaCorte = null,
        [FromQuery] int diasMinimos = 60,
        [FromQuery] int estadoCliente = 0,
        [FromQuery] int cicloId = 0)
    {
        var companyId = _currentCompany.GetCompanyId();
        if (companyId <= 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Compania invalida",
                Detail = "No fue posible determinar la empresa actual."
            });
        }

        if (companyId > int.MaxValue)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Parametros invalidos",
                Detail = "El ID de la empresa excede el rango soportado por el reporte."
            });
        }

        if (diasMinimos <= 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Parametros invalidos",
                Detail = "Los dias minimos deben ser mayores que cero."
            });
        }

        var corte = fechaCorte ?? DateOnly.FromDateTime(DateTime.Today);

        var reporte = new Rpt_Dev_Saldo_Clientes_Antiguedad();
        ReportingRuntimeBootstrap.ConfigureSqlDataSources(reporte, _configuration);
        reporte.RequestParameters = false;
        reporte.Parameters["p_Compania_ID"].Value = (int)companyId;
        reporte.Parameters["p_Compania_ID"].Visible = false;
        reporte.Parameters["p_Fecha_Corte"].Value = corte;
        reporte.Parameters["p_Fecha_Corte"].Visible = false;
        reporte.Parameters["p_Dias_Minimos"].Value = diasMinimos;
        reporte.Parameters["p_Dias_Minimos"].Visible = false;
        reporte.Parameters["p_Estado_Cliente"].Value = estadoCliente;
        reporte.Parameters["p_Estado_Cliente"].Visible = false;
        reporte.Parameters["p_Ciclo_ID"].Value = cicloId;
        reporte.Parameters["p_Ciclo_ID"].Visible = false;

        using var stream = new MemoryStream();
        reporte.ExportToPdf(stream);

        Response.Headers.ContentDisposition =
            $"inline; filename=saldo-clientes-antiguedad-{corte.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.pdf";

        return File(stream.ToArray(), "application/pdf");
    }

    [HttpGet("reportes/analisis-antiguedad-cobros/pdf")]
    public IActionResult GetReporteAnalisisAntiguedadCobrosPdf(
        [FromQuery] DateOnly? fechaBase = null,
        [FromQuery] int retrocesoValor = 12,
        [FromQuery] string? unidadTiempo = null)
    {
        var companyId = _currentCompany.GetCompanyId();
        if (companyId <= 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Compania invalida",
                Detail = "No fue posible determinar la empresa actual."
            });
        }

        if (companyId > int.MaxValue)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Parametros invalidos",
                Detail = "El ID de la empresa excede el rango soportado por el reporte."
            });
        }

        if (retrocesoValor <= 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Parametros invalidos",
                Detail = "El retroceso debe ser mayor que cero."
            });
        }

        var unidad = string.IsNullOrWhiteSpace(unidadTiempo) ? "MESES" : unidadTiempo.Trim().ToUpperInvariant();
        if (unidad is not ("MESES" or "ANIOS"))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Parametros invalidos",
                Detail = "La unidad de tiempo soportada es MESES o ANIOS."
            });
        }

        var baseDate = fechaBase ?? DateOnly.FromDateTime(DateTime.Today);

        var reporte = new Rpt_Dev_Analisis_Antiguedad_Cobros();
        ReportingRuntimeBootstrap.ConfigureSqlDataSources(reporte, _configuration);
        reporte.RequestParameters = false;
        reporte.Parameters["p_Compania_ID"].Value = (int)companyId;
        reporte.Parameters["p_Compania_ID"].Visible = false;
        reporte.Parameters["p_Fecha_Base"].Value = baseDate;
        reporte.Parameters["p_Fecha_Base"].Visible = false;
        reporte.Parameters["p_Retroceso_Valor"].Value = retrocesoValor;
        reporte.Parameters["p_Retroceso_Valor"].Visible = false;
        reporte.Parameters["p_Unidad_Tiempo"].Value = unidad;
        reporte.Parameters["p_Unidad_Tiempo"].Visible = false;

        using var stream = new MemoryStream();
        reporte.ExportToPdf(stream);

        Response.Headers.ContentDisposition =
            $"inline; filename=analisis-antiguedad-cobros-{baseDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.pdf";

        return File(stream.ToArray(), "application/pdf");
    }

    [HttpGet("reportes/saldo-clientes-ciclo/pdf")]
    public IActionResult GetReporteSaldoClientesCicloPdf(
        [FromQuery] DateOnly? fechaDesde = null,
        [FromQuery] DateOnly? fechaHasta = null,
        [FromQuery] int cicloId = 0)
    {
        var companyId = _currentCompany.GetCompanyId();
        if (companyId <= 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Compania invalida",
                Detail = "No fue posible determinar la empresa actual."
            });
        }

        if (companyId > int.MaxValue)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Parametros invalidos",
                Detail = "El ID de la empresa excede el rango soportado por el reporte."
            });
        }

        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var desde = fechaDesde ?? new DateOnly(hoy.Year, hoy.Month, 1);
        var hasta = fechaHasta ?? hoy;

        if (desde > hasta)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Parametros invalidos",
                Detail = "La fecha desde no puede ser mayor que la fecha hasta."
            });
        }

        var reporte = new Rpt_Dev_Saldo_Clientes_Ciclo();
        ReportingRuntimeBootstrap.ConfigureSqlDataSources(reporte, _configuration);

        if (cicloId > 0)
        {
            foreach (var dataSource in DevExpress.XtraReports.DataSourceManager.GetDataSources<DevExpress.DataAccess.Sql.SqlDataSource>(reporte, includeSubReports: true))
            {
                foreach (var query in dataSource.Queries.OfType<DevExpress.DataAccess.Sql.CustomSqlQuery>())
                {
                    if (query.Sql.Contains("public.rep_saldo_clientes_ciclo", StringComparison.OrdinalIgnoreCase))
                    {
                        query.Sql = $"SELECT * FROM public.rep_saldo_clientes_ciclo(@CompaniaID, @FechaDesde::date, @FechaHasta::date, {cicloId})";
                    }
                }
            }
        }

        reporte.RequestParameters = false;
        reporte.Parameters["p_Compania_ID"].Value = (int)companyId;
        reporte.Parameters["p_Compania_ID"].Visible = false;
        reporte.Parameters["p_Fecha_Desde"].Value = desde;
        reporte.Parameters["p_Fecha_Desde"].Visible = false;
        reporte.Parameters["p_Fecha_Hasta"].Value = hasta;
        reporte.Parameters["p_Fecha_Hasta"].Visible = false;

        using var stream = new MemoryStream();
        reporte.ExportToPdf(stream);

        Response.Headers.ContentDisposition =
            $"inline; filename=saldo-clientes-ciclo-{desde.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}-{hasta.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.pdf";

        return File(stream.ToArray(), "application/pdf");
    }

    [HttpGet("reportes/saldo-clientes-categoria-cobranza/pdf")]
    public IActionResult GetReporteSaldoClientesCategoriaCobranzaPdf(
        [FromQuery] DateOnly? fechaDesde = null,
        [FromQuery] DateOnly? fechaHasta = null,
        [FromQuery] int categoriaServicioId = 0)
    {
        var companyId = _currentCompany.GetCompanyId();
        if (companyId <= 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Compania invalida",
                Detail = "No fue posible determinar la empresa actual."
            });
        }

        if (companyId > int.MaxValue)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Parametros invalidos",
                Detail = "El ID de la empresa excede el rango soportado por el reporte."
            });
        }

        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var desde = fechaDesde ?? new DateOnly(hoy.Year, hoy.Month, 1);
        var hasta = fechaHasta ?? hoy;

        if (desde > hasta)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Parametros invalidos",
                Detail = "La fecha desde no puede ser mayor que la fecha hasta."
            });
        }

        var reporte = new Rpt_Dev_Saldo_Clientes_Categoria_Cobranza();
        ReportingRuntimeBootstrap.ConfigureSqlDataSources(reporte, _configuration);
        reporte.RequestParameters = false;
        reporte.Parameters["p_Compania_ID"].Value = (int)companyId;
        reporte.Parameters["p_Compania_ID"].Visible = false;
        reporte.Parameters["p_Fecha_Desde"].Value = desde;
        reporte.Parameters["p_Fecha_Desde"].Visible = false;
        reporte.Parameters["p_Fecha_Hasta"].Value = hasta;
        reporte.Parameters["p_Fecha_Hasta"].Visible = false;
        reporte.Parameters["p_Categoria_Servicio_ID"].Value = categoriaServicioId;
        reporte.Parameters["p_Categoria_Servicio_ID"].Visible = false;

        using var stream = new MemoryStream();
        reporte.ExportToPdf(stream);

        Response.Headers.ContentDisposition =
            $"inline; filename=saldo-clientes-categoria-cobranza-{desde.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}-{hasta.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.pdf";

        return File(stream.ToArray(), "application/pdf");
    }

    [HttpGet("reportes/informe-recaudacion/pdf")]
    public IActionResult GetReporteRecaudacionPdf(
        [FromQuery] DateOnly? fechaDesde = null,
        [FromQuery] DateOnly? fechaHasta = null,
        [FromQuery] string? medioPagoCodigo = null)
    {
        var companyId = _currentCompany.GetCompanyId();
        if (companyId <= 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Compania invalida",
                Detail = "No fue posible determinar la empresa actual."
            });
        }

        if (companyId > int.MaxValue)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Parametros invalidos",
                Detail = "El ID de la empresa excede el rango soportado por el reporte."
            });
        }

        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var desde = fechaDesde ?? new DateOnly(hoy.Year, hoy.Month, 1);
        var hasta = fechaHasta ?? hoy;

        if (desde > hasta)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Parametros invalidos",
                Detail = "La fecha desde no puede ser mayor que la fecha hasta."
            });
        }

        var reporte = new Rpt_Dev_Recaudacion();
        ReportingRuntimeBootstrap.ConfigureSqlDataSources(reporte, _configuration);
        reporte.RequestParameters = false;
        reporte.Parameters["p_Compania_ID"].Value = (int)companyId;
        reporte.Parameters["p_Compania_ID"].Visible = false;
        reporte.Parameters["p_Fecha_Desde"].Value = desde;
        reporte.Parameters["p_Fecha_Desde"].Visible = false;
        reporte.Parameters["p_Fecha_Hasta"].Value = hasta;
        reporte.Parameters["p_Fecha_Hasta"].Visible = false;
        reporte.Parameters["p_Medio_Pago_Codigo"].Value = string.IsNullOrWhiteSpace(medioPagoCodigo) ? (object)DBNull.Value : medioPagoCodigo.Trim();
        reporte.Parameters["p_Medio_Pago_Codigo"].Visible = false;

        using var stream = new MemoryStream();
        reporte.ExportToPdf(stream);

        Response.Headers.ContentDisposition =
            $"inline; filename=informe-recaudacion-{desde.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}-{hasta.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.pdf";

        return File(stream.ToArray(), "application/pdf");
    }

    [HttpGet("reportes/saldo-clientes-categoria-detalle/pdf")]
    public IActionResult GetReporteSaldoClientesCategoriaDetallePdf(
        [FromQuery] DateOnly? fechaDesde = null,
        [FromQuery] DateOnly? fechaHasta = null,
        [FromQuery] int categoriaServicioId = 0)
    {
        var companyId = _currentCompany.GetCompanyId();
        if (companyId <= 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Compania invalida",
                Detail = "No fue posible determinar la empresa actual."
            });
        }

        if (companyId > int.MaxValue)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Parametros invalidos",
                Detail = "El ID de la empresa excede el rango soportado por el reporte."
            });
        }

        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var desde = fechaDesde ?? new DateOnly(hoy.Year, hoy.Month, 1);
        var hasta = fechaHasta ?? hoy;

        if (desde > hasta)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Parametros invalidos",
                Detail = "La fecha desde no puede ser mayor que la fecha hasta."
            });
        }

        var reporte = new Rpt_Dev_Saldo_Clientes_Categoria_Detalle();
        ReportingRuntimeBootstrap.ConfigureSqlDataSources(reporte, _configuration);
        reporte.RequestParameters = false;
        reporte.Parameters["p_Compania_ID"].Value = (int)companyId;
        reporte.Parameters["p_Compania_ID"].Visible = false;
        reporte.Parameters["p_Fecha_Desde"].Value = desde;
        reporte.Parameters["p_Fecha_Desde"].Visible = false;
        reporte.Parameters["p_Fecha_Hasta"].Value = hasta;
        reporte.Parameters["p_Fecha_Hasta"].Visible = false;
        reporte.Parameters["p_Categoria_Servicio_ID"].Value = categoriaServicioId;
        reporte.Parameters["p_Categoria_Servicio_ID"].Visible = false;

        using var stream = new MemoryStream();
        reporte.ExportToPdf(stream);

        Response.Headers.ContentDisposition =
            $"inline; filename=saldo-clientes-categoria-detalle-{desde.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}-{hasta.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.pdf";

        return File(stream.ToArray(), "application/pdf");
    }
}
