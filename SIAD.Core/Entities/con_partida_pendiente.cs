using System;

using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Cola de regularización contable (plan 2026-07-02 F1, se consume en F3).
/// Partidas que no pudieron postearse (ej. sin período contable abierto)
/// quedan aquí para reproceso. Estados: 1=PENDIENTE, 2=PROCESADA, 3=DESCARTADA.
/// </summary>
public partial class con_partida_pendiente : ICompanyScopedEntity
{
    public long partida_pendiente_id { get; set; }

    public long company_id { get; set; }

    public string module { get; set; } = null!;

    public string origen_tipo { get; set; } = null!;

    public long? origen_id { get; set; }

    public string? origen_referencia { get; set; }

    public DateOnly fecha_documento { get; set; }

    public string? descripcion { get; set; }

    public string payload { get; set; } = "{}";

    public string motivo { get; set; } = null!;

    public short status_id { get; set; } = 1;

    public int intentos { get; set; }

    public string? ultimo_error { get; set; }

    public long? poliza_id { get; set; }

    public DateTime? procesada_at { get; set; }

    public string? procesada_by { get; set; }

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }
}
