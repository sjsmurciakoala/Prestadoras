namespace SIAD.Core.DTOs.Tarifario;

public sealed class TarifarioConflictoListDto
{
    public long ConflictoId { get; set; }
    public DateTime FechaRegistro { get; set; }
    public string? EstadoCodigo { get; set; }
    public string? CodigoConflicto { get; set; }
    public long? ClienteId { get; set; }
    public string? ClienteClave { get; set; }
    public string? ClienteNombre { get; set; }
    public string? LecturaUuid { get; set; }
    public long? CaiId { get; set; }
    public long? CaiBloqueId { get; set; }
    public string? CodigoCai { get; set; }
    public string? PrefijoDocumento { get; set; }
    public string? RutaCodigo { get; set; }
    public string? UsuarioAsignado { get; set; }
    public string? DispositivoId { get; set; }
    public long? Correlativo { get; set; }
    public string? NumeroFactura { get; set; }
    public string? DetalleConflicto { get; set; }
    public long? FacturaId { get; set; }
}

public sealed record TarifarioConflictoResolveRequest(
    long ConflictoId,
    string? Observaciones);
