using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Services.Almacen;
using apc.Security;

namespace apc.Controllers.Almacen;

[ApiController]
[Route("api/almacen/unidades-medida")]
[ModuleAuthorize(PermissionModules.Inventario)]
public sealed class UnidadesMedidaController : ControllerBase
{
    private readonly IUnidadesMedidaService _service;

    public UnidadesMedidaController(IUnidadesMedidaService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] UnidadMedidaFilterDto filtro, CancellationToken ct)
    {
        var unidades = await _service.GetAsync(filtro, ct);
        return Ok(unidades);
    }

    [HttpGet("lookup")]
    public async Task<IActionResult> GetLookup(CancellationToken ct)
    {
        var unidades = await _service.GetLookupAsync(ct);
        return Ok(unidades);
    }

    [HttpGet("categorias")]
    public async Task<IActionResult> GetCategorias(CancellationToken ct)
    {
        var categorias = await _service.GetCategoriasAsync(ct);
        return Ok(categorias);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var unidad = await _service.GetByIdAsync(id, ct);
        return unidad is null ? NotFound() : Ok(unidad);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UnidadMedidaEditDto dto, CancellationToken ct)
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
    public async Task<IActionResult> Update(int id, [FromBody] UnidadMedidaEditDto dto, CancellationToken ct)
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
