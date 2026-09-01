using SIAD.Core.DTOs.Proveedores;

namespace SIAD.Services.Proveedores;

/// <summary>
/// Estado de cuenta del proveedor: qué se le debe, por cuáles documentos y con qué historial.
/// <para>
/// Solo lectura. Unifica las dos ramas donde vive la deuda —facturas de compra
/// (<c>alm_compra_cxp</c>) y compromisos / órdenes de pago directo
/// (<c>prv_compromiso_hdr</c>)— a través de las funciones <c>fn_prv_estado_cuenta_*</c>,
/// que son las dueñas de las reglas de vigencia.
/// </para>
/// </summary>
public interface IProveedorEstadoCuentaService
{
    /// <summary>
    /// Identidad del proveedor + resumen de saldo y antigüedad. <c>null</c> si el código no
    /// existe en la empresa actual.
    /// </summary>
    /// <param name="corte">Fecha de corte; <c>null</c> = hoy.</param>
    Task<ProveedorEstadoCuentaDto?> GetResumenAsync(
        string codigo, DateOnly? corte = null, CancellationToken cancellationToken = default);

    /// <summary>Documentos del proveedor con su abonado y saldo, ordenados por vencimiento.</summary>
    /// <param name="soloPendientes">true = únicamente los que conservan saldo.</param>
    Task<IReadOnlyList<ProveedorEstadoCuentaDocumentoDto>> GetDocumentosAsync(
        string codigo, DateOnly? corte = null, bool soloPendientes = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Libro de cargos y abonos con saldo corrido. El rango acota las filas visibles; el saldo
    /// de cada fila sigue siendo el acumulado histórico real.
    /// </summary>
    Task<IReadOnlyList<ProveedorEstadoCuentaMovimientoDto>> GetMovimientosAsync(
        string codigo, DateOnly? desde = null, DateOnly? hasta = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Arma los datos del PDF del estado de cuenta (identidad + resumen + documentos).
    /// <c>null</c> si el proveedor no existe en la empresa actual.
    /// </summary>
    Task<ProveedorEstadoCuentaImpresionDto?> GetDatosImpresionAsync(
        string codigo, DateOnly? corte = null, bool soloPendientes = true, string? impresoPor = null,
        CancellationToken cancellationToken = default);
}
