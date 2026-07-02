namespace SIAD.Core.DTOs.Cobranza;

public record LlamadaCobranzaDto(
    int Id,
    DateTime Fecha,
    string? NumeroLlamado,
    string Resultado,
    string? Observacion,
    string? Usuario);

public record RegistrarLlamadaRequest(
    string ClienteClave,
    string? NumeroLlamado,
    string Resultado,
    string? Observacion);
