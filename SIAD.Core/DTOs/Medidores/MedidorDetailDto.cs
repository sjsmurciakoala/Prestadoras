using System.Collections.Generic;
using System;

namespace SIAD.Core.DTOs.Medidores;

public record MedidorDetailDto(
    int Id,
    string Numero,
    string? Marca,
    decimal? Diametro,
    DateTime? FechaInstalacion,
    string? Acueducto,
    bool Estado,
    string? ClienteClave,
    string? ClienteNombre,
    string? ClienteBarrio,
    IReadOnlyList<MedidorHistorialDto> Historial,
    IReadOnlyList<string> ConfiguracionesApp);
