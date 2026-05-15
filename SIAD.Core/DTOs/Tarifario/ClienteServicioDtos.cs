namespace SIAD.Core.DTOs.Tarifario;

/// <summary>
/// Elemento de la grilla de servicios asignados al cliente (adm_cliente_servicio).
/// </summary>
public class ClienteServicioItemDto
{
    public long ClienteServicioId { get; set; }
    public long ServicioId { get; set; }
    public string? ServicioCodigo { get; set; }
    public string? ServicioNombre { get; set; }
    public long? CategoriaRegulatoriaId { get; set; }
    public string? CategoriaCodigo { get; set; }
    public string? CategoriaNombre { get; set; }
    public long? CondicionMedicionId { get; set; }
    public string? CondicionCodigo { get; set; }
    public string? CondicionNombre { get; set; }
    public long? SegmentoTarifarioId { get; set; }
    public string? SegmentoCodigo { get; set; }
    public string? SegmentoNombre { get; set; }
    public long? CuadroTarifarioId { get; set; }
    public string? CuadroCodigo { get; set; }
    public string? CuadroNombre { get; set; }
    public DateTime? FechaAlta { get; set; }
    public DateTime? FechaBaja { get; set; }
    public int StatusId { get; set; }
}

/// <summary>
/// Lookup genérico para catálogos adm_*.
/// </summary>
public record CatalogoLookupDto(long Id, string Codigo, string Nombre);

/// <summary>
/// Lookup de segmento con su categoría padre para cascada.
/// </summary>
public record SegmentoLookupDto(long Id, string Codigo, string Nombre, long? CategoriaRegulatoriaId);

/// <summary>
/// Catálogos necesarios para el formulario de asignación.
/// </summary>
public record ClienteServicioCatalogosDto(
    IReadOnlyList<CatalogoLookupDto> Servicios,
    IReadOnlyList<CatalogoLookupDto> Categorias,
    IReadOnlyList<CatalogoLookupDto> Condiciones,
    IReadOnlyList<SegmentoLookupDto> Segmentos);

/// <summary>
/// Request para crear o actualizar una asignación de servicio a un cliente.
/// </summary>
public record ClienteServicioSaveRequest(
    long? ClienteServicioId,
    long ServicioId,
    long? CategoriaRegulatoriaId,
    long? CondicionMedicionId,
    long? SegmentoTarifarioId,
    DateTime FechaAlta);

/// <summary>
/// Request para desactivar una asignación.
/// </summary>
public record ClienteServicioDesactivarRequest(long ClienteServicioId);
