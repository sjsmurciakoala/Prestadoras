using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class cln_accion_cobranza : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }
    public string codigocliente { get; set; } = null!;
    public DateTime fecha { get; set; }
    public string accion { get; set; } = null!;
    public string? observacion { get; set; }

    // Nuevas columnas (agregadas 2026-06-04)
    public int? cod_accion { get; set; }       // FK → axl_accion_cobranza.cod_accion
    public int? cod_observacion { get; set; }  // FK → axl_observacion_cobranza.id
    public int? abogado_id { get; set; }       // FK → abogado.abogado_id
    public string? ejecutado_por { get; set; } // usuario ejecutor (de sesión)
}
