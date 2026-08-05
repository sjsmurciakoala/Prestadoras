using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Último número de recepción de compra emitido por empresa (la PK ES el <c>company_id</c>,
/// mismo patrón que <see cref="alm_orden_compra_correlativo"/>).
/// <para>
/// El servicio toma la fila con <c>SELECT … FOR UPDATE</c> antes de incrementarla: el
/// candado serializa dos altas concurrentes de la misma empresa, que si no calcularían el
/// mismo número y chocarían contra <c>uq_alm_compra_hdr_numero</c>.
/// </para>
/// </summary>
public partial class alm_compra_correlativo : ICompanyScopedEntity
{
    /// <summary>PK y tenant a la vez: un solo contador por empresa.</summary>
    public long company_id { get; set; }

    /// <summary>Último número entregado. El siguiente es este + 1.</summary>
    public int ultimo_numero { get; set; }
}
