using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.Cobranza;
using SIAD.Services.Cobranza;

namespace apc.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CobranzaController : ControllerBase
{
    private readonly ICobranzaService _service;

    public CobranzaController(ICobranzaService service)
    {
        _service = service;
    }

    [HttpGet("clientes/{clave}/saldos")]
    public async Task<IActionResult> ObtenerSaldos(string clave, CancellationToken ct)
    {
        var saldos = await _service.ObtenerSaldosClienteAsync(clave, ct);
        return Ok(saldos);
    }

    [HttpGet("clientes/{clave}/bloqueo")]
    public async Task<IActionResult> ObtenerBloqueo(string clave, CancellationToken ct)
    {
        var bloqueado = await _service.EstaBloqueadoAsync(clave, ct);
        return Ok(new { Bloqueado = bloqueado });
    }

    [HttpGet("numero-letras")]
    public async Task<IActionResult> NumeroALetras([FromQuery] decimal valor, CancellationToken ct)
    {
        var letras = await _service.NumeroALetrasAsync(valor, ct);
        return Ok(new { Valor = valor, Texto = letras });
    }

    [HttpPost("planes/calcular")]
    public async Task<IActionResult> CalcularPlan([FromBody] CobranzaPlanPreviewRequestDto dto, CancellationToken ct)
    {
        var preview = await _service.CalcularCuotasAsync(dto, ct);
        return Ok(preview);
    }

    [HttpPost("planes")]
    public async Task<IActionResult> GuardarPlan([FromBody] CobranzaPlanGuardarDto dto, CancellationToken ct)
    {
        var respuesta = await _service.GuardarPlanPagoAsync(dto, ct);
        if (!respuesta.Success)
        {
            return BadRequest(respuesta);
        }

        return Ok(respuesta);
    }

    [HttpGet("planes")]
    public async Task<IActionResult> ListarPlanes(CancellationToken ct)
    {
        var planes = await _service.ListarPlanesAsync(ct);
        return Ok(planes);
    }

    [HttpGet("planes/{correlativo}")]
    public async Task<IActionResult> ObtenerPlan(string correlativo, CancellationToken ct)
    {
        var plan = await _service.ObtenerPlanAsync(correlativo, ct);
        return plan is null ? NotFound() : Ok(plan);
    }
}
