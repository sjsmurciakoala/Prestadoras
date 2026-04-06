namespace SIAD.Core.DTOs.Informes;

public sealed record ReporteDisenoCatalogoItemDto(
    long InformeId,
    string Codigo,
    string Nombre,
    string? Descripcion,
    string Categoria,
    string? DatasetCodigo,
    string Estado,
    int? DraftVersion,
    int? PublishedVersion,
    bool TieneBorrador,
    bool TieneLayoutPublicado,
    string ViewerRoute,
    string DesignerRoute,
    bool IsActive
);

public sealed record ReporteDisenoDetalleDto(
    long InformeId,
    string Codigo,
    string Nombre,
    string? Descripcion,
    string Categoria,
    string? DatasetCodigo,
    string Estado,
    int? DraftVersion,
    int? PublishedVersion,
    bool TieneBorrador,
    bool TieneLayoutPublicado,
    string ViewerRoute,
    string DesignerRoute,
    string ViewerReportName,
    string DesignerReportName,
    bool IsActive,
    bool TieneDesfaseDatasetLayout,
    bool PuedeRegenerarBorrador,
    string? MensajeRegeneracionBorrador,
    DateTime? LayoutBaseActualizadoEnUtc,
    DateTime? DatasetActualizadoEnUtc
);

public sealed class ReporteDisenoCreateDto
{
    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public string Categoria { get; set; } = "General";

    public string? Descripcion { get; set; }

    public string? DatasetCodigo { get; set; }
}
