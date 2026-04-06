using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace SIAD.Core.DTOs.Bancos;

public class BanTransaccionCreateDto : IValidatableObject
{
    [Required(ErrorMessage = "La cuenta bancaria es obligatoria.")]
    [Range(typeof(long), "1", "9223372036854775807", ErrorMessage = "Seleccione una cuenta bancaria válida.")]
    public long BancoCuentaId { get; set; }

    [Required(ErrorMessage = "El tipo de transacción es obligatorio.")]
    [StringLength(3, ErrorMessage = "El tipo de transacción no puede superar 3 caracteres.")]
    public string IdTipoTransaccion { get; set; } = string.Empty;

    [Required(ErrorMessage = "La fecha de movimiento es obligatoria.")]
    public DateOnly FechaMovimiento { get; set; }

    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [StringLength(500, ErrorMessage = "La descripción no puede superar 500 caracteres.")]
    public string Descripcion { get; set; } = string.Empty;

    [Required(ErrorMessage = "La referencia es obligatoria.")]
    [StringLength(100, ErrorMessage = "La referencia no puede superar 100 caracteres.")]
    public string? Referencia { get; set; }

    [StringLength(120, ErrorMessage = "La referencia contable no puede superar 120 caracteres.")]
    public string? SourceDocument { get; set; }

    [Range(typeof(decimal), "0.0001", "999999999.9999", ErrorMessage = "La tasa de cambio debe ser mayor a cero.")]
    public decimal TasaCambio { get; set; } = 1m;

    [Required(ErrorMessage = "El monto es obligatorio.")]
    [Range(typeof(decimal), "0.01", "999999999.99", ErrorMessage = "El monto debe ser un número positivo.")]
    public decimal Monto { get; set; }

    [Range(typeof(long), "1", "9223372036854775807", ErrorMessage = "Seleccione una cuenta contable válida.")]
    public long? ContraCuentaId { get; set; }

    public List<BanTransaccionContraLineaDto> ContraCuentas { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (FechaMovimiento > today)
        {
            yield return new ValidationResult(
                "No se permiten transacciones futuras.",
                new[] { nameof(FechaMovimiento) });
        }

        var tieneLineasValidas = ContraCuentas != null
                                 && ContraCuentas.Any(l => l is not null && l.CuentaId > 0 && l.Monto > 0);

        if (!tieneLineasValidas
            && (!ContraCuentaId.HasValue || ContraCuentaId.Value <= 0))
        {
            yield return new ValidationResult(
                "Agregue al menos una contracuenta.",
                new[] { nameof(ContraCuentas), nameof(ContraCuentaId) });
        }

        if (tieneLineasValidas)
        {
            foreach (var linea in ContraCuentas!.Where(l => l is not null && l.CuentaId > 0 && l.Monto > 0))
            {
                if (string.IsNullOrWhiteSpace(linea.Descripcion))
                {
                    yield return new ValidationResult(
                        "La descripción de la partida es obligatoria.",
                        new[] { nameof(ContraCuentas) });
                }

                if (string.IsNullOrWhiteSpace(linea.SourceDocument))
                {
                    yield return new ValidationResult(
                        "La referencia de la partida es obligatoria.",
                        new[] { nameof(ContraCuentas) });
                }
            }

            var sumaContra = ContraCuentas!
                .Where(l => l is not null && l.CuentaId > 0 && l.Monto > 0)
                .Sum(l => l.Monto);

            if (Math.Round(sumaContra, 2) != Math.Round(Monto, 2))
            {
                yield return new ValidationResult(
                    "El monto total debe ser igual a la sumatoria de las contracuentas.",
                    new[] { nameof(Monto), nameof(ContraCuentas) });
            }
        }
    }
}
