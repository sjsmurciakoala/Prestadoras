using SIAD.Core.DTOs.Informes;

namespace SIAD.Reports;

public interface IReportesDisenoService
{
    Task<IReadOnlyList<ReporteDisenoCatalogoItemDto>> ListarAsync(long companyId, CancellationToken ct = default);

    Task<ReporteDisenoDetalleDto?> ObtenerAsync(long companyId, string codigo, CancellationToken ct = default);

    Task<ReporteDisenoDetalleDto> CrearAsync(long companyId, ReporteDisenoCreateDto dto, string actor, CancellationToken ct = default);

    Task<ReporteDisenoDetalleDto> PublicarAsync(long companyId, string codigo, string actor, CancellationToken ct = default);

    Task EliminarAsync(long companyId, string codigo, CancellationToken ct = default);
}
