using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Reports;
using SIAD.Services.Almacen;
using apc.Security;

namespace apc.Controllers.Almacen;

/// <summary>
/// Documento de movimiento de almacén: entradas y salidas manuales multi-renglón.
/// Equivalente del <c>dlgTransaccionesGenericasINV</c> de Centura.
/// <para>
/// Thin por diseño: valida el modelo, resuelve el permiso de tipos sensibles a partir de los
/// claims y delega. Toda la regla de negocio vive en <see cref="IMovimientoAlmacenService"/>.
/// </para>
/// </summary>
[ApiController]
[Route("api/almacen/movimientos")]
[ModuleAuthorize(PermissionModules.Inventario, PermissionResources.Inventario.Movimientos)]
public sealed class MovimientosAlmacenController : ControllerBase
{
    private readonly IMovimientoAlmacenService _service;

    public MovimientosAlmacenController(IMovimientoAlmacenService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] MovimientoAlmacenFilterDto filtro, CancellationToken ct)
        => Ok(await _service.GetAsync(filtro, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var doc = await _service.GetByIdAsync(id, ct);
        return doc is null ? NotFound() : Ok(doc);
    }

    /// <summary>Comprobante del movimiento en PDF, para imprimir/firmar.</summary>
    [HttpGet("{id:int}/comprobante/pdf")]
    public async Task<IActionResult> GetComprobantePdf(int id, CancellationToken ct)
    {
        var datos = await _service.GetDatosImpresionAsync(id, UsuarioActual, ct);
        if (datos is null)
            return NotFound(new { message = $"No se encontró el movimiento {id}." });

        using var report = new Rpt_Dev_Comprobante_Movimiento(datos);
        using var stream = new MemoryStream();
        report.ExportToPdf(stream);

        Response.Headers.ContentDisposition = $"inline; filename=Movimiento-{datos.Documento.Numero:00000}.pdf";
        return File(stream.ToArray(), "application/pdf");
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] MovimientoAlmacenDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        try
        {
            var creado = await _service.CrearYPostearAsync(dto, UsuarioActual, PuedeUsarTiposSensibles, ct);
            return CreatedAtAction(nameof(GetById), new { id = creado.Id }, creado);
        }
        catch (UnauthorizedAccessException ex)
        {
            // 403, no 500: el usuario está autenticado pero le falta el permiso del tipo.
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>
    /// POST pero se autoriza como <see cref="PermissionAction.Edit"/>: cambia el estado de un
    /// documento existente y postea su reversa, no crea uno nuevo. Mismo criterio que la
    /// anulación de recepciones de compra.
    /// </summary>
    [HttpPost("{id:int}/anular")]
    [ModuleAuthorize(PermissionModules.Inventario, PermissionResources.Inventario.Movimientos, PermissionAction.Edit)]
    public async Task<IActionResult> Anular(int id, [FromBody] AnulacionRequest body, CancellationToken ct)
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

    public sealed class AnulacionRequest
    {
        public string? Motivo { get; set; }
    }

    private string UsuarioActual => User?.Identity?.Name ?? "system";

    /// <summary>
    /// ¿Puede usar tipos marcados <c>requiere_autorizacion</c>? El SuperAdministrador siempre;
    /// el resto necesita el claim explícito. Se resuelve aquí porque los claims son de la capa
    /// web; la decisión de qué hacer con el dato la toma el servicio.
    /// </summary>
    private bool PuedeUsarTiposSensibles =>
        User.IsInRole(RoleNames.SuperAdministrador)
        || User.HasClaim(PermissionClaimTypes.Permission, PermissionNames.Inventario.Movimientos.AutorizarSensibles);
}
