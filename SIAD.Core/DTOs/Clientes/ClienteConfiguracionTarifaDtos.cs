using System.Collections.Generic;

namespace SIAD.Core.DTOs.Clientes;

public record ClienteConfiguracionTarifaHeaderDto(
    int ClienteId,
    string? ClienteClave,
    string? ClienteNombre,
    string? ClienteDireccion,
    bool ClienteTieneMedidor,
    int? ClienteCategoriaId,
    string? CategoriaDescripcion,
    int? MaestroMedidorId,
    string? MedidorNumero,
    string? LecturaActual,
    string? LecturaAnterior,
    string? Consumo,
    string? TipoUsoCodigo,
    string? LetraCodigo);

public class ClienteConfiguracionTarifaDetalleDto
{
    public int ConfiguracionTasasId { get; set; }
    public int ConfiguracionTasasDetalleId { get; set; }
    public int ServiciosId { get; set; }
    public string? ServicioCodigo { get; set; }
    public string? DescripcionTasa { get; set; }
    public bool ConfiguracionTasasDetalleAplicaservicio { get; set; }
    public decimal ConfiguracionTasasDetalleMonto { get; set; }
}

public record ClienteConfiguracionTarifaUpdateItemDto(
    int ConfiguracionTasasDetalleId,
    decimal ConfiguracionTasasDetalleMonto,
    bool ConfiguracionTasasDetalleAplicaservicio);

public record ClienteConfiguracionTarifaUpdateRequest(
    int CategoriaSelected,
    IReadOnlyList<ClienteConfiguracionTarifaUpdateItemDto> DetalleTasas);

public record ClienteConfiguracionTarifaAddRequest(
    int ServiciosId,
    decimal Monto,
    bool Aplicable);
