namespace SIAD.Core.DTOs.Tarifario;

public sealed class CaiFacturacionListDto
{
    public long CaiId { get; set; }
    public string? CodigoCai { get; set; }
    public string? PrefijoDocumento { get; set; }
    public string? PuntoEmision { get; set; }
    public long RangoDesde { get; set; }
    public long RangoHasta { get; set; }
    public long CorrelativoActual { get; set; }
    public DateTime VigenciaDesde { get; set; }
    public DateTime? VigenciaHasta { get; set; }
    public string? Observaciones { get; set; }
    public int StatusId { get; set; }
    public int TotalBloques { get; set; }
    public long? SiguienteCorrelativoDisponible { get; set; }

    // SAR-Compliance: EstablecimientoCodigo (EEE) es texto libre que el usuario
    // escribe (autorizado por SAR). El proyecto es multi-empresa pero mono-sucursal,
    // por eso aqui solo guardamos el codigo, sin nombre ni FK a tabla de sucursales.
    public string EstablecimientoCodigo { get; set; } = "000";
    public short TipoDocumentoFiscalId { get; set; }
    public string? TipoDocumentoFiscalCodigo { get; set; }
    public string? TipoDocumentoFiscalDescripcion { get; set; }
    public DateTime? FechaLimiteEmision { get; set; }
    public string? LeyendaRango { get; set; }

    // Estado lookup numerico (cfg_cai_estado): 1=DISPONIBLE 2=EN_USO 3=VIGENTE 4=VENCIDA 5=ANULADA
    public short EstadoId { get; set; } = 1;
    public string? EstadoCodigo { get; set; }
    public string? EstadoDescripcion { get; set; }
}

public sealed class CaiBloqueReservadoListDto
{
    public long CaiBloqueId { get; set; }
    public long CaiId { get; set; }
    public string? CodigoCai { get; set; }
    public string? PrefijoDocumento { get; set; }
    public string? UsuarioAsignado { get; set; }
    public string? DispositivoId { get; set; }
    public string? RutaCodigo { get; set; }
    public long CorrelativoDesde { get; set; }
    public long CorrelativoHasta { get; set; }
    public long CorrelativoActual { get; set; }
    public long CorrelativosDisponibles { get; set; }
    public DateTime FechaReserva { get; set; }
    public DateTime? FechaExpiracion { get; set; }
    public string? EstadoCodigo { get; set; }
    public int StatusId { get; set; }
}

public sealed class CaiFacturacionSaveRequest
{
    public long? CaiId { get; set; }
    public string CodigoCai { get; set; } = string.Empty;
    public long RangoDesde { get; set; }
    public long RangoHasta { get; set; }
    public DateTime VigenciaDesde { get; set; } = DateTime.Today;
    public DateTime? VigenciaHasta { get; set; }
    public string? Observaciones { get; set; }
    public bool Activo { get; set; } = true;

    // SAR-Compliance (obligatorios desde 2026-05-07)
    // El prefijo SAR EEE-PPP-TD se compone en el server desde:
    //   EEE = EstablecimientoCodigo (3 dígitos, escrito por el usuario, autorizado por SAR)
    //   PPP = PuntoEmision (3 dígitos, escrito por el usuario)
    //   TD  = tipo_documento_fiscal_id (2 dígitos)
    // La fecha límite de emisión = VigenciaHasta (mismo dato según SAR).
    public string EstablecimientoCodigo { get; set; } = "000";
    public string PuntoEmision { get; set; } = "001";
    public short TipoDocumentoFiscalId { get; set; } = 1; // default Factura
    public string? LeyendaRango { get; set; }

    // Correlativo desde el cual continúa la emisión.
    // Caso típico: SAR autoriza rango 800-900 pero ya emitiste 40 manualmente
    // en otro sistema → CorrelativoActual = 840 → siguiente emit será 841.
    // Si null, se interpreta como rango_desde - 1 (inicio limpio).
    public long? CorrelativoActual { get; set; }

    // Estado del CAI (cfg_cai_estado). 1=DISPONIBLE por default al crear.
    public short EstadoId { get; set; } = 1;
}

public sealed class CaiFacturacionFilterDto
{
    public string? Search { get; set; }
    public bool? Activo { get; set; }
    public short? EstadoId { get; set; }
}

// Lookups para combos en la UI de CAI
public sealed class TipoDocumentoFiscalLookupDto
{
    public short TipoDocumentoFiscalId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public bool EsComprobanteFiscal { get; set; }
}

public sealed class CaiEstadoLookupDto
{
    public short CaiEstadoId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public short Orden { get; set; }
}

public sealed class CaiBloqueReservadoSaveRequest
{
    public long CaiId { get; set; }
    public string? UsuarioAsignado { get; set; }
    public string? DispositivoId { get; set; }
    public string? RutaCodigo { get; set; }
    public int CantidadCorrelativos { get; set; }
    public DateTime? FechaExpiracion { get; set; }
}
