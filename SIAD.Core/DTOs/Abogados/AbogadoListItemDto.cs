using System;

namespace SIAD.Core.DTOs.Abogados;

public sealed class AbogadoListItemDto
{
    public int Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string NombreCorto { get; init; } = string.Empty;
    public string? NombreLargo { get; init; }
    public string? Telefono { get; init; }
    public bool Activo { get; init; }
    public string? CodCuenta { get; init; }
    public DateTime? FechaCreacion { get; init; }
    public string? UsuarioCreacion { get; init; }
    public DateTime? FechaModificacion { get; init; }
    public string? UsuarioModificacion { get; init; }
}
