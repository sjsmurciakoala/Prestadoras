using System;

namespace SIAD.Core.Entities;

public partial class prv_proveedor_cuenta_bancaria
{
    public long proveedor_cuenta_bancaria_id { get; set; }

    public string cod_proveedor { get; set; } = null!;

    public string banco { get; set; } = null!;

    public string cuenta_bancaria { get; set; } = null!;

    public int orden { get; set; }

    public DateTime fecha_creacion { get; set; }

    public DateTime? fecha_modificacion { get; set; }

    public string usuario_creo { get; set; } = null!;

    public string? usuario_modifica { get; set; }

    public Guid? rowid { get; set; }
}
