using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Services.Almacen;
using apc.Security;

namespace apc.Controllers.Almacen;

[ApiController]
[Route("api/almacen/categorias-unidad")]
[ModuleAuthorize(PermissionModules.Inventario)]
public sealed class CategoriasUnidadController : ControllerBase
{
    private readonly ICategoriaUnidadService _service;
    public CategoriasUnidadController(ICategoriaUnidadService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] ClasificacionFilterDto filtro, CancellationToken ct)
        => Ok(await _service.GetAsync(filtro, ct));

    [HttpGet("lookup")]
    public async Task<IActionResult> GetLookup(CancellationToken ct) => Ok(await _service.GetLookupAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var x = await _service.GetByIdAsync(id, ct);
        return x is null ? NotFound() : Ok(x);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CategoriaUnidadEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            var creado = await _service.CreateAsync(dto, User?.Identity?.Name ?? "system", ct);
            return CreatedAtAction(nameof(GetById), new { id = creado.Id }, creado);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CategoriaUnidadEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            return Ok(await _service.UpdateAsync(id, dto, User?.Identity?.Name ?? "system", ct));
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("{id:int}/desactivar")]
    public async Task<IActionResult> Desactivar(int id, CancellationToken ct)
    {
        var ok = await _service.DeactivateAsync(id, User?.Identity?.Name ?? "system", ct);
        return ok ? Ok(new { success = true }) : NotFound();
    }
}
