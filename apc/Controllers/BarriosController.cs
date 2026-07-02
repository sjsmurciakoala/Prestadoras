using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.Catalogos;
using SIAD.Services.Catalogos;
using apc.Security;
using SIAD.Core.Constants;

namespace apc.Controllers;

[ApiController]
[Route("api/mantenimientos/barrios")]
[ModuleAuthorize(PermissionModules.Configuracion)]
public class BarriosController : ControllerBase
{
    private readonly ICatalogosService _service;
    public BarriosController(ICatalogosService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
        => Ok(await _service.ListarBarriosDtoAsync(ct));

    [HttpGet("{codigo}")]
    public async Task<IActionResult> ObtenerUno(string codigo, CancellationToken ct)
    {
        var b = await _service.GetBarrioAsync(codigo, ct);
        return b is null ? NotFound() : Ok(b);
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] BarrioCreateDto dto, CancellationToken ct)
    {
        try
        {
            var usuario = User.Identity?.Name ?? "sistema";
            var creado = await _service.CrearBarrioAsync(dto, usuario, ct);
            return CreatedAtAction(nameof(ObtenerUno), new { codigo = creado.Codigo }, creado);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{codigo}")]
    public async Task<IActionResult> Actualizar(string codigo, [FromBody] BarrioUpdateDto dto, CancellationToken ct)
    {
        try
        {
            var usuario = User.Identity?.Name ?? "sistema";
            var actualizado = await _service.ActualizarBarrioAsync(codigo, dto, usuario, ct);
            return Ok(actualizado);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpDelete("{codigo}")]
    public async Task<IActionResult> Eliminar(string codigo, CancellationToken ct)
    {
        try
        {
            await _service.EliminarBarrioAsync(codigo, ct);
            return Ok(new { success = true });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
