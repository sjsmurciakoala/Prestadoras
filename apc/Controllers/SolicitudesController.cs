using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.Solicitudes;
using SIAD.Services.Solicitudes;

[ApiController]
[Route("api/[controller]")]
public class SolicitudesController : ControllerBase
{
    private readonly ISolicitudesService _service;

    public SolicitudesController(ISolicitudesService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? clienteIdentidad, CancellationToken ct)
    {
        var data = await _service.GetSolicitudesAsync(clienteIdentidad, ct);
        return Ok(data);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var solicitud = await _service.GetSolicitudAsync(id, ct);
        return solicitud is null ? NotFound() : Ok(solicitud);
    }

    [HttpGet("categorias")]
    public async Task<IActionResult> GetCategorias(CancellationToken ct)
    {
        var categorias = await _service.GetCategoriasAsync(ct);
        return Ok(categorias);
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] SolicitudDetailDto dto, CancellationToken ct)
    {
        var id = await _service.CreateSolicitudAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id }, null);
    }
}
