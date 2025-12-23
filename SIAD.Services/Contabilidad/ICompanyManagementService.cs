using SIAD.Core.DTOs.Contabilidad;

namespace SIAD.Services.Contabilidad;

public interface ICompanyManagementService
{
    Task<CompanyCreationDto> CrearAsync(long tenantCompanyId, CompanyCreationDto dto, string usuario,
        CancellationToken ct = default);

    Task<CompanyCreationDto?> ObtenerAsync(long companyId, CancellationToken ct = default);

    Task<CompanyCreationDto> ActualizarAsync(long companyId, CompanyCreationDto dto, string usuario,
        CancellationToken ct = default);

    Task<bool> GuardarLogoAsync(long companyId, byte[] logoBytes, string usuario, CancellationToken ct = default);
    Task<(byte[] logoBytes, string? contentType)?> ObtenerLogoAsync(long companyId, CancellationToken ct = default);
}
