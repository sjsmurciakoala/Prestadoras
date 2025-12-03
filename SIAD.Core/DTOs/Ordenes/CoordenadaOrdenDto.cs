using System;

namespace SIAD.Core.DTOs.Ordenes;

public sealed record CoordenadaOrdenDto(
    string Nombre,
    string Usuario,
    int Tipo,
    DateTime? Fecha,
    decimal? Latitud,
    decimal? Longitud,
    string DescripcionTipo);
