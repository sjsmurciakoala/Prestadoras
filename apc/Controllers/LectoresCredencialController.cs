using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.AppLectores;
using SIAD.Services.AppLectores;
using apc.Security;

namespace apc.Controllers;

/// <summary>
/// Mantenimiento de credenciales de lectores de la app móvil V3
/// (<c>adm_lector_credencial</c>). Reemplaza a <c>usuarios-app</c> (app Java).
/// </summary>
[ApiController]
[Route("api/lectores-credenciales")]
[ModuleAuthorize(PermissionModules.Configuracion)]
public sealed class LectoresCredencialController : ControllerBase
{
    private readonly ILectoresCredencialService _service;

    public LectoresCredencialController(ILectoresCredencialService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] LectorCredencialFilterDto filtro, CancellationToken ct)
        => Ok(await _service.GetAsync(filtro, ct));

    [HttpGet("paged")]
    public async Task<IActionResult> GetPaged(
        [FromQuery] LectorCredencialFilterDto filtro,
        [FromQuery] int skip,
        [FromQuery] int take,
        [FromQuery] string? sortField,
        [FromQuery] bool sortDesc,
        CancellationToken ct)
        => Ok(await _service.GetPagedAsync(filtro, skip, take, sortField, sortDesc, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        var lector = await _service.GetByIdAsync(id, ct);
        return lector is null ? NotFound() : Ok(lector);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] LectorCredencialEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var user = User?.Identity?.Name ?? "system";
            var creado = await _service.CreateAsync(dto, user, ct);
            return CreatedAtAction(nameof(GetById), new { id = creado.Id }, creado);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] LectorCredencialEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var user = User?.Identity?.Name ?? "system";
            var actualizado = await _service.UpdateAsync(id, dto, user, ct);
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

    [HttpPost("{id:long}/desactivar")]
    public async Task<IActionResult> Deactivate(long id, CancellationToken ct)
    {
        try
        {
            var user = User?.Identity?.Name ?? "system";
            var ok = await _service.DeactivateAsync(id, user, ct);
            return ok ? Ok(new { success = true }) : NotFound();
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
