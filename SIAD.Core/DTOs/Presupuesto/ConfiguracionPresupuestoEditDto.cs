using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Presupuesto;

public sealed class ConfiguracionPresupuestoEditDto : IValidatableObject
{
    [StringLength(10, ErrorMessage = "El ID de presupuesto no puede exceder 10 caracteres.")]
    public string IdPresupuesto { get; set; } = string.Empty;

    [StringLength(20, ErrorMessage = "La cuenta contable no puede exceder 20 caracteres.")]
    public string CuentaContable { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "99999999999999.9999", ErrorMessage = "El valor de proyeccion no es valido.")]
    public decimal ValorProyeccion { get; set; }

    [Range(typeof(decimal), "0", "99999999999999.9999", ErrorMessage = "El valor real no es valido.")]
    public decimal ValorReal { get; set; }

    [Range(typeof(decimal), "0", "99999999999999.9999", ErrorMessage = "El valor global no es valido.")]
    public decimal ValorGlobal { get; set; }

    [Range(typeof(decimal), "0", "99999999999999.9999", ErrorMessage = "El valor disponible no es valido.")]
    public decimal ValorDisponible { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "El rango de periodo debe ser mayor a cero.")]
    public int RangoPeriodo { get; set; }

    public DateOnly FechaInicia { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public DateOnly FechaFinaliza { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public bool EstadoAprobado { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ValorGlobal <= 0m)
        {
            yield return new ValidationResult(
                "El presupuesto debe ser mayor a cero.",
                new[] { nameof(ValorGlobal) });
        }

        if (FechaFinaliza < FechaInicia)
        {
            yield return new ValidationResult(
                "La fecha finaliza no puede ser menor a la fecha inicia.",
                new[] { nameof(FechaFinaliza), nameof(FechaInicia) });
        }
    }
}
