using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Retenciones;

/// <summary>
/// Operación "cambiar la tasa por nuevo decreto". NO es una edición.
/// <para>
/// En una sola transacción el servicio:
/// <list type="number">
///   <item>cierra la tasa vigente poniéndole <c>vigencia_hasta = <see cref="VigenciaHasta"/></c>, y</item>
///   <item>crea una tasa nueva de la MISMA retención con el porcentaje nuevo y
///         <c>vigencia_desde = <see cref="VigenciaHasta"/> + 1 día</c>.</item>
/// </list>
/// Así el histórico queda intacto: reconstruir un pago viejo sigue dando el % que regía ese día.
/// </para>
/// </summary>
public sealed class CambiarTasaDto : IValidatableObject
{
    /// <summary>Id de la tasa vigente que se va a cerrar.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Debe indicar la tasa vigente a cerrar.")]
    public int TasaId { get; set; }

    /// <summary>Último día en que rige la tasa actual. La nueva empieza al día siguiente.</summary>
    public DateOnly VigenciaHasta { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Range(0.01, 100, ErrorMessage = "El porcentaje debe ser mayor que 0 y hasta 100.")]
    public decimal NuevoPorcentaje { get; set; }

    /// <summary>Opcional: motivo/decreto. Si viene vacío se hereda la descripción anterior.</summary>
    [StringLength(250, ErrorMessage = "La descripción no puede superar los 250 caracteres.")]
    public string? NuevaDescripcion { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (NuevoPorcentaje <= 0)
        {
            yield return new ValidationResult(
                "El nuevo porcentaje debe ser mayor que cero.",
                [nameof(NuevoPorcentaje)]);
        }

        if (VigenciaHasta >= DateOnly.MaxValue.AddDays(-1))
        {
            yield return new ValidationResult(
                "La fecha de cierre no es válida.",
                [nameof(VigenciaHasta)]);
        }
    }
}
