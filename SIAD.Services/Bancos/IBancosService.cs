using SIAD.Core.DTOs.Bancos;

namespace SIAD.Services.Bancos;

public interface IBancosService
{
    Task<IReadOnlyList<BancoListDto>> GetAsync(BancoFilterDto? filtro, CancellationToken ct = default);
    Task<BancoEditDto?> GetByIdAsync(long bancoId, CancellationToken ct = default);
    Task<BancoEditDto> CreateAsync(BancoCreateDto dto, string user, CancellationToken ct = default);
    Task<BancoEditDto> UpdateAsync(long bancoId, BancoEditDto dto, string user, CancellationToken ct = default);
    Task<bool> DeleteAsync(long bancoId, CancellationToken ct = default);
}
