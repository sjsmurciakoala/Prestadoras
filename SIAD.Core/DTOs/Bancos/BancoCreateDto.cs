using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Bancos;

public class BancoCreateDto
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(60, ErrorMessage = "El nombre no puede superar 60 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;
}
