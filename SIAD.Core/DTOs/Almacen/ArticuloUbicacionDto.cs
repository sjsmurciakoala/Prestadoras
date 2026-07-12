using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Almacen;

/// <summary>
/// Ubicación de un artículo en una bodega (fila del grid de ubicación).
/// La existencia por bodega no se administra en este paso (Fase 2).
/// </summary>
public sealed class ArticuloUbicacionDto
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "La bodega es obligatoria.")]
    [Range(1, int.MaxValue, ErrorMessage = "Seleccione una bodega.")]
    public int BodegaId { get; set; }

    /// <summary>Solo lectura: "código — nombre" de la bodega.</summary>
    public string? BodegaDisplay { get; set; }

    /// <summary>Estantería del estante seleccionado (ayuda de UI para la cascada; se deriva al leer).</summary>
    public int? EstanteriaId { get; set; }

    public int? EstanteId { get; set; }

    /// <summary>Solo lectura: código compuesto bodega-estantería-estante (null si no hay estante).</summary>
    public string? EstanteUbicacion { get; set; }

    [Range(0, 9_999_999_999_999d, ErrorMessage = "La existencia no puede ser negativa.")]
    public decimal Existencia { get; set; }

    [Range(0, 9_999_999_999_999d, ErrorMessage = "La existencia mínima no puede ser negativa.")]
    public decimal ExistenciaMinima { get; set; }

    public bool Principal { get; set; }
}
