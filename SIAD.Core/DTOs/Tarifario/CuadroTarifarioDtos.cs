namespace SIAD.Core.DTOs.Tarifario;

// ── Cuadro tarifario ──

public class CuadroTarifarioListDto
{
    public long CuadroTarifarioId { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public long ServicioId { get; set; }
    public string? ServicioCodigo { get; set; }
    public string? ServicioNombre { get; set; }
    public long? CategoriaRegulatoriaId { get; set; }
    public string? CategoriaCodigo { get; set; }
    public long? CondicionMedicionId { get; set; }
    public string? CondicionCodigo { get; set; }
    public long? SegmentoTarifarioId { get; set; }
    public string? SegmentoCodigo { get; set; }
    public DateTime VigenciaDesde { get; set; }
    public DateTime? VigenciaHasta { get; set; }
    public int Prioridad { get; set; }
    public string? ReferenciaNormativa { get; set; }
    public int StatusId { get; set; }
    public int TotalReglas { get; set; }
}

public record CuadroTarifarioSaveRequest(
    long? CuadroTarifarioId,
    long ServicioId,
    long? CategoriaRegulatoriaId,
    long? CondicionMedicionId,
    long? SegmentoTarifarioId,
    string Codigo,
    string Nombre,
    string? Descripcion,
    DateTime VigenciaDesde,
    DateTime? VigenciaHasta,
    int Prioridad,
    string? ReferenciaNormativa);

// ── Regla tarifaria ──

public class ReglaTarifariaListDto
{
    public long ReglaTarifariaId { get; set; }
    public long CuadroTarifarioId { get; set; }
    public long TipoReglaTarifariaId { get; set; }
    public string? TipoReglaCodigo { get; set; }
    public string? TipoReglaNombre { get; set; }
    public int Orden { get; set; }
    public decimal? ConsumoMinimo { get; set; }
    public decimal? ConsumoMaximo { get; set; }
    public decimal? MontoFijo { get; set; }
    public decimal? MontoUnitario { get; set; }
    public decimal? Porcentaje { get; set; }
    public long? ServicioReferenciaId { get; set; }
    public string? ServicioReferenciaCodigo { get; set; }
    public string? Parametros { get; set; }
    public int StatusId { get; set; }
}

public record ReglaTarifariaSaveRequest(
    long? ReglaTarifariaId,
    long CuadroTarifarioId,
    long TipoReglaTarifariaId,
    int Orden,
    decimal? ConsumoMinimo,
    decimal? ConsumoMaximo,
    decimal? MontoFijo,
    decimal? MontoUnitario,
    decimal? Porcentaje,
    long? ServicioReferenciaId,
    string? Parametros);

// ── Catálogos para lookups ──

public record TipoReglaTarifariaLookupDto(long Id, string Codigo, string Nombre);

public record CuadroTarifarioCatalogosDto(
    IReadOnlyList<CatalogoLookupDto> Servicios,
    IReadOnlyList<CatalogoLookupDto> Categorias,
    IReadOnlyList<CatalogoLookupDto> Condiciones,
    IReadOnlyList<SegmentoLookupDto> Segmentos,
    IReadOnlyList<TipoReglaTarifariaLookupDto> TiposRegla,
    IReadOnlyList<CatalogoLookupDto> ServiciosReferencia);
