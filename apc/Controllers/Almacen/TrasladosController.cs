using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Reports;
using SIAD.Services.Almacen;
using apc.Security;

namespace apc.Controllers.Almacen;

/// <summary>
/// Traslado entre bodegas (Fase 5): documento de dos bodegas con tránsito y recepción parcial,
/// o directo en un paso. Thin por diseño: valida el modelo y delega; la regla vive en
/// <see cref="ITrasladoAlmacenService"/>.
/// </summary>
[ApiController]
[Route("api/almacen/traslados")]
[ModuleAuthorize(PermissionModules.Inventario, PermissionResources.Inventario.Traslados)]
public sealed class TrasladosController : ControllerBase
{
    private readonly ITrasladoAlmacenService _service;

    public TrasladosController(ITrasladoAlmacenService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] TrasladoFilterDto filtro, CancellationToken ct)
        => Ok(await _service.GetAsync(filtro, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var doc = await _service.GetByIdAsync(id, ct);
        return doc is null ? NotFound() : Ok(doc);
    }

    /// <summary>Comprobante (vale) del traslado —el envío— en PDF, para imprimir/firmar.</summary>
    [HttpGet("{id:int}/comprobante/pdf")]
    public async Task<IActionResult> GetComprobantePdf(int id, CancellationToken ct)
    {
        var datos = await _service.GetDatosImpresionAsync(id, UsuarioActual, ct);
        if (datos is null)
            return NotFound(new { message = $"No se encontró el traslado {id}." });

        using var report = new Rpt_Dev_Comprobante_Traslado(datos);
        using var stream = new MemoryStream();
        report.ExportToPdf(stream);

        Response.Headers.ContentDisposition = $"inline; filename=Traslado-{datos.Documento.Numero:00000}.pdf";
        return File(stream.ToArray(), "application/pdf");
    }

    /// <summary>Comprobante de una recepción (tanda) del traslado en PDF —la contraparte que recibe destino—.</summary>
    [HttpGet("{id:int}/recepciones/{recepcionId:int}/comprobante/pdf")]
    public async Task<IActionResult> GetRecepcionComprobantePdf(int id, int recepcionId, CancellationToken ct)
    {
        var datos = await _service.GetDatosImpresionRecepcionAsync(id, recepcionId, UsuarioActual, ct);
        if (datos is null)
            return NotFound(new { message = $"No se encontró la recepción {recepcionId} del traslado {id}." });

        using var report = new Rpt_Dev_Comprobante_Traslado_Recepcion(datos);
        using var stream = new MemoryStream();
        report.ExportToPdf(stream);

        Response.Headers.ContentDisposition = $"inline; filename=Traslado-{datos.Traslado.Numero:00000}-Recepcion-{recepcionId}.pdf";
        return File(stream.ToArray(), "application/pdf");
    }

    /// <summary>Enviar (crear) el traslado. Con recepción o directo según <c>RequiereRecepcion</c>.</summary>
    [HttpPost]
    public async Task<IActionResult> Enviar([FromBody] TrasladoDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            var creado = await _service.EnviarAsync(dto, UsuarioActual, ct);
            return CreatedAtAction(nameof(GetById), new { id = creado.Id }, creado);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>
    /// Recibir una tanda (recepción parcial). POST autorizado como <see cref="PermissionAction.Edit"/>:
    /// cambia el estado de un traslado existente, no crea uno nuevo.
    /// </summary>
    [HttpPost("{id:int}/recibir")]
    [ModuleAuthorize(PermissionModules.Inventario, PermissionResources.Inventario.Traslados, PermissionAction.Edit)]
    public async Task<IActionResult> Recibir(int id, [FromBody] RecepcionTrasladoDto dto, CancellationToken ct)
    {
        try
        {
            var doc = await _service.RecibirAsync(id, dto, UsuarioActual, ct);
            return Ok(doc);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("{id:int}/anular")]
    [ModuleAuthorize(PermissionModules.Inventario, PermissionResources.Inventario.Traslados, PermissionAction.Edit)]
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
}
