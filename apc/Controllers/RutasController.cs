using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.Rutas;
using SIAD.Services.Rutas;
using apc.Security;
using SIAD.Core.Constants;

namespace apc.Controllers;

[ApiController]
[Route("api/[controller]")]
[ModuleAuthorize(PermissionModules.Inventario)]
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

    [HttpGet("paged")]
    public async Task<IActionResult> GetPaged(
        [FromQuery] RutaFilterDto filtro,
        [FromQuery] int skip,
        [FromQuery] int take,
        [FromQuery] string? sortField,
        [FromQuery] bool sortDesc,
        CancellationToken ct)
    {
        if (take <= 0)
        {
            take = 50;
        }

        if (take > 500)
        {
            take = 500;
        }

        if (skip < 0)
        {
            skip = 0;
        }

        var rutas = await _service.GetRutasPagedAsync(filtro, skip, take, sortField, sortDesc, ct);
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
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var usuario = User?.Identity?.Name ?? "system";
            var id = await _service.CreateRutaAsync(dto, usuario, ct);
            var creado = await _service.GetRutaAsync(id, ct);
            return CreatedAtAction(nameof(GetById), new { id }, creado);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Put(int id, [FromBody] RutaUpsertDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var usuario = User?.Identity?.Name ?? "system";
            await _service.UpdateRutaAsync(id, dto, usuario, ct);
            var actualizado = await _service.GetRutaAsync(id, ct);
            return Ok(actualizado);
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

    [HttpGet("ciclos")]
    public async Task<IActionResult> GetCiclos(CancellationToken ct)
    {
        var ciclos = await _service.GetCiclosAsync(ct);
        return Ok(ciclos);
    }

    [HttpPost("{id:int}/desactivar")]
    public async Task<IActionResult> Desactivar(int id, CancellationToken ct)
    {
        var usuario = User?.Identity?.Name ?? "system";
        var ok = await _service.DeactivateRutaAsync(id, usuario, ct);
        return ok ? Ok(new { success = true }) : NotFound();
    }
}

