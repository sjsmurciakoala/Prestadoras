using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Bancos;
using SIAD.Core.Tenancy;
using SIAD.Reports;
using SIAD.Services;
using SIAD.Services.Bancos;
using System.IO;
using System.Security.Claims;
using apc.Security;
using apc.Reportes;

namespace apc.Controllers.Bancos;

[ApiController]
[Route("api/bancos/transacciones")]
[ModuleAuthorize(PermissionModules.Bancos)]
public sealed class BanTransaccionesController : ControllerBase
{
    private readonly IBanTransaccionesService transaccionesService;
    private readonly ICurrentCompanyService currentCompanyService;
    private readonly IConfiguration configuration;

    public BanTransaccionesController(
        IBanTransaccionesService transaccionesService,
        ICurrentCompanyService currentCompanyService,
        IConfiguration configuration)
    {
        this.transaccionesService = transaccionesService;
        this.currentCompanyService = currentCompanyService;
        this.configuration = configuration;
    }

    [HttpGet]
    public async Task<ActionResult<List<BanTransaccionListDto>>> GetTransacciones(
        long companyId,
        [FromQuery] long? bancoId = null,
        [FromQuery] long? bancoCuentaId = null,
        [FromQuery] DateOnly? fechaDesde = null,
        [FromQuery] DateOnly? fechaHasta = null,
        [FromQuery] bool incluirAnuladas = false,
        CancellationToken ct = default)
    {
        try
        {
            // Validar que la empresa sea válida
            if (companyId <= 0)
            {
                return BadRequest(CrearProblemDetalle(
                    "Compañía Inválida",
                    "El ID de la compañía debe ser un número positivo."));
            }

            var transacciones = await transaccionesService.GetTransaccionesAsync(
                companyId,
                bancoId,
                bancoCuentaId,
                fechaDesde,
                fechaHasta,
                incluirAnuladas,
                ct);

            return Ok(transacciones);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(CrearProblemDetalle("Parámetro Inválido", ex.Message));
        }
        catch (PostgresException ex)
        {
            var detalle = string.IsNullOrWhiteSpace(ex.MessageText) ? ex.Message : ex.MessageText;
            return BadRequest(CrearProblemDetalle(
                "Error de Base de Datos",
                detalle));
        }
        catch (Exception ex)
        {
            return StatusCode(500, CrearProblemDetalle(
                "Error del Servidor",
                $"No fue posible obtener las transacciones bancarias: {ex.Message}"));
        }
    }

    [HttpGet("{banKardexId}")]
    public async Task<ActionResult<BanTransaccionListDto>> GetTransaccionById(
        long banKardexId,
        [FromQuery] long companyId,
        CancellationToken ct = default)
    {
        try
        {
            if (banKardexId <= 0 || companyId <= 0)
            {
                return BadRequest(CrearProblemDetalle(
                    "Parámetros Inválidos",
                    "Los IDs deben ser números positivos."));
            }

            var transaccion = await transaccionesService.GetTransaccionByIdAsync(
                banKardexId,
                companyId,
                ct);

            if (transaccion == null)
            {
                return NotFound(CrearProblemDetalle(
                    "No Encontrada",
                    "La transacción bancaria especificada no existe."));
            }

            return Ok(transaccion);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(CrearProblemDetalle("Parámetro Inválido", ex.Message));
        }
        catch (PostgresException ex)
        {
            var detalle = string.IsNullOrWhiteSpace(ex.MessageText) ? ex.Message : ex.MessageText;
            return BadRequest(CrearProblemDetalle(
                "Error de Base de Datos",
                detalle));
        }
        catch (Exception ex)
        {
            return StatusCode(500, CrearProblemDetalle(
                "Error del Servidor",
                $"No fue posible obtener la transacción bancaria: {ex.Message}"));
        }
    }

