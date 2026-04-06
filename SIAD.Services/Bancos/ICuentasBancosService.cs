using SIAD.Core.DTOs.Bancos;
using SIAD.Core.DTOs.Contabilidad;

namespace SIAD.Services.Bancos;

public interface ICuentasBancosService
{
    Task<IReadOnlyList<BancoCuentaListDto>> GetAsync(long companyId, CancellationToken ct = default);
    Task<IReadOnlyList<BancoCuentaConciliacionDto>> GetConciliacionAsync(
        long companyId,
        long bancoCuentaId,
        DateOnly fechaHasta,
        CancellationToken ct = default);
    Task<IReadOnlyList<BancoCuentaConciliacionDto>> GetConciliadasAsync(
        long companyId,
        long bancoCuentaId,
        DateOnly fechaDesde,
        DateOnly fechaHasta,
        CancellationToken ct = default);
    Task ConciliarAsync(
        long companyId,
        long bancoCuentaId,
        string user,
        DateOnly fechaConciliacion,
        IReadOnlyList<BancoCuentaConciliacionDto> movimientos,
        CancellationToken ct = default);
    Task<IReadOnlyList<CuentaContableLookupDto>> ListarCuentasContablesAsync(long companyId, CancellationToken ct = default);
    Task<BancoCuentaEditDto?> GetByIdAsync(long cuentaId, CancellationToken ct = default);
    Task<BancoCuentaEditDto> CreateAsync(BancoCuentaCreateDto dto, string user, CancellationToken ct = default);
    Task<BancoCuentaEditDto> UpdateAsync(long cuentaId, BancoCuentaEditDto dto, string user, CancellationToken ct = default);
    Task DeleteAsync(long cuentaId, CancellationToken ct = default);
}
