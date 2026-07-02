using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class axl_accion_cobranza
{
    public int cod_accion { get; set; }

    public string nombre { get; set; } = null!;

    public bool activo { get; set; }

    public Guid? rowid { get; set; }

    // Documentos generados por la acción (agregadas 2026-06-29)
    public bool genera_documento { get; set; }

    public string? documento_codigo { get; set; }  // identifica el generador de documento
}
