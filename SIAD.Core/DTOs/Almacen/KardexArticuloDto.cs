using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SIAD.Core.DTOs.Almacen;

/// <summary>
/// Kardex de un artículo: cabecera con totales del período consultado + los
/// movimientos con saldo corrido.
/// </summary>
public sealed class KardexArticuloDto
{
    public string Codigo { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public string? UnidadMedida { get; init; }

    /// <summary>Existencia almacenada en el catálogo (alm_articulo.existencia). Es el TOTAL del artículo, todas las bodegas.</summary>
    public decimal ExistenciaRegistrada { get; init; }

    /// <summary>Saldo resultante de todo el kardex (Σ ingresos − Σ salidas, sin filtrar por fecha).</summary>
    public decimal SaldoCalculado { get; init; }

    /// <summary>Bodega a la que quedó acotado el kardex (eco de KardexFilterDto.BodegaId); null = todas.</summary>
    public int? BodegaId { get; init; }

    /// <summary>
    /// Existencia registrada de la bodega consultada (alm_articulo_bodega ACTIVA, mismo
    /// contrato de rollup que el maestro). Sólo trae valor cuando <see cref="BodegaId"/>
    /// no es null; queda en null si esa bodega no tiene fila de existencia activa para el
    /// artículo (no hay cifra contra la que comparar).
    /// </summary>
    public decimal? ExistenciaBodega { get; init; }

    /// <summary>
    /// Existencia comparable contra <see cref="SaldoCalculado"/> en el MISMO ámbito del
    /// kardex: la del artículo si no hay bodega filtrada, la de la bodega si la hay.
    /// null = ámbito sin existencia registrada, no se puede afirmar descuadre.
    /// </summary>
    [JsonIgnore]
    public decimal? ExistenciaComparable => BodegaId.HasValue ? ExistenciaBodega : ExistenciaRegistrada;

    /// <summary>
    /// true si el saldo del kardex no cuadra con la existencia de su mismo ámbito. Comparar
    /// contra <see cref="ExistenciaRegistrada"/> con una bodega filtrada daría un falso
    /// positivo en todo artículo multi-bodega (saldo de UNA bodega vs existencia TOTAL).
    /// </summary>
    [JsonIgnore]
    public bool SaldoDescuadrado => ExistenciaComparable.HasValue && SaldoCalculado != ExistenciaComparable.Value;

    /// <summary>Suma de ingresos de los movimientos mostrados (período/tipo filtrado).</summary>
    public decimal TotalIngresos { get; init; }

    /// <summary>Suma de salidas de los movimientos mostrados (período/tipo filtrado).</summary>
    public decimal TotalSalidas { get; init; }

    public IReadOnlyList<KardexMovimientoDto> Movimientos { get; init; } = new List<KardexMovimientoDto>();
}
