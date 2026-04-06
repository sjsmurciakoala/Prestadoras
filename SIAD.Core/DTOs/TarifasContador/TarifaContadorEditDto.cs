using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.TarifasContador;

public sealed class TarifaContadorEditDto
{
    public int? Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "El tipo es obligatorio.")]
    public int Tipo { get; set; }

    public int? CategoriaId { get; set; }

    public string? Codigo { get; set; }

    public string? Descripcion { get; set; }

    public decimal? Minimo { get; set; }

    public decimal? Maximo { get; set; }

    public decimal? Cuota { get; set; }

    public decimal? ValorBase { get; set; }

    public decimal? Alquiler { get; set; }
}
