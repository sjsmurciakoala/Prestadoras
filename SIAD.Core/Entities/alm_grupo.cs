using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Categoría de artículos de almacén. Migrada de MySQL bdsimafi.grupoinv.
/// Históricamente colgaba de una línea (alm_linea); desde la unificación
/// 2026-07-16 cuelga del tipo de artículo (tipo_articulo_id). Las columnas
/// legacy linea_id/linea_codigo siguen en la BD (sin mapear en EF) hasta la
/// Fase 3 del plan de unificación.
/// </summary>
public partial class alm_grupo : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }
    public string codigo { get; set; } = null!;
    public string nombre { get; set; } = null!;
    public int? tipo_articulo_id { get; set; }
    public bool activo { get; set; }
    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }
    public string? usuariomodificacion { get; set; }
    public DateTime? fechamodificacion { get; set; }

    public virtual alm_tipo_articulo? tipo_articulo { get; set; }
}
