using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Presupuesto;
using SIAD.Reports;
using SIAD.Services.Presupuesto;

namespace apc.Controllers.Presupuesto;

[ApiController]
[Route("api/presupuesto/ordenes-pago-directo")]
[Authorize(Policy = AuthorizationPolicies.Contabilidad)]
public sealed class OrdenesPagoDirectoController : ControllerBase
{
    private readonly IOrdenesPagoDirectoService _service;

    public OrdenesPagoDirectoController(IOrdenesPagoDirectoService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] OrdenPagoDirectoFilterDto? filtro, CancellationToken ct)
    {
        try
        {
            var result = await _service.GetAsync(filtro, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("centros-costo")]
    public async Task<IActionResult> GetCentrosCosto(CancellationToken ct)
    {
        var result = await _service.GetCentrosCostoAsync(ct);
        return Ok(result);
    }

    [HttpGet("cuentas-contables")]
    public async Task<IActionResult> GetCuentasContables(CancellationToken ct)
    {
        var result = await _service.GetCuentasContablesAsync(ct);
        return Ok(result);
    }

    [HttpGet("cuentas-contra-procesamiento")]
    public async Task<IActionResult> GetCuentasContraProcesamiento(CancellationToken ct)
    {
        var result = await _service.GetCuentasContraProcesamientoAsync(ct);
        return Ok(result);
    }

    [HttpGet("cuentas-gasto")]
    public async Task<IActionResult> GetCuentasGasto(CancellationToken ct)
    {
        var result = await _service.GetCuentasGastoAsync(ct);
        return Ok(result);
    }

    [HttpGet("{numeroOrden:int}")]
    public async Task<IActionResult> GetByNumeroOrden(int numeroOrden, CancellationToken ct)
    {
        if (numeroOrden <= 0)
        {
            return BadRequest(new { message = "El numero de orden no es valido." });
        }

        var result = await _service.GetByNumeroOrdenAsync(numeroOrden, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{numeroOrden:int}/pdf")]
    public async Task<IActionResult> GetPdf(int numeroOrden, CancellationToken ct)
    {
        if (numeroOrden <= 0)
        {
            return BadRequest(new { message = "El numero de orden no es valido." });
        }

        var datos = await _service.GetDatosImpresionAsync(numeroOrden, ct);
        if (datos is null)
        {
            return NotFound(new { message = $"No se encontro el compromiso {numeroOrden}." });
        }

        using var report = new Rpt_Dev_Compromiso_Proveedor(datos);
        using var stream = new MemoryStream();
        report.ExportToPdf(stream);

        Response.Headers.ContentDisposition = $"inline; filename=Compromiso-{numeroOrden}.pdf";

        return File(stream.ToArray(), "application/pdf");
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] OrdenPagoDirectoUpsertDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _service.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetByNumeroOrden), new { numeroOrden = result.NumeroOrden }, result);
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

    [HttpPut("{numeroOrden:int}")]
    public async Task<IActionResult> Update(int numeroOrden, [FromBody] OrdenPagoDirectoUpsertDto dto, CancellationToken ct)
    {
        if (numeroOrden <= 0)
        {
            return BadRequest(new { detail = "El numero de orden no es valido." });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _service.UpdateAsync(numeroOrden, dto, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { detail = ex.Message });
        }
    }

    [HttpDelete("{numeroOrden:int}")]
    public async Task<IActionResult> Delete(int numeroOrden, CancellationToken ct)
    {
        if (numeroOrden <= 0)
        {
            return BadRequest(new { detail = "El numero de orden no es valido." });
        }

        try
        {
            var result = await _service.AnularAsync(numeroOrden, new AnularOrdenPagoDirectoDto(), ct);
            return result.Success ? Ok(result) : Conflict(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { detail = ex.Message });
        }
    }

    [HttpPost("{numeroOrden:int}/procesar")]
    public async Task<IActionResult> ProcessOrder(
        int numeroOrden,
        [FromBody] ProcesarOrdenPagoDirectoDto dto,
        CancellationToken ct)
    {
        if (numeroOrden <= 0)
            return BadRequest(new { message = "El numero de orden no es valido." });

        try
        {
            var result = await _service.MarkAsProcessedAsync(numeroOrden, dto, ct);
            return result.Success ? Ok(result) : Conflict(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("{numeroOrden:int}/generar-partida")]
    public async Task<IActionResult> GenerarPartida(int numeroOrden, CancellationToken ct)
    {
        if (numeroOrden <= 0)
            return BadRequest(new { message = "El numero de orden no es valido." });

        try
        {
            var result = await _service.GenerarPartidaCreacionAsync(numeroOrden, ct);
            return result.Success ? Ok(result) : Conflict(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("{numeroOrden:int}/anular")]
    public async Task<IActionResult> Anular(
        int numeroOrden,
        [FromBody] AnularOrdenPagoDirectoDto dto,
        CancellationToken ct)
    {
        if (numeroOrden <= 0)
            return BadRequest(new { message = "El numero de orden no es valido." });

        try
        {
            var result = await _service.AnularAsync(numeroOrden, dto, ct);
            return result.Success ? Ok(result) : Conflict(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("{numeroOrden:int}/saldo")]
    public async Task<IActionResult> GetSaldo(int numeroOrden, CancellationToken ct)
    {
        if (numeroOrden <= 0)
            return BadRequest(new { message = "El numero de orden no es valido." });

        var result = await _service.GetSaldoConAbonosAsync(numeroOrden, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{numeroOrden:int}/abonos")]
    public async Task<IActionResult> RegistrarAbono(
        int numeroOrden,
        [FromBody] AbonoCompromisoUpsertDto dto,
        CancellationToken ct)
    {
        if (numeroOrden <= 0)
            return BadRequest(new { message = "El numero de orden no es valido." });

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var result = await _service.RegistrarAbonoAsync(numeroOrden, dto, ct);
            return result.Success ? Ok(result) : Conflict(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("{numeroOrden:int}/abonos/{numeroAbono:int}/anular")]
    public async Task<IActionResult> AnularAbono(
        int numeroOrden,
        int numeroAbono,
        [FromBody] AnularOrdenPagoDirectoDto dto,
        CancellationToken ct)
    {
        if (numeroOrden <= 0)
            return BadRequest(new { message = "El numero de orden no es valido." });

        if (numeroAbono <= 0)
            return BadRequest(new { message = "El numero de abono no es valido." });

        try
        {
            var result = await _service.AnularAbonoAsync(numeroOrden, numeroAbono, dto, ct);
            return result.Success ? Ok(result) : Conflict(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("{numeroOrden:int}/abonos/{numeroAbono:int}/comprobante/pdf")]
    public async Task<IActionResult> GetComprobanteAbonoPdf(int numeroOrden, int numeroAbono, CancellationToken ct)
    {
        if (numeroOrden <= 0)
            return BadRequest(new { message = "El numero de orden no es valido." });

        if (numeroAbono <= 0)
            return BadRequest(new { message = "El numero de abono no es valido." });

        var datos = await _service.GetDatosImpresionAbonoAsync(numeroOrden, numeroAbono, ct);
        if (datos is null)
        {
            return NotFound(new { message = $"No se encontro el abono {numeroAbono} del compromiso {numeroOrden}." });
        }

        using var report = new Rpt_Dev_Comprobante_Abono(datos);
        using var stream = new MemoryStream();
        report.ExportToPdf(stream);

        Response.Headers.ContentDisposition = $"inline; filename=Comprobante-Abono-{numeroOrden}-{numeroAbono}.pdf";

        return File(stream.ToArray(), "application/pdf");
    }
}
