using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Services.Almacen;
using apc.Security;

namespace apc.Controllers.Almacen;

[ApiController]
[Route("api/almacen/bodegas")]
[ModuleAuthorize(PermissionModules.Inventario)]
public sealed class BodegasController : ControllerBase
{
    private readonly IBodegaService _service;
    public BodegasController(IBodegaService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] ClasificacionFilterDto filtro, CancellationToken ct)
        => Ok(await _service.GetAsync(filtro, ct));

    [HttpGet("lookup")]
    public async Task<IActionResult> GetLookup(CancellationToken ct) => Ok(await _service.GetLookupAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    { var x = await _service.GetByIdAsync(id, ct); return x is null ? NotFound() : Ok(x); }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] BodegaEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var creado = await _service.CreateAsync(dto, User?.Identity?.Name ?? "system", ct);
        return CreatedAtAction(nameof(GetById), new { id = creado.Id }, creado);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] BodegaEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return Ok(await _service.UpdateAsync(id, dto, User?.Identity?.Name ?? "system", ct));
    }

    [HttpPost("{id:int}/desactivar")]
    public async Task<IActionResult> Desactivar(int id, CancellationToken ct)
    { var ok = await _service.DeactivateAsync(id, User?.Identity?.Name ?? "system", ct); return ok ? Ok(new { success = true }) : NotFound(); }
}
