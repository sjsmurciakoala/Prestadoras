using SIAD.Core.DTOs.Informes;

namespace SIAD.Reports;

public interface IInformesConsultaService
{
    Task<PartidasInformeResultadoDto> ConsultarPartidasAsync(long companyId, PartidasInformeFiltroDto filtro, CancellationToken ct = default);
}
