using System.Collections.Generic;

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

    /// <summary>Existencia almacenada en el catálogo (alm_articulo.existencia).</summary>
    public decimal ExistenciaRegistrada { get; init; }

    /// <summary>Saldo resultante de todo el kardex (Σ ingresos − Σ salidas, sin filtrar por fecha).</summary>
    public decimal SaldoCalculado { get; init; }

    /// <summary>Suma de ingresos de los movimientos mostrados (período/tipo filtrado).</summary>
    public decimal TotalIngresos { get; init; }

    /// <summary>Suma de salidas de los movimientos mostrados (período/tipo filtrado).</summary>
    public decimal TotalSalidas { get; init; }

    public IReadOnlyList<KardexMovimientoDto> Movimientos { get; init; } = new List<KardexMovimientoDto>();
}
