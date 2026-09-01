namespace SIAD.Core.DTOs.TalentoHumano;

public sealed class EmpleadoListItemDto
{
    public int Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string? CodigoSimafi { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string? Identidad { get; init; }
    public string? CargoNombre { get; init; }
    public string? DepartamentoNombre { get; init; }
    public bool Activo { get; init; }
}
