using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Reports;
using SIAD.Services.Almacen;
using apc.Security;

namespace apc.Controllers.Almacen;

/// <summary>
/// Documento de requisición (Fase 6): la SOLICITUD de materiales. No mueve inventario. Thin: valida,
/// resuelve el permiso de aprobar a partir de los claims y delega en
/// <see cref="IRequisicionDocumentoService"/>.
/// <para>
/// Ruta aparte de <c>api/almacen/requisiciones</c> (que es la consulta del histórico plano de SIMAFI).
/// </para>
/// </summary>
[ApiController]
[Route("api/almacen/requisiciones/documentos")]
[ModuleAuthorize(PermissionModules.Inventario, PermissionResources.Inventario.Requisiciones)]
public sealed class RequisicionesDocumentoController : ControllerBase
{
    private readonly IRequisicionDocumentoService _service;

    public RequisicionesDocumentoController(IRequisicionDocumentoService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] RequisicionDocumentoFilterDto filtro, CancellationToken ct)
        => Ok(await _service.GetAsync(filtro, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var doc = await _service.GetByIdAsync(id, ct);
        return doc is null ? NotFound() : Ok(doc);
    }

    /// <summary>Comprobante (vale) de la requisición en PDF, para imprimir/firmar.</summary>
    [HttpGet("{id:int}/comprobante/pdf")]
    public async Task<IActionResult> GetComprobantePdf(int id, CancellationToken ct)
    {
        var datos = await _service.GetDatosImpresionAsync(id, UsuarioActual, ct);
        if (datos is null)
            return NotFound(new { message = $"No se encontró la requisición {id}." });

        using var report = new Rpt_Dev_Comprobante_Requisicion(datos);
        using var stream = new MemoryStream();
        report.ExportToPdf(stream);

        Response.Headers.ContentDisposition = $"inline; filename=Requisicion-{datos.Documento.Numero:00000}.pdf";
        return File(stream.ToArray(), "application/pdf");
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] RequisicionDocumentoDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            var creado = await _service.CrearAsync(dto, UsuarioActual, ct);
            return CreatedAtAction(nameof(GetById), new { id = creado.Id }, creado);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPut("{id:int}")]
    [ModuleAuthorize(PermissionModules.Inventario, PermissionResources.Inventario.Requisiciones, PermissionAction.Edit)]
    public async Task<IActionResult> Actualizar(int id, [FromBody] RequisicionDocumentoDto dto, CancellationToken ct)
        => await EjecutarAsync(() => _service.ActualizarAsync(id, dto, UsuarioActual, ct));

    [HttpPost("{id:int}/enviar")]
    [ModuleAuthorize(PermissionModules.Inventario, PermissionResources.Inventario.Requisiciones, PermissionAction.Edit)]
    public async Task<IActionResult> Enviar(int id, CancellationToken ct)
        => await EjecutarAsync(() => _service.EnviarARevisionAsync(id, UsuarioActual, ct));

    /// <summary>
    /// Aprobar. POST autorizado como <see cref="PermissionAction.View"/> a nivel de módulo, pero exige
    /// además el permiso propio <c>requisiciones.aprobar</c> (o SuperAdministrador), que se comprueba
    /// aquí a partir de los claims — decisión del usuario: quien tenga el permiso, aprueba.
    /// </summary>
    [HttpPost("{id:int}/aprobar")]
    [ModuleAuthorize(PermissionModules.Inventario, PermissionResources.Inventario.Requisiciones, PermissionAction.View)]
    public async Task<IActionResult> Aprobar(int id, CancellationToken ct)
    {
        if (!PuedeAprobar)
            return Problem(detail: "No tiene el permiso para aprobar requisiciones.", statusCode: StatusCodes.Status403Forbidden);
        return await EjecutarAsync(() => _service.AprobarAsync(id, UsuarioActual, ct));
    }

    [HttpPost("{id:int}/rechazar")]
    [ModuleAuthorize(PermissionModules.Inventario, PermissionResources.Inventario.Requisiciones, PermissionAction.View)]
    public async Task<IActionResult> Rechazar(int id, [FromBody] MotivoRequisicionDto body, CancellationToken ct)
    {
        if (!PuedeAprobar)
            return Problem(detail: "No tiene el permiso para aprobar/rechazar requisiciones.", statusCode: StatusCodes.Status403Forbidden);
        return await EjecutarAsync(() => _service.RechazarAsync(id, body?.Motivo ?? string.Empty, UsuarioActual, ct));
    }

    [HttpPost("{id:int}/anular")]
    [ModuleAuthorize(PermissionModules.Inventario, PermissionResources.Inventario.Requisiciones, PermissionAction.Edit)]
    public async Task<IActionResult> Anular(int id, [FromBody] MotivoRequisicionDto body, CancellationToken ct)
    {
        try
        {
            var ok = await _service.AnularAsync(id, body?.Motivo ?? string.Empty, UsuarioActual, ct);
            return ok ? Ok(new { success = true }) : NotFound();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private async Task<IActionResult> EjecutarAsync(Func<Task<RequisicionDocumentoDto>> accion)
    {
        try { return Ok(await accion()); }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private string UsuarioActual => User?.Identity?.Name ?? "system";

    private bool PuedeAprobar =>
        User.IsInRole(RoleNames.SuperAdministrador)
        || User.HasClaim(PermissionClaimTypes.Permission, PermissionNames.Inventario.Requisiciones.Aprobar);
}
