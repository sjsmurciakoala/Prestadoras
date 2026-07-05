using System;

using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Credencial del WS bancario (plan 2026-07-02 F8, contrato SIMAFI congelado —
/// docs/f8-contrato-ws-bancario.md). Equivalente SIAD de bdsimafi.recolector:
/// el banco autentica con banco+key por query string y la fila resuelve el
/// tenant y la cuenta bancaria destino de los depósitos. La vigencia es
/// informativa: el contrato NUNCA la valida.
/// </summary>
public partial class ban_ws_credencial : ICompanyScopedEntity
{
    public long credencial_id { get; set; }

    public long company_id { get; set; }

    public string banco { get; set; } = null!;

    public string? nombre { get; set; }

    public string? llave { get; set; }

    public DateOnly? vigencia { get; set; }

    public long? banco_cuenta_id { get; set; }

    public bool activo { get; set; } = true;

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }
}
