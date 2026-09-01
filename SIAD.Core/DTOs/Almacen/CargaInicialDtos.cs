using System;
using System.Collections.Generic;

namespace SIAD.Core.DTOs.Almacen;

/// <summary>Por qué un par (artículo, bodega) no se puede postear tal cual en el corte.</summary>
public static class ClaseePendienteCarga
{
    /// <summary>Listo: tiene existencia y costo. Se postea sin intervención.</summary>
    public const string Posteable = "POSTEABLE";

    /// <summary>Tiene existencia pero el artículo vale 0: hay que capturar el costo a mano.</summary>
    public const string SinCosto = "SIN_COSTO";

    /// <summary>Existencia negativa: sanear con un ajuste antes de abrir.</summary>
    public const string Negativa = "NEGATIVA";

    /// <summary>El artículo está descontinuado (soft-delete).</summary>
    public const string ArticuloDescontinuado = "ARTICULO_DESCONTINUADO";

    /// <summary>La ubicación está deshabilitada pero conserva existencia.</summary>
    public const string BodegaInactiva = "BODEGA_INACTIVA";
}

/// <summary>Una fila del universo del corte: un par (artículo, bodega) sin apertura vigente.</summary>
public sealed class CargaInicialPendienteDto
{
    public int ArticuloBodegaId { get; init; }
    public int ArticuloId { get; init; }
    public int BodegaId { get; init; }
    public string ArticuloCodigo { get; init; } = string.Empty;
    public string ArticuloDescripcion { get; init; } = string.Empty;
    public string BodegaCodigo { get; init; } = string.Empty;
    public string BodegaNombre { get; init; } = string.Empty;

    public decimal Existencia { get; init; }

    /// <summary>Costo con el que se abriría: <c>alm_articulo.valor_unitario</c> (sin ISV).</summary>
    public decimal CostoApertura { get; init; }

    /// <summary>Ver <see cref="ClaseePendienteCarga"/>.</summary>
    public string Clase { get; init; } = string.Empty;

    /// <summary>Valor que sembraría el asiento (existencia × costo).</summary>
    public decimal ValorApertura => Existencia * CostoApertura;
}

/// <summary>Resumen del <i>dry-run</i>: qué pasaría si se ejecutara el corte. No escribe nada.</summary>
public sealed class CargaInicialSimulacionDto
{
    public int TotalPares { get; init; }
    public int Posteables { get; init; }
    public int SinCosto { get; init; }
    public int Negativas { get; init; }
    public int ArticulosDescontinuados { get; init; }
    public int BodegasInactivas { get; init; }

    /// <summary>Suma de existencia × costo de los pares POSTEABLES.</summary>
    public decimal ValorTotalASembrar { get; init; }

    /// <summary>Cuántos artículos distintos tocaría el rollup de cabecera.</summary>
    public int ArticulosAfectados { get; init; }

    /// <summary>
    /// true si el corte puede ejecutarse completo: no quedan pares sin costo ni negativos.
    /// Es el gate del cierre.
    /// </summary>
    public bool PuedeCerrarse => SinCosto == 0 && Negativas == 0;
}

/// <summary>Resultado de ejecutar el lote (o un tramo).</summary>
public sealed class CargaInicialResultadoDto
{
    public int Posteadas { get; init; }
    public int Omitidas { get; init; }
    public List<CargaInicialOmisionDto> Detalle { get; init; } = new();
}

public sealed class CargaInicialOmisionDto
{
    public int ArticuloBodegaId { get; init; }
    public string ArticuloCodigo { get; init; } = string.Empty;
    public string Motivo { get; init; } = string.Empty;
}

/// <summary>Costo capturado a mano para un par sin <c>valor_unitario</c>.</summary>
public sealed class CargaInicialCostoManualDto
{
    public int ArticuloId { get; init; }
    public int BodegaId { get; init; }
    public decimal Costo { get; init; }
}

/// <summary>Política del corte por empresa (<c>alm_config_inventario</c>).</summary>
public sealed class CargaInicialConfigDto
{
    public string BaseCostoApertura { get; init; } = "VALOR_UNITARIO";
    public bool CostoAperturaIncluyeIsv { get; init; }
    public DateOnly? FechaCorteApertura { get; init; }
    public bool AperturaCerrada { get; init; }
}
