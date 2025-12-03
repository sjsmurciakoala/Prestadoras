using SIAD.Core.DTOs.Contabilidad;

namespace SIAD.Services.Contabilidad;

public interface ICompanyManagementService
{
    Task<CompanyCreationDto> CrearAsync(long tenantCompanyId, CompanyCreationDto dto, string usuario,
        CancellationToken ct = default);
}
