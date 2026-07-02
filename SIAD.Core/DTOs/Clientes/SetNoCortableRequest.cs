namespace SIAD.Core.DTOs.Clientes;

public record SetNoCortableRequest(
    string ClienteClave,
    bool NoCortable,
    string Password,
    string? Motivo);
