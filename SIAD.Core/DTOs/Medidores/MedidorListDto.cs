using System;

namespace SIAD.Core.DTOs.Medidores;

public record MedidorListDto(
    int Id,
    string Numero,
    string? Marca,
    decimal? Diametro,
    DateTime? FechaInstalacion,
    string? Acueducto,
    bool Estado,
    string? ClienteClave,
    string? ClienteNombre,
    string? ClaseMedidorCodigo);
