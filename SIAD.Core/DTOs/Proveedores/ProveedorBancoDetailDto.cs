namespace SIAD.Core.DTOs.Proveedores;

public record ProveedorBancoDetailDto(
    long Id,
    string Nombre,
    bool Activo,
    int ProveedoresAsignados);
