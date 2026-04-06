using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.Abogados;
using SIAD.Services.Abogados;
using apc.Security;
using SIAD.Core.Constants;

namespace apc.Controllers;

[ApiController]
[Route("api/[controller]")]
[ModuleAuthorize(PermissionModules.Inventario)]
public sealed class AbogadosController : ControllerBase
{
    private readonly IAbogadosService _service;

    public AbogadosController(IAbogadosService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] AbogadoFilterDto filtro, CancellationToken ct)
    {
        var abogados = await _service.GetAsync(filtro, ct);
        return Ok(abogados);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var abogado = await _service.GetByIdAsync(id, ct);
        return abogado is null ? NotFound() : Ok(abogado);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AbogadoEditDto dto, CancellationToken ct)
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
    public async Task<IActionResult> Update(int id, [FromBody] AbogadoEditDto dto, CancellationToken ct)
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

