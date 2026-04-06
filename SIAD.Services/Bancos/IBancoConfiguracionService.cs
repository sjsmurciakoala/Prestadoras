using SIAD.Core.DTOs.Bancos;

namespace SIAD.Services.Bancos;

public interface IBancoConfiguracionService
{
    Task<BancoConfiguracionDto> ObtenerAsync(long companyId, CancellationToken ct = default);
    Task<BancoConfiguracionDto> GuardarAsync(long companyId, BancoConfiguracionDto dto, string user, CancellationToken ct = default);
}
