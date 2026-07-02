using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Presupuesto;

public sealed class PresupuestoActividadSolicitudCreateDto
{
    [Required(ErrorMessage = "El tipo de actividad es obligatorio.")]
    [StringLength(20, ErrorMessage = "El tipo de actividad no puede exceder 20 caracteres.")]
    public string TipoActividad { get; set; } = string.Empty;

    [StringLength(20, ErrorMessage = "La cuenta destino no puede exceder 20 caracteres.")]
    public string? CuentaDestinoCode { get; set; }

    [Range(typeof(decimal), "0.01", "99999999999999.9999", ErrorMessage = "El monto debe ser mayor a cero.")]
    public decimal Monto { get; set; }

    [Required(ErrorMessage = "La justificacion es obligatoria.")]
    [StringLength(1000, ErrorMessage = "La justificacion no puede exceder 1000 caracteres.")]
    public string Justificacion { get; set; } = string.Empty;

    [Range(1, 3, ErrorMessage = "La prioridad debe estar entre 1 y 3.")]
    public short Prioridad { get; set; } = 2;
}
