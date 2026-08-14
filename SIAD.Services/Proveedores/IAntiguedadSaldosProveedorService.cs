using SIAD.Core.DTOs.Proveedores;

namespace SIAD.Services.Proveedores;

/// <summary>
/// Antigüedad de saldos de los proveedores (aging de cuentas por pagar): reparte la deuda de cada
/// proveedor por tramos de vencimiento a una fecha de corte.
/// <para>
/// Solo lectura. Se apoya en <c>fn_prv_antiguedad_saldos</c>, que corre el mismo cálculo del estado
/// de cuenta (<c>fn_prv_estado_cuenta_documentos</c>) sobre todos los proveedores y abre el último
/// tramo en 91–120 y más de 120 días. No duplica las reglas de vigencia.
/// </para>
/// </summary>
public interface IAntiguedadSaldosProveedorService
{
    /// <summary>
    /// Matriz de antigüedad: una fila por proveedor con saldo, más los totales por tramo.
    /// </summary>
    /// <param name="corte">Fecha de corte; <c>null</c> = hoy.</param>
    /// <param name="incluirPorVencer"><c>false</c> = solo lo vencido (la columna «por vencer» llega en 0).</param>
    /// <param name="origen">0 = compras + compromisos, 1 = solo facturas de compra, 2 = solo compromisos.</param>
    /// <param name="codTipoProveedor">Filtra por tipo de proveedor; <c>null</c> = todos.</param>
    Task<AntiguedadSaldosProveedorDto> GetAsync(
        DateOnly? corte = null,
        bool incluirPorVencer = true,
        int origen = 0,
        int? codTipoProveedor = null,
        CancellationToken cancellationToken = default);
}
