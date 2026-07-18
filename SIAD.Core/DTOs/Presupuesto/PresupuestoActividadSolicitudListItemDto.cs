namespace SIAD.Core.DTOs.Presupuesto;

public sealed class PresupuestoActividadSolicitudListItemDto
{
    public long SolicitudId { get; set; }
    public string IdPresupuesto { get; set; } = string.Empty;
    public string TipoActividad { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string? CuentaOrigenCode { get; set; }
    public string CuentaDestinoCode { get; set; } = string.Empty;
    public string? CuentaOrigenDisplay { get; set; }
    public string CuentaDestinoDisplay { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public short Prioridad { get; set; }
    public string Justificacion { get; set; } = string.Empty;
    public DateOnly? FechaNecesaria { get; set; }
    public string SolicitadoPor { get; set; } = string.Empty;
    public DateTime SolicitadoEn { get; set; }
    public string? RevisadoPor { get; set; }
    public DateTime? RevisadoEn { get; set; }
    public string? ComentarioRevision { get; set; }
    public long? ActividadId { get; set; }

    public bool EsPendiente =>
        string.Equals(Estado, "PENDIENTE", StringComparison.OrdinalIgnoreCase);
}
