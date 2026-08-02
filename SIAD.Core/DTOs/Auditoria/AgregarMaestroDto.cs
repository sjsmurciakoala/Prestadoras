using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Auditoria;

public sealed class AgregarMaestroDto
{
    [Required] public string Tabla { get; set; } = string.Empty;
    [Required] public string Nombre { get; set; } = string.Empty;
    [Required] public string Modulo { get; set; } = string.Empty;
}
