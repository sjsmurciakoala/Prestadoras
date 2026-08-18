using System;
using System.Text.Json.Serialization;
using SIAD.Core.Constants;

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

    // ── Valorización corrida (promedio ponderado móvil, regla del kardex legacy) ──
    // Estos cuatro los calcula KardexService recorriendo el libro; NO vienen de la base.
    // A diferencia de CostoPromedioResultante (snapshot del motor, NULL en el histórico
    // SIMAFI), se derivan de cantidad y costo, así que existen en toda fila post-corte.
    // Son null en los PRE-CORTE, por la misma razón que Saldo.

    /// <summary>Existencia del par ANTES de este asiento (saldo corrido de la fila previa).</summary>
    public decimal? ExistenciaAnterior { get; set; }

    /// <summary>
    /// Valor monetario acumulado del par DESPUÉS de este asiento: Σ (ingresos − salidas) × valor_unitario
    /// desde el corte. Es el numerador del costo promedio.
    /// </summary>
    public decimal? SaldoValorizado { get; set; }

    /// <summary>
    /// Costo promedio vigente JUSTO ANTES de este asiento. Con el resultante permite leer cada
    /// línea como una frase: "venía a 56,3889 → entraron 6 a 58,00 → queda a 56,7917".
    /// </summary>
    public decimal? CostoPromedioAnterior { get; set; }

    /// <summary>
    /// Costo promedio DESPUÉS de este asiento, recalculado como valor acumulado / cantidad
    /// acumulada. Es <c>null</c> cuando la cantidad acumulada no es positiva: el legacy dividía
    /// sin guarda y reventaba con saldo cero.
    /// </summary>
    public decimal? CostoPromedioCorrido { get; set; }

    /// <summary>
    /// Valor monetario de ESTE movimiento, con signo: (ingresos − salidas) × valor_unitario.
    /// Derivada de campos que ya viajan, así que no se serializa: el cliente la recalcula.
    /// </summary>
    [JsonIgnore]
    public decimal ValorMovimiento => (Ingresos - Salidas) * ValorUnitario;

    /// <summary>
    /// Cantidad del movimiento con signo: entradas en positivo, salidas en negativo. Es la
    /// columna combinada del estado de cuenta, que reemplaza las dos columnas Entradas/Salidas.
    /// </summary>
    [JsonIgnore]
    public decimal CantidadFirmada => Ingresos - Salidas;

    /// <summary>
    /// Concepto legible del movimiento: la carga inicial, el tipo de documento que lo originó
    /// (compra, descargo, traslado…) o —para el histórico migrado sin documento— el tipo legacy
    /// (entrada/salida/ajuste). Es lo que absorbe las columnas Tipo y Documento cuando el estado
    /// de cuenta las funde en la descripción.
    /// </summary>
    [JsonIgnore]
    public string Concepto => EsLineaDeCorte
        ? "Carga inicial"
        : DocumentoTipo switch
        {
            TipoDocumentoInventario.Compra => "Compra",
            TipoDocumentoInventario.Descargo => "Descargo",
            TipoDocumentoInventario.Traslado => "Traslado",
            TipoDocumentoInventario.Ajuste => "Ajuste",
            TipoDocumentoInventario.CargaInicial => "Carga inicial",
            TipoDocumentoInventario.Reversa => "Reversa",
            TipoDocumentoInventario.Requisicion => "Requisición",
            _ => TipoDescripcion
        };

    /// <summary>
    /// Descripción con el concepto concatenado al frente ("Compra · a proveedor X"). Para las
    /// vistas que quitan las columnas Tipo/Documento y dejan la identidad del movimiento en una
    /// sola columna. Si no hay texto libre, es sólo el concepto; si el concepto es "—", el texto.
    /// </summary>
    [JsonIgnore]
    public string DescripcionCompleta =>
        string.IsNullOrWhiteSpace(Descripcion) ? Concepto
        : Concepto is "—" ? Descripcion!
        // Si el texto libre ya arranca con el concepto ("Carga inicial de existencias"), no se
        // repite el prefijo; sólo se antepone cuando aporta información nueva.
        : Descripcion!.StartsWith(Concepto, StringComparison.OrdinalIgnoreCase) ? Descripcion!
        : $"{Concepto} · {Descripcion}";

    /// <summary>
    /// true si el movimiento es ANTERIOR al asiento de carga inicial de su par: histórico
    /// informativo, fuera del saldo. Lo calcula <c>KardexService</c> al aplicar el punto
    /// de corte; no viene de la base.
    /// </summary>
    public bool EsPreCorte { get; set; }

    /// <summary>true si este asiento ES la línea de corte (la carga inicial vigente del par).</summary>
    public bool EsLineaDeCorte { get; set; }
}
