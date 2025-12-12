using SIAD.Core.DTOs.Contabilidad;

namespace SIAD.Services.Contabilidad;

public interface ICompanyManagementService
{
    Task<CompanyCreationDto> CrearAsync(long tenantCompanyId, CompanyCreationDto dto, string usuario,
        CancellationToken ct = default);

    Task<CompanyCreationDto?> ObtenerAsync(long companyId, CancellationToken ct = default);

    Task<CompanyCreationDto> ActualizarAsync(long companyId, CompanyCreationDto dto, string usuario,
        CancellationToken ct = default);

    /// <summary>
    /// Actualiza la URL del logo para una empresa.
    /// </summary>
    Task<string> ActualizarLogoAsync(long companyId, string logoUrl, string usuario, CancellationToken ct = default);
}
