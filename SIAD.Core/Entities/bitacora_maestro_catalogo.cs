using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

// Catálogo por empresa de entidades (tablas) auditables por la bitácora de maestros.
// Editable desde la UI; reemplaza en runtime a la lista blanca de AuditableMaestros
// (que queda como semilla base para el auto-seed de empresas nuevas).
public partial class bitacora_maestro_catalogo : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }
    public string tabla { get; set; } = null!;
    public string nombre { get; set; } = null!;
    public string modulo { get; set; } = null!;
    public bool activo { get; set; }
    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }
    public string? usuariomodificacion { get; set; }
    public DateTime? fechamodificacion { get; set; }
}
