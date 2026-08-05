using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Services.Almacen;
using apc.Security;

namespace apc.Controllers.Almacen;

/// <summary>
/// Corte de carga inicial de existencias.
/// <para>
/// <b>Ojo con los permisos:</b> las acciones de consulta y ejecución van con el recurso
/// <c>carga_inicial</c> del módulo Inventario, pero <b>cerrar</b> y <b>reabrir</b> llevan
/// <c>[ModuleAuthorize(PermissionModules.Configuracion)]</c> <b>sin recurso</b>. No es un
/// capricho: <c>ModuleAuthorize</c> hace <i>fallback</i> al permiso de módulo, así que un
/// sub-recurso de Inventario sería un SUPERCONJUNTO de <c>module.inventario.*</c> y no
/// restringiría a nadie. Sacar esas dos acciones del módulo es la única palanca que sí
/// restringe: valorizar y cerrar todo el inventario no puede quedar al alcance de
/// cualquier digitador de bodega.
/// </para>
/// </summary>
[ApiController]
[Route("api/almacen/carga-inicial")]
public sealed class CargaInicialController : ControllerBase
{
    private readonly ICargaInicialInventarioService _service;

    public CargaInicialController(ICargaInicialInventarioService service)
    {
        _service = service;
    }

    private string Usuario => User?.Identity?.Name ?? "system";

    [HttpGet("pendientes")]
    [ModuleAuthorize(PermissionModules.Inventario, PermissionResources.Inventario.CargaInicial)]
    public async Task<IActionResult> GetPendientes([FromQuery] int? bodegaId, CancellationToken ct)
        => Ok(await _service.GetPendientesAsync(bodegaId, ct));

    [HttpGet("simular")]
    [ModuleAuthorize(PermissionModules.Inventario, PermissionResources.Inventario.CargaInicial)]
    public async Task<IActionResult> Simular([FromQuery] int? bodegaId, CancellationToken ct)
        => Ok(await _service.SimularLoteAsync(bodegaId, ct));

    [HttpGet("config")]
    [ModuleAuthorize(PermissionModules.Inventario, PermissionResources.Inventario.CargaInicial)]
    public async Task<IActionResult> GetConfig(CancellationToken ct)
        => Ok(await _service.GetConfigAsync(ct));

    [HttpPost("ejecutar")]
    [ModuleAuthorize(PermissionModules.Inventario, PermissionResources.Inventario.CargaInicial)]
    public async Task<IActionResult> Ejecutar([FromBody] EjecutarLoteRequest request, CancellationToken ct)
    {
        if (request is null || request.FechaCorte == default)
        {
            return Problem(
                detail: "La fecha de corte es obligatoria: es la fecha contable de todos los asientos del lote.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            return Ok(await _service.EjecutarLoteAsync(
                request.FechaCorte, request.TamanoLote, Usuario, request.BodegaId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("costo-manual")]
    [ModuleAuthorize(PermissionModules.Inventario, PermissionResources.Inventario.CargaInicial)]
    public async Task<IActionResult> CostoManual([FromBody] CostoManualRequest request, CancellationToken ct)
    {
        if (request?.Items is null || request.Items.Count == 0)
        {
            return Problem(detail: "No se recibió ningún costo.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (request.FechaCorte == default)
        {
            return Problem(detail: "La fecha de corte es obligatoria.", statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            return Ok(await _service.PostearConCostoManualAsync(request.Items, request.FechaCorte, Usuario, ct));
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    // ── Acciones sensibles: permiso de CONFIGURACIÓN, sin recurso ────────────

    [HttpPost("cerrar")]
    [ModuleAuthorize(PermissionModules.Configuracion)]
    public async Task<IActionResult> Cerrar(CancellationToken ct)
    {
        try
        {
            await _service.CerrarAperturaAsync(Usuario, ct);
            return Ok(new { success = true });
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>
    /// Reabre un par mal costeado (reversa + nueva apertura, atómico). Sigue disponible
    /// AUNQUE el corte esté cerrado: es la única vía de corrección y bloquearla dejaría el
    /// sistema sin salida. Cada reapertura queda en el kardex con su motivo.
    /// </summary>
    [HttpPost("reabrir")]
    [ModuleAuthorize(PermissionModules.Configuracion)]
    public async Task<IActionResult> Reabrir([FromBody] ReabrirRequest request, CancellationToken ct)
    {
        if (request is null)
        {
            return Problem(detail: "Solicitud vacía.", statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var resultado = await _service.ReabrirAsync(
                request.ArticuloId, request.BodegaId, request.NuevoCosto, request.Motivo ?? string.Empty, Usuario, ct);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary><c>BodegaId</c> opcional: permite cortar una bodega a la vez.</summary>
    public sealed record EjecutarLoteRequest(DateOnly FechaCorte, int TamanoLote, int? BodegaId);
    public sealed record CostoManualRequest(DateOnly FechaCorte, List<CargaInicialCostoManualDto> Items);
    public sealed record ReabrirRequest(int ArticuloId, int BodegaId, decimal NuevoCosto, string? Motivo);
}
