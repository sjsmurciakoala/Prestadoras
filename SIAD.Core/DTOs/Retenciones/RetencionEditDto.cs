using System.ComponentModel.DataAnnotations;
using SIAD.Core.Constants;

namespace SIAD.Core.DTOs.Retenciones;

/// <summary>
/// Alta/edición de una retención (el concepto). Refleja los CHECK de la BD en
/// <see cref="Validate"/> para dar un mensaje amable antes de que Postgres reviente.
/// Sin <c>CompanyId</c>: el catálogo es global.
/// </summary>
public sealed class RetencionEditDto : IValidatableObject
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(20, ErrorMessage = "El código no puede superar los 20 caracteres.")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(80, ErrorMessage = "El nombre no puede superar los 80 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(250, ErrorMessage = "La descripción no puede superar los 250 caracteres.")]
    public string? Descripcion { get; set; }

    /// <summary>TOTAL | SIN_ISV. Ver <see cref="BaseRetencion"/>.</summary>
    [Required(ErrorMessage = "La base de cálculo es obligatoria.")]
    public string BaseCalculo { get; set; } = BaseRetencion.SinIsv;

    /// <summary>ISR | ISV. Ver <see cref="TipoImpuestoRetencion"/>.</summary>
    [Required(ErrorMessage = "El tipo de impuesto es obligatorio.")]
    public string TipoImpuesto { get; set; } = TipoImpuestoRetencion.Isr;

    public bool Activo { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Espejo de ck_cfg_retencion_base.
        if (!BaseRetencion.EsValido(BaseCalculo))
        {
            yield return new ValidationResult(
                "La base de cálculo debe ser TOTAL o SIN_ISV.",
                [nameof(BaseCalculo)]);
        }

        // Espejo de ck_cfg_retencion_tipo_impuesto.
        if (!TipoImpuestoRetencion.EsValido(TipoImpuesto))
        {
            yield return new ValidationResult(
                "El tipo de impuesto debe ser ISR o ISV.",
                [nameof(TipoImpuesto)]);
        }
    }
}
