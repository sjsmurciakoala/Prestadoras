using System;

namespace SIAD.Core.DTOs.Cobranza;

public record NotaCobroDto(
    int Id,
    string Correlativo,
    DateTime Fecha,
    decimal Monto,
    string? Descripcion,
    string Estado,
    string? Usuario);

public record EmitirNotaCobroRequest(
    string ClienteClave,
    decimal Monto,
    string? Descripcion);

public record AnularNotaCobroRequest(string Motivo);
