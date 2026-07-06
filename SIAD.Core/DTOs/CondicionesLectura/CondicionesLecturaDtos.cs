using System.Collections.Generic;

namespace SIAD.Core.DTOs.CondicionesLectura;

// =============================================================================
// DTOs del ABM de condiciones de lectura por empresa (portal, 2026-07-06).
// El admin edita codigo/descripcion/orden/activo/facturacion/aplica_descuento y
// elige el `tipo` de la referencia global (no texto libre). Lo consume el ABM
// Blazor; el catálogo resultante lo sirve GET /api/condiciones (apc.MobileApi).
// =============================================================================

/// <summary>
/// Tipo de condición del catálogo GLOBAL (referencia, no editable). Alimenta el
/// desplegable del ABM: el admin elige el comportamiento del motor V3, no lo
/// inventa. requiereLectura es semántica del motor (true solo para N).
/// </summary>
public sealed class CondicionLecturaTipoDto
{
    public string Tipo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public bool RequiereLectura { get; set; }
}

/// <summary>Fila administrable por empresa (adm_condicion_lectura).</summary>
public sealed class CondicionLecturaAdminDto
{
    /// <summary>0 = fila nueva (insert).</summary>
    public long CondicionLecturaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Tipo { get; set; } = "N";
    public string Facturacion { get; set; } = "S";
    public string AplicaDescuento { get; set; } = "N";
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
}

/// <summary>Catálogo completo de una empresa: tipos (ref) + condiciones editables.</summary>
public sealed class CondicionesLecturaCatalogoDto
{
    public List<CondicionLecturaTipoDto> Tipos { get; set; } = new();
    public List<CondicionLecturaAdminDto> Condiciones { get; set; } = new();
}