    [HttpGet("{banKardexId}/detalle")]
    public async Task<ActionResult<BanTransaccionDetalleDto>> GetTransaccionDetalle(
        long banKardexId,
        [FromQuery] long companyId,
        CancellationToken ct = default)
    {
        try
        {
            if (banKardexId <= 0 || companyId <= 0)
            {
                return BadRequest(CrearProblemDetalle(
                    "Parámetros Inválidos",
                    "Los IDs deben ser números positivos."));
            }

            var transaccion = await transaccionesService.GetTransaccionDetalleAsync(
                banKardexId,
                companyId,
                ct);

            if (transaccion == null)
            {
                return NotFound(CrearProblemDetalle(
                    "No Encontrada",
                    "La transacción bancaria especificada no existe."));
            }

            return Ok(transaccion);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(CrearProblemDetalle("Parámetro Inválido", ex.Message));
        }
        catch (PostgresException ex)
        {
            var detalle = string.IsNullOrWhiteSpace(ex.MessageText) ? ex.Message : ex.MessageText;
            return BadRequest(CrearProblemDetalle(
                "Error de Base de Datos",
                detalle));
        }
        catch (Exception ex)
        {
            return StatusCode(500, CrearProblemDetalle(
                "Error del Servidor",
                $"No fue posible obtener el detalle de la transacción bancaria: {ex.Message}"));
        }
    }

    [HttpGet("{banKardexId}/reporte")]
    public async Task<IActionResult> GetReporteTransaccion(
        long banKardexId,
        CancellationToken ct = default)
    {
        try
        {
            if (banKardexId <= 0)
            {
                return BadRequest(CrearProblemDetalle(
                    "Parametros Invalidos",
                    "El ID de la transaccion debe ser un numero positivo."));
            }

            var companyId = currentCompanyService.GetCompanyId();
            if (companyId <= 0)
            {
                return BadRequest(CrearProblemDetalle(
                    "Compania Invalida",
                    "No fue posible determinar la empresa actual."));
            }

            var transaccion = await transaccionesService.GetTransaccionDetalleAsync(
                banKardexId,
                companyId,
                ct);

            if (transaccion is null)
            {
                return NotFound(CrearProblemDetalle(
                    "No Encontrada",
                    "La transaccion bancaria especificada no existe."));
            }

            if (companyId > int.MaxValue || banKardexId > int.MaxValue)
            {
                return BadRequest(CrearProblemDetalle(
                    "Parametros Invalidos",
                    "Los IDs exceden el rango soportado por el reporte."));
            }

            var reporte = new Rpt_DE_Transacciones_Bancarias();
            ReportingRuntimeBootstrap.ConfigureSqlDataSources(reporte, configuration);
            reporte.RequestParameters = false;
            reporte.Parameters["id_compani"].Value = (int)companyId;
            reporte.Parameters["id_compani"].Visible = false;
            reporte.Parameters["kdx_id"].Value = (int)banKardexId;
            reporte.Parameters["kdx_id"].Visible = false;

            using var stream = new MemoryStream();
            reporte.ExportToPdf(stream);

            return File(stream.ToArray(), "application/pdf");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(CrearProblemDetalle("Parametro Invalido", ex.Message));
        }
        catch (PostgresException ex)
        {
            var detalle = string.IsNullOrWhiteSpace(ex.MessageText) ? ex.Message : ex.MessageText;
            return BadRequest(CrearProblemDetalle(
                "Error de Base de Datos",
                detalle));
        }
        catch (Exception ex)
        {
            return StatusCode(500, CrearProblemDetalle(
                "Error del Servidor",
                $"No fue posible generar el reporte: {ex.Message}"));
        }
    }

    [HttpGet("reporte/lista")]
    public IActionResult GetReporteListaTransacciones(
        [FromQuery] DateOnly? fechaDesde = null,
        [FromQuery] DateOnly? fechaHasta = null)
    {
        try
        {
            var companyId = currentCompanyService.GetCompanyId();
            if (companyId <= 0)
            {
                return BadRequest(CrearProblemDetalle(
                    "Compania Invalida",
                    "No fue posible determinar la empresa actual."));
            }

            var hoy = DateOnly.FromDateTime(DateTime.Today);
            var desde = fechaDesde ?? new DateOnly(hoy.Year, hoy.Month, 1);
            var hasta = fechaHasta ?? hoy;

            if (desde > hasta)
            {
                return BadRequest(CrearProblemDetalle(
                    "Parametros Invalidos",
                    "La fecha inicial no puede ser mayor que la fecha final."));
            }

            if (companyId > int.MaxValue)
            {
                return BadRequest(CrearProblemDetalle(
                    "Parametros Invalidos",
                    "El ID de la empresa excede el rango soportado por el reporte."));
            }

            var reporte = new Rpt_Dev_Lista_Transacciones_Bancarias();
            ReportingRuntimeBootstrap.ConfigureSqlDataSources(reporte, configuration);
            reporte.RequestParameters = false;
            reporte.Parameters["p_Compania_ID"].Value = (int)companyId;
            reporte.Parameters["p_Compania_ID"].Visible = false;
            reporte.Parameters["p_Fecha_Inicio"].Value = desde;
            reporte.Parameters["p_Fecha_Inicio"].Visible = false;
            reporte.Parameters["p_Fecha_Fin"].Value = hasta;
            reporte.Parameters["p_Fecha_Fin"].Visible = false;

            using var stream = new MemoryStream();
            reporte.ExportToPdf(stream);

            return File(stream.ToArray(), "application/pdf");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(CrearProblemDetalle("Parametro Invalido", ex.Message));
        }
        catch (PostgresException ex)
        {
            var detalle = string.IsNullOrWhiteSpace(ex.MessageText) ? ex.Message : ex.MessageText;
            return BadRequest(CrearProblemDetalle(
                "Error de Base de Datos",
                detalle));
        }
        catch (Exception ex)
        {
            return StatusCode(500, CrearProblemDetalle(
                "Error del Servidor",
                $"No fue posible generar el reporte de transacciones: {ex.Message}"));
        }
    }

