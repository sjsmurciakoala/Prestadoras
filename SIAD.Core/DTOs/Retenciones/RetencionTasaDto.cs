using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Retenciones;

/// <summary>
/// Tasa de una retención. Sirve para la grilla (lectura) y para el formulario (edición).
/// A diferencia de la tasa de impuesto NO lleva código/nombre/tipo: la retención ES el concepto.
/// Refleja los CHECK de la BD en <see cref="Validate"/>.
/// </summary>
public sealed class RetencionTasaDto : IValidatableObject
{
    public int? Id { get; set; }

    public int RetencionId { get; set; }

    /// <summary>Porcentaje a retener. Estrictamente &gt; 0.</summary>
    [Range(0.01, 100, ErrorMessage = "El porcentaje debe ser mayor que 0 y hasta 100.")]
    public decimal Porcentaje { get; set; }

    public DateOnly VigenciaDesde { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>NULL = vigente indefinidamente.</summary>
    public DateOnly? VigenciaHasta { get; set; }

    [StringLength(250, ErrorMessage = "La descripción no puede superar los 250 caracteres.")]
    public string? Descripcion { get; set; }

    public bool Activo { get; set; } = true;

    /// <summary>true si la vigencia sigue abierta (no se ha cerrado por decreto).</summary>
    public bool EsAbierta => VigenciaHasta is null;

    /// <summary>true si la tasa rige hoy. Las cerradas en el pasado son histórico.</summary>
    public bool EsVigenteHoy
    {
        get
        {
            var hoy = DateOnly.FromDateTime(DateTime.Today);
            return Activo
                && VigenciaDesde <= hoy
                && (VigenciaHasta is null || VigenciaHasta >= hoy);
        }
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Espejo de ck_cfg_retencion_tasa_rango.
        if (Porcentaje <= 0)
        {
            yield return new ValidationResult(
                "Una retención debe tener un porcentaje mayor que cero.",
                [nameof(Porcentaje)]);
        }

        // Espejo de ck_cfg_retencion_tasa_vigencia.
        if (VigenciaHasta is not null && VigenciaHasta < VigenciaDesde)
        {
            yield return new ValidationResult(
                "La fecha 'vigente hasta' no puede ser anterior a 'vigente desde'.",
                [nameof(VigenciaHasta)]);
        }
    }
}
