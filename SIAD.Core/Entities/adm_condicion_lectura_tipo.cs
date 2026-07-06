namespace SIAD.Core.Entities;

/// <summary>
/// Referencia GLOBAL de tipos de condición de lectura (N/MIN/PND/PD), acoplada
/// al motor V3 (sp_adm_calcular_factura_lectura ramifica por tipo). NO editable
/// por el admin: no se inventan tipos porque el motor no sabría calcularlos.
/// Es catálogo del sistema, no tenant-scoped (spec §2, 2026-07-06).
/// </summary>
public partial class adm_condicion_lectura_tipo
{
    public string tipo { get; set; } = null!;

    public string descripcion { get; set; } = null!;

    /// <summary>true solo para N — semántica del motor (requiere lectura del medidor).</summary>
    public bool requiere_lectura { get; set; }
}
