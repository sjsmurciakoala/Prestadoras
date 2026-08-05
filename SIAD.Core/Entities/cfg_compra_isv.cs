using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Política del ISV en compras, una fila por empresa (la PK ES el <c>company_id</c>).
/// <para>
/// Segunda capa de la configuración del ISV: la primera
/// (<c>alm_tipo_articulo.impuesto_tasa_id</c>) dice cuánto ISV lleva cada compra; esta dice
/// qué se hace con él: <c>COSTO</c> (se suma al costo del artículo) o <c>FISCAL</c> (se
/// registra como impuesto, separado del costo). Sin implicaciones contables: solo guarda la
/// decisión; el asiento del crédito fiscal queda fuera de alcance.
/// </para>
/// </summary>
public partial class cfg_compra_isv : ICompanyScopedEntity
{
    /// <summary>PK y tenant a la vez: una sola configuración por empresa.</summary>
    public long company_id { get; set; }

    /// <summary>
    /// COSTO | FISCAL. Ver <see cref="SIAD.Core.Constants.TratamientoIsvCompra"/> y el CHECK
    /// <c>ck_cfg_compra_isv_tratamiento</c>.
    /// </summary>
    public string tratamiento { get; set; } = "COSTO";

    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }
    public string? usuariomodificacion { get; set; }
    public DateTime? fechamodificacion { get; set; }
}
