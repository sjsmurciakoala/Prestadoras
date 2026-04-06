using SIAD.Core.DTOs.Informes;

namespace SIAD.Reports;

public interface IInformesCatalogoService
{
    Task<IReadOnlyList<InformeCatalogoItemDto>> ListarAsync(long companyId, CancellationToken ct = default);
}
