using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.AppLectores;
using SIAD.Services.AppLectores;
using apc.Security;
using SIAD.Core.Constants;

namespace apc.Controllers;

[ApiController]
[Route("api/servicios-roles-ws")]
[ModuleAuthorize(PermissionModules.Configuracion)]
public sealed class ServiciosRolesWsController : ControllerBase
{
    private readonly IServiciosRolesWsService _service;

    public ServiciosRolesWsController(IServiciosRolesWsService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] ServicioRolWsFilterDto filtro, CancellationToken ct)
    {
        var items = await _service.GetAsync(filtro, ct);
        return Ok(items);
    }

    [HttpGet("paged")]
    public async Task<IActionResult> GetPaged(
        [FromQuery] ServicioRolWsFilterDto filtro,
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

        var result = await _service.GetPagedAsync(filtro, skip, take, sortField, sortDesc, ct);
        return Ok(result);
    }

    [HttpGet("{rol}/{codigo}")]
    public async Task<IActionResult> GetById(string rol, string codigo, CancellationToken ct)
    {
        var item = await _service.GetByIdAsync(rol, codigo, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ServicioRolWsEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var usuario = User?.Identity?.Name ?? "system";
            var created = await _service.CreateAsync(dto, usuario, ct);
            return CreatedAtAction(nameof(GetById), new { rol = created.Rol, codigo = created.Codigo }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{rol}/{codigo}")]
    public async Task<IActionResult> Update(string rol, string codigo, [FromBody] ServicioRolWsEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var usuario = User?.Identity?.Name ?? "system";
            var updated = await _service.UpdateAsync(rol, codigo, dto, usuario, ct);
            return Ok(updated);
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

    [HttpDelete("{rol}/{codigo}")]
    public async Task<IActionResult> Delete(string rol, string codigo, CancellationToken ct)
    {
        var ok = await _service.DeleteAsync(rol, codigo, ct);
        return ok ? Ok(new { success = true }) : NotFound();
    }
}

