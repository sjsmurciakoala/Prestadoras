namespace SIAD.Core.DTOs.Clientes;

public record ClienteTarifaDto(
    int ConfiguracionId,
    int? TarifaCatalogoId,
    string? TarifaNombre,
    decimal? PrecioBase,
    decimal? CargoFijo,
    decimal? MontoConfigurado,
    DateTime? FechaCreacion,
    DateTime? FechaModificacion,
    bool Activa);
