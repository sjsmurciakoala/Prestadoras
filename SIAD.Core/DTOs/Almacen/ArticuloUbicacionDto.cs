using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace SIAD.Core.DTOs.Almacen;

/// <summary>
/// Ubicación de un artículo en una bodega (fila del grid de ubicación).
/// La ubicación física es manual: cinco campos de texto libre de 20 caracteres.
/// <para>
/// La existencia YA NO se teclea: al dar de alta el par (artículo, bodega) la cantidad
/// capturada se postea como <c>CARGA_INICIAL</c> en el kardex junto con
/// <see cref="CostoApertura"/>; a partir de ahí solo la mueven documentos de inventario
/// (ajuste, compra, traslado). Ver <c>ICargaInicialInventarioService</c>.
/// </para>
/// </summary>
public sealed class ArticuloUbicacionDto
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "La bodega es obligatoria.")]
    [Range(1, int.MaxValue, ErrorMessage = "Seleccione una bodega.")]
    public int BodegaId { get; set; }

    /// <summary>Solo lectura: "código — nombre" de la bodega.</summary>
    public string? BodegaDisplay { get; set; }

    [StringLength(20, ErrorMessage = "La ubicación no puede superar los 20 caracteres.")]
    public string? Ubicacion1 { get; set; }

    [StringLength(20, ErrorMessage = "La ubicación no puede superar los 20 caracteres.")]
    public string? Ubicacion2 { get; set; }

    [StringLength(20, ErrorMessage = "La ubicación no puede superar los 20 caracteres.")]
    public string? Ubicacion3 { get; set; }

    [StringLength(20, ErrorMessage = "La ubicación no puede superar los 20 caracteres.")]
    public string? Ubicacion4 { get; set; }

    [StringLength(20, ErrorMessage = "La ubicación no puede superar los 20 caracteres.")]
    public string? Ubicacion5 { get; set; }

    /// <summary>Solo lectura: las ubicaciones no vacías unidas para mostrar en la grilla.</summary>
    public string UbicacionDisplay =>
        string.Join(" · ", new[] { Ubicacion1, Ubicacion2, Ubicacion3, Ubicacion4, Ubicacion5 }
            .Where(s => !string.IsNullOrWhiteSpace(s)));

    /// <summary>
    /// Al ALTA del par (artículo, bodega): cantidad de apertura que se postea como carga
    /// inicial en el kardex. En cualquier otro caso es SOLO LECTURA — el servicio la ignora
    /// y devuelve la existencia real de la fila. Para moverla hay que registrar un
    /// documento de inventario (ajuste, compra o traslado).
    /// </summary>
    [Range(0, 9_999_999_999_999d, ErrorMessage = "La existencia no puede ser negativa.")]
    public decimal Existencia { get; set; }

    /// <summary>
    /// Costo unitario con el que se siembra la apertura. Obligatorio y mayor que cero
    /// cuando <see cref="Existencia"/> es &gt; 0 al crear el par; se ignora en cualquier
    /// otro caso. No se persiste en ninguna columna: entra al asiento de kardex, que es el
    /// registro (de ahí salen <see cref="CostoPromedio"/> y <see cref="UltimoCosto"/>).
    /// <para>
    /// Sin él no hay apertura posible: un movimiento a costo 0 corrompe el promedio
    /// ponderado de la primera compra que entre después, y el kardex es inmutable.
    /// </para>
    /// </summary>
    [Range(0, 99_999_999d, ErrorMessage = "El costo de apertura no puede ser negativo.")]
    public decimal CostoApertura { get; set; }

    [Range(0, 9_999_999_999_999d, ErrorMessage = "La existencia mínima no puede ser negativa.")]
    public decimal ExistenciaMinima { get; set; }

    [Range(0, 9_999_999_999_999d, ErrorMessage = "La existencia máxima no puede ser negativa.")]
    public decimal ExistenciaMaxima { get; set; }

    /// <summary>
    /// Nivel al que conviene reponer (disparador de compra). EDITABLE: es configuración
    /// que teclea el usuario, igual que el mínimo/máximo.
    /// </summary>
    [Range(0, 9_999_999_999_999d, ErrorMessage = "El punto de reorden no puede ser negativo.")]
    public decimal PuntoReorden { get; set; }

    // ── Campos del motor de movimientos: SOLO LECTURA ─────────────────────────────
    // Se exponen para mostrarlos en la grilla, pero el servicio NUNCA los persiste
    // desde este DTO: los mantiene el motor de posteo (kardex). Si se escribieran
    // desde aquí, un usuario podría teclear un costo promedio o un comprometido
    // inventado y desalinear el inventario. Desde la Fase 6, EXISTENCIA entra en este
    // grupo salvo en el alta del par (ver el comentario de la propiedad).

    /// <summary>Solo lectura: stock reservado por requisiciones aprobadas sin despachar.</summary>
    public decimal ExistenciaComprometida { get; set; }

    /// <summary>Solo lectura: cantidad en camino a la bodega (orden de compra o traslado).</summary>
    public decimal ExistenciaTransito { get; set; }

    /// <summary>Solo lectura: costo promedio ponderado en la bodega; lo recalcula el motor en cada ingreso.</summary>
    public decimal CostoPromedio { get; set; }

    /// <summary>Solo lectura: precio unitario de la última compra registrada en la bodega.</summary>
    public decimal UltimoCosto { get; set; }

    public bool Principal { get; set; }

    /// <summary>
    /// Soft-delete: false = ubicación deshabilitada (histórico). Las ubicaciones no
    /// se eliminan físicamente; se deshabilitan para conservar el registro.
    /// </summary>
    public bool Activo { get; set; } = true;
}
