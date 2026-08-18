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

    // ── Valorización ─────────────────────────────────────────────────────────
    // El costo promedio se DERIVA del libro (valor acumulado / cantidad acumulada), la
    // misma regla del kardex legacy, en vez de leerse de un campo. Así existe también en
    // los asientos que el motor no posteó.

    /// <summary>Valor monetario de las entradas mostradas (período/tipo filtrado).</summary>
    public decimal ValorIngresos { get; init; }

    /// <summary>Valor monetario de las salidas mostradas (período/tipo filtrado).</summary>
    public decimal ValorSalidas { get; init; }

    /// <summary>Valor del inventario al cierre del kardex: Σ (ingresos − salidas) × valor_unitario desde el corte.</summary>
    public decimal SaldoValorizado { get; init; }

    /// <summary>
    /// Costo promedio al cierre, derivado del libro. null cuando el saldo no es positivo
    /// (no hay entre qué dividir).
    /// </summary>
    public decimal? CostoPromedioActual { get; init; }

    /// <summary>
    /// Costo promedio almacenado en <c>alm_articulo_bodega.costo_promedio</c> — el que usan el
    /// catálogo y el motor para valorizar salidas. Es el valor CONTRA el que se contrasta el
    /// corrido. Con bodega filtrada es el de esa bodega; sin filtro, el ponderado de las bodegas
    /// activas. null = sin fila de existencia activa, no hay con qué comparar.
    /// </summary>
    public decimal? CostoPromedioCache { get; init; }

    /// <summary>
    /// Tolerancia del contraste. El corrido acumula el valor sin redondear, mientras el motor
    /// reconstruye el numerador desde un promedio ya redondeado a 4 decimales: una diferencia de
    /// centavos es esperable y no es un descuadre.
    /// </summary>
    public const decimal ToleranciaCosto = 0.01m;

    /// <summary>
    /// El libro tiene costo pero la ficha de existencias está en cero: el par nunca recibió
    /// costo de apertura. No es un descuadre —nada se corrompió— sino un pendiente de costeo,
    /// y son dos cosas que piden acciones distintas. Se separa porque mientras el corte de
    /// inventario no se ejecute hay cientos de pares así, y llamarlos a todos "descuadre"
    /// convertiría la señal en ruido.
    /// </summary>
    [JsonIgnore]
    public bool CostoSinRegistrar => CostoPromedioCache == 0m
        && CostoPromedioActual.HasValue
        && CostoPromedioActual.Value != 0m;

    /// <summary>
    /// true si el costo promedio derivado del libro se aparta del almacenado más allá de
    /// <see cref="ToleranciaCosto"/>. Mismo criterio que <see cref="SaldoDescuadrado"/>: sin
    /// cifra con la que comparar no se afirma descuadre. El caso "ficha en cero" queda fuera
    /// —lo reporta <see cref="CostoSinRegistrar"/>.
    /// </summary>
    [JsonIgnore]
    public bool CostoDescuadrado => CostoPromedioActual.HasValue
        && CostoPromedioCache.HasValue
        && CostoPromedioCache.Value != 0m
        && Math.Abs(CostoPromedioActual.Value - CostoPromedioCache.Value) > ToleranciaCosto;

    // ── Arrastre del período (saldo inicial) ─────────────────────────────────
    // Al filtrar por fecha, el saldo de la primera fila ya viene arrastrado pero nada lo
    // explica en pantalla. Estos tres son ese arranque: cantidad, valor y costo promedio
    // acumulados ANTES de la fecha desde.

    /// <summary>Cantidad acumulada antes del inicio del período. null = sin filtro de fecha desde.</summary>
    public decimal? CantidadAnterior { get; init; }

    /// <summary>Valor monetario acumulado antes del inicio del período.</summary>
    public decimal? ValorAnterior { get; init; }

    /// <summary>Costo promedio con el que arranca el período (valor anterior / cantidad anterior).</summary>
    public decimal? CostoPromedioAnterior { get; init; }

    /// <summary>true si hay un arrastre que mostrar (se consultó con fecha desde y había historia previa).</summary>
    [JsonIgnore]
    public bool TieneArrastre => CantidadAnterior.HasValue;

    public IReadOnlyList<KardexMovimientoDto> Movimientos { get; init; } = new List<KardexMovimientoDto>();
}
