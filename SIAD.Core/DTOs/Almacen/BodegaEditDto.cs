using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Almacen;

public sealed class BodegaEditDto
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(10, ErrorMessage = "El código no puede superar los 10 caracteres.")]
    public string Codigo { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Direccion { get; set; }

    [StringLength(100)]
    public string? Responsable { get; set; }

    public bool Activo { get; set; } = true;

    /// <summary>
    /// Override del interruptor de existencia negativa, por bodega (tri-estado): <c>null</c> =
    /// hereda del interruptor de la empresa; <c>true</c> = fuerza permitir aquí; <c>false</c> =
    /// fuerza bloquear aquí.
    /// </summary>
    public bool? PermiteExistenciaNegativa { get; set; }
}
