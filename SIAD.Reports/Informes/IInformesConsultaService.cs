using SIAD.Core.DTOs.Informes;
using SIAD.Core.DTOs.Rutas;

namespace SIAD.Reports;

public interface IInformesConsultaService
{
    Task<PartidasInformeResultadoDto> ConsultarPartidasAsync(long companyId, PartidasInformeFiltroDto filtro, CancellationToken ct = default);

    Task<IReadOnlyList<ServicioCategoriaLookupDto>> ListarCategoriasServicioAsync(CancellationToken ct = default);

    Task<IReadOnlyList<CicloLookupDto>> ListarCiclosAsync(CancellationToken ct = default);

    Task<IReadOnlyList<UsuarioInformeLookupDto>> ListarUsuariosRecibosAsync(CancellationToken ct = default);
}
