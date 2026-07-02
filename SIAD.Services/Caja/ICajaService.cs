using System.Collections.Generic;
using System.Threading.Tasks;
using SIAD.Core.DTOs.Caja;

namespace SIAD.Services.Caja;

public interface ICajaService
{
    // Sesión activa del usuario (null si no tiene caja abierta)
    Task<SesionCajaDto?> ObtenerSesionActivaAsync(string usuario);

    // Apertura
    Task<CajaResponseDto> AbrirCajaAsync(AbrirCajaRequestDto request);

    // Cierre
    Task<CajaResponseDto> CerrarCajaAsync(CerrarCajaRequestDto request);

    // Resumen de transacciones de la sesión
    Task<ResumenCajaDto?> ObtenerResumenAsync(int sesionId);

    // Historial de sesiones cerradas del usuario en la empresa
    Task<IReadOnlyList<HistorialCierreDto>> ListarHistorialAsync(string usuario);
}
