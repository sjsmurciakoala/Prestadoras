using System;
using System.Collections.Generic;

namespace SIAD.Core.DTOs.Ordenes;

public sealed record OrdenTrabajoDetailDto(
    int Id,
    int Numero,
    string ClienteClave,
    string Propietario,
    string? Direccion,
    DateTime Fecha,
    DateTime FechaCreacion,
    string Concepto,
    string Estado,
    string EstadoDescripcion,
    string? Empleado,
    string? UsuarioAsignado,
    string? Personas,
    string? Tipo,
    string? TipoDescripcion,
    decimal? Saldo,
    string? Informe,
    IReadOnlyList<OrdenTrabajoAdjuntoDto> Adjuntos,
    IReadOnlyList<OrdenTrabajoMaterialDto> Materiales);
