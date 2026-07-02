namespace SIAD.Core.DTOs.Clientes;

public record ClienteEstadoLogItemDto(
    long Id,
    string Tipo,
    bool? ValorAnterior,
    bool ValorNuevo,
    string? Motivo,
    string Usuario,
    DateTime Fecha);
