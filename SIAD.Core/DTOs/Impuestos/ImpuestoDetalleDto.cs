namespace SIAD.Core.DTOs.Impuestos;

/// <summary>Un impuesto con todas sus tasas (vigentes e históricas).</summary>
public sealed class ImpuestoDetalleDto
{
    public ImpuestoEditDto Impuesto { get; init; } = new();
    public List<ImpuestoTasaDto> Tasas { get; init; } = new();
}
