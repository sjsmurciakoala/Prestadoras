using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Facturacion;
using SIAD.Services.Facturacion;
using apc.Security;

namespace apc.Controllers.Facturacion;

/// <summary>
/// Emisión de la factura de una lectura desde el portal: el mismo acto que hace el lector en
/// campo, pero desde el escritorio. Sirve para refacturar tras anular con nota de crédito, para
/// el abonado leído en papel y para el que el teléfono no alcanzó.
/// </summary>
[ApiController]
[Route("api/facturacion/emision-lectura")]
[ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.EmisionLectura)]
public sealed class EmisionLecturaController : ControllerBase
{
    private readonly IEmisionLecturaService _service;

    public EmisionLecturaController(IEmisionLecturaService service) => _service = service;

    private string Usuario => User?.Identity?.Name ?? "system";

    /// <summary>Estado del bloque de folios del portal, para avisar antes de que se agote.</summary>
    [HttpGet("bloque")]
    public async Task<IActionResult> Bloque(CancellationToken ct)
    {
        try
        {
            return Ok(await _service.ObtenerBloqueAsync(ct));
        }
        catch (Npgsql.PostgresException ex)
        {
            // Lo normal aquí es CAI_VIGENTE_NO_DISPONIBLE: no hay CAI vigente o se agotó el
            // rango. Es una condición de configuración, no un fallo del servidor.
            return BadRequest(new { error = ex.MessageText ?? ex.Message });
        }
    }

    /// <summary>
    /// Calcula lo que saldria en el papel, sin emitir ni consumir folio.
    /// </summary>
    [HttpPost("preview")]
    public async Task<IActionResult> Preview(
        [FromBody] EmitirFacturaLecturaRequest request, CancellationToken ct)
    {
        if (request is null)
        {
            return BadRequest(new { error = "Falta el cuerpo de la solicitud." });
        }

        return Ok(await _service.PrevisualizarAsync(request, ct));
    }

    /// <summary>
    /// Emite la factura. Los rechazos de negocio vuelven como 200 con
    /// <c>success = false</c> y su código, no como error HTTP: la pantalla los muestra tal cual.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Emitir(
        [FromBody] EmitirFacturaLecturaRequest request, CancellationToken ct)
    {
        if (request is null)
        {
            return BadRequest(new { error = "Falta el cuerpo de la solicitud." });
        }

        var resultado = await _service.EmitirAsync(request, Usuario, ct);
        return Ok(resultado);
    }
}
