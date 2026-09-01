using SIAD.Core.DTOs.Aprobaciones;

namespace SIAD.Services.Aprobaciones;

/// <summary>
/// Mantenimiento de la aprobación por niveles: el interruptor por documento, la escalera de
/// montos y quién firma cada nivel. Es la cara configurable del motor
/// (<see cref="IAprobacionService"/>), y lo que evita tener que desplegar código para cambiar
/// quién autoriza una compra.
/// <para>
/// Multiempresa: todo se resuelve contra la empresa de la sesión, nunca contra un id recibido.
/// </para>
/// </summary>
public interface IAprobacionConfigService
{
    /// <summary>
    /// Configuración completa de un documento: interruptor, niveles, aprobadores y las
    /// advertencias de una escalera que no podría operar (nivel sin aprobadores, control
    /// encendido sin niveles).
    /// </summary>
    Task<AprobacionConfiguracionDto> ObtenerAsync(string documento, CancellationToken ct = default);

    /// <summary>Enciende o apaga el control y fija la autoaprobación (D5) de un documento.</summary>
    Task GuardarControlAsync(
        string documento, short modo, bool permiteAutoaprobacion, CancellationToken ct = default);

    /// <summary>
    /// Crea o actualiza un nivel. Valida que el umbral <b>crezca con el nivel</b>: una escalera
    /// donde el nivel 2 pide menos que el 1 es incoherente y dejaría documentos sin firmar.
    /// </summary>
    /// <exception cref="InvalidOperationException">Escalera incoherente o nivel repetido.</exception>
    Task<AprobacionNivelConfigDto> GuardarNivelAsync(
        string documento, AprobacionNivelConfigDto dto, CancellationToken ct = default);

    /// <summary>
    /// Elimina un nivel y, en cascada, sus aprobadores. Los documentos que ya están circulando no
    /// se ven afectados: su flujo guarda un snapshot de la descripción del nivel.
    /// </summary>
    Task<bool> EliminarNivelAsync(int nivelId, CancellationToken ct = default);

    /// <summary>
    /// Agrega un aprobador a un nivel: usuario nominal o rol. El usuario se guarda normalizado a
    /// minúsculas, que es como compara el motor.
    /// </summary>
    Task<AprobacionAprobadorConfigDto> AgregarAprobadorAsync(
        int nivelId, AprobacionAprobadorConfigDto dto, CancellationToken ct = default);

    /// <summary>Quita un aprobador de su nivel.</summary>
    Task<bool> EliminarAprobadorAsync(int aprobadorId, CancellationToken ct = default);
}
