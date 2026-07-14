namespace SIAD.Core.Constants;

/// <summary>
/// Vocabulario cerrado de la columna <c>origen</c> de los documentos de almacén
/// (<c>alm_compra</c>, <c>alm_requisicion</c>, <c>alm_descargo</c>): de dónde salió
/// el documento. Espejo de los CHECK <c>ck_alm_&lt;tabla&gt;_origen</c> en la base de
/// datos — si se agrega un valor aquí, hay que ampliar los tres CHECK.
/// <para>
/// BLINDAJE: los documentos <see cref="Simafi"/> son el histórico migrado, cuya
/// existencia ya está reflejada en el inventario. Vienen con <c>posteado = true</c>
/// y el motor de posteo NUNCA debe procesarlos: hacerlo duplicaría el stock.
/// El motor solo levanta lo pendiente: <c>origen = SIAD AND posteado = false</c>.
/// La columna es inmutable en la BD (triggers <c>trg_alm_&lt;tabla&gt;_blindaje</c>,
/// SQLSTATE K0002).
/// </para>
/// <para>
/// La columna NO tiene DEFAULT en la BD, a propósito: toda importación futura de datos
/// SIMAFI debe declarar <c>origen = 'SIMAFI', posteado = true</c> EXPLÍCITAMENTE. Con un
/// DEFAULT 'SIAD', el olvido sería silencioso y el motor terminaría posteando —
/// y duplicando — el histórico. Sin DEFAULT, el olvido falla con violación de NOT NULL.
/// </para>
/// </summary>
public static class OrigenDocumento
{
    /// <summary>Histórico migrado desde MySQL bdsimafi. Nunca posteable por el motor.</summary>
    public const string Simafi = "SIMAFI";

    /// <summary>Documento creado por el sistema nuevo. Es el único que el motor postea.</summary>
    public const string Siad = "SIAD";

    /// <summary>Todos los orígenes válidos, en el mismo orden que el CHECK de la BD.</summary>
    public static readonly string[] Todos =
    [
        Simafi,
        Siad
    ];

    public static bool EsValido(string? origen) =>
        !string.IsNullOrWhiteSpace(origen) && Array.IndexOf(Todos, origen) >= 0;
}
