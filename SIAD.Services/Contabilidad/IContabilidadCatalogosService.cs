using SIAD.Core.DTOs.Contabilidad;

namespace SIAD.Services.Contabilidad;

public interface IContabilidadCatalogosService
{
    Task<IReadOnlyList<PlanCuentaDto>> GetPlanCuentasAsync(CancellationToken cancellationToken = default);

    Task<long> SavePlanCuentaAsync(PlanCuentaUpsertDto request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CentroCostoDto>> GetCentrosCostoAsync(CancellationToken cancellationToken = default);

    Task<long> SaveCentroCostoAsync(CentroCostoUpsertDto request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DiarioDto>> GetDiariosAsync(CancellationToken cancellationToken = default);

    Task<long> SaveDiarioAsync(DiarioUpsertDto request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PeriodoContableDto>> GetPeriodosAsync(CancellationToken cancellationToken = default);

    Task<long> SavePeriodoAsync(PeriodoContableUpsertDto request, CancellationToken cancellationToken = default);

    Task<bool> ClosePeriodoAsync(long periodId, string user, CancellationToken cancellationToken = default);
}
