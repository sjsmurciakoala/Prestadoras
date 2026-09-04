using SIAD.Core.DTOs.Almacen;

namespace SIAD.Core.DTOs.Proveedores;

/// <summary>
/// Datos de impresión del estado de cuenta del proveedor: identidad, resumen con antigüedad y el
/// detalle de los documentos por pagar. Reutiliza <see cref="ComprobanteAlmacenImpresionBase"/>
/// para el encabezado de empresa y el pie, como el resto de los reportes programáticos.
/// </summary>
public sealed class ProveedorEstadoCuentaImpresionDto : ComprobanteAlmacenImpresionBase
{
    public string Titulo { get; set; } = "ESTADO DE CUENTA DE PROVEEDOR";

    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Rtn { get; set; }
    public string? TipoNombre { get; set; }

    /// <summary>Cuenta contable ya formateada con el formato de cuentas de la empresa.</summary>
    public string? CuentaContable { get; set; }

    public DateOnly Corte { get; set; }

    public ProveedorEstadoCuentaResumenDto Resumen { get; set; } = new();

    /// <summary>Documentos del reporte, ya ordenados por vencimiento.</summary>
    public List<ProveedorEstadoCuentaDocumentoDto> Items { get; set; } = new();

    /// <summary>Descripción legible de lo que se imprimió (va bajo el título y en el pie).</summary>
    public string FiltroTexto { get; set; } = string.Empty;
}
