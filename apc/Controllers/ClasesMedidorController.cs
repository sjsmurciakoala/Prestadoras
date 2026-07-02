using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.Catalogos;
using SIAD.Services.Catalogos;
using apc.Security;
using SIAD.Core.Constants;

namespace apc.Controllers;

[ApiController]
[Route("api/mantenimientos/clases-medidor")]
[ModuleAuthorize(PermissionModules.Configuracion)]
public class ClasesMedidorController : ControllerBase
{
    private readonly ICatalogosService _service;
    public ClasesMedidorController(ICatalogosService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
        => Ok(await _service.ListarClasesMedidorDtoAsync(ct));

    [HttpGet("{codigo}")]
    public async Task<IActionResult> ObtenerUno(string codigo, CancellationToken ct)
    {
        var c = await _service.GetClaseMedidorAsync(codigo, ct);
        return c is null ? NotFound() : Ok(c);
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] ClaseMedidorCreateDto dto, CancellationToken ct)
    {
        try
        {
            var usuario = User.Identity?.Name ?? "sistema";
            var creado = await _service.CrearClaseMedidorAsync(dto, usuario, ct);
            return CreatedAtAction(nameof(ObtenerUno), new { codigo = creado.Codigo }, creado);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("{codigo}")]
    public async Task<IActionResult> Actualizar(string codigo, [FromBody] ClaseMedidorUpdateDto dto, CancellationToken ct)
    {
        try
        {
            var usuario = User.Identity?.Name ?? "sistema";
            var actualizado = await _service.ActualizarClaseMedidorAsync(codigo, dto, usuario, ct);
            return Ok(actualizado);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpDelete("{codigo}")]
    public async Task<IActionResult> Eliminar(string codigo, CancellationToken ct)
    {
        try
        {
            await _service.EliminarClaseMedidorAsync(codigo, ct);
            return Ok(new { success = true });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
