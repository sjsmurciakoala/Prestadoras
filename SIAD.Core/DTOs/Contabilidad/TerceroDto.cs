namespace SIAD.Core.DTOs.Contabilidad;

public record TerceroDto(
    long ThirdPartyId,
    string Code,
    string Name,
    string? Description,
    string? TaxId,
    string Category,
    bool IsSupplier,
    bool IsCustomer,
    string Status,
    DateTime CreatedAt,
    string CreatedBy);

public record TerceroUpsertDto(
    string Code,
    string Name,
    string? Description,
    string? TaxId,
    string Category,
    bool IsSupplier,
    bool IsCustomer,
    string Status,
    string User,
    long? ThirdPartyId = null);

public record SincronizarTercerosResultDto(
    int ProveedoresSincronizados,
    int ClientesSincronizados,
    int ProveedoresOmitidos,
    int ClientesOmitidos,
    int Errores,
    List<string> Mensajes);
