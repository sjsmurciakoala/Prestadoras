using SIAD.Core.DTOs.Configuracion;

namespace SIAD.Services.Aprobaciones;

/// <summary>
/// Avisos por correo del flujo de aprobación: al aprobador que tiene que firmar y al comprador
/// cuando su documento se resuelve.
/// <para>
/// <b>Nunca lanza.</b> Un correo que no sale no puede tumbar una firma ya guardada: todos los
/// métodos devuelven un resultado <c>Omitido</c> o fallido en vez de propagar la excepción, y los
/// llamadores los invocan <b>después</b> de confirmar la transacción del documento.
/// </para>
/// </summary>
public interface IAprobacionNotificador
{
    /// <summary>
    /// Avisa a los aprobadores del nivel que quedó pendiente en una orden de compra: hay algo
    /// esperando su firma.
    /// </summary>
    Task<CorreoEnvioResultado> NotificarPendienteOrdenCompraAsync(
        int ordenCompraId, string numero, string proveedor, decimal total, string nivel,
        CancellationToken ct = default);

    /// <summary>
    /// Avisa a quien creó la orden que su documento quedó resuelto: aprobado, rechazado o devuelto
    /// a borrador. Es el aviso que cierra el círculo para el comprador.
    /// </summary>
    Task<CorreoEnvioResultado> NotificarResueltaOrdenCompraAsync(
        string? creador, string numero, string proveedor, decimal total, string desenlace,
        string? motivo, CancellationToken ct = default);
}
