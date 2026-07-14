using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Almacen;

public sealed class UnidadMedidaEditDto
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(10, ErrorMessage = "El código no puede superar los 10 caracteres.")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(60, ErrorMessage = "El nombre no puede superar los 60 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(10, ErrorMessage = "La abreviatura no puede superar los 10 caracteres.")]
    public string? Abreviatura { get; set; }

    /// <summary>Categoría (tipo) de la unidad: FK al catálogo alm_categoria_unidad.</summary>
    public int? CategoriaId { get; set; }

    public bool PermiteDecimales { get; set; } = true;
    public bool Activo { get; set; } = true;

    /// <summary>Unidad base de la categoría. Null si esta unidad ES la base.</summary>
    public int? UnidadBaseId { get; set; }

    [Range(0.000001, 999_999_999d, ErrorMessage = "El factor de conversión debe ser mayor que cero.")]
    public decimal FactorConversion { get; set; } = 1;
}
