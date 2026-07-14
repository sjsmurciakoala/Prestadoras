using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Impuestos;

/// <summary>
/// Operación "cambiar tasa por nuevo decreto". NO es una edición.
/// <para>
/// En una sola transacción el servicio:
/// <list type="number">
///   <item>cierra la tasa vigente poniéndole <c>vigencia_hasta = <see cref="VigenciaHasta"/></c>, y</item>
///   <item>crea una tasa nueva con el MISMO código y tipo, el porcentaje nuevo y
///         <c>vigencia_desde = <see cref="VigenciaHasta"/> + 1 día</c>.</item>
/// </list>
/// Así el histórico queda intacto: reimprimir una factura vieja sigue dando el
/// impuesto que regía ese día.
/// </para>
/// El tipo (GRAVADO/EXENTO/EXONERADO) se hereda de la tasa que se cierra: un decreto
/// cambia el porcentaje, no la naturaleza fiscal del renglón. Si hace falta cambiar el
/// tipo, es una tasa nueva con otro código.
/// </summary>
public sealed class CambiarTasaDto : IValidatableObject
{
    /// <summary>Id de la tasa vigente que se va a cerrar.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Debe indicar la tasa vigente a cerrar.")]
    public int TasaId { get; set; }

    /// <summary>Último día en que rige la tasa actual. La nueva empieza al día siguiente.</summary>
    public DateOnly VigenciaHasta { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Range(0, 100, ErrorMessage = "El porcentaje debe estar entre 0 y 100.")]
    public decimal NuevoPorcentaje { get; set; }

    /// <summary>Opcional: si viene vacío, se hereda el nombre de la tasa que se cierra.</summary>
    [StringLength(80, ErrorMessage = "El nombre no puede superar los 80 caracteres.")]
    public string? NuevoNombre { get; set; }

    /// <summary>Opcional: motivo/decreto. Si viene vacío se hereda la descripción anterior.</summary>
    [StringLength(250, ErrorMessage = "La descripción no puede superar los 250 caracteres.")]
    public string? NuevaDescripcion { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // El servicio revalida contra el tipo real de la tasa (fuente de verdad);
        // esto es solo para no dejar mandar un absurdo desde la UI.
        if (VigenciaHasta >= DateOnly.MaxValue.AddDays(-1))
        {
            yield return new ValidationResult(
                "La fecha de cierre no es válida.",
                [nameof(VigenciaHasta)]);
        }
    }
}
