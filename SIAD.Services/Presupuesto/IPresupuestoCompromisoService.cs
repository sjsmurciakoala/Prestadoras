using SIAD.Core.DTOs.Presupuesto;

namespace SIAD.Services.Presupuesto;

/// <summary>
/// Control presupuestario de los documentos de compra: compromete al aprobar la orden y libera el
/// saldo pendiente al anularla o cancelarla.
/// <para>
/// Es una capa <b>delgada</b> sobre los procedimientos de PostgreSQL (<c>sp_pst_*</c>): arma
/// parámetros, corre dentro de la transacción del documento y traduce el error de negocio de la
/// base a <see cref="InvalidOperationException"/>. La lógica —validación, locks, kardex— vive en la
/// base, no aquí (ver <c>Database/2026-08-27_pst_compromiso_03_procedimientos.sql</c>).
/// </para>
/// <para>
/// <b>Apagado por defecto.</b> Si <c>cfg_presupuesto_control.modo = 0</c> para la empresa, todos los
/// métodos son no-op y devuelven vacío: el comportamiento del portal no cambia.
/// </para>
/// </summary>
public interface IPresupuestoCompromisoService
{
    /// <summary>
    /// Compromete el presupuesto de una orden de compra al aprobarla. Valida el disponible
    /// <b>por partida</b> (consolidando los renglones que caen en la misma cuenta), no por el total
    /// de la orden.
    /// <para>
    /// Debe invocarse <b>dentro de la transacción</b> que aprueba la orden: si lanza, el cambio de
    /// estado se revierte con él y la orden se queda en Borrador.
    /// </para>
    /// <para>
    /// Es idempotente a nivel de documento: reintentar la aprobación de una orden ya comprometida
    /// no vuelve a comprometer ni falla.
    /// </para>
    /// </summary>
    /// <returns>
    /// Avisos en modo Advertencia; lista vacía si todo entró holgado o si el control está apagado.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// En modo Bloqueo, cuando alguna partida no alcanza, no tiene presupuesto vigente, o hay
    /// renglones sin cuenta presupuestaria. El mensaje está redactado para el usuario final.
    /// </exception>
    Task<IReadOnlyList<PresupuestoAvisoDto>> ComprometerOrdenCompraAsync(
        int ordenCompraId, string numero, DateOnly fecha, string usuario, string? usuarioAprobo,
        CancellationToken ct = default);

    /// <summary>
    /// Libera el <b>saldo pendiente</b> del compromiso de una orden (anulación o cancelación). No
    /// libera el total pedido: una orden de 100,000 con 60,000 ya recibidos libera 40,000.
    /// <para>
    /// No exige que el presupuesto esté vigente ni aprobado — devolver presupuesto nunca debe estar
    /// bloqueado— y devuelve el importe al presupuesto de la <b>fecha original</b> del compromiso.
    /// </para>
    /// </summary>
    /// <returns>Total liberado. Cero si la orden no tenía compromisos o el control está apagado.</returns>
    Task<decimal> LiberarOrdenCompraAsync(
        int ordenCompraId, string motivo, string usuario, CancellationToken ct = default);

    /// <summary>
    /// Ajusta el compromiso de una orden <b>ya aprobada</b> cuya distribución cambió: valida y
    /// compromete <b>solo el aumento</b>, o libera <b>solo la disminución</b>, partida por partida.
    /// Rechaza reducir una partida por debajo de lo que ya se devengó en ella.
    /// <para>
    /// <b>Todavía no lo llama nadie:</b> el portal no permite editar una orden aprobada
    /// (<c>ActualizarAsync</c> exige Borrador). Queda expuesto y probado para cuando se defina ese
    /// flujo de compras, que es una decisión aparte de este control.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<PresupuestoAvisoDto>> AjustarCompromisoOrdenCompraAsync(
        int ordenCompraId, string numero, string motivo, string usuario, CancellationToken ct = default);

    /// <summary>
    /// Devenga la factura de compra: convierte compromiso en ejecutado. <b>El disponible no cambia</b>
    /// cuando la factura viene contra una orden — es lo que evita el doble conteo.
    /// <para>
    /// Lo que exceda al compromiso (variación de precio, flete no previsto) se devenga directo y
    /// <b>sí valida disponible</b>, con la tolerancia configurada como margen exento. Una compra sin
    /// orden consume disponible según <c>permite_devengo_sin_oc</c>.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<PresupuestoAvisoDto>> DevengarFacturaAsync(
        int compraHdrId, string numero, int? ordenCompraId, DateOnly fecha, string usuario,
        CancellationToken ct = default);

    /// <summary>
    /// Revierte el devengo al anular la factura, con la <b>fecha original</b>. Si la orden sigue
    /// abierta (Aprobada o Recibida parcial) restituye el compromiso para poder volver a recibir;
    /// si ya está cerrada, cancelada o anulada, el importe vuelve al disponible.
    /// </summary>
    Task<decimal> RevertirDevengoFacturaAsync(
        int compraHdrId, string motivo, string usuario, CancellationToken ct = default);

    /// <summary>
    /// Registra el pago de un abono a la CxP, prorrateado entre las partidas que devengó la factura.
    /// <b>No altera el disponible</b>: el pago es tesorería, se registra para el reporte de ejecución.
    /// </summary>
    Task<decimal> RegistrarPagoAsync(
        long abonoId, string numero, int compraHdrId, DateOnly fecha, decimal monto, string usuario,
        CancellationToken ct = default);

    /// <summary>Revierte el pago de un abono anulado.</summary>
    Task<decimal> RevertirPagoAsync(
        long abonoId, string motivo, string usuario, CancellationToken ct = default);

    /// <summary>
    /// Afecta el <b>ejecutado</b> de un documento que consume presupuesto sin pasar por un
    /// compromiso previo: hoy, los compromisos a proveedor (OPD).
    /// <para>
    /// A diferencia del resto del motor, <b>valida siempre</b>, incluso con el módulo apagado: es
    /// una regla que existía antes de este control y apagarlo no debe desactivarla. El modo solo
    /// decide si el comprometido por las órdenes de compra entra en la base del disponible.
    /// </para>
    /// </summary>
    /// <param name="direccion">+1 consume presupuesto · −1 lo devuelve.</param>
    Task AfectarEjecutadoAsync(
        string modulo, string documentoTipo, long documentoId, string? documentoNumero,
        DateOnly fecha, string usuario, short direccion, bool exigeAprobado,
        IReadOnlyCollection<(string Cuenta, decimal Monto)> lineas,
        CancellationToken ct = default);

    /// <summary>
    /// Cómo quedaría el presupuesto si se aprobara la orden, <b>sin comprometerla</b>. Es una
    /// lectura pura y <b>sin locks</b>: sirve para el panel de la pantalla, no para decidir.
    /// <para>
    /// La decisión la sigue tomando <see cref="ComprometerOrdenCompraAsync"/> bajo
    /// <c>FOR UPDATE</c>; entre esta consulta y la aprobación el disponible puede haber cambiado.
    /// Por eso el panel informa y no reemplaza la validación.
    /// </para>
    /// </summary>
    Task<PresupuestoPrevioDto> ConsultarPrevioOrdenCompraAsync(
        int ordenCompraId, DateOnly fecha, CancellationToken ct = default);
}
