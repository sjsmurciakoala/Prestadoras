using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class ban_cheque_bitacora : ICompanyScopedEntity
{
    public long bitacora_id { get; set; }

    public long company_id { get; set; }

    public long cheque_id { get; set; }

    public long banco_cuenta_id { get; set; }

    public decimal numero_cheque { get; set; }

    public string accion { get; set; } = null!;

    public DateTime fecha { get; set; }

    public string usuario { get; set; } = null!;

    public decimal monto { get; set; }

    public string? beneficiario { get; set; }

    public string? concepto { get; set; }

    public string? motivo { get; set; }

    public string origen { get; set; } = null!;

    public string? origen_documento { get; set; }

    public long? ban_kardex_id { get; set; }

    public Guid? rowid { get; set; }
}
