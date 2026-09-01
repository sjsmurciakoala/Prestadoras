using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Almacen;

public sealed class TerminoPagoEditDto
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(60, ErrorMessage = "El nombre no puede superar los 60 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [Range(0, 3650, ErrorMessage = "Los días de crédito deben estar entre 0 y 3650.")]
    public int Dias { get; set; }

    /// <summary>Término propuesto por defecto en la factura de compra. Solo uno por empresa.</summary>
    public bool EsDefault { get; set; }

    public bool Activo { get; set; } = true;
}
