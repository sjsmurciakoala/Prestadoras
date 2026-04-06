using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.AppLectores;
using SIAD.Services.AppLectores;
using apc.Security;
using SIAD.Core.Constants;

namespace apc.Controllers;

[ApiController]
[Route("api/configuraciones-app")]
[ModuleAuthorize(PermissionModules.Configuracion)]
public sealed class ConfiguracionesAppController : ControllerBase
{
    private readonly IConfiguracionAppService _service;

    public ConfiguracionesAppController(IConfiguracionAppService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] ConfiguracionAppFilterDto filtro, CancellationToken ct)
    {
        var configuraciones = await _service.GetAsync(filtro, ct);
        return Ok(configuraciones);
    }

    [HttpGet("paged")]
    public async Task<IActionResult> GetPaged(
        [FromQuery] ConfiguracionAppFilterDto filtro,
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
        var configuracion = await _service.GetByIdAsync(id, ct);
        return configuracion is null ? NotFound() : Ok(configuracion);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ConfiguracionAppEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var usuario = User?.Identity?.Name ?? "system";
            var creada = await _service.CreateAsync(dto, usuario, ct);
            return CreatedAtAction(nameof(GetById), new { id = creada.Id }, creada);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ConfiguracionAppEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var usuario = User?.Identity?.Name ?? "system";
            var actualizada = await _service.UpdateAsync(id, dto, usuario, ct);
            return Ok(actualizada);
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

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            var ok = await _service.DeleteAsync(id, ct);
            return ok ? Ok(new { success = true }) : NotFound();
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

