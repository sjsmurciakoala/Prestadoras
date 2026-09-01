namespace SIAD.Core.DTOs.Retenciones;

/// <summary>Filtro de la consulta del registro fiscal de retenciones (F4). company_id lo resuelve el tenant.</summary>
public sealed class RetencionRegistroFilterDto
{
    /// <summary>Código de proveedor (exacto, sin espacios). Null = todos.</summary>
    public string? CodProveedor { get; set; }

    /// <summary>Búsqueda libre por proveedor (código/RTN) o número de póliza/folio.</summary>
    public string? Search { get; set; }

    /// <summary>Rango de fecha de emisión (inclusive). Null = sin límite.</summary>
    public DateOnly? Desde { get; set; }

    public DateOnly? Hasta { get; set; }

    /// <summary>Estado numérico: 1 Vigente, 9 Anulada. Null = ambos.</summary>
    public short? EstadoId { get; set; }

    /// <summary>Folio interno exacto. Null = todos.</summary>
    public int? Folio { get; set; }

    public int Skip { get; set; }

    public int Take { get; set; } = 15;

    public string? SortField { get; set; }

    public bool SortDescending { get; set; }
}

/// <summary>Fila del registro fiscal de retenciones (cabecera) para la grilla de consulta.</summary>
public sealed class RetencionRegistroListItemDto
{
    public long RetencionHdrId { get; init; }

    public int Folio { get; init; }

    public DateOnly FechaEmision { get; init; }

    /// <summary>Compromiso origen; NULL cuando la retención nace de una factura de compra (origen=2).</summary>
    public int? NumeroOrden { get; init; }

    public int NumeroAbono { get; init; }

    /// <summary>Origen del pago: 1 compromiso (OPD), 2 factura de compra (CxP).</summary>
    public short Origen { get; init; }

    /// <summary>Etiqueta legible del origen (Compromiso / Compra). La resuelve el servicio.</summary>
    public string OrigenDescripcion { get; init; } = string.Empty;

    public string? CodProveedor { get; init; }

    public string? RtnProveedor { get; init; }

    public decimal BaseTotal { get; init; }

    public decimal TotalRetenido { get; init; }

    public long? PartidaId { get; init; }

    public string? PolizaNumber { get; init; }

    public short EstadoId { get; init; }

    /// <summary>Etiqueta legible del estado (nunca el código): VIGENTE / ANULADA.</summary>
    public string EstadoDescripcion { get; init; } = string.Empty;

    public string? MotivoAnulacion { get; init; }
}

/// <summary>Línea del registro fiscal de retenciones (detalle) para el maestro-detalle.</summary>
public sealed class RetencionRegistroLineaDto
{
    public long RetencionDtlId { get; init; }

    public int RetencionId { get; init; }

    public string Codigo { get; init; } = string.Empty;

    public string Nombre { get; init; } = string.Empty;

    public decimal Porcentaje { get; init; }

    public decimal BaseLinea { get; init; }

    public decimal MontoRetenido { get; init; }

    public long AccountId { get; init; }
}

/// <summary>Detalle completo de una retención (cabecera + líneas) para reimpresión/inspección.</summary>
public sealed class RetencionRegistroDetalleDto
{
    public RetencionRegistroListItemDto Cabecera { get; init; } = new();

    public IReadOnlyList<RetencionRegistroLineaDto> Lineas { get; init; } = Array.Empty<RetencionRegistroLineaDto>();
}

/// <summary>Página de resultados de la consulta (grilla remota).</summary>
public sealed class RetencionRegistroPagedResultDto
{
    public IReadOnlyList<RetencionRegistroListItemDto> Items { get; init; } = Array.Empty<RetencionRegistroListItemDto>();

    public int TotalCount { get; init; }
}
