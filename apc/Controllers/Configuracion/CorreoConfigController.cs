using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Configuracion;
using SIAD.Services.Configuracion;
using apc.Security;

namespace apc.Controllers.Configuracion;

/// <summary>
/// Mantenimiento de correo por empresa: la conexión (SendGrid) y las áreas de notificación.
/// <para>
/// Permisos: módulo <c>configuracion</c>, recurso fino <c>correo</c>
/// (<see cref="PermissionResources.Configuracion.Correo"/>). ModuleAuthorize mapea GET→View,
/// PUT→Edit → <c>module.configuracion.correo.[view|edit]</c> con fallback al permiso de módulo.
/// </para>
/// <para>El GET de la conexión <b>nunca</b> devuelve la API key (solo el flag <c>TieneApiKey</c>).</para>
/// </summary>
[ApiController]
[Route("api/configuracion/correo")]
[ModuleAuthorize(PermissionModules.Configuracion, PermissionResources.Configuracion.Correo)]
public sealed class CorreoConfigController : ControllerBase
{
    private readonly ICorreoConfigService _service;

    public CorreoConfigController(ICorreoConfigService service) => _service = service;

    private string Usuario => User?.Identity?.Name ?? "system";

    // ---------------------------------------------------------------- conexión

    [HttpGet("conexion")]
    public async Task<IActionResult> GetConexion(CancellationToken ct)
        => Ok(await _service.ObtenerConexionAsync(ct));

    [HttpPut("conexion")]
    public async Task<IActionResult> PutConexion([FromBody] ConexionCorreoUpsertDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            return Ok(await _service.GuardarConexionAsync(dto, Usuario, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ------------------------------------------------------------ notificaciones

    [HttpGet("notificaciones")]
    public async Task<IActionResult> GetNotificaciones(CancellationToken ct)
        => Ok(await _service.ListarNotificacionesAsync(ct));

    [HttpPut("notificaciones")]
    public async Task<IActionResult> PutNotificacion([FromBody] NotificacionCorreoDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            return Ok(await _service.GuardarNotificacionAsync(dto, Usuario, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
