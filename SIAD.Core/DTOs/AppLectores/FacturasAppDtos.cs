namespace SIAD.Core.DTOs.AppLectores;

/// <summary>
/// Filtros de la consulta de facturas subidas desde la app de lectores V3.
/// Todo opcional: sin filtros devuelve el período más reciente acotado por LIMIT.
/// </summary>
public sealed class FacturaAppFilterDto
{
    /// <summary>Año del período facturado (factura.ano).</summary>
    public int? Anio { get; set; }

    /// <summary>Mes del período facturado (factura.mes).</summary>
    public int? Mes { get; set; }

    /// <summary>Busca en número de factura, clave, nombre del cliente, lector y UUID.</summary>
    public string? Search { get; set; }

    /// <summary>Usuario/código del lector que subió la lectura.</summary>
    public string? Lector { get; set; }

    /// <summary>Código de condición de lectura (historicomedicion.condicion).</summary>
    public string? Condicion { get; set; }

    /// <summary>Estado de sincronización CAI (PENDIENTE, PENDING_SYNC, CONFIRMADO, SYNC_CONFLICT).</summary>
    public string? EstadoSync { get; set; }

    /// <summary>Fecha de subida (adm_cai_correlativo_emitido.fecha_emision) desde.</summary>
    public DateTime? FechaDesde { get; set; }

    /// <summary>Fecha de subida hasta (inclusive).</summary>
    public DateTime? FechaHasta { get; set; }
}

/// <summary>
/// Una factura emitida vía sincronización de la app (fila con lectura_uuid en
/// adm_cai_correlativo_emitido) con su lectura y datos del cliente.
/// </summary>
public sealed class FacturaAppListItemDto
{
    public long FacturaId { get; set; }
    public string NumeroFactura { get; set; } = string.Empty;
    public int NumRecibo { get; set; }

    public string ClienteClave { get; set; } = string.Empty;
    public string ClienteNombre { get; set; } = string.Empty;

    public int? Anio { get; set; }
    public int? Mes { get; set; }
    public DateTime? FechaEmision { get; set; }
    public DateTime? FechaVence { get; set; }
    public decimal Total { get; set; }
    public string? EstadoFactura { get; set; }
    public bool? ConMedicion { get; set; }

    public string? Lector { get; set; }

    // Lectura (historicomedicion)
    public string? Condicion { get; set; }
    public string? CondicionNombre { get; set; }
    public string? Contador { get; set; }
    public decimal? LecturaAnterior { get; set; }
    public decimal? LecturaActual { get; set; }
    public decimal? Consumo { get; set; }
    public DateTime? FechaLectura { get; set; }
    public string? Ciclo { get; set; }
    public string? Ruta { get; set; }
    public string? Secuencia { get; set; }
    public string? Categoria { get; set; }
    public string? Observacion { get; set; }

    // Sincronización (adm_cai_correlativo_emitido)
    public string? LecturaUuid { get; set; }
    public string EstadoSync { get; set; } = string.Empty;
    public DateTime FechaSubida { get; set; }
    public DateTime? FechaConfirmacion { get; set; }
    public long CorrelativoCai { get; set; }
    public string? DetalleConflicto { get; set; }
}
