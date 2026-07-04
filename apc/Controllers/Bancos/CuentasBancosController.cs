using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Bancos;
using SIAD.Core.Tenancy;
using SIAD.Services.Bancos;
using System.IO;
using System.Globalization;
using System.Text;
using apc.Security;

namespace apc.Controllers.Bancos;

[ApiController]
[Route("api/bancos/cuentas")]
[ModuleAuthorize(PermissionModules.Bancos)]
public sealed class CuentasBancosController : ControllerBase
{
    private readonly ICuentasBancosService cuentasService;
    private readonly ICompanyAccessValidator accessValidator;
    private readonly ICurrentCompanyService currentCompanyService;

    public CuentasBancosController(
        ICuentasBancosService cuentasService,
        ICompanyAccessValidator accessValidator,
        ICurrentCompanyService currentCompanyService)
    {
        this.cuentasService = cuentasService;
        this.accessValidator = accessValidator;
        this.currentCompanyService = currentCompanyService;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] long companyId, CancellationToken ct)
    {
        if (companyId <= 0)
        {
            return BadRequest(new { detail = "Debe proporcionar un companyId v�lido." });
        }

        if (!await accessValidator.ValidarAccesoAsync(companyId, ct))
        {
            return Forbid();
        }

        try
        {
            var cuentas = await cuentasService.GetAsync(companyId, ct);
            return Ok(cuentas);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { detail = ex.Message });
        }
    }

    [HttpGet("conciliacion")]
    public async Task<IActionResult> GetConciliacion(
        [FromQuery] long companyId,
        [FromQuery] long bancoCuentaId,
        [FromQuery] DateOnly? fechaHasta = null,
        CancellationToken ct = default)
    {
        if (companyId <= 0 || bancoCuentaId <= 0)
        {
            return BadRequest(new { detail = "Debe proporcionar companyId y bancoCuentaId validos." });
        }

        if (!await accessValidator.ValidarAccesoAsync(companyId, ct))
        {
            return Forbid();
        }

        var hasta = fechaHasta ?? DateOnly.FromDateTime(DateTime.Today);


        try
        {
            var conciliacion = await cuentasService.GetConciliacionAsync(companyId, bancoCuentaId, hasta, ct);
            return Ok(conciliacion);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { detail = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    
    [HttpGet("conciliacion/conciliadas")]
    public async Task<IActionResult> GetConciliadas(
        [FromQuery] long companyId,
        [FromQuery] long bancoCuentaId,
        [FromQuery] DateOnly? fechaDesde = null,
        [FromQuery] DateOnly? fechaHasta = null,
        CancellationToken ct = default)
    {
        if (companyId <= 0 || bancoCuentaId <= 0)
        {
            return BadRequest(new { detail = "Debe proporcionar companyId y bancoCuentaId validos." });
        }

        if (!await accessValidator.ValidarAccesoAsync(companyId, ct))
        {
            return Forbid();
        }

        var hasta = fechaHasta ?? DateOnly.FromDateTime(DateTime.Today);
        var desde = fechaDesde ?? new DateOnly(hasta.Year, hasta.Month, 1);

        if (desde > hasta)
        {
            return BadRequest(new { detail = "La fecha inicial no puede ser mayor que la fecha final." });
        }

        try
        {
            var conciliadas = await cuentasService.GetConciliadasAsync(companyId, bancoCuentaId, desde, hasta, ct);
            return Ok(conciliadas);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { detail = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }
    [HttpGet("conciliacion/plantilla")]
    public IActionResult DescargarPlantillaConciliacion()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Conciliacion");

        sheet.Cell(1, 1).Value = "ID";
        sheet.Cell(1, 2).Value = "Fecha";
        sheet.Cell(1, 3).Value = "Referencia";
        sheet.Cell(1, 4).Value = "Monto";

        sheet.Row(1).Style.Font.Bold = true;
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var content = stream.ToArray();
        var fileName = $"Conciliacion-{DateTime.Now:yyyyMMdd-HHmm}.xlsx";

        return File(
            content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    [HttpGet("server-datetime")]
    public IActionResult GetServerDateTime()
    {
        var serverDateTime = DateTime.Now;
        return Ok(new { serverDateTime });
    }

    [HttpPost("conciliacion/importar")]
    public IActionResult ImportarConciliacion([FromForm] IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { detail = "Debe adjuntar un archivo valido." });
        }
        using var stream = file.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheets.FirstOrDefault();
        if (sheet is null)
        {
            return BadRequest(new { detail = "El archivo no contiene hojas." });
        }
        var headerRow = sheet.Row(1);
        var headers = headerRow.CellsUsed()
            .ToDictionary(c => NormalizeHeader(c.GetString()), c => c.Address.ColumnNumber);
        var hasNumero = headers.TryGetValue("numerotransaccion", out var colNumero)
            || headers.TryGetValue("id", out colNumero);
        var hasFecha = headers.TryGetValue("fecha", out var colFecha);
        var hasMonto = headers.TryGetValue("monto", out var colMonto);
        var hasReferencia = headers.TryGetValue("referencia", out var colReferencia);
        var hasTipo = headers.TryGetValue("tipo", out var colTipo);
        if (!hasNumero || !hasFecha || !hasMonto || (!hasReferencia && !hasTipo))
        {
            return BadRequest(new { detail = "La plantilla no contiene las columnas requeridas: ID, Fecha, Referencia (o Tipo), Monto." });
        }
        var items = new List<BancoCuentaConciliacionDto>();
        var errores = new List<string>();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var row = 2; row <= lastRow; row++)
        {
            var numero = sheet.Cell(row, colNumero).GetString().Trim();
            var referencia = hasReferencia ? sheet.Cell(row, colReferencia).GetString().Trim() : string.Empty;
            var tipo = hasTipo ? sheet.Cell(row, colTipo).GetString().Trim() : string.Empty;
            var fechaCell = sheet.Cell(row, colFecha);
            var montoCell = sheet.Cell(row, colMonto);
            var fechaTexto = fechaCell.GetString().Trim();
            var montoTexto = montoCell.GetString().Trim();
            if (string.IsNullOrWhiteSpace(numero)
                && string.IsNullOrWhiteSpace(referencia)
                && string.IsNullOrWhiteSpace(tipo)
                && string.IsNullOrWhiteSpace(fechaTexto)
                && string.IsNullOrWhiteSpace(montoTexto)
                && fechaCell.IsEmpty()
                && montoCell.IsEmpty())
            {
                continue;
            }
            if (string.IsNullOrWhiteSpace(numero))
            {
                errores.Add($"Fila {row}: ID es requerido.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(referencia) && string.IsNullOrWhiteSpace(tipo))
            {
                errores.Add($"Fila {row}: Referencia es requerida.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(referencia))
            {
                referencia = tipo;
            }
            if (string.IsNullOrWhiteSpace(tipo))
            {
                tipo = referencia;
            }
            if (!TryGetDate(fechaCell, fechaTexto, out var fecha))
            {
                errores.Add($"Fila {row}: Fecha invalida.");
                continue;
            }
            if (!TryGetDecimal(montoCell, montoTexto, out var monto))
            {
                errores.Add($"Fila {row}: Monto invalido.");
                continue;
            }
            items.Add(new BancoCuentaConciliacionDto
            {
                NumeroTransaccion = numero,
                Fecha = fecha,
                Tipo = tipo,
                Referencia = referencia,
                Monto = monto
            });
        }
        if (errores.Count > 0)
        {
            return BadRequest(new { detail = "Errores al leer el archivo.", errors = errores });
        }
        return Ok(items);
    }
    [HttpPost("conciliacion/confirmar")]
    public async Task<IActionResult> ConciliarSeleccionados(
        [FromBody] BancoCuentaConciliarDto dto,
        CancellationToken ct = default)
    {
        if (dto is null || dto.BancoCuentaId <= 0)
        {
            return BadRequest(new { detail = "Debe proporcionar una cuenta bancaria v�lida." });
        }

        if (dto.Movimientos is null || dto.Movimientos.Count == 0)
        {
            return BadRequest(new { detail = "Debe seleccionar movimientos para conciliar." });
        }
        if (!dto.FechaConciliacion.HasValue)
        {
            return BadRequest(new { detail = "Debe proporcionar la fecha de conciliacion." });
        }

        var companyId = currentCompanyService.GetCompanyId();
        if (!await accessValidator.ValidarAccesoAsync(companyId, ct))
        {
            return Forbid();
        }

        var user = User?.Identity?.Name ?? "system";
        try
        {
            await cuentasService.ConciliarAsync(companyId, dto.BancoCuentaId, user, dto.FechaConciliacion.Value, dto.Movimientos, ct);
            return Ok(new { ok = true });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { detail = ex.Message });
        }
    }

    private static string NormalizeHeader(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant();
        normalized = normalized.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    private static bool TryGetDate(IXLCell cell, string raw, out DateOnly fecha)
    {
        if (cell.TryGetValue<DateTime>(out var dt))
        {
            fecha = DateOnly.FromDateTime(dt);
            return true;
        }

        if (!string.IsNullOrWhiteSpace(raw))
        {
            if (DateTime.TryParse(raw, CultureInfo.GetCultureInfo("es-ES"), DateTimeStyles.None, out var parsed)
                || DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)
                || DateTime.TryParse(raw, CultureInfo.CurrentCulture, DateTimeStyles.None, out parsed))
            {
                fecha = DateOnly.FromDateTime(parsed);
                return true;
            }
        }

        fecha = default;
        return false;
    }

    private static bool TryGetDecimal(IXLCell cell, string raw, out decimal monto)
    {
        if (cell.TryGetValue<decimal>(out monto))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(raw))
        {
            if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.GetCultureInfo("es-ES"), out monto)
                || decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out monto)
                || decimal.TryParse(raw, NumberStyles.Any, CultureInfo.CurrentCulture, out monto))
            {
                return true;
            }
        }

        monto = default;
        return false;
    }


[HttpGet("contables")]
    public async Task<IActionResult> GetCuentasContables([FromQuery] long companyId, CancellationToken ct)
    {
        if (companyId <= 0)
        {
            return BadRequest(new { detail = "Debe proporcionar un companyId v?lido." });
        }

        if (!await accessValidator.ValidarAccesoAsync(companyId, ct))
        {
            return Forbid();
        }

        try
        {
            var cuentas = await cuentasService.ListarCuentasContablesAsync(companyId, ct);
            return Ok(cuentas);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { detail = ex.Message });
        }
    }

    
[HttpGet("{cuentaId:long}")]
    public async Task<IActionResult> GetById(long cuentaId, CancellationToken ct)
    {
        try
        {
            if (!await accessValidator.ValidarAccesoAsync(currentCompanyService.GetCompanyId(), ct))
            {
                return Forbid();
            }

            var cuenta = await cuentasService.GetByIdAsync(cuentaId, ct);
            return cuenta is null ? NotFound() : Ok(cuenta);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return StatusCode(499);
        }
    }

[HttpPost]
    public async Task<IActionResult> Post([FromBody] BancoCuentaCreateDto dto, CancellationToken ct)
    {
        if (!await accessValidator.ValidarAccesoAsync(currentCompanyService.GetCompanyId(), ct))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var user = User?.Identity?.Name ?? "system";

        try
        {
            var created = await cuentasService.CreateAsync(dto, user, ct);
            return CreatedAtAction(nameof(GetById), new { cuentaId = created.BancoCuentaId }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { detail = ex.Message });
        }
        catch (DbUpdateException ex)
        {
            var detail = ex.GetBaseException()?.Message ?? ex.Message;
            return Conflict(new { detail });
        }
    }

    [HttpPut("{cuentaId:long}")]
    public async Task<IActionResult> Put(long cuentaId, [FromBody] BancoCuentaEditDto dto, CancellationToken ct)
    {
        if (!await accessValidator.ValidarAccesoAsync(currentCompanyService.GetCompanyId(), ct))
        {
            return Forbid();
        }

        if (dto.BancoCuentaId != 0 && dto.BancoCuentaId != cuentaId)
        {
            return BadRequest(new { detail = "El identificador de la cuenta no coincide con la solicitud." });
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var user = User?.Identity?.Name ?? "system";

        try
        {
            var updated = await cuentasService.UpdateAsync(cuentaId, dto, user, ct);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { detail = ex.Message });
        }
        catch (DbUpdateException ex)
        {
            var detail = ex.GetBaseException()?.Message ?? ex.Message;
            return Conflict(new { detail });
        }
    }

    [HttpDelete("{cuentaId:long}")]
    public async Task<IActionResult> Delete(long cuentaId, CancellationToken ct)
    {
        if (!await accessValidator.ValidarAccesoAsync(currentCompanyService.GetCompanyId(), ct))
        {
            return Forbid();
        }

        try
        {
            await cuentasService.DeleteAsync(cuentaId, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { detail = ex.Message });
        }
        catch (DbUpdateException ex)
        {
            var detail = ex.GetBaseException()?.Message ?? ex.Message;
            return Conflict(new { detail });
        }
    }
}