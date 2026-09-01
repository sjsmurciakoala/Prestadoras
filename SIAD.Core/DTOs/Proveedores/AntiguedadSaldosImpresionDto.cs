using SIAD.Core.DTOs.Almacen;

namespace SIAD.Core.DTOs.Proveedores;

/// <summary>
/// Datos de impresión del cuadro de antigüedad de saldos: una fila por proveedor con sus seis
/// tramos, más los totales del pie. Reutiliza <see cref="ComprobanteAlmacenImpresionBase"/> para el
/// encabezado de empresa y el pie, como el resto de los reportes programáticos.
/// </summary>
public sealed class AntiguedadSaldosImpresionDto : ComprobanteAlmacenImpresionBase
{
    public string Titulo { get; set; } = "ANTIGÜEDAD DE SALDOS DE PROVEEDORES";

    public DateOnly Corte { get; set; }

    /// <summary>Descripción legible de lo que se imprimió (va bajo el título).</summary>
    public string FiltroTexto { get; set; } = string.Empty;

    /// <summary>Filas del cuadro, ya ordenadas por saldo.</summary>
    public List<AntiguedadSaldosProveedorFilaDto> Items { get; set; } = new();

    /// <summary>Totales por tramo, para el pie del cuadro.</summary>
    public AntiguedadSaldosTotalesDto Totales { get; set; } = new();
}
