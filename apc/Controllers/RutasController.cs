using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.Rutas;
using SIAD.Services.Rutas;

namespace apc.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RutasController : ControllerBase
{
    private readonly IRutasService _service;

    public RutasController(IRutasService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] RutaFilterDto filtro, CancellationToken ct)
    {
        var rutas = await _service.GetRutasAsync(filtro, ct);
        return Ok(rutas);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var ruta = await _service.GetRutaAsync(id, ct);
        return ruta is null ? NotFound() : Ok(ruta);
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] RutaUpsertDto dto, CancellationToken ct)
    {
        var id = await _service.CreateRutaAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id }, null);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Put(int id, [FromBody] RutaUpsertDto dto, CancellationToken ct)
    {
        await _service.UpdateRutaAsync(id, dto, ct);
        return NoContent();
    }

    [HttpGet("ciclos")]
    public async Task<IActionResult> GetCiclos(CancellationToken ct)
    {
        var ciclos = await _service.GetCiclosAsync(ct);
        return Ok(ciclos);
    }
}
