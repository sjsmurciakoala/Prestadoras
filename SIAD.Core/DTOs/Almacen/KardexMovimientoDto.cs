using System;

namespace SIAD.Core.DTOs.Almacen;

public sealed class KardexMovimientoDto
{
    public int Id { get; init; }
    public DateOnly? Fecha { get; init; }
    public decimal? NumeroDocumento { get; init; }
    public string? TipoTransaccion { get; init; }
    public string TipoDescripcion => TipoMovimientoKardex.Describir(TipoTransaccion);
    public string? Descripcion { get; init; }
    public string? Departamento { get; init; }
    public int? BodegaId { get; init; }
    public string? BodegaCodigo { get; init; }
    public string? BodegaNombre { get; init; }
    public decimal Ingresos { get; init; }
    public decimal Salidas { get; init; }
    public decimal ValorUnitario { get; init; }
    public decimal Total { get; init; }

    /// <summary>Usuario que registró el movimiento (alm_kardex.usuariocreacion).</summary>
    public string? UsuarioCreacion { get; init; }

    /// <summary>Fecha y hora de registro del movimiento (hora local, alm_kardex.fechacreacion).</summary>
    public DateTime? FechaCreacion { get; init; }

    /// <summary>
    /// Saldo corrido (Σ ingresos − Σ salidas) desde el asiento de CARGA INICIAL del par.
    /// <para>
    /// Es <c>null</c> en los movimientos PRE-CORTE (ver <see cref="EsPreCorte"/>): antes de
    /// la apertura no hay un saldo con significado, porque el histórico migrado de SIMAFI
    /// no arranca de un punto cero conocido. La UI muestra "—".
    /// </para>
    /// </summary>
    public decimal? Saldo { get; set; }

    // ── Trazabilidad del libro nuevo (motor de posteo) ───────────────────────

    /// <summary>
    /// Qué documento originó el asiento (<c>TipoDocumentoInventario</c>). NULL = histórico
    /// SIMAFI, no posteado por el motor. Es lo que distingue una carga inicial de una
    /// entrada cualquiera: sin esto ambas se rotulan "Entrada" y son indistinguibles.
    /// </summary>
    public string? DocumentoTipo { get; init; }

    /// <summary>Id del documento origen dentro de la tabla que corresponda a <see cref="DocumentoTipo"/>.</summary>
    public int? DocumentoId { get; init; }

    /// <summary>Existencia del par DESPUÉS de este asiento (snapshot que escribe el motor).</summary>
    public decimal? ExistenciaResultante { get; init; }

    /// <summary>Costo promedio del par DESPUÉS de este asiento.</summary>
    public decimal? CostoPromedioResultante { get; init; }

    /// <summary>
    /// true si el movimiento es ANTERIOR al asiento de carga inicial de su par: histórico
    /// informativo, fuera del saldo. Lo calcula <c>KardexService</c> al aplicar el punto
    /// de corte; no viene de la base.
    /// </summary>
    public bool EsPreCorte { get; set; }

    /// <summary>true si este asiento ES la línea de corte (la carga inicial vigente del par).</summary>
    public bool EsLineaDeCorte { get; set; }
}
