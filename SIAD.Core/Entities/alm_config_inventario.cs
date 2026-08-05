using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Política de la carga inicial de inventario: una fila por empresa (la PK ES el
/// <c>company_id</c>).
/// <para>
/// NO guarda la política del ISV (al costo vs. crédito fiscal): esa es una decisión
/// fiscal/contable que vive en la configuración de empresa. Meterla aquí obligaría a
/// compras y contabilidad a depender del almacén para leer algo que no es de almacén.
/// </para>
/// </summary>
public partial class alm_config_inventario : ICompanyScopedEntity
{
    /// <summary>PK y tenant a la vez: una sola configuración por empresa.</summary>
    public long company_id { get; set; }

    /// <summary>
    /// De dónde sale el costo de apertura: <c>VALOR_UNITARIO</c> (desde
    /// <c>alm_articulo.valor_unitario</c>) o <c>MANUAL</c> (capturado en el corte).
    /// </summary>
    public string base_costo_apertura { get; set; } = "VALOR_UNITARIO";

    /// <summary>
    /// En qué base está el costo con el que se sembró la apertura. Resuelto por el
    /// contador el 2026-07-30: <c>valor_unitario</c> NO lleva ISV, por eso nace en false.
    /// Si la apertura y las compras futuras usaran bases distintas, el promedio ponderado
    /// mezclaría dos bases en silencio.
    /// </summary>
    public bool costo_apertura_incluye_isv { get; set; }

    /// <summary>
    /// Fecha contable del LOTE retroactivo; obligatoria al ejecutarlo. NULL = lote no
    /// ejecutado. No aplica a las aperturas unitarias, que se fechan el día del alta.
    /// </summary>
    public DateOnly? fecha_corte_apertura { get; set; }

    /// <summary>
    /// true = el corte se dio por terminado: no se acepta ninguna apertura nueva para
    /// pares preexistentes. Los pares creados después siguen abriendo normalmente.
    /// </summary>
    public bool apertura_cerrada { get; set; }

    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }
    public string? usuariomodificacion { get; set; }
    public DateTime? fechamodificacion { get; set; }
}
