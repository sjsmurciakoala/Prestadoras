using SIAD.Core.DTOs.Contabilidad;

namespace SIAD.Core.DTOs.Tarifario;

public sealed class ServicioTarifarioV3ListDto
{
    public long ServicioId { get; set; }
    public long TipoServicioId { get; set; }
    public string? TipoServicioCodigo { get; set; }
    public string? TipoServicioNombre { get; set; }
    public string? Codigo { get; set; }
    public string? Nombre { get; set; }
    public string? Descripcion { get; set; }
    public bool EsAsignableCliente { get; set; }
    public bool UsaCondicionMedicion { get; set; }
    public bool FacturableApp { get; set; }
    public int AppOrden { get; set; }
    public bool PermiteEvento { get; set; }
    public bool GeneraPorRegla { get; set; }
    public long? CuentaContableId { get; set; }
    public string? CuentaContableCodigo { get; set; }
    public string? CuentaContableNombre { get; set; }
    public int OrdenVisual { get; set; }
    public int StatusId { get; set; }
    public int TotalCuadros { get; set; }
    public int TotalClientes { get; set; }
    public int TotalReferencias { get; set; }
}

public sealed class ServicioTarifarioV3EditDto
{
    public long? ServicioId { get; set; }
    public long? TipoServicioId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool EsAsignableCliente { get; set; }
    public bool UsaCondicionMedicion { get; set; }
    public bool FacturableApp { get; set; }
    public int AppOrden { get; set; }
    public bool PermiteEvento { get; set; }
    public bool GeneraPorRegla { get; set; }
    public long? CuentaContableId { get; set; }
    public int OrdenVisual { get; set; }
    public bool Activo { get; set; } = true;
}

public record TipoServicioLookupDto(long Id, string Codigo, string Nombre);

public record ServicioTarifarioV3CatalogosDto(
    IReadOnlyList<TipoServicioLookupDto> TiposServicio,
    IReadOnlyList<CuentaContableLookupDto> CuentasContables);
