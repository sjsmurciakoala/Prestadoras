namespace SIAD.Core.DTOs.Proveedores;

public record ProveedorBancoListItemDto(
    long Id,
    string Nombre,
    bool Activo,
    int ProveedoresAsignados);
