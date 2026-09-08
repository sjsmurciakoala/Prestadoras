using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.ActivosFijos;
using SIAD.Services.ActivosFijos;
using apc.Security;

namespace apc.Controllers.ActivosFijos;

/// <summary>Maestro de activos fijos: registro, ficha, asignaciones y componentes.</summary>
[ApiController]
[Route("api/activosfijos/activos")]
[ModuleAuthorize(PermissionModules.ActivosFijos, PermissionResources.ActivosFijos.Activos)]
public sealed class ActivosFijosController : ControllerBase
{
    private readonly IActivosFijosService _service;
    public ActivosFijosController(IActivosFijosService service) => _service = service;

    private string UsuarioActual => User?.Identity?.Name ?? "system";

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] ActivoFijoFilterDto filtro, CancellationToken ct)
        => Ok(await _service.GetAsync(filtro, ct));

    [HttpGet("resumen")]
    public async Task<IActionResult> GetResumen(CancellationToken ct)
        => Ok(await _service.GetResumenAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var activo = await _service.GetByIdAsync(id, ct);
        return activo is null ? NotFound() : Ok(activo);
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] ActivoFijoEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            dto.Id = null;
            var creado = await _service.GuardarAsync(dto, UsuarioActual, ct);
            return CreatedAtAction(nameof(GetById), new { id = creado.Id }, creado);
        }
        catch (Exception ex) when (CatalogosActivosFijosController.EsErrorDeNegocio(ex))
        {
            return Problem(detail: CatalogosActivosFijosController.MensajeNegocio(ex),
                           statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Actualizar(int id, [FromBody] ActivoFijoEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            dto.Id = id;
            return Ok(await _service.GuardarAsync(dto, UsuarioActual, ct));
        }
        catch (Exception ex) when (CatalogosActivosFijosController.EsErrorDeNegocio(ex))
        {
            return Problem(detail: CatalogosActivosFijosController.MensajeNegocio(ex),
                           statusCode: StatusCodes.Status400BadRequest);
        }
    }

    // ── Asignaciones ────────────────────────────────────────────────────────

    [HttpGet("{id:int}/asignaciones")]
    public async Task<IActionResult> GetAsignaciones(int id, CancellationToken ct)
        => Ok(await _service.GetAsignacionesAsync(id, ct));

    /// <summary>
    /// Reasignar responsable, ubicación o centro de costo. Permiso propio: quien lleva el
    /// control físico del inventario reasigna sin poder tocar valores ni cuentas contables.
    /// </summary>
    /// <remarks>
    /// Lleva su propio <see cref="ModuleAuthorizeAttribute"/> con acción Edit para ANULAR el
    /// de la clase: sin él, el POST se resolvía como Create y exigía además el permiso de
    /// alta, con lo que un rol de control físico con view y asignar recibía 403.
    /// </remarks>
    [HttpPost("{id:int}/asignaciones")]
    [ModuleAuthorize(PermissionModules.ActivosFijos, PermissionResources.ActivosFijos.Activos, PermissionAction.Edit)]
    [Authorize(Policy = PermissionNames.ActivosFijos.Activos.Asignar)]
    public async Task<IActionResult> Asignar(int id, [FromBody] ActivoAsignacionRequestDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            await _service.AsignarAsync(id, dto, UsuarioActual, ct);
            return Ok(new { success = true });
        }
        catch (Exception ex) when (CatalogosActivosFijosController.EsErrorDeNegocio(ex))
        {
            return Problem(detail: CatalogosActivosFijosController.MensajeNegocio(ex),
                           statusCode: StatusCodes.Status400BadRequest);
        }
    }

    // ── Historial de depreciación ───────────────────────────────────────────

    [HttpGet("{id:int}/depreciaciones")]
    public async Task<IActionResult> GetDepreciaciones(int id, CancellationToken ct)
        => Ok(await _service.GetDepreciacionesAsync(id, ct));

    [HttpGet("{id:int}/depreciaciones/resumen")]
    public async Task<IActionResult> GetDepreciacionResumen(int id, CancellationToken ct)
        => Ok(await _service.GetDepreciacionResumenAsync(id, ct));

    // ── Componentes ─────────────────────────────────────────────────────────

    [HttpGet("{id:int}/componentes")]
    public async Task<IActionResult> GetComponentes(int id, CancellationToken ct)
        => Ok(await _service.GetComponentesAsync(id, ct));

    [HttpPost("{id:int}/componentes")]
    public async Task<IActionResult> GuardarComponente(int id, [FromBody] ActivoComponenteDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            return Ok(await _service.GuardarComponenteAsync(id, dto, UsuarioActual, ct));
        }
        catch (Exception ex) when (CatalogosActivosFijosController.EsErrorDeNegocio(ex))
        {
            return Problem(detail: CatalogosActivosFijosController.MensajeNegocio(ex),
                           statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpDelete("componentes/{componenteId:int}")]
    public async Task<IActionResult> EliminarComponente(int componenteId, CancellationToken ct)
    {
        try
        {
            await _service.EliminarComponenteAsync(componenteId, ct);
            return Ok(new { success = true });
        }
        catch (Exception ex) when (CatalogosActivosFijosController.EsErrorDeNegocio(ex))
        {
            return Problem(detail: CatalogosActivosFijosController.MensajeNegocio(ex),
                           statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
