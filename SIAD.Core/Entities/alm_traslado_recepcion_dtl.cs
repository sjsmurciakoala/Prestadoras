using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Renglón recibido de un traslado: <b>la unidad de posteo de la entrada a destino</b>. Su
/// <see cref="uuid"/> ancla la idempotencia del asiento de entrada; anclarlo a la línea del traslado
/// haría que la segunda recepción parcial se tomara por un reintento de la primera (mismo motivo que
/// el descargo contra la requisición). Ver diseño §2.4.
/// </summary>
public partial class alm_traslado_recepcion_dtl : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }

    /// <summary>Acto de recepción al que pertenece (<see cref="alm_traslado_recepcion"/>).</summary>
    public int recepcion_id { get; set; }

    /// <summary>Renglón del traslado que se recibe (<see cref="alm_movimiento_dtl"/>).</summary>
    public int movimiento_dtl_id { get; set; }

    public int articulo_id { get; set; }

    /// <summary>Cantidad recibida en ESTE acto (parte de lo enviado en el renglón del traslado).</summary>
    public decimal cantidad { get; set; }

    /// <summary>Costo con que viaja la mercadería, copiado del renglón del traslado (promedio de origen).</summary>
    public decimal costo_real { get; set; }

    public decimal total { get; set; }

    /// <summary>Asiento de ENTRADA a destino generado por esta línea. NULL antes de postear.</summary>
    public int? kardex_id { get; set; }

    /// <summary>Idempotencia del asiento de entrada: uuid v5 derivado de esta línea de recepción.</summary>
    public Guid uuid { get; set; }

    public virtual alm_traslado_recepcion recepcion { get; set; } = null!;
    public virtual alm_movimiento_dtl renglonTraslado { get; set; } = null!;
}
