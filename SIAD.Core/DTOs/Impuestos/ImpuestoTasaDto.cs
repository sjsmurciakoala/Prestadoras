using System.ComponentModel.DataAnnotations;
using SIAD.Core.Constants;

namespace SIAD.Core.DTOs.Impuestos;

/// <summary>
/// Tasa de un impuesto. Sirve para la grilla (lectura) y para el formulario (edición).
/// Refleja los CHECK de la BD en <see cref="Validate"/> para que el usuario reciba un
/// mensaje amable ANTES de que Postgres reviente.
/// </summary>
public sealed class ImpuestoTasaDto : IValidatableObject
{
    public int? Id { get; set; }

    public int ImpuestoId { get; set; }

    [Required(ErrorMessage = "El código de la tasa es obligatorio.")]
    [StringLength(20, ErrorMessage = "El código no puede superar los 20 caracteres.")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(80, ErrorMessage = "El nombre no puede superar los 80 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    /// <summary>GRAVADO | EXENTO | EXONERADO.</summary>
    [Required(ErrorMessage = "El tipo es obligatorio.")]
    public string Tipo { get; set; } = TipoImpuestoTasa.Gravado;

    [Range(0, 100, ErrorMessage = "El porcentaje debe estar entre 0 y 100.")]
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
        if (!TipoImpuestoTasa.EsValido(Tipo))
        {
            yield return new ValidationResult(
                "El tipo debe ser GRAVADO, EXENTO o EXONERADO.",
                [nameof(Tipo)]);
            yield break;
        }

        // Espejo de ck_cfg_impuesto_tasa_coherencia.
        if (TipoImpuestoTasa.ExigePorcentaje(Tipo) && Porcentaje <= 0)
        {
            yield return new ValidationResult(
                "Una tasa GRAVADA debe tener un porcentaje mayor que cero.",
                [nameof(Porcentaje)]);
        }

        if (!TipoImpuestoTasa.ExigePorcentaje(Tipo) && Porcentaje != 0)
        {
            yield return new ValidationResult(
                $"Una tasa {Tipo} debe tener porcentaje 0: por definición no paga impuesto.",
                [nameof(Porcentaje)]);
        }

        // Espejo de ck_cfg_impuesto_tasa_vigencia.
        if (VigenciaHasta is not null && VigenciaHasta < VigenciaDesde)
        {
            yield return new ValidationResult(
                "La fecha 'vigente hasta' no puede ser anterior a 'vigente desde'.",
                [nameof(VigenciaHasta)]);
        }
    }
}
