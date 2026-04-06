using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Letras;

public class LetraFilterDto
{
    public string? Search { get; set; }
}

public class LetraListItemDto
{
    public string? Letra { get; set; }
    public decimal? Numero { get; set; }
}

public class LetraEditDto
{
    [Required(ErrorMessage = "La letra es requerida")]
    [StringLength(1, MinimumLength = 1, ErrorMessage = "La letra debe ser un único carácter")]
    public string? Letra { get; set; }

    [Range(0, 9, ErrorMessage = "El número debe estar entre 0 y 9")]
    public decimal? Numero { get; set; }
}

public class LetraDetailDto : LetraEditDto
{
    public DateTime? FechaCreacion { get; set; }
    public string? UsuarioCreacion { get; set; }
    public DateTime? FechaModificacion { get; set; }
    public string? UsuarioModificacion { get; set; }
}
