using SIAD.Core.DTOs.Aprobaciones;

namespace SIAD.Services.Aprobaciones;

/// <summary>
/// Motor de aprobación por niveles, transversal a los documentos (<c>DocumentosAprobacion</c>).
/// La orden de compra es el primer consumidor; factura, pago a proveedor y requisición se
/// enganchan después sin tocar este contrato.
/// <para>
/// <b>Quién firma no es un parámetro.</b> La identidad sale de <c>ICurrentUserService</c>, no de
/// un <c>string user</c> del llamador: una firma es un acto de autorización y no debe poder
/// atribuirse a un tercero pasando otro nombre. El documento sigue sellando <c>usuariomodificacion</c>
/// por su cuenta.
/// </para>
/// <para>
/// <b>Apagado por defecto.</b> Si <c>cfg_aprobacion_control.modo = 0</c> para la empresa y el
/// documento, <see cref="RequiereAprobacionAsync"/> devuelve false y el documento se aprueba como
/// siempre, de un clic. Encenderlo es una decisión deliberada por empresa.
/// </para>
/// <para>
/// <b>Sin LINQ</b> (regla <c>hodsoft-sin-linq</c>): todo el acceso a datos va por las funciones
/// <c>fn_apr_*</c> y por SQL explícito sobre las tablas <c>cfg_aprobacion_*</c>.
/// </para>
/// </summary>
public interface IAprobacionService
{
    /// <summary>Configuración vigente del documento en la empresa actual (modo y autoaprobación).</summary>
    Task<AprobacionControlDto> ObtenerControlAsync(string documento, CancellationToken ct = default);

    /// <summary>
    /// Si este documento debe pasar por la escalera. Atajo de <see cref="ObtenerControlAsync"/>
    /// para el enganche, que lo consulta en cada transición.
    /// </summary>
    Task<bool> RequiereAprobacionAsync(string documento, CancellationToken ct = default);

    /// <summary>
    /// Niveles que exige un monto (D1, acumulativa). Lista vacía si el control está apagado.
    /// Cada nivel informa si tiene aprobadores activos.
    /// </summary>
    Task<IReadOnlyList<NivelExigidoDto>> ResolverEscaleraAsync(
        string documento, decimal total, CancellationToken ct = default);

    /// <summary>
    /// Abre el flujo: materializa un renglón por nivel exigido (el primero Pendiente, el resto
    /// Bloqueados) y registra <c>ENVIADA</c> en la bitácora.
    /// <para>Debe invocarse <b>dentro de la transacción</b> que cambia el estado del documento.</para>
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Si el control está apagado, si ya hay un flujo abierto, si la escalera no exige ningún
    /// nivel para ese monto, o si algún nivel exigido se quedó sin aprobadores activos. El
    /// mensaje está redactado para el usuario final.
    /// </exception>
    Task IniciarAsync(
        string documento, long documentoId, string? numero, decimal total, string creadoPor,
        CancellationToken ct = default);

    /// <summary>
    /// Firma el nivel pendiente en nombre del usuario de la sesión.
    /// <para>
    /// Valida, en este orden: que haya flujo abierto, que el nivel esté pendiente, que el usuario
    /// sea aprobador elegible (usuario o rol), que no sea el creador —salvo
    /// <c>permite_autoaprobacion</c>— y que no haya firmado ya otro nivel del mismo documento.
    /// </para>
    /// </summary>
    /// <returns>
    /// Qué pasó: el nivel firmado, si fue la <b>primera</b> firma (la que compromete presupuesto,
    /// D2), si el flujo quedó completo y cuál nivel sigue.
    /// </returns>
    /// <exception cref="InvalidOperationException">Cuando alguna de las validaciones falla.</exception>
    Task<FirmaResultadoDto> FirmarAsync(
        string documento, long documentoId, string? comentario, CancellationToken ct = default);

    /// <summary>
    /// Rechaza el documento desde el nivel pendiente. Marca ese nivel como Rechazado, deja el
    /// resto como está y registra <c>RECHAZADA</c>. El documento queda terminal: quien lo cambie
    /// de estado es el enganche, que además libera el presupuesto comprometido.
    /// </summary>
    Task RechazarAsync(
        string documento, long documentoId, string motivo, CancellationToken ct = default);

    /// <summary>
    /// Devuelve el documento a borrador: <b>borra todas las firmas</b> (D4, sin excepción) y
    /// registra <c>DEVUELTA</c>. Lo borrado sobrevive en la bitácora.
    /// </summary>
    Task ReiniciarAsync(
        string documento, long documentoId, string motivo, CancellationToken ct = default);

    /// <summary>
    /// Registra un evento suelto en la bitácora (hoy: <c>ANULADA</c>), sin tocar el flujo.
    /// Para que anular un documento en aprobación deje rastro.
    /// </summary>
    Task RegistrarEventoAsync(
        string documento, long documentoId, string? numero, string accion, string? comentario,
        CancellationToken ct = default);

    /// <summary>
    /// Estado del flujo de un documento para la pantalla: los niveles con su firma, cuál está
    /// pendiente, cuántos van y si el usuario de la sesión puede firmar ahora.
    /// </summary>
    Task<AprobacionEstadoDto> ObtenerEstadoAsync(
        string documento, long documentoId, CancellationToken ct = default);

    /// <summary>
    /// Órdenes de compra esperando la firma del usuario de la sesión, más antiguas primero.
    /// Es la bandeja "Mis aprobaciones".
    /// </summary>
    Task<IReadOnlyList<PendienteAprobacionDto>> PendientesOrdenCompraAsync(CancellationToken ct = default);

    /// <summary>
    /// Correos de los aprobadores del nivel que está esperando firma en un documento.
    /// <para>
    /// Solo devuelve los aprobadores declarados como <b>usuario</b> (su valor es el user_name, que
    /// en este portal es el correo). Los declarados por <b>rol</b> no se pueden resolver desde
    /// aquí: los miembros de un rol viven en ASP.NET Identity, en otro contexto. Para esos, el
    /// aviso queda en la copia al área.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<string>> CorreosNivelPendienteAsync(
        string documento, long documentoId, CancellationToken ct = default);

    /// <summary>
    /// Progreso de las órdenes que están en la escalera: cuántos niveles firmaron y cuántos
    /// exige cada una. Una sola consulta para todo el listado, en vez de una por fila.
    /// <para>
    /// Solo devuelve las que tienen flujo abierto, que son pocas: el listado compone con esto el
    /// badge «En aprobación (2 de 3)» y deja el resto de filas como están.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<ProgresoAprobacionDto>> ProgresoOrdenesCompraAsync(CancellationToken ct = default);
}
