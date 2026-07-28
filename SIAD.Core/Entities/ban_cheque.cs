using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class ban_cheque : ICompanyScopedEntity
{
    public long cheque_id { get; set; }

    public long company_id { get; set; }

    public long banco_cuenta_id { get; set; }

    public decimal numero_cheque { get; set; }

    public DateTime fecha_emision { get; set; }

    public decimal monto { get; set; }

    public string? beneficiario { get; set; }

    public string? concepto { get; set; }

    public string origen { get; set; } = null!;

    public string? origen_documento { get; set; }

    public long? ban_kardex_id { get; set; }

    public long? partida_id { get; set; }

    public long? ban_kardex_id_reverso { get; set; }

    public string estado { get; set; } = "E";

    public string usuario_emision { get; set; } = null!;

    public DateTime fecha_creacion { get; set; }

    public string? motivo_anulacion { get; set; }

    public string? usuario_anulacion { get; set; }

    public DateTime? fecha_anulacion { get; set; }

    public Guid? rowid { get; set; }
}
