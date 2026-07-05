using System;

using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Bitácora e idempotencia del WS bancario (plan 2026-07-02 F8). Equivalente
/// SIAD de bdsimafi.pagos_bancos: una fila por referencia aplicada; el replay
/// del mismo pago devuelve la misma respuesta sin duplicar. Estados numéricos:
/// 1 = APLICADO, 2 = REVERSADO (EstadoBanWsPago).
/// </summary>
public partial class ban_ws_pago : ICompanyScopedEntity
{
    public long pago_id { get; set; }

    public long company_id { get; set; }

    public string banco { get; set; } = null!;

    public string referencia { get; set; } = null!;

    public string clave { get; set; } = null!;

    public string tipo { get; set; } = "S";

    public decimal monto { get; set; }

    public DateOnly fecha_registro { get; set; }

    public TimeOnly? hora_registro { get; set; }

    public DateOnly? fecha_efectiva { get; set; }

    public string? sucursal { get; set; }

    public string? cajero { get; set; }

    public long? banco_cuenta_id { get; set; }

    public long? ban_kardex_id { get; set; }

    public long? ban_kardex_reverso_id { get; set; }

    public long? poliza_id { get; set; }

    public short status_id { get; set; } = 1;

    public DateTime? reversado_at { get; set; }

    public string? reversado_por { get; set; }

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;
}
