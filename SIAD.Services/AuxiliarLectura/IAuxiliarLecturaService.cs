using SIAD.Core.DTOs.AuxiliarLectura;

namespace SIAD.Services.AuxiliarLectura;

public interface IAuxiliarLecturaService
{
    Task<AuxiliarLecturaPeriodoDto?> GetPeriodoActualAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AuxiliarLecturaDto>> SearchAsync(AuxiliarLecturaFilterDto filtro, CancellationToken ct = default);
    Task<bool> GenerarPeriodoAsync(int anio, int mes, string usuario, CancellationToken ct = default);
    Task<bool> CerrarPeriodoAsync(int anio, int mes, CancellationToken ct = default);
    Task<bool> EliminarPeriodoAsync(int anio, int mes, CancellationToken ct = default);
    Task RegistrarLecturasMasivasAsync(LecturaMasivaDto payload, CancellationToken ct = default);
}
