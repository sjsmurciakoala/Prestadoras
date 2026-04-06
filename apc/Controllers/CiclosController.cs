using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.Ciclos;
using SIAD.Services.Ciclos;
using apc.Security;
using SIAD.Core.Constants;

namespace apc.Controllers;

[ApiController]
[Route("api/[controller]")]
[ModuleAuthorize(PermissionModules.Inventario)]
public sealed class CiclosController : ControllerBase
{
    private readonly ICiclosService _service;

    public CiclosController(ICiclosService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] CicloFilterDto filtro, CancellationToken ct)
    {
        var ciclos = await _service.GetAsync(filtro, ct);
        return Ok(ciclos);
    }

    [HttpGet("paged")]
    public async Task<IActionResult> GetPaged(
        [FromQuery] CicloFilterDto filtro,
        [FromQuery] int skip,
        [FromQuery] int take,
        [FromQuery] string? sortField,
        [FromQuery] bool sortDesc,
        CancellationToken ct)
    {
        var result = await _service.GetPagedAsync(filtro, skip, take, sortField, sortDesc, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var ciclo = await _service.GetByIdAsync(id, ct);
        return ciclo is null ? NotFound() : Ok(ciclo);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CicloEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var usuario = User?.Identity?.Name ?? "system";
        var creado = await _service.CreateAsync(dto, usuario, ct);
        return CreatedAtAction(nameof(GetById), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CicloEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var usuario = User?.Identity?.Name ?? "system";
        var actualizado = await _service.UpdateAsync(id, dto, usuario, ct);
        return Ok(actualizado);
    }

    [HttpPost("{id:int}/desactivar")]
    public async Task<IActionResult> Desactivar(int id, CancellationToken ct)
    {
        var usuario = User?.Identity?.Name ?? "system";
        var ok = await _service.DeactivateAsync(id, usuario, ct);
        return ok ? Ok(new { success = true }) : NotFound();
    }
}

