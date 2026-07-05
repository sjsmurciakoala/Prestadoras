using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SIAD.Core.DTOs.MobileApi;

namespace SIAD.Services.MobileApi;

/// <summary>
/// Servicio de la API móvil de lectores (apc.MobileApi, plan app_lectores L3).
/// Paridad FUNCIONAL con el WS WCF viejo: llama exactamente los mismos SPs V3
/// (sp_medidores_por_ruta_ws, sp_adm_generar_snapshot_offline_cliente_lectura,
/// sp_lectura_v3, sp_informacion_ciclo, CAI sync) — la lógica de negocio vive
/// en los SPs; este servicio es el puente Dapper + la orquestación (idéntica a
/// la del WS). La autenticación es propia (adm_lector_credencial/_sesion), el
/// WS viejo y usuarioapc quedan intactos para la app Java.
/// </summary>
public interface ILectoresMobileService
{
    /// <summary>Valida el token bearer y devuelve el contexto de sesión (tenant + ruta). Null si inválido/expirado.</summary>
    Task<LectorSesionContexto?> ValidarSesionAsync(string? token, CancellationToken ct = default);

    /// <summary>Login del lector: valida código+clave (bcrypt) global, emite un token con expiración. Null si credencial inválida.</summary>
    Task<LoginRespuesta?> LoginAsync(LoginRequest request, TimeSpan duracion, CancellationToken ct = default);

    /// <summary>Revoca la sesión del token (logout). No falla si el token no existe.</summary>
    Task LogoutAsync(string? token, CancellationToken ct = default);

    /// <summary>Ciclo pendiente de la ruta (CTE V3 + fallback sp_informacion_ciclo).</summary>
    Task<InformacionCicloDto> GetCicloAsync(string ruta, CancellationToken ct = default);

    /// <summary>Medidores de la ruta para el ciclo/período (sp_medidores_por_ruta_ws).</summary>
    Task<List<MedidorDto>> GetRutaAsync(string ruta, int ciclo, int anio, int mes, CancellationToken ct = default);

    /// <summary>Snapshot offline V3 de la ruta: medidores + snapshot por cliente + bloque CAI inyectado.</summary>
    Task<OfflineSnapshotRutaDto> GetSnapshotAsync(string ruta, int ciclo, int anio, int mes, CancellationToken ct = default);

    /// <summary>
    /// Sube una lectura (sp_lectura_v3, idempotente por UUID) con el flujo CAI
    /// del WS (prepare/confirm, preflight de total, conflictos). companyId es el
    /// del tenant de la sesión (A6): se valida contra el del cliente.
    /// </summary>
    Task<LecturaV3Respuesta> ActualizarLecturaAsync(LecturaV3Request request, long companyId, CancellationToken ct = default);
}
