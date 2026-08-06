namespace SIAD.Core.Constants;

/// <summary>
/// Vocabulario cerrado de <c>cfg_compra_isv.tratamiento</c>: qué hace la empresa con el ISV
/// que paga en las compras. Espejo del CHECK <c>ck_cfg_compra_isv_tratamiento</c> en la base
/// de datos — si se agrega un valor aquí, hay que ampliar el CHECK.
/// <para>
/// Es la SEGUNDA capa de la configuración del ISV en compras: la primera
/// (<c>alm_tipo_articulo.impuesto_tasa_id</c>) dice cuánto ISV lleva cada compra; esta dice
/// qué se hace con ese ISV. Sin implicaciones contables: solo se guarda la decisión.
/// </para>
/// </summary>
public static class TratamientoIsvCompra
{
    /// <summary>El ISV pagado se suma al costo del artículo.</summary>
    public const string Costo = "COSTO";

    /// <summary>El ISV pagado se registra como impuesto (fiscal), separado del costo.</summary>
    public const string Fiscal = "FISCAL";

    /// <summary>Todos los valores válidos, en el mismo orden que el CHECK de la BD.</summary>
    public static readonly string[] Todos =
    [
        Costo,
        Fiscal
    ];

    public static bool EsValido(string? valor) =>
        !string.IsNullOrWhiteSpace(valor) && Array.IndexOf(Todos, valor) >= 0;
}
