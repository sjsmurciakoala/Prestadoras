using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.Mantenimientos;
using SIAD.Services.Mantenimientos;

namespace apc.Controllers;

[ApiController]
[Route("api/mantenimientos")]
[Authorize]
public class MantenimientosController : ControllerBase
{
    private readonly IMantenimientosService _service;

    public MantenimientosController(IMantenimientosService service)
    {
        _service = service;
    }

    [HttpGet("recargo-mora")]
    public async Task<IActionResult> ObtenerRecargoMora(CancellationToken ct)
        => Ok(await _service.ObtenerRecargoMoraAsync(ct));

    [HttpPost("recargo-mora")]
    public async Task<IActionResult> GuardarRecargoMora([FromBody] RecargoMoraDto dto, CancellationToken ct)
    {
        var resp = await _service.GuardarRecargoMoraAsync(dto, ct);
        return resp.Success ? Ok(resp) : BadRequest(resp);
    }

    [HttpGet("ajustes-tarifarios")]
    public async Task<IActionResult> ListarAjustesTarifarios(CancellationToken ct)
        => Ok(await _service.ListarAjustesTarifariosAsync(ct));

    [HttpPost("ajustes-tarifarios")]
    public async Task<IActionResult> GuardarAjusteTarifario([FromBody] AjusteTarifarioSaveRequestDto dto, CancellationToken ct)
    {
        var resp = await _service.GuardarAjusteTarifarioAsync(dto, ct);
        return resp.Success ? Ok(resp) : BadRequest(resp);
    }
}
