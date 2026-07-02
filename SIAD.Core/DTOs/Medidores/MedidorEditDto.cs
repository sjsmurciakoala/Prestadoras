using System;
using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Medidores;

public sealed class MedidorEditDto
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "El numero es obligatorio.")]
    [StringLength(50, ErrorMessage = "El numero no puede superar los 50 caracteres.")]
    public string Numero { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "La marca no puede superar los 50 caracteres.")]
    public string? Marca { get; set; }

    public DateTime? FechaInstalacion { get; set; }

    [Range(typeof(decimal), "0", "99.99", ErrorMessage = "El diametro debe estar entre 0 y 99.99.")]
    public decimal? Diametro { get; set; }

    [StringLength(50, ErrorMessage = "El empleado no puede superar los 50 caracteres.")]
    public string? Empleado { get; set; }

    [StringLength(20, ErrorMessage = "El acueducto no puede superar los 20 caracteres.")]
    public string? Acueducto { get; set; }

    [StringLength(1, ErrorMessage = "El código de clase no puede superar 1 carácter.")]
    public string? ClaseMedidorCodigo { get; set; }

    public bool Activo { get; set; } = true;
}
