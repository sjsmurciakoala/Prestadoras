namespace SIAD.Core.DTOs.Retenciones;

/// <summary>
/// Filtro del reporte mensual de retenciones para la declaración (F5). company_id lo resuelve el
/// tenant. Por defecto la pantalla arranca en Vigentes (lo declarable); Anuladas se ven aparte.
/// </summary>
public sealed class RetencionDeclaracionFilterDto
{
    /// <summary>Rango de fecha de emisión (inclusive). Null = sin límite.</summary>
    public DateOnly? Desde { get; set; }

    public DateOnly? Hasta { get; set; }

    /// <summary>Estado numérico: 1 Vigente, 9 Anulada. Null = ambos (para revisión).</summary>
    public short? EstadoId { get; set; }

    /// <summary>Búsqueda libre por proveedor (código/RTN) o por tipo de retención (código/nombre).</summary>
    public string? Search { get; set; }
}

/// <summary>
/// Fila plana (a nivel de <c>prv_retencion_dtl</c>) del reporte mensual para la declaración. La
/// pantalla la agrupa por TIPO (<see cref="TipoNombre"/>) y por PROVEEDOR. El monto sujeto por
/// retención es <see cref="BaseLinea"/> (NO el bruto del pago), y el total a declarar suma
/// <see cref="MontoRetenido"/> de las Vigentes.
/// </summary>
public sealed class RetencionDeclaracionLineaDto
{
    public int Folio { get; init; }

    public DateOnly FechaEmision { get; init; }

    public int NumeroOrden { get; init; }

    public int NumeroAbono { get; init; }

    public string? CodProveedor { get; init; }

    public string? NombreProveedor { get; init; }

    public string? RtnProveedor { get; init; }

    public int RetencionId { get; init; }

    /// <summary>Snapshot del código del tipo de retención (cfg_retencion.codigo).</summary>
    public string TipoCodigo { get; init; } = string.Empty;

    /// <summary>Snapshot del nombre del tipo de retención (cfg_retencion.nombre); clave de agrupación.</summary>
    public string TipoNombre { get; init; } = string.Empty;

    public decimal Porcentaje { get; init; }

    /// <summary>Base de ESTA retención (dtl.base_linea) — el monto sujeto para la declaración.</summary>
    public decimal BaseLinea { get; init; }

    public decimal MontoRetenido { get; init; }

    public short EstadoId { get; init; }

    /// <summary>Etiqueta legible del estado (nunca el código): VIGENTE / ANULADA.</summary>
    public string EstadoDescripcion { get; init; } = string.Empty;

    /// <summary>Fecha ya formateada (dd/MM/yyyy) para bindear en el reporte sin formatear DateOnly en el motor de expresiones.</summary>
    public string FechaTexto => FechaEmision.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Clave de agrupación por tipo ("código - nombre"); el grid agrupa por este campo.</summary>
    public string TipoDisplay => string.IsNullOrWhiteSpace(TipoCodigo) ? TipoNombre : $"{TipoCodigo} - {TipoNombre}";

    /// <summary>Clave de agrupación por proveedor ("código - nombre").</summary>
    public string ProveedorDisplay =>
        string.IsNullOrWhiteSpace(NombreProveedor)
            ? (string.IsNullOrWhiteSpace(CodProveedor) ? "(sin proveedor)" : CodProveedor!)
            : $"{CodProveedor} - {NombreProveedor}";
}
