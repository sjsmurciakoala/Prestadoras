using System;

namespace SIAD.Core.DTOs.Ordenes;

public sealed record OrdenTrabajoAdjuntoDto(
    int Id,
    string? Nombre,
    string? Tipo,
    string? Latitud,
    string? Longitud,
    DateTime? FechaInicio,
    DateTime? FechaFin,
    DateTime? FechaSincronizacion);
