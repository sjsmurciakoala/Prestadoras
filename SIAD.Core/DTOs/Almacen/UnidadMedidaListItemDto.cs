namespace SIAD.Core.DTOs.Almacen;

public sealed class UnidadMedidaListItemDto
{
    public int Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string? Abreviatura { get; init; }
    public string? Categoria { get; init; }
    public bool PermiteDecimales { get; init; }
    public bool Activo { get; init; }
    public int? UnidadBaseId { get; init; }
    public string? UnidadBaseCodigo { get; init; }
    public decimal FactorConversion { get; init; }

    public bool EsBase => UnidadBaseId is null;
}
