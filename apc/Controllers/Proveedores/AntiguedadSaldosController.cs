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
        CancellationToken ct = default)
        => Ok(await _service.GetAsync(corte, incluirPorVencer, origen, tipoProveedor, ct));
}
