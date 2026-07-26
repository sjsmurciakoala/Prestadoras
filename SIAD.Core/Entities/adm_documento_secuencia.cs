using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

// Serie de folios administrable por empresa (tipo_documento = 'RECIBO_PAGO', ...).
// El consumo se hace SIEMPRE por fn_adm_siguiente_correlativo_documento (atómico);
// esta entidad existe para el mantenimiento/consulta de la serie.
// Unificación cobranza F2 (2026-07-26), plan §3.8.
public partial class adm_documento_secuencia : ICompanyScopedEntity
{
    public long secuencia_id { get; set; }

    public long company_id { get; set; }

    public string tipo_documento { get; set; } = null!;

    /// <summary>0 = serie general; &gt;0 serie por canal (CanalCobro).</summary>
    public short canal_id { get; set; }

    public string prefijo { get; set; } = string.Empty;

    public short longitud_padding { get; set; }

    public long valor_actual { get; set; }

    public bool activo { get; set; }

    public string? updated_by { get; set; }

    public DateTime? updated_at { get; set; }
}
