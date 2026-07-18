using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class prv_compromiso_abono : ICompanyScopedEntity
{
    public long abono_id { get; set; }

    public long company_id { get; set; }

    public int numero_orden { get; set; }

    public int numero_abono { get; set; }

    public DateTime fecha { get; set; }

    public decimal monto { get; set; }

    public string metodo_pago { get; set; } = null!;

    public long? cuenta_contra_id { get; set; }

    public long? banco_cuenta_id { get; set; }

    public long? ban_kardex_id { get; set; }

    public long? partida_id { get; set; }

    public long? partida_reverso_id { get; set; }

    public long? ban_kardex_id_reverso { get; set; }

    public string estado { get; set; } = "V";

    public string? motivo_anulacion { get; set; }

    public string usuario_creo { get; set; } = null!;

    public DateTime fecha_creacion { get; set; }

    public string? usuario_anulacion { get; set; }

    public DateTime? fecha_anulacion { get; set; }

    public string? usuario_modifica { get; set; }

    public DateTime? fecha_modificacion { get; set; }

    public Guid? rowid { get; set; }
}