    [HttpPost]
    public async Task<ActionResult<(long, decimal)>> RegistrarMovimiento(
        [FromBody] BanTransaccionCreateDto dto,
        CancellationToken ct = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(CrearProblemDetalle(
                    "Validación Fallida",
                    "Los datos proporcionados no son válidos."));
            }

            var companyId = currentCompanyService.GetCompanyId();
            if (companyId <= 0)
            {
                return BadRequest(CrearProblemDetalle(
                    "Compañía Inválida",
                    "El ID de la compañía debe ser un número positivo."));
            }

            var usuario = User.Identity?.Name 
                          ?? User.FindFirst(ClaimTypes.Email)?.Value 
                          ?? "Sistema";

            var contraLineas = dto.ContraCuentas?
                .Where(l => l != null && l.CuentaId > 0 && l.Monto > 0)
                .ToList()
                ?? new List<BanTransaccionContraLineaDto>();

            if (contraLineas.Count == 0 && dto.ContraCuentaId.HasValue && dto.ContraCuentaId.Value > 0)
            {
                contraLineas.Add(new BanTransaccionContraLineaDto
                {
                    CuentaId = dto.ContraCuentaId.Value,
                    Monto = dto.Monto,
                    Descripcion = dto.Descripcion,
                    SourceDocument = string.IsNullOrWhiteSpace(dto.SourceDocument) ? dto.Referencia : dto.SourceDocument
                });
            }

            if (contraLineas.Count == 0)
            {
                return BadRequest(CrearProblemDetalle(
                    "Validación Fallida",
                    "Agregue al menos una contracuenta válida."));
            }

            var resultado = await transaccionesService.RegistrarMovimientoAsync(
                dto.BancoCuentaId,
                dto.IdTipoTransaccion,
                dto.FechaMovimiento,
                dto.Descripcion,
                dto.Referencia,
                dto.SourceDocument,
                dto.TasaCambio,
                dto.Monto,
                contraLineas,
                usuario,
                ct);

