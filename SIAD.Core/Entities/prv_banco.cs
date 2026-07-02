using System;

namespace SIAD.Core.Entities;

public partial class prv_banco
{
    public long prv_banco_id { get; set; }

    public string nombre { get; set; } = null!;

    public bool activo { get; set; }

    public DateTime fecha_creacion { get; set; }

    public DateTime? fecha_modificacion { get; set; }

    public string usuario_creo { get; set; } = null!;

    public string? usuario_modifica { get; set; }

    public Guid? rowid { get; set; }
}
