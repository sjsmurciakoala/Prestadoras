using System;

namespace SIAD.Core.DTOs.Medidores;

public sealed class MedidorListItemDto
{
    public int Id { get; set; }
    public string Numero { get; set; } = string.Empty;
    public string? Marca { get; set; }
    public DateTime? FechaInstalacion { get; set; }
    public decimal? Diametro { get; set; }
    public string? Empleado { get; set; }
    public string? Acueducto { get; set; }
    public bool Activo { get; set; }
    public string? ClienteClave { get; set; }
    public string? ClienteNombre { get; set; }
}
