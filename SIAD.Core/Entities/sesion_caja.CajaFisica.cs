namespace SIAD.Core.Entities;

// Unificación cobranza F2 (2026-07-26): la sesión se abre EN una caja física
// (adm_caja). Una sola sesión ABIERTA por caja (índice parcial en BD).
// NULL solo en sesiones legacy pre-F3.
public partial class sesion_caja
{
    public int? caja_fisica_id { get; set; }

    /// <summary>Fondo inicial del turno (opcional), visible en el cierre.</summary>
    public decimal? monto_apertura { get; set; }

    /// <summary>Efectivo contado por el cajero en el arqueo del cierre.</summary>
    public decimal? monto_cierre { get; set; }
}
