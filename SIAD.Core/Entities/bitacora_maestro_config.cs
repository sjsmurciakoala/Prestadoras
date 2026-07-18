using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class bitacora_maestro_config : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }
    public string entidad { get; set; } = null!;
    public bool habilitado { get; set; }
    public bool audita_crear { get; set; }
    public bool audita_editar { get; set; }
    public bool audita_eliminar { get; set; }
    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }
    public string? usuariomodificacion { get; set; }
    public DateTime? fechamodificacion { get; set; }
}
