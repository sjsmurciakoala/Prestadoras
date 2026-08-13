using System.Collections.Generic;
using SIAD.Core.DTOs.Almacen;

namespace SIAD.Core.DTOs.Retenciones;

/// <summary>
/// Datos de impresión del reporte mensual de retenciones para la declaración (F5.1, PDF servidor).
/// Reutiliza <see cref="ComprobanteAlmacenImpresionBase"/> (encabezado de empresa + ImpresoPor) igual
/// que los listados de almacén (Existencias/Valuación). Las filas son a nivel de detalle; el reporte
/// las agrupa por tipo (<c>TipoDisplay</c>) y proveedor (<c>ProveedorDisplay</c>) con subtotales.
/// </summary>
public sealed class RetencionesDeclaracionImpresionDto : ComprobanteAlmacenImpresionBase
{
    public string Titulo { get; set; } = "RETENCIONES APLICADAS — DECLARACIÓN";

    public List<RetencionDeclaracionLineaDto> Items { get; set; } = new();

    /// <summary>Texto legible del filtro para el encabezado (rango de fechas · estado · búsqueda).</summary>
    public string FiltroTexto { get; set; } = string.Empty;
}
