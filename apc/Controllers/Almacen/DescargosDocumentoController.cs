using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Reports;
using SIAD.Services.Almacen;
using apc.Security;

namespace apc.Controllers.Almacen;

/// <summary>
/// Documento de descargo (Fase 6): la ENTREGA real de materiales (sí postea). Thin: valida y delega
/// en <see cref="IDescargoDocumentoService"/>. Ruta aparte de <c>api/almacen/descargos</c> (que es la
/// consulta del histórico plano de SIMAFI).
/// </summary>
[ApiController]
[Route("api/almacen/descargos/documentos")]
[ModuleAuthorize(PermissionModules.Inventario, PermissionResources.Inventario.Descargos)]
public sealed class DescargosDocumentoController : ControllerBase
{
    private readonly IDescargoDocumentoService _service;

    public DescargosDocumentoController(IDescargoDocumentoService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] DescargoDocumentoFilterDto filtro, CancellationToken ct)
        => Ok(await _service.GetAsync(filtro, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var doc = await _service.GetByIdAsync(id, ct);
        return doc is null ? NotFound() : Ok(doc);
    }

    /// <summary>Comprobante (vale de salida) del descargo en PDF, para imprimir/firmar.</summary>
    [HttpGet("{id:int}/comprobante/pdf")]
    public async Task<IActionResult> GetComprobantePdf(int id, CancellationToken ct)
    {
        var datos = await _service.GetDatosImpresionAsync(id, UsuarioActual, ct);
        if (datos is null)
            return NotFound(new { message = $"No se encontró el descargo {id}." });

        using var report = new Rpt_Dev_Comprobante_Descargo(datos);
        using var stream = new MemoryStream();
        report.ExportToPdf(stream);

        Response.Headers.ContentDisposition = $"inline; filename=Descargo-{datos.Documento.Numero:00000}.pdf";
        return File(stream.ToArray(), "application/pdf");
    }

    /// <summary>Entrega (crea y postea) el descargo. Contra requisición (parcial) o directo con motivo.</summary>
    [HttpPost]
    public async Task<IActionResult> Entregar([FromBody] DescargoDocumentoDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            var creado = await _service.EntregarAsync(dto, UsuarioActual, ct);
            return CreatedAtAction(nameof(GetById), new { id = creado.Id }, creado);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("{id:int}/anular")]
    [ModuleAuthorize(PermissionModules.Inventario, PermissionResources.Inventario.Descargos, PermissionAction.Edit)]
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
