using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.AppLectores;

public record ConfiguracionAppFilterDto
{
    public string? Search { get; set; }
}

public record ConfiguracionAppListItemDto(
    int Id,
    string? Descripcion,
    decimal? ValorNumeros,
    string? ValorLetras);

public sealed class ConfiguracionAppEditDto
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "La descripcion es obligatoria.")]
    [StringLength(200, ErrorMessage = "La descripcion no puede superar los 200 caracteres.")]
    public string Descripcion { get; set; } = string.Empty;

    [Range(typeof(decimal), "-99999999", "99999999", ErrorMessage = "El valor numerico no es valido.")]
    public decimal? ValorNumeros { get; set; }

    [StringLength(100, ErrorMessage = "El valor en letras no puede superar los 100 caracteres.")]
    public string? ValorLetras { get; set; }
}
