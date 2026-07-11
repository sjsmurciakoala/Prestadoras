using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Almacen;

public sealed class ArticuloEditDto
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(20, ErrorMessage = "El código no puede superar los 20 caracteres.")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [StringLength(120, ErrorMessage = "La descripción no puede superar los 120 caracteres.")]
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>Unidad del catálogo alm_unidad_medida (reemplaza el texto libre en el form).</summary>
    public int? UnidadMedidaId { get; set; }

    /// <summary>Unidad de almacenaje (cómo se guarda en bodega). Catálogo alm_unidad_medida.</summary>
    public int? UnidadAlmacenajeId { get; set; }

    /// <summary>Unidad de salidas (cómo se despacha/consume). Catálogo alm_unidad_medida.</summary>
    public int? UnidadSalidaId { get; set; }

    /// <summary>Clasificación por uso (alm_tipo_articulo): operativo/mantenimiento/consumo.</summary>
    public int? TipoArticuloId { get; set; }

    /// <summary>Línea de inventario del catálogo alm_linea (reemplaza el código de texto).</summary>
    public int? LineaId { get; set; }

    /// <summary>Grupo de producto del catálogo alm_grupo (reemplaza el código de texto).</summary>
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
    /// Existencia física. Editable sólo al crear (existencia inicial); en
    /// edición se conserva porque debe ajustarse por movimientos de kardex.
    /// </summary>
    [Range(0, 9_999_999_999_999d, ErrorMessage = "La existencia no puede ser negativa.")]
    public decimal Existencia { get; set; }
}
