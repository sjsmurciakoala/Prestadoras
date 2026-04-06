namespace SIAD.Core.DTOs.Contabilidad;

/// <summary>DTO para actualizar póliza (servicio)</summary>
public sealed record PolizaActualizarDto(
    DateTime PolizaDate,
    string? Description
);
