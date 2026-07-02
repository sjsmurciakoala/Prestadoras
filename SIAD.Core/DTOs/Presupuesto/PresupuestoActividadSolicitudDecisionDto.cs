using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Presupuesto;

public sealed class PresupuestoActividadSolicitudDecisionDto
{
    [StringLength(500, ErrorMessage = "El comentario no puede exceder 500 caracteres.")]
    public string? Comentario { get; set; }
}
