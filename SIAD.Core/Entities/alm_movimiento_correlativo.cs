using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Consecutivo del documento de movimiento de almacén, uno por empresa. La PK <b>es</b> el
/// <c>company_id</c>. Se incrementa con <c>SELECT … FOR UPDATE</c> dentro de la transacción
/// de posteo, igual que <see cref="alm_compra_correlativo"/>.
/// </summary>
public partial class alm_movimiento_correlativo : ICompanyScopedEntity
{
    public long company_id { get; set; }
    public int ultimo_numero { get; set; }
}
