using System.Diagnostics.CodeAnalysis;

namespace SIAD.Core.DTOs.Ordenes;



public sealed record OrdenTrabajoTipoDto(
    string Tipo,
    string Descripcion,
    string? DepartamentoAplicacion);
