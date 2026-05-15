namespace SIAD.Core.DTOs.Mantenimientos;

// =============================================================================
// Mantenimientos de catálogos / configuración (Sprint 3, 2026-05-14).
// Agrupa los CRUD sueltos: recargo por mora, ajustes tarifarios.
// =============================================================================

/// <summary>Configuración del recargo por mora (cfg_recargo_mora) — 1 fila por empresa.</summary>
public class RecargoMoraDto
{
    public long CompanyId { get; set; }
    public decimal TasaMensual { get; set; }
    public int DiasGracia { get; set; }
    public string? Descripcion { get; set; }
    public bool Activo { get; set; }
}

/// <summary>Fila del listado de ajustes tarifarios (adm_ajuste_tarifario).</summary>
public class AjusteTarifarioDto
{
    public long AjusteTarifarioId { get; set; }
    public long CuadroTarifarioId { get; set; }
    public string CuadroCodigo { get; set; } = string.Empty;
    public string CuadroNombre { get; set; } = string.Empty;
    public string TipoAjusteCodigo { get; set; } = string.Empty;
    public string TipoAjusteNombre { get; set; } = string.Empty;
    public string? CondicionCodigo { get; set; }
    public decimal? Porcentaje { get; set; }
    public decimal? MontoFijo { get; set; }
    public decimal? TopeMaximo { get; set; }
    public decimal? TopeMensual { get; set; }   // parametros ->> 'tope_mensual'
    public bool Activo { get; set; }
}

/// <summary>Request para actualizar los campos editables de un ajuste tarifario.</summary>
public class AjusteTarifarioSaveRequestDto
{
    public long AjusteTarifarioId { get; set; }
    public decimal? Porcentaje { get; set; }
    public decimal? MontoFijo { get; set; }
    public decimal? TopeMaximo { get; set; }
    public decimal? TopeMensual { get; set; }
    public bool Activo { get; set; }
}
