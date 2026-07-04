using System;

using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Puente factura ↔ partida del lote de facturación (plan 2026-07-02 F3).
/// Una factura pertenece a lo sumo a un lote posteado (idempotencia del lote).
/// </summary>
public partial class con_partida_factura : ICompanyScopedEntity
{
    public long partida_factura_id { get; set; }

    public long company_id { get; set; }

    public int factura_id { get; set; }

    public long lote_id { get; set; }

    /// <summary>NULL = factura procesada sin efecto contable (detalle neto en cero).</summary>
    public long? poliza_id { get; set; }

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;
}
