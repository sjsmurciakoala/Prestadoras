using System;

using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Diario contable y tipo de partida por módulo comercial (plan 2026-07-02 F2,
/// pestaña Asientos). Una fila por empresa × módulo; los flags de activación
/// viven en con_integracion_config.
/// </summary>
public partial class con_integracion_asiento : ICompanyScopedEntity
{
    public long integracion_asiento_id { get; set; }

    public long company_id { get; set; }

    public string module { get; set; } = null!;

    public long? journal_id { get; set; }

    public long? type_id { get; set; }

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }
}
