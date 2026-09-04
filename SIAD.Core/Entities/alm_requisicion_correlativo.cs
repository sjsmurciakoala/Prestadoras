using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Último número de requisición emitido por empresa. Sembrado desde <c>max(alm_requisicion.numero)</c>
/// del histórico (17124 en la empresa 2): la numeración nueva continúa la vieja. Se incrementa con
/// <c>SELECT … FOR UPDATE</c> dentro de la transacción del alta (patrón de <c>alm_compra_correlativo</c>).
/// </summary>
public partial class alm_requisicion_correlativo : ICompanyScopedEntity
{
    public long company_id { get; set; }
    public int ultimo_numero { get; set; }
}
