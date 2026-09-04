using System;

namespace SIAD.Core.DTOs.Almacen;

/// <summary>
/// Resultado de postear un movimiento de inventario. Devuelve el estado del par
/// (artículo, bodega) DESPUÉS del asiento, que es el mismo par de cifras que queda
/// congelado en <c>alm_kardex.existencia_resultante</c> / <c>costo_promedio_resultante</c>.
/// </summary>
public sealed class PosteoResultDto
{
    /// <summary>Id del asiento en <c>alm_kardex</c>.</summary>
    public int KardexId { get; init; }

    /// <summary>uuid determinista del asiento (clave de idempotencia).</summary>
    public Guid Uuid { get; init; }

    /// <summary>
    /// true si el movimiento YA estaba posteado y esta llamada no escribió nada: se
    /// devolvió el asiento existente. Es el camino normal de un reintento, no un error.
    /// </summary>
    public bool YaExistia { get; init; }

    /// <summary>Existencia del par después del asiento.</summary>
    public decimal ExistenciaResultante { get; init; }

    /// <summary>Costo promedio del par después del asiento.</summary>
    public decimal CostoPromedioResultante { get; init; }

    /// <summary>
    /// true si este movimiento hizo <b>cruzar</b> el par de "en orden" a un estado de alerta
    /// (bajo mínimo, sin stock o negativa). Solo el cruce, no cada movimiento estando ya bajo:
    /// es la base del aviso por evento sin spam.
    /// </summary>
    public bool CruzoAlerta { get; init; }

    /// <summary>Severidad resultante (<see cref="StockSeveridad"/>) si <see cref="CruzoAlerta"/>; si no, null.</summary>
    public string? SeveridadAlerta { get; init; }
}
