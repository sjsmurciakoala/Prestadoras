using System;

using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Historial de lotes de partidas de facturación (plan 2026-07-02 F3).
/// Cada corrida de sp_con_generar_partidas_facturacion que encuentra facturas
/// crea un lote. Estados: 1=GENERADO, 2=PARCIAL, 3=ENCOLADO.
/// </summary>
public partial class con_lote_facturacion : ICompanyScopedEntity
{
    public long lote_id { get; set; }

    public long company_id { get; set; }

    public DateOnly fecha_desde { get; set; }

    public DateOnly fecha_hasta { get; set; }

    public string modo_agrupacion { get; set; } = "DIA";

    public int facturas { get; set; }

    public int polizas { get; set; }

    public int encoladas { get; set; }

    public decimal total { get; set; }

    public short status_id { get; set; } = 1;

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;
}
