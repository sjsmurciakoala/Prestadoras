using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Libretas;
using SIAD.Services.Libretas;
using apc.Security;

namespace apc.Controllers;

/// <summary>
/// Catálogo global de libretas (libro del lector, sin ciclo — 2026-07-16).
/// Reemplaza al mantenimiento rutas-por-ciclo para el combo del cliente.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[ModuleAuthorize(PermissionModules.Inventario)]
public class LibretasController : ControllerBase
{
    private readonly ILibretasService _service;

    public LibretasController(ILibretasService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        return Ok(await _service.ListarAsync(ct));
    }

    [HttpGet("activas")]
    public async Task<IActionResult> GetActivas(CancellationToken ct)
    {
        return Ok(await _service.ListarActivasAsync(ct));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        var libreta = await _service.ObtenerAsync(id, ct);
        return libreta is null ? NotFound() : Ok(libreta);
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] LibretaUpsertDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var usuario = User?.Identity?.Name ?? "system";
            var id = await _service.CrearAsync(dto, usuario, ct);
            var creada = await _service.ObtenerAsync(id, ct);
            return CreatedAtAction(nameof(GetById), new { id }, creada);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Put(long id, [FromBody] LibretaUpsertDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var usuario = User?.Identity?.Name ?? "system";
            await _service.ActualizarAsync(id, dto, usuario, ct);
            return Ok(await _service.ObtenerAsync(id, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:long}/desactivar")]
    public async Task<IActionResult> Desactivar(long id, CancellationToken ct)
    {
        var usuario = User?.Identity?.Name ?? "system";
        var ok = await _service.DesactivarAsync(id, usuario, ct);
        return ok ? Ok(new { success = true }) : NotFound();
    }
}
