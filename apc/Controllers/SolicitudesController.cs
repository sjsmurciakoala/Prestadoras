using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.Solicitudes;
using SIAD.Services.Solicitudes;
using apc.Security;
using SIAD.Core.Constants;

namespace apc.Controllers;

/// <summary>
/// Controlador para gestión de solicitudes de servicio.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[ModuleAuthorize(PermissionModules.Inventario)]
public class SolicitudesController : ControllerBase
{
    private readonly ISolicitudesService _service;

    public SolicitudesController(ISolicitudesService service) => _service = service;

    /// <summary>
    /// Obtiene listado de solicitudes, opcionalmente filtradas por identidad del cliente.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? clienteIdentidad, CancellationToken ct)
    {
        var data = await _service.GetSolicitudesAsync(clienteIdentidad, ct);
        return Ok(data);
    }

    /// <summary>
    /// Obtiene el detalle completo de una solicitud por ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var solicitud = await _service.GetSolicitudAsync(id, ct);
        return solicitud is null ? NotFound() : Ok(solicitud);
    }

    /// <summary>
    /// Obtiene listado de categorías de servicio activas.
    /// </summary>
    [HttpGet("categorias")]
    public async Task<IActionResult> GetCategorias(CancellationToken ct)
    {
        var categorias = await _service.GetCategoriasAsync(ct);
        return Ok(categorias);
    }

    /// <summary>
    /// Crea una nueva solicitud de servicio.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] SolicitudCreateDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var usuarioCreacion = User.Identity?.Name ?? "api";
        var id = await _service.CreateSolicitudAsync(dto, usuarioCreacion, ct);
        return CreatedAtAction(nameof(GetById), new { id }, null);
    }

    /// <summary>
    /// Actualiza una solicitud de servicio existente.
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Put(int id, [FromBody] SolicitudUpdateDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (id != dto.Id)
            return BadRequest("ID en la URL no coincide con el DTO");

        try
        {
            var usuarioModificacion = User.Identity?.Name ?? "api";
            await _service.UpdateSolicitudAsync(dto, usuarioModificacion, ct);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Inactiva una solicitud (cambia estado a false).
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        try
        {
            var usuarioModificacion = User.Identity?.Name ?? "api";
            await _service.InactivateSolicitudAsync(id, usuarioModificacion, ct);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Marca una solicitud como asignada.
    /// </summary>
    [HttpPost("{id:int}/asignar")]
    public async Task<IActionResult> Asignar(int id, CancellationToken ct)
    {
        try
        {
            var usuarioModificacion = User.Identity?.Name ?? "api";
            await _service.AsignarSolicitudAsync(id, usuarioModificacion, ct);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Desasigna una solicitud (marca como no asignada).
    /// </summary>
    [HttpPost("{id:int}/desasignar")]
    public async Task<IActionResult> Desasignar(int id, CancellationToken ct)
    {
        try
        {
            var usuarioModificacion = User.Identity?.Name ?? "api";
            await _service.DesasignarSolicitudAsync(id, usuarioModificacion, ct);
            return Ok();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}

