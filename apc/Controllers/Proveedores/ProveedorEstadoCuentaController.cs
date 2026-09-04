using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Services.Proveedores;
using apc.Security;

namespace apc.Controllers.Proveedores;

/// <summary>
/// Estado de cuenta de un proveedor: saldo, documentos por pagar y libro de movimientos.
/// <para>
/// Módulo de permisos: <c>proveedores</c>, recurso FINO <c>estado_cuenta</c>
/// (<see cref="PermissionResources.Proveedores.EstadoCuenta"/>). Solo lectura (GET→View):
/// unifica lo que ya registran Compras y Compromisos, no crea ni modifica nada.
/// La empresa la resuelve el tenant; el código de la ruta nunca la decide.
/// </para>
/// </summary>
[ApiController]
[Route("api/proveedores/{codigo}/estado-cuenta")]
[ModuleAuthorize(PermissionModules.Proveedores, PermissionResources.Proveedores.EstadoCuenta)]
public sealed class ProveedorEstadoCuentaController : ControllerBase
{
    private readonly IProveedorEstadoCuentaService _service;

    public ProveedorEstadoCuentaController(IProveedorEstadoCuentaService service) => _service = service;

    /// <summary>Identidad del proveedor + resumen de saldo, antigüedad y último pago.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(string codigo, [FromQuery] DateOnly? corte, CancellationToken ct)
    {
        var estado = await _service.GetResumenAsync(codigo, corte, ct);
        return estado is null
            ? NotFound(new { mensaje = "No se encontró el proveedor." })
            : Ok(estado);
    }

    /// <summary>Documentos por pagar (facturas de compra + compromisos) con su abonado y saldo.</summary>
    [HttpGet("documentos")]
    public async Task<IActionResult> GetDocumentos(
        string codigo,
        [FromQuery] DateOnly? corte,
        [FromQuery] bool soloPendientes = true,
        CancellationToken ct = default)
        => Ok(await _service.GetDocumentosAsync(codigo, corte, soloPendientes, ct));

    /// <summary>
    /// Libro de cargos y abonos con saldo corrido. El rango de fechas acota las filas; el saldo
    /// de cada una sigue siendo el acumulado histórico.
    /// </summary>
    [HttpGet("movimientos")]
    public async Task<IActionResult> GetMovimientos(
        string codigo,
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        CancellationToken ct)
        => Ok(await _service.GetMovimientosAsync(codigo, desde, hasta, ct));

    /// <summary>
    /// PDF del estado de cuenta (inline): identidad, resumen con antigüedad y los documentos por
    /// pagar. Reimprimible: solo lee, no cambia nada.
    /// </summary>
    [HttpGet("pdf")]
    public async Task<IActionResult> GetPdf(
        string codigo,
        [FromQuery] DateOnly? corte,
        [FromQuery] bool soloPendientes = true,
        CancellationToken ct = default)
    {
        var datos = await _service.GetDatosImpresionAsync(
            codigo, corte, soloPendientes, User?.Identity?.Name, ct);

        if (datos is null)
        {
            return NotFound(new { mensaje = "No se encontró el proveedor." });
        }

        using var report = new SIAD.Reports.Rpt_Dev_EstadoCuenta_Proveedor(datos);
        using var ms = new MemoryStream();
        report.ExportToPdf(ms);

        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
        Response.Headers.ContentDisposition =
            $"inline; filename=estado_cuenta_{datos.Codigo}_{stamp}.pdf";
        return File(ms.ToArray(), "application/pdf");
    }
}
