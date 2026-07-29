using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class prv_tipo_contacto : ICompanyScopedEntity
{
    public long tipo_contacto_id { get; set; }

    public long company_id { get; set; }

    public string nombre { get; set; } = null!;

    public string? observaciones { get; set; }

    public bool activo { get; set; } = true;

    public DateTime fecha_creacion { get; set; }

    public string usuario_creo { get; set; } = null!;

    public DateTime? fecha_modificacion { get; set; }

    public string? usuario_modifica { get; set; }

    public Guid? rowid { get; set; }
}
