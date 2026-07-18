using System;
namespace SIAD.Core.DTOs.Auditoria;
public sealed class BitacoraMaestroListItemDto
{
    public long Id { get; init; }
    public DateTime Fecha { get; init; }
    public string Usuario { get; init; } = string.Empty;
    public string Modulo { get; init; } = string.Empty;
    public string Tabla { get; init; } = string.Empty;
    public string Entidad { get; init; } = string.Empty;
    public string Accion { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public string? RegistroId { get; init; }
    public string? ValoresAnteriores { get; init; }
    public string? ValoresNuevos { get; init; }
}
