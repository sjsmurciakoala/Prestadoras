using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class cln_plan_pago_hdr
{
    public int id { get; set; }

    public string? correlativo { get; set; }

    public int? clienteid { get; set; }

    public decimal? monto { get; set; }

    public string? direccion { get; set; }

    public string? representante { get; set; }

    public string? docrepresentante { get; set; }

    public string? numrepresentante { get; set; }

    public DateTime? fecha { get; set; }

    public DateTime? fechappago { get; set; }

    public string? comentario { get; set; }

    public decimal? porcprima { get; set; }

    public decimal? vprima { get; set; }

    public decimal? montofinanc { get; set; }

    public int? meses { get; set; }

    public string? estadopago { get; set; }

    public string? usuariocreacion { get; set; }

    public DateTime? fechacreacion { get; set; }

    public string? usuariomodificacion { get; set; }

    public DateTime? fechamodificacion { get; set; }

    public virtual cliente_maestro? cliente { get; set; }

    public virtual ICollection<cln_plan_pago_dtl> cln_plan_pago_dtls { get; set; } = new List<cln_plan_pago_dtl>();
}
