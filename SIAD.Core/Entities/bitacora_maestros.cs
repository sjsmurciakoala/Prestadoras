using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class bitacora_maestros : ICompanyScopedEntity
{
    public long bitacora_maestro_id { get; set; }
    public long company_id { get; set; }
    public string modulo { get; set; } = null!;
    public string tabla { get; set; } = null!;
    public string entidad { get; set; } = null!;
    public string? registro_id { get; set; }
    public string accion { get; set; } = null!;
    public string descripcion { get; set; } = null!;
    public string? valores_anteriores { get; set; }
    public string? valores_nuevos { get; set; }
    public DateTime fecha { get; set; }
    public string usuario { get; set; } = null!;
}
