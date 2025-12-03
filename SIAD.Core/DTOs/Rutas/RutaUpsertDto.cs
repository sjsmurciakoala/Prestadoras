using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Rutas;

public class RutaUpsertDto
{
    public int? Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Seleccione un ciclo valido.")]
    public int CodCiclo { get; set; }

    [Required]
    [StringLength(50)]
    public string CodRuta { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Descripcion { get; set; } = string.Empty;
}
