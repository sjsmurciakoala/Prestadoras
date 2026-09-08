using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.ActivosFijos;
using SIAD.Services.ActivosFijos;
using apc.Security;

namespace apc.Controllers.ActivosFijos;

/// <summary>
/// Catálogos del módulo de Activos Fijos. Los tipos de activo llevan la vida útil y las
/// cuentas contables que heredan todos sus activos, así que el recurso de permiso es
/// <c>catalogos</c>, distinto del maestro.
/// </summary>
[ApiController]
[Route("api/activosfijos/catalogos")]
[ModuleAuthorize(PermissionModules.ActivosFijos, PermissionResources.ActivosFijos.Catalogos)]
public sealed class CatalogosActivosFijosController : ControllerBase
{
    private readonly ICatalogosActivosFijosService _service;
    public CatalogosActivosFijosController(ICatalogosActivosFijosService service) => _service = service;

    private string UsuarioActual => User?.Identity?.Name ?? "system";

    [HttpGet("metodos-depreciacion")]
    public async Task<IActionResult> GetMetodos(CancellationToken ct)
        => Ok(await _service.GetMetodosDepreciacionAsync(ct));

    [HttpGet("estados")]
    public async Task<IActionResult> GetEstados(CancellationToken ct)
        => Ok(await _service.GetEstadosAsync(ct));

    // ── Tipos de activo ─────────────────────────────────────────────────────

    [HttpGet("tipos")]
    public async Task<IActionResult> GetTipos([FromQuery] bool? activo, [FromQuery] string? search, CancellationToken ct)
        => Ok(await _service.GetTiposAsync(activo, search, ct));

    [HttpGet("tipos/lookup")]
    public async Task<IActionResult> GetTiposLookup(CancellationToken ct)
        => Ok(await _service.GetTiposLookupAsync(ct));

    [HttpGet("tipos/{id:int}")]
    public async Task<IActionResult> GetTipo(int id, CancellationToken ct)
    {
        var tipo = await _service.GetTipoByIdAsync(id, ct);
        return tipo is null ? NotFound() : Ok(tipo);
    }

    [HttpPost("tipos")]
    public async Task<IActionResult> CrearTipo([FromBody] TipoActivoEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            dto.Id = null;
            return Ok(await _service.GuardarTipoAsync(dto, UsuarioActual, ct));
        }
        catch (Exception ex) when (EsErrorDeNegocio(ex))
        {
            return Problem(detail: MensajeNegocio(ex), statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPut("tipos/{id:int}")]
    public async Task<IActionResult> ActualizarTipo(int id, [FromBody] TipoActivoEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            dto.Id = id;
            return Ok(await _service.GuardarTipoAsync(dto, UsuarioActual, ct));
        }
        catch (Exception ex) when (EsErrorDeNegocio(ex))
        {
            return Problem(detail: MensajeNegocio(ex), statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("tipos/{id:int}/desactivar")]
    public async Task<IActionResult> DesactivarTipo(int id, CancellationToken ct)
    {
        try
        {
            await _service.DesactivarTipoAsync(id, UsuarioActual, ct);
            return Ok(new { success = true });
        }
        catch (Exception ex) when (EsErrorDeNegocio(ex))
        {
            return Problem(detail: MensajeNegocio(ex), statusCode: StatusCodes.Status400BadRequest);
        }
    }

    // ── Ubicaciones ─────────────────────────────────────────────────────────

    [HttpGet("ubicaciones")]
    public async Task<IActionResult> GetUbicaciones([FromQuery] bool? activo, [FromQuery] string? search, CancellationToken ct)
        => Ok(await _service.GetUbicacionesAsync(activo, search, ct));

    [HttpGet("ubicaciones/lookup")]
    public async Task<IActionResult> GetUbicacionesLookup(CancellationToken ct)
        => Ok(await _service.GetUbicacionesLookupAsync(ct));

    [HttpGet("ubicaciones/{id:int}")]
    public async Task<IActionResult> GetUbicacion(int id, CancellationToken ct)
    {
        var ubicacion = await _service.GetUbicacionByIdAsync(id, ct);
        return ubicacion is null ? NotFound() : Ok(ubicacion);
    }

    [HttpPost("ubicaciones")]
    public async Task<IActionResult> CrearUbicacion([FromBody] UbicacionActivoEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            dto.Id = null;
            return Ok(await _service.GuardarUbicacionAsync(dto, UsuarioActual, ct));
        }
        catch (Exception ex) when (EsErrorDeNegocio(ex))
        {
            return Problem(detail: MensajeNegocio(ex), statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPut("ubicaciones/{id:int}")]
    public async Task<IActionResult> ActualizarUbicacion(int id, [FromBody] UbicacionActivoEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            dto.Id = id;
            return Ok(await _service.GuardarUbicacionAsync(dto, UsuarioActual, ct));
        }
        catch (Exception ex) when (EsErrorDeNegocio(ex))
        {
            return Problem(detail: MensajeNegocio(ex), statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("ubicaciones/{id:int}/desactivar")]
    public async Task<IActionResult> DesactivarUbicacion(int id, CancellationToken ct)
    {
        try
        {
            await _service.DesactivarUbicacionAsync(id, UsuarioActual, ct);
            return Ok(new { success = true });
        }
        catch (Exception ex) when (EsErrorDeNegocio(ex))
        {
            return Problem(detail: MensajeNegocio(ex), statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>
    /// Las reglas de negocio viven en los procedimientos de Postgres y llegan como
    /// <c>RAISE EXCEPTION</c>. Se traducen a 400 con el mensaje tal cual, que ya está
    /// redactado para el usuario final.
    /// </summary>
    internal static bool EsErrorDeNegocio(Exception ex)
        => ex is InvalidOperationException || ex is Npgsql.PostgresException;

    internal static string MensajeNegocio(Exception ex)
        => ex is Npgsql.PostgresException pg ? pg.MessageText : ex.Message;
}
