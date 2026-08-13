using SIAD.Core.DTOs.Retenciones;

namespace SIAD.Services.Retenciones;

/// <summary>
/// Consulta del registro fiscal de retenciones aplicadas (F4): libro prv_retencion_hdr/dtl.
/// Solo lectura; el registro lo escribe el flujo de pago (OrdenesPagoDirectoService). El filtro
/// por empresa lo aplica SiadDbContext (tenant) automáticamente.
/// </summary>
public interface IRetencionRegistroService
{
    /// <summary>Lista paginada/filtrada de cabeceras (proveedor, fechas, estado, folio).</summary>
    Task<RetencionRegistroPagedResultDto> BuscarAsync(RetencionRegistroFilterDto filtro, CancellationToken ct = default);

    /// <summary>Detalle de una retención (cabecera + líneas). Null si no existe en la empresa actual.</summary>
    Task<RetencionRegistroDetalleDto?> GetDetalleAsync(long retencionHdrId, CancellationToken ct = default);

    /// <summary>
    /// Datos de impresión de la CONSTANCIA de retención (F5): cabecera + líneas + empresa (agente
    /// retenedor) + nombre del proveedor + concepto del compromiso + monto en letras. Null si el hdr
    /// no existe en la empresa actual. <paramref name="impresoPor"/> alimenta el pie del documento.
    /// </summary>
    Task<ConstanciaRetencionImpresionDto?> GetDatosConstanciaAsync(
        long retencionHdrId, string? impresoPor = null, CancellationToken ct = default);

    /// <summary>
    /// Reporte mensual para la declaración (F5): filas planas a nivel de detalle en el rango de
    /// fechas, para agrupar por tipo/proveedor. Usa <c>dtl.base_linea</c>. Por defecto la pantalla
    /// filtra Vigentes (lo declarable); las Anuladas se consultan aparte.
    /// </summary>
    Task<IReadOnlyList<RetencionDeclaracionLineaDto>> BuscarDeclaracionAsync(
        RetencionDeclaracionFilterDto filtro, CancellationToken ct = default);

    /// <summary>
    /// Datos de impresión del reporte mensual para la declaración (F5.1, PDF servidor): filas del
    /// filtro (via <see cref="BuscarDeclaracionAsync"/>) + empresa (agente retenedor) + texto de
    /// filtro legible. <paramref name="impresoPor"/> alimenta el pie del reporte.
    /// </summary>
    Task<RetencionesDeclaracionImpresionDto> GetDatosDeclaracionImpresionAsync(
        RetencionDeclaracionFilterDto filtro, string impresoPor, CancellationToken ct = default);
}
