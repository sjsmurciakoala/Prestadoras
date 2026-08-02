using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class prv_proveedor_contacto : ICompanyScopedEntity
{
    public long proveedor_contacto_id { get; set; }

    public long company_id { get; set; }

    public string cod_proveedor { get; set; } = null!;

    public long? tipo_contacto_id { get; set; }

    public string nombre { get; set; } = null!;

    public string? cargo { get; set; }

    public string? telefono { get; set; }

    public string? extension { get; set; }

    public string? celular { get; set; }

    public string? email { get; set; }

    public string? observaciones { get; set; }

    public int orden { get; set; }

    public DateTime fecha_creacion { get; set; }

    public string usuario_creo { get; set; } = null!;

    public DateTime? fecha_modificacion { get; set; }

    public string? usuario_modifica { get; set; }

    public Guid? rowid { get; set; }
}
