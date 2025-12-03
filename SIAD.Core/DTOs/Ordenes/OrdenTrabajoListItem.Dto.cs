using System;

namespace SIAD.Core.DTOs.Ordenes;

public sealed record OrdenTrabajoListItemDto(
    int Id,
    int Numero,
    string ClienteClave,
    string Propietario,
    string? Direccion,
    DateTime Fecha,
    DateTime FechaCreacion,
    string Concepto,
    string? Empleado,
    string? Tipo,
    string TipoDescripcion,
    string Estado,
    string EstadoDescripcion,
    string? UsuarioAsignado);
