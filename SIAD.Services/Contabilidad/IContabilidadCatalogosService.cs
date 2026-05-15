using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.DTOs.Common;

namespace SIAD.Services.Contabilidad;

public interface IContabilidadCatalogosService
{
    Task<IReadOnlyList<PlanCuentaDto>> GetPlanCuentasAsync(CancellationToken cancellationToken = default);

    Task<long> SavePlanCuentaAsync(PlanCuentaUpsertDto request, CancellationToken cancellationToken = default);

    Task<PagedResult<PlanCuentaCatalogoItemDto>> GetPlanCuentasPagedAsync(
        PlanCuentaCatalogoFilterDto filter,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken cancellationToken = default);

    Task<PlanCuentaCatalogoLookupDto> BuscarPlanCuentaAsync(string cuenta, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CentroCostoDto>> GetCentrosCostoAsync(CancellationToken cancellationToken = default);

    Task<long> SaveCentroCostoAsync(CentroCostoUpsertDto request, CancellationToken cancellationToken = default);

    Task<bool> DeleteCentroCostoAsync(long costCenterId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TipoPartidaDto>> GetTiposPartidaAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DiarioDto>> GetDiariosAsync(CancellationToken cancellationToken = default);

    Task<long> SaveDiarioAsync(DiarioUpsertDto request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PeriodoContableDto>> GetPeriodosAsync(CancellationToken cancellationToken = default);

    Task<long> SavePeriodoAsync(PeriodoContableUpsertDto request, CancellationToken cancellationToken = default);

    Task<bool> ClosePeriodoAsync(long periodId, string user, CancellationToken cancellationToken = default);

    Task<PlanCuentasImportResult> ImportPlanCuentasAsync(Stream fileStream, string user, bool dryRun, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TipoTransaccionDto>> GetTiposTransaccionAsync(CancellationToken cancellationToken = default);
    Task<long> SaveTipoTransaccionAsync(TipoTransaccionUpsertDto request, CancellationToken cancellationToken = default);
    Task<bool> DeleteTipoTransaccionAsync(long typeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TipoTransaccionRuleDto>> GetTipoTransaccionRulesAsync(long typeId, CancellationToken cancellationToken = default);
    Task<long> SaveTipoTransaccionRuleAsync(TipoTransaccionRuleUpsertDto request, CancellationToken cancellationToken = default);
    Task<bool> DeleteTipoTransaccionRuleAsync(long ruleId, CancellationToken cancellationToken = default);

}
