using System;

namespace SIAD.Core.DTOs.Ordenes;

public sealed record OrdenTrabajoEstadoDto(
    string Codigo,
    string Nombre,
    bool PermiteAsignacion);
