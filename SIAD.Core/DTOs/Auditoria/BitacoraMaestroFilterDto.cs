using System;
namespace SIAD.Core.DTOs.Auditoria;
public sealed class BitacoraMaestroFilterDto
{
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
    public string? Modulo { get; set; }
    public string? Tabla { get; set; }
    public string? Accion { get; set; }
    public string? Usuario { get; set; }
}
