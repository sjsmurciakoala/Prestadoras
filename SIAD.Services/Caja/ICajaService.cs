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

    // Cajas físicas de la empresa con su disponibilidad (varias cajas
    // simultáneas — unificación cobranza F2)
    Task<IReadOnlyList<CajaFisicaDto>> ListarCajasAsync();

    // F3: la caja asignada al usuario (la apertura la resuelve el sistema)
    Task<MiCajaDto?> ObtenerMiCajaAsync(string usuario);

    // F3: mantenimiento de cajas + asignación de cajeros
    Task<IReadOnlyList<CajaAdminDto>> ListarCajasAdminAsync();
    Task<CajaResponseDto> GuardarCajaAsync(CajaGuardarDto dto, string usuario);
    Task<CajaResponseDto> AsignarCajeroAsync(AsignarCajeroDto dto, string usuario);
    Task<CajaResponseDto> QuitarCajeroAsync(string cajero);
}
