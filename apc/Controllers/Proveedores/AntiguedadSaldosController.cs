using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Services.Proveedores;
using apc.Security;

namespace apc.Controllers.Proveedores;

/// <summary>
/// Antigüedad de saldos de los proveedores (aging de cuentas por pagar): la deuda de cada proveedor
/// repartida por tramos de vencimiento a una fecha de corte.
/// <para>
/// Módulo de permisos: <c>proveedores</c>, recurso fino <c>antiguedad_saldos</c>
/// (<see cref="PermissionResources.Proveedores.AntiguedadSaldos"/>). Solo lectura (GET→View):
/// reparte por tramos la misma deuda que calcula el estado de cuenta, no crea ni modifica nada.
/// La empresa la resuelve el tenant.
/// </para>
/// </summary>
[ApiController]
[Route("api/proveedores/antiguedad-saldos")]
[ModuleAuthorize(PermissionModules.Proveedores, PermissionResources.Proveedores.AntiguedadSaldos)]
public sealed class AntiguedadSaldosController : ControllerBase
{
    private readonly IAntiguedadSaldosProveedorService _service;

    public AntiguedadSaldosController(IAntiguedadSaldosProveedorService service) => _service = service;

    /// <summary>
    /// Matriz de antigüedad: una fila por proveedor con saldo más los totales por tramo.
    /// </summary>
    /// <param name="corte">Fecha de corte; vacío = hoy.</param>
    /// <param name="incluirPorVencer">false = solo lo vencido.</param>
    /// <param name="origen">0 = compras + compromisos, 1 = solo compras, 2 = solo compromisos.</param>
    /// <param name="tipoProveedor">Filtra por tipo de proveedor; vacío = todos.</param>
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] DateOnly? corte,
        [FromQuery] bool incluirPorVencer = true,
        [FromQuery] int origen = 0,
        [FromQuery] int? tipoProveedor = null,
        [FromQuery] string? proveedor = null,
        CancellationToken ct = default)
        => Ok(await _service.GetAsync(corte, incluirPorVencer, origen, tipoProveedor, proveedor, ct));

    /// <summary>Cuadro de antigüedad en PDF (inline). Mismos filtros que la matriz.</summary>
    [HttpGet("pdf")]
    public async Task<IActionResult> GetPdf(
        [FromQuery] DateOnly? corte,
        [FromQuery] bool incluirPorVencer = true,
        [FromQuery] int origen = 0,
        [FromQuery] int? tipoProveedor = null,
        [FromQuery] string? proveedor = null,
        CancellationToken ct = default)
    {
        var datos = await _service.GetDatosImpresionAsync(
            corte, incluirPorVencer, origen, tipoProveedor, proveedor, User?.Identity?.Name, ct);

        using var report = new SIAD.Reports.Rpt_Dev_AntiguedadSaldos_Proveedor(datos);
        using var ms = new MemoryStream();
        report.ExportToPdf(ms);

        Response.Headers.ContentDisposition = $"inline; filename=antiguedad_saldos_{Stamp()}.pdf";
        return File(ms.ToArray(), "application/pdf");
    }

    /// <summary>Cuadro de antigüedad en Excel (descarga).</summary>
    [HttpGet("excel")]
    public async Task<IActionResult> GetExcel(
        [FromQuery] DateOnly? corte,
        [FromQuery] bool incluirPorVencer = true,
        [FromQuery] int origen = 0,
        [FromQuery] int? tipoProveedor = null,
        [FromQuery] string? proveedor = null,
        CancellationToken ct = default)
    {
        var datos = await _service.GetDatosImpresionAsync(
            corte, incluirPorVencer, origen, tipoProveedor, proveedor, User?.Identity?.Name, ct);

        using var report = new SIAD.Reports.Rpt_Dev_AntiguedadSaldos_Proveedor(datos);
        using var ms = new MemoryStream();
        report.ExportToXlsx(ms);

        var nombre = $"antiguedad_saldos_{Stamp()}.xlsx";
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombre);
    }

    private static string Stamp() => DateTime.Now.ToString("yyyyMMdd_HHmm");
}
