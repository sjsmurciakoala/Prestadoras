using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.TarifasBase;

public sealed class TarifaBaseEditDto
{
    [Range(1, int.MaxValue, ErrorMessage = "El tipo es obligatorio.")]
    public int Tipo { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "La categoria es obligatoria.")]
    public int CategoriaId { get; set; }

    [Required(ErrorMessage = "El codigo es obligatorio.")]
    public string Codigo { get; set; } = string.Empty;

    public string? Descripcion { get; set; }

    public decimal? Valor { get; set; }
}
