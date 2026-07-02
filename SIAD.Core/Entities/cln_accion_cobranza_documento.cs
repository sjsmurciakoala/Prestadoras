using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Snapshot del documento (PDF) generado al registrar una acción de cobranza.
/// Un registro = el documento exacto entregado al cliente ese día.
/// </summary>
public partial class cln_accion_cobranza_documento : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }
    public int accion_id { get; set; }            // FK → cln_accion_cobranza.id
    public string documento_codigo { get; set; } = null!;
    public string nombre_archivo { get; set; } = null!;
    public byte[] contenido { get; set; } = null!; // PDF snapshot (bytea)
    public string content_type { get; set; } = "application/pdf";
    public DateTime generado_en { get; set; }
    public string? generado_por { get; set; }
}
