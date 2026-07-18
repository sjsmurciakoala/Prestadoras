using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Almacen;

public sealed class ArticuloEditDto
{
    public int? Id { get; set; }

    /// <summary>
    /// Código del sistema anterior (SIMAFI). OPCIONAL: los artículos nuevos no lo usan
    /// (queda en blanco); el identificador es el Id. Solo referencia en los migrados.
    /// </summary>
    [StringLength(20, ErrorMessage = "El código no puede superar los 20 caracteres.")]
    public string? Codigo { get; set; }

    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [StringLength(120, ErrorMessage = "La descripción no puede superar los 120 caracteres.")]
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>Unidad del catálogo alm_unidad_medida (reemplaza el texto libre en el form).</summary>
    public int? UnidadMedidaId { get; set; }

    /// <summary>Unidad de almacenaje (cómo se guarda en bodega). Catálogo alm_unidad_medida.</summary>
    public int? UnidadAlmacenajeId { get; set; }

    /// <summary>Unidad de salidas (cómo se despacha/consume). Catálogo alm_unidad_medida.</summary>
    public int? UnidadSalidaId { get; set; }

    /// <summary>
    /// Tipo de artículo (alm_tipo_articulo): la clasificación única desde la
    /// unificación línea→tipo del 2026-07-16. Obligatorio.
    /// </summary>
    [Required(ErrorMessage = "El tipo de artículo es obligatorio.")]
    public int? TipoArticuloId { get; set; }

    /// <summary>Categoría (alm_grupo) del artículo; debe pertenecer al tipo elegido.</summary>
    public int? GrupoId { get; set; }

    [StringLength(80, ErrorMessage = "El diámetro no puede superar los 80 caracteres.")]
    public string? Diametro { get; set; }

    [StringLength(20, ErrorMessage = "La cuenta contable no puede superar los 20 caracteres.")]
    public string? CuentaContable { get; set; }

    [Range(0, 9_999_999_999_999d, ErrorMessage = "La existencia mínima no puede ser negativa.")]
    public decimal ExistenciaMinima { get; set; }

    [Range(0, 99_999_999d, ErrorMessage = "El valor unitario no puede ser negativo.")]
    public decimal ValorUnitario { get; set; }

    /// <summary>
    /// Existencia física. En edición es el rollup (suma de bodegas) y se conserva.
    /// Al crear ya no se captura aquí: la existencia entra por bodega en <see cref="Ubicaciones"/>.
    /// </summary>
    [Range(0, 9_999_999_999_999d, ErrorMessage = "La existencia no puede ser negativa.")]
    public decimal Existencia { get; set; }

    /// <summary>
    /// Ubicaciones (bodegas) del artículo. SOLO se usa al CREAR: el artículo debe
    /// nacer con al menos una bodega, y su existencia/mínimo salen de la suma de estas
    /// filas. En edición las ubicaciones se administran por endpoints propios y esta
    /// lista se ignora.
    /// </summary>
    public List<ArticuloUbicacionDto> Ubicaciones { get; set; } = new();

    /// <summary>
    /// Proveedores que suministran el artículo ("UPC"). SOLO se usa al CREAR: se
    /// insertan junto con el artículo. En edición se administran por endpoints propios
    /// y esta lista se ignora.
    /// </summary>
    public List<ArticuloProveedorDto> Proveedores { get; set; } = new();
}
