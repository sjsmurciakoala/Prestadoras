using SIAD.Core.DTOs.Aprobaciones;

namespace SIAD.Services.Aprobaciones;

/// <summary>
/// Motor de autorización por monto, transversal a los documentos (<c>DocumentosAprobacion</c>).
/// La orden de compra y la requisición son sus consumidores.
/// <para>
/// <b>La aprobación NO es en cascada</b> (regla del usuario, 2026-09-01). Cada tramo declara
/// CUÁNTO puede autorizar quien pertenece a él; un documento lo aprueba, de <b>una sola firma</b>,
/// cualquiera cuyo tramo cubra su total — aunque existan tramos más bajos que no han firmado.
/// Una orden de 75,000 la autoriza directamente quien llegue a 75,000. El monto <b>no</b> se
/// reparte entre varios aprobadores.
/// </para>
/// <para>
/// <b>Quién firma no es un parámetro.</b> La identidad sale de <c>ICurrentUserService</c>, no de
/// un <c>string user</c> del llamador: una autorización no debe poder atribuirse a un tercero.
/// </para>
/// <para>
/// <b>Apagado por defecto.</b> Con <c>cfg_aprobacion_control.modo = 0</c> el documento se aprueba
/// como siempre, de un clic, y nada de esto interviene.
/// </para>
/// </summary>
public interface IAprobacionService
{
    /// <summary>Configuración vigente del documento en la empresa actual.</summary>
    Task<AprobacionControlDto> ObtenerControlAsync(string documento, CancellationToken ct = default);

    /// <summary>Si este documento exige autorización antes de aprobarse.</summary>
    Task<bool> RequiereAprobacionAsync(string documento, CancellationToken ct = default);

    /// <summary>
    /// Tramos capaces de autorizar un monto, del límite más bajo al más alto (el sin tope al
    /// final). El primero es el <b>tramo mínimo suficiente</b>. Lista vacía = nadie puede
    /// autorizar ese monto.
    /// </summary>
    Task<IReadOnlyList<TramoAutorizacionDto>> ResolverAutorizadoresAsync(
        string documento, decimal total, CancellationToken ct = default);

    /// <summary>
    /// Abre el trámite: deja el documento esperando UNA autorización y lo registra en la bitácora.
    /// <para>
    /// <b>No exige que exista un aprobador capaz.</b> Si ningún tramo cubre el monto, el documento
    /// queda pendiente y la pantalla lo dice; bloquear el envío escondería el problema en vez de
    /// mostrarlo.
    /// </para>
    /// <para>Debe invocarse dentro de la transacción que cambia el estado del documento.</para>
    /// </summary>
    Task IniciarAsync(
        string documento, long documentoId, string? numero, decimal total, string creadoPor,
        short estadoAnterior, short estadoNuevo, CancellationToken ct = default);

    /// <summary>
    /// Autoriza el documento en nombre del usuario de la sesión, en un solo acto.
    /// <para>
    /// Valida que no esté ya resuelto, que el usuario tenga un tramo cuyo límite cubra el total y
    /// que no sea el creador (salvo <c>permite_autoaprobacion</c>). Registra quién autorizó, con
    /// qué límite, sobre qué monto, cuándo y el cambio de estado.
    /// </para>
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Si el documento ya fue resuelto, si el usuario no alcanza el monto o si es su propio
    /// documento. El mensaje está redactado para el usuario final.
    /// </exception>
    Task<FirmaResultadoDto> AutorizarAsync(
        string documento, long documentoId, decimal total, string? comentario,
        short estadoAnterior, short estadoNuevo, CancellationToken ct = default);

    /// <summary>
    /// Rechaza el documento. Exige la misma capacidad que autorizarlo: quien no podría aprobar el
    /// monto tampoco puede tumbarlo.
    /// </summary>
    Task RechazarAsync(
        string documento, long documentoId, decimal total, string motivo,
        short estadoAnterior, short estadoNuevo, CancellationToken ct = default);

    /// <summary>
    /// Devuelve el documento a borrador: borra la autorización si la hubo y lo registra. Lo
    /// borrado sobrevive en la bitácora.
    /// </summary>
    Task ReiniciarAsync(
        string documento, long documentoId, string motivo,
        short estadoAnterior, short estadoNuevo, CancellationToken ct = default);

    /// <summary>
    /// Registra un evento suelto en la bitácora (hoy: <c>ANULADA</c>), sin tocar la autorización.
    /// </summary>
    Task RegistrarEventoAsync(
        string documento, long documentoId, string? numero, string accion, string? comentario,
        short estadoAnterior, short estadoNuevo, CancellationToken ct = default);

    /// <summary>
    /// Estado de la autorización para la pantalla: si ya la dieron, si el usuario de la sesión
    /// puede darla, y —si nadie puede— que no hay aprobador con límite suficiente.
    /// </summary>
    Task<AprobacionEstadoDto> ObtenerEstadoAsync(
        string documento, long documentoId, decimal total, CancellationToken ct = default);

    /// <summary>
    /// Correos de quienes pueden autorizar este monto. Solo los aprobadores declarados como
    /// <b>usuario</b>: los de un rol viven en Identity y no se resuelven desde aquí.
    /// </summary>
    Task<IReadOnlyList<string>> CorreosAutorizadoresAsync(
        string documento, decimal total, CancellationToken ct = default);

    /// <summary>
    /// Órdenes de compra que el usuario de la sesión puede autorizar, más antiguas primero.
    /// Es la bandeja "Mis aprobaciones".
    /// </summary>
    Task<IReadOnlyList<PendienteAprobacionDto>> PendientesOrdenCompraAsync(CancellationToken ct = default);

    /// <summary>
    /// Por cada orden en aprobación, si existe alguien capaz de autorizarla. Una sola consulta
    /// para todo el listado, en vez de una por fila.
    /// </summary>
    Task<IReadOnlyList<CapacidadAprobacionDto>> CapacidadOrdenesCompraAsync(CancellationToken ct = default);
}
