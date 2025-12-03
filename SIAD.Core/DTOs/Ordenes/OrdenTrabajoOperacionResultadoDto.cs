namespace SIAD.Core.DTOs.Ordenes;

public sealed record OrdenTrabajoOperacionResultadoDto(
    bool Exitoso,
    string Mensaje,
    int? NumeroGenerado = null);
