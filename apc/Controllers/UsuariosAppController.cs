using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.AppLectores;
using SIAD.Services.AppLectores;
using apc.Security;
using SIAD.Core.Constants;

namespace apc.Controllers;

[ApiController]
[Route("api/usuarios-app")]
[ModuleAuthorize(PermissionModules.Configuracion)]
public sealed class UsuariosAppController : ControllerBase
{
    private readonly IUsuariosAppService _service;

    public UsuariosAppController(IUsuariosAppService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] UsuarioAppFilterDto filtro, CancellationToken ct)
    {
        var usuarios = await _service.GetAsync(filtro, ct);
        return Ok(usuarios);
    }

    [HttpGet("paged")]
    public async Task<IActionResult> GetPaged(
        [FromQuery] UsuarioAppFilterDto filtro,
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

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var usuario = await _service.GetByIdAsync(id, ct);
        return usuario is null ? NotFound() : Ok(usuario);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UsuarioAppEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var usuario = User?.Identity?.Name ?? "system";
            var creado = await _service.CreateAsync(dto, usuario, ct);
            return CreatedAtAction(nameof(GetById), new { id = creado.Id }, creado);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UsuarioAppEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var usuario = User?.Identity?.Name ?? "system";
            var actualizado = await _service.UpdateAsync(id, dto, usuario, ct);
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

    [HttpPost("{id:int}/desactivar")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
    {
        try
        {
            var usuario = User?.Identity?.Name ?? "system";
            var ok = await _service.DeactivateAsync(id, usuario, ct);
            return ok ? Ok(new { success = true }) : NotFound();
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

