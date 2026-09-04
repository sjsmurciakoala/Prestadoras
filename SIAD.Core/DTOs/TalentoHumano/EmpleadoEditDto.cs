using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.TalentoHumano;

public sealed class EmpleadoEditDto
{
    public int? Id { get; set; }

    /// <summary>
    /// Código interno. Se AUTOGENERA al crear (correlativo por empresa) y no se puede editar: en el
    /// alta llega vacío y el servidor lo asigna; en la edición se muestra de solo lectura y el servidor
    /// conserva el guardado.
    /// </summary>
    public string? Codigo { get; set; }

    /// <summary>
    /// Código del empleado en SIMAFI. Solo lectura: el usuario no lo escribe. Solo se puebla al importar
    /// desde Excel; el servidor ignora cualquier valor que llegue por el formulario.
    /// </summary>
    public string? CodigoSimafi { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre no puede superar los 120 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(20, ErrorMessage = "La identidad no puede superar los 20 caracteres.")]
    public string? Identidad { get; set; }

    /// <summary>Cargo elegido del catálogo (th_cargo). NULL = sin asignar.</summary>
    public int? CargoId { get; set; }

    /// <summary>Departamento elegido del catálogo (th_departamento). NULL = sin asignar.</summary>
    public int? DepartamentoId { get; set; }

    public bool Activo { get; set; } = true;
}