            return Ok(resultado);
        }
        catch (ArgumentNullException ex)
        {
            return BadRequest(CrearProblemDetalle(
                "Validación Fallida",
                $"Campo requerido: {ex.ParamName}"));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(CrearProblemDetalle("Validación Fallida", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(CrearProblemDetalle(
                "Recurso No Encontrado",
                ex.Message));
        }
        catch (PostgresException ex)
        {
            var detalle = string.IsNullOrWhiteSpace(ex.MessageText) ? ex.Message : ex.MessageText;
            return BadRequest(CrearProblemDetalle(
                "Error de Base de Datos",
                detalle));
        }
        catch (Exception ex)
        {
            return StatusCode(500, CrearProblemDetalle(
                "Error del Servidor",
                $"No fue posible registrar la transacción bancaria: {ex.Message}"));
        }
    }

    [HttpPost("anular")]
    public async Task<ActionResult<(long, decimal)>> AnularMovimiento(
        [FromBody] BanTransaccionAnularDto dto,
        CancellationToken ct = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(CrearProblemDetalle(
                    "Validación Fallida",
                    "Los datos proporcionados no son válidos."));
            }

            var companyId = currentCompanyService.GetCompanyId();
            if (companyId <= 0)
            {
                return BadRequest(CrearProblemDetalle(
                    "Compañía Inválida",
                    "El ID de la compañía debe ser un número positivo."));
            }

            var usuario = User.Identity?.Name 
                          ?? User.FindFirst(ClaimTypes.Email)?.Value 
                          ?? "Sistema";

            var resultado = await transaccionesService.AnularMovimientoAsync(
                dto.BancoCuentaId,
                dto.BanKardexIdOriginal,
                dto.Motivo,
                usuario,
                ct);

            return Ok(resultado);
        }
        catch (ArgumentNullException ex)
        {
            return BadRequest(CrearProblemDetalle(
                "Validación Fallida",
                $"Campo requerido: {ex.ParamName}"));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(CrearProblemDetalle("Validación Fallida", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(CrearProblemDetalle(
                "Recurso No Encontrado",
                ex.Message));
        }
        catch (PostgresException ex)
        {
            var detalle = string.IsNullOrWhiteSpace(ex.MessageText) ? ex.Message : ex.MessageText;
            return BadRequest(CrearProblemDetalle(
                "Error de Base de Datos",
                detalle));
        }
        catch (Exception ex)
        {
            return StatusCode(500, CrearProblemDetalle(
                "Error del Servidor",
                $"No fue posible anular la transacción bancaria: {ex.Message}"));
        }
    }

    [HttpGet("estado-cuenta/excel")]
    public async Task<IActionResult> ExportarEstadoCuentaExcel(
        [FromQuery] long bancoCuentaId,
        [FromQuery] DateOnly? fechaDesde = null,
        [FromQuery] DateOnly? fechaHasta = null,
        CancellationToken ct = default)
    {
        try
        {
            if (bancoCuentaId <= 0)
            {
                return BadRequest(CrearProblemDetalle(
                    "Parámetro Inválido",
                    "Debe indicar la cuenta bancaria."));
            }

            var companyId = currentCompanyService.GetCompanyId();
            var estado = await transaccionesService.GetEstadoCuentaAsync(
                companyId, bancoCuentaId, fechaDesde, fechaHasta, ct);

            if (estado is null)
            {
                return NotFound(CrearProblemDetalle(
                    "Cuenta no encontrada",
                    "La cuenta bancaria no existe para la compañía actual."));
            }

            var content = ConstruirEstadoCuentaExcel(estado);
            var fileName =
                $"EstadoCuenta_{SanitizarNombreArchivo(estado.NumeroCuenta)}_{estado.FechaDesde:yyyyMMdd}-{estado.FechaHasta:yyyyMMdd}.xlsx";

            return File(
                content,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
        catch (PostgresException ex)
        {
            var detalle = string.IsNullOrWhiteSpace(ex.MessageText) ? ex.Message : ex.MessageText;
            return BadRequest(CrearProblemDetalle("Error de Base de Datos", detalle));
        }
        catch (Exception ex)
        {
            return StatusCode(500, CrearProblemDetalle(
                "Error del Servidor",
                $"No fue posible generar el estado de cuenta: {ex.Message}"));
        }
    }

    private static byte[] ConstruirEstadoCuentaExcel(EstadoCuentaDto estado)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Estado de Cuenta");

        const int colFecha = 1, colTipo = 2, colDoc = 3, colDesc = 4, colCargo = 5, colAbono = 6, colSaldo = 7;
        const string fmtMoneda = "#,##0.00";
        const string fmtFecha = "dd/MM/yyyy";

        ws.Cell(1, colFecha).Value = "ESTADO DE CUENTA";
        ws.Range(1, colFecha, 1, colSaldo).Merge();
        ws.Cell(1, colFecha).Style.Font.Bold = true;
        ws.Cell(1, colFecha).Style.Font.FontSize = 14;
        ws.Cell(1, colFecha).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Cell(2, colFecha).Value = "Banco:";
        ws.Cell(2, colTipo).Value = estado.BancoNombre ?? "";
        ws.Cell(3, colFecha).Value = "Cuenta:";
        ws.Cell(3, colTipo).Value = $"{estado.CuentaNombre} ({estado.NumeroCuenta})";
        ws.Cell(4, colFecha).Value = "Moneda:";
        ws.Cell(4, colTipo).Value = estado.MonedaCodigo ?? "";
        ws.Cell(4, colDesc).Value = "Período:";
        ws.Cell(4, colCargo).Value = $"{estado.FechaDesde:dd/MM/yyyy} al {estado.FechaHasta:dd/MM/yyyy}";
        ws.Range(2, colFecha, 4, colFecha).Style.Font.Bold = true;
        ws.Cell(4, colDesc).Style.Font.Bold = true;

        var headerRow = 6;
        ws.Cell(headerRow, colFecha).Value = "Fecha";
        ws.Cell(headerRow, colTipo).Value = "Tipo";
        ws.Cell(headerRow, colDoc).Value = "Documento";
        ws.Cell(headerRow, colDesc).Value = "Descripción";
        ws.Cell(headerRow, colCargo).Value = "Cargo";
        ws.Cell(headerRow, colAbono).Value = "Abono";
        ws.Cell(headerRow, colSaldo).Value = "Saldo";
        var headerRange = ws.Range(headerRow, colFecha, headerRow, colSaldo);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
        headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

        var row = headerRow + 1;
        ws.Cell(row, colDesc).Value = "SALDO ANTERIOR";
        ws.Cell(row, colDesc).Style.Font.Italic = true;
        ws.Cell(row, colSaldo).Value = estado.SaldoAnterior;
        ws.Cell(row, colSaldo).Style.NumberFormat.Format = fmtMoneda;
        row++;

        var saldoCorrido = estado.SaldoAnterior;
        foreach (var m in estado.Movimientos)
        {
            saldoCorrido += m.Monto;
            ws.Cell(row, colFecha).Value = m.FechaMovimiento.ToDateTime(TimeOnly.MinValue);
            ws.Cell(row, colFecha).Style.NumberFormat.Format = fmtFecha;
            ws.Cell(row, colTipo).Value = m.IdTipoTransaccion;
            ws.Cell(row, colDoc).Value = m.Referencia ?? "";
            ws.Cell(row, colDesc).Value = m.Descripcion;
            if (m.Monto < 0m)
            {
                ws.Cell(row, colCargo).Value = -m.Monto;
                ws.Cell(row, colCargo).Style.NumberFormat.Format = fmtMoneda;
            }
            else if (m.Monto > 0m)
            {
                ws.Cell(row, colAbono).Value = m.Monto;
                ws.Cell(row, colAbono).Style.NumberFormat.Format = fmtMoneda;
            }
            ws.Cell(row, colSaldo).Value = saldoCorrido;
            ws.Cell(row, colSaldo).Style.NumberFormat.Format = fmtMoneda;
            row++;
        }

        var totalRow = row;
        ws.Cell(totalRow, colDesc).Value = "TOTALES";
        ws.Cell(totalRow, colCargo).Value = estado.TotalCargos;
        ws.Cell(totalRow, colAbono).Value = estado.TotalAbonos;
        var totalRange = ws.Range(totalRow, colFecha, totalRow, colSaldo);
        totalRange.Style.Font.Bold = true;
        totalRange.Style.Border.TopBorder = XLBorderStyleValues.Thin;
        ws.Cell(totalRow, colCargo).Style.NumberFormat.Format = fmtMoneda;
        ws.Cell(totalRow, colAbono).Style.NumberFormat.Format = fmtMoneda;

        var finalRow = totalRow + 1;
        ws.Cell(finalRow, colDesc).Value = "SALDO FINAL";
        ws.Cell(finalRow, colSaldo).Value = estado.SaldoFinal;
        ws.Cell(finalRow, colDesc).Style.Font.Bold = true;
        ws.Cell(finalRow, colSaldo).Style.Font.Bold = true;
        ws.Cell(finalRow, colSaldo).Style.NumberFormat.Format = fmtMoneda;

        ws.Columns().AdjustToContents();
        if (ws.Column(colDesc).Width > 50)
        {
            ws.Column(colDesc).Width = 50;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string SanitizarNombreArchivo(string valor)
    {
        var limpio = new string((valor ?? string.Empty)
            .Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        return string.IsNullOrWhiteSpace(limpio) ? "cuenta" : limpio;
    }

    private ProblemDetails CrearProblemDetalle(string titulo, string detalle)
    {
        return new ProblemDetails
        {
            Title = titulo,
            Detail = detalle,
            Status = StatusCodes.Status400BadRequest
        };
    }
}




