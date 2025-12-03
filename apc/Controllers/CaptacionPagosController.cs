using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.CaptacionPagos;
using SIAD.Services.CaptacionPagos;

namespace apc.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CaptacionPagosController : ControllerBase
{
    private readonly ICaptacionPagosService _service;

    public CaptacionPagosController(ICaptacionPagosService service)
    {
        _service = service;
    }

    [HttpGet("cajas")]
    public async Task<IActionResult> GetCajas(CancellationToken ct)
    {
        var cajas = await _service.ListarCatalogoCajasAsync(ct);
        return Ok(cajas);
    }

    [HttpGet("arqueos")]
    public async Task<IActionResult> GetArqueos([FromQuery] CaptacionArqueoFilterDto filtro, CancellationToken ct)
    {
        filtro ??= new CaptacionArqueoFilterDto();
        var arqueos = await _service.ListarArqueosAsync(filtro, ct);
        return Ok(arqueos);
    }

    [HttpGet("miscelaneos")]
    public async Task<IActionResult> GetMiscelaneos([FromQuery] string? clienteClave, CancellationToken ct)
    {
        var recibos = await _service.ListarPagosMiscelaneosAsync(clienteClave, ct);
        return Ok(recibos);
    }

    [HttpGet("{numFactura}")]
    public async Task<IActionResult> GetPago(string numFactura, CancellationToken ct)
    {
        var pago = await _service.ObtenerPagoAsync(numFactura, ct);
        return pago is null ? NotFound() : Ok(pago);
    }

    [HttpPost]
    public async Task<IActionResult> RegistrarPago([FromBody] PagoCrearDto dto, CancellationToken ct)
    {
        var respuesta = await _service.RegistrarPagoAsync(dto, ct);
        if (!respuesta.Success)
        {
            return BadRequest(respuesta);
        }

        return Ok(respuesta);
    }

    [HttpPost("reverso")]
    public async Task<IActionResult> ReversarPago([FromBody] ReversoRequestDto dto, CancellationToken ct)
    {
        var respuesta = await _service.ReversarPagoAsync(dto, ct);
        if (!respuesta.Success)
        {
            return BadRequest(respuesta);
        }

        return Ok(respuesta);
    }
}
