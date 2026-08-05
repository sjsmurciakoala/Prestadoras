using System;
using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Almacen;

/// <summary>
/// Documento de ajuste de inventario: la vía legítima de mover stock fuera de compras.
/// Una línea por documento.
/// </summary>
public sealed class AjusteInventarioDto
{
    public int? Id { get; init; }

    [Required(ErrorMessage = "El artículo es obligatorio.")]
    public int ArticuloId { get; init; }

    [Required(ErrorMessage = "La bodega es obligatoria.")]
    public int BodegaId { get; init; }

    /// <summary>
    /// <c>ENTRADA</c> suma existencia, <c>SALIDA</c> resta, <c>VALOR</c> solo corrige el
    /// costo. Ver <see cref="SIAD.Core.Constants.ClaseAjusteInventario"/>.
    /// </summary>
    [Required(ErrorMessage = "La clase de ajuste es obligatoria.")]
    public string Clase { get; init; } = string.Empty;

    /// <summary>Siempre positiva; 0 en los ajustes de valor. El signo lo pone la clase.</summary>
    [Range(0, 9_999_999_999_999d, ErrorMessage = "La cantidad no puede ser negativa.")]
    public decimal Cantidad { get; init; }

    /// <summary>
    /// Costo unitario. Obligatorio (&gt; 0) en ENTRADA y VALOR. En SALIDA se IGNORA: el
    /// motor valoriza al costo promedio vigente, porque una salida no cambia el promedio.
    /// </summary>
    [Range(0, 99_999_999d, ErrorMessage = "El costo no puede ser negativo.")]
    public decimal CostoUnitario { get; init; }

    [Required(ErrorMessage = "El motivo es obligatorio.")]
    [StringLength(120, ErrorMessage = "El motivo no puede superar los 120 caracteres.")]
    public string Motivo { get; init; } = string.Empty;

    [StringLength(254, ErrorMessage = "La observación no puede superar los 254 caracteres.")]
    public string? Observacion { get; init; }

    /// <summary>Fecha contable del ajuste. Si no viene, el servicio usa hoy.</summary>
    public DateOnly? Fecha { get; init; }

    // ── Solo lectura (los llena el servidor tras postear) ────────────────────

    public bool Posteado { get; init; }
    public decimal ExistenciaResultante { get; init; }
    public decimal CostoPromedioResultante { get; init; }

    /// <summary>Id del asiento generado en el kardex.</summary>
    public int KardexId { get; init; }
}
