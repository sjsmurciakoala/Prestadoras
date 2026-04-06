using SIAD.Core.DTOs.Bancos;

namespace SIAD.Services.Bancos;

public interface IBancoConfiguracionTransaccionesService
{
    Task<IReadOnlyList<BancoConfiguracionTransaccionListDto>> GetAsync(
        BancoConfiguracionTransaccionFilterDto? filtro,
        CancellationToken ct = default);

    Task<BancoConfiguracionTransaccionEditDto?> GetByIdAsync(
        string tipoTransaccionId,
        CancellationToken ct = default);

    Task<BancoConfiguracionTransaccionEditDto> CreateAsync(
        BancoConfiguracionTransaccionEditDto dto,
        string user,
        CancellationToken ct = default);

    Task<BancoConfiguracionTransaccionEditDto> UpdateAsync(
        string tipoTransaccionId,
        BancoConfiguracionTransaccionEditDto dto,
        string user,
        CancellationToken ct = default);

    Task<bool> DeleteAsync(string tipoTransaccionId, CancellationToken ct = default);
}
