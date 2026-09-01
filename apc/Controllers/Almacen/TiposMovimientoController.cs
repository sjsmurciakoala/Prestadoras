using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Services.Almacen;
using apc.Security;

namespace apc.Controllers.Almacen;

/// <summary>
/// Mantenimiento del catálogo de conceptos de movimiento de almacén. Es el equivalente
/// configurable de <c>INV_TIPOSTRANSACC</c> de Centura: alta de conceptos de negocio sin
/// recompilar. (En el código el nombre sigue siendo "TipoMovimiento" / <c>alm_tipo_movimiento</c>;
/// "concepto" es la etiqueta que ve el usuario.)
/// </summary>
[ApiController]
[Route("api/almacen/conceptos-movimiento")]
[ModuleAuthorize(PermissionModules.Inventario, PermissionResources.Inventario.ConceptosMovimiento)]
public sealed class TiposMovimientoController : ControllerBase
{
    private readonly ITipoMovimientoService _service;

    public TiposMovimientoController(ITipoMovimientoService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] bool soloActivos, CancellationToken ct)
        => Ok(await _service.GetAsync(soloActivos, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var x = await _service.GetByIdAsync(id, ct);
        return x is null ? NotFound() : Ok(x);
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] TipoMovimientoAlmacenDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            var creado = await _service.CrearAsync(dto, User?.Identity?.Name ?? "system", ct);
            return CreatedAtAction(nameof(GetById), new { id = creado.Id }, creado);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Actualizar(int id, [FromBody] TipoMovimientoAlmacenDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            return Ok(await _service.ActualizarAsync(id, dto, User?.Identity?.Name ?? "system", ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>
    /// POST pero se autoriza como <see cref="PermissionAction.Edit"/>: cambia el estado de un
    /// concepto existente (lo desactiva), no crea uno nuevo. Mismo criterio que la anulación de
    /// recepciones de compra.
    /// </summary>
    [HttpPost("{id:int}/desactivar")]
    [ModuleAuthorize(PermissionModules.Inventario, PermissionResources.Inventario.ConceptosMovimiento, PermissionAction.Edit)]
    public async Task<IActionResult> Desactivar(int id, CancellationToken ct)
    {
        var ok = await _service.DesactivarAsync(id, User?.Identity?.Name ?? "system", ct);
        return ok ? Ok(new { success = true }) : NotFound();
    }
}
