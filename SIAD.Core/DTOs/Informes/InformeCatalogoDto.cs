namespace SIAD.Core.DTOs.Informes;

public sealed record InformeCatalogoItemDto(
    long InformeId,
    string Codigo,
    string Nombre,
    string? Descripcion,
    string Categoria,
    string TipoOrigen,
    string Ruta,
    string? ConsultaClave,
    string? IconoCssClass,
    int Orden,
    bool PermiteExportar,
    bool PermiteImprimir,
    bool IsActive
);
