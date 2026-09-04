using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Último número de descargo emitido por empresa. Sembrado desde
/// <c>max(alm_descargo.numero_documento)</c> del histórico. Se incrementa con
/// <c>SELECT … FOR UPDATE</c> dentro de la transacción de la entrega.
/// </summary>
public partial class alm_descargo_correlativo : ICompanyScopedEntity
{
    public long company_id { get; set; }
    public int ultimo_numero { get; set; }
}
