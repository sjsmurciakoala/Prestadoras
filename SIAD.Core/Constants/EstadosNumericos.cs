namespace SIAD.Core.Constants;

// Constantes de estados numéricos. Sustituyen el uso de literales string en
// código C#. Sincronizadas con los catálogos `cfg_estado_*` y
// `cfg_codigo_conflicto` aplicados en BD el 2026-05-07.
//
// Convivencia: hoy las columnas string (`factura.estado`, `transaccion_abonado.estado`,
// etc.) y las numéricas (`*_id`) coexisten. Los nuevos writes/reads deben usar `*_id`.
// Los chequeos por string siguen vivos para no romper consumidores legacy hasta
// que se eliminen las columnas string post-25-may.

public static class EstadoDocumentoComercial
{
    public const short Activa               = 1;   // 'A'
    public const short Cobrada              = 2;   // 'C'
    public const short Anulada              = 3;   // 'N'
    public const short ParcialmenteAbonada  = 4;   // 'B' (unificación cobranza F1, 2026-07)

    public static string ToCodigo(short id) => id switch
    {
        Activa              => "A",
        Cobrada             => "C",
        Anulada             => "N",
        ParcialmenteAbonada => "B",
        _ => string.Empty
    };

    /// <summary>
    /// Descripción para el usuario final (fase 2 estados: los DTOs mandan
    /// texto, nunca la letra interna). Único punto de traducción en C#.
    /// </summary>
    public static string Descripcion(short? id) => id switch
    {
        Cobrada             => "PAGADA",
        Anulada             => "ANULADA",
        ParcialmenteAbonada => "ABONO PARCIAL",
        _                   => "PENDIENTE"
    };

    /// <summary>
    /// Puente del lado ESCRITOR (fase 3 lo retira): mientras la letra siga
    /// siendo la fuente de escritura, la entidad EF en memoria tiene el
    /// estado_id VIEJO tras asignar la letra (el trigger lo sincroniza en la
    /// BD, no en el objeto). Este helper deriva el id de la letra recién
    /// escrita sin releer la fila.
    /// </summary>
    public static short FromCodigo(string? codigo) => (codigo ?? string.Empty).Trim().ToUpperInvariant() switch
    {
        "C" => Cobrada,
        "N" => Anulada,
        "B" => ParcialmenteAbonada,
        _   => Activa
    };
}

// Estados de PAGO (adm_estado_pago) — catálogo separado del de documentos
// porque la letra 'A' significa "activo" en cargos y "anulado" en pagos.
// Aplica a transaccion_abonado.estado_pago_id (solo tipos 201/202) y, desde
// F2, a adm_pago.estado_id. Unificación cobranza F1 (2026-07).
public static class EstadoPago
{
    public const short Aplicado  = 1;  // 'C' en 201/202
    public const short Pendiente = 2;  // 'P' (recibo generado sin pagar)
    public const short Anulado   = 3;  // 'A' desde caja
    public const short Reversado = 4;  // 'A' con trans_aplicar 'WSBANCO:%'
}

// Tipos de movimiento comercial (adm_tipo_transaccion) — reemplaza el string
// libre transaccion_abonado.tipotransaccion. El espejo tipo_transaccion_id lo
// mantiene el trigger de BD desde el código legacy. Unificación cobranza F1.
public static class TipoTransaccion
{
    public const short CargoServicio = 1;   // códigos de servicio (AGUA_POTABLE, ...)
    public const short PagoCaja      = 2;   // legacy '201' (y 'PAGO%' migrados SIMAFI)
    public const short PagoBanco     = 3;   // legacy '202' con marker WSBANCO:
    public const short Abono         = 4;   // legacy '202' de caja
    public const short NotaCredito   = 5;   // legacy '205'
    public const short NotaDebito    = 6;   // legacy '206'
    public const short PlanTraslado  = 7;   // legacy 'PLAN'
    public const short PlanPrima     = 8;   // legacy 'PLAN-PR'
    public const short PlanCuota     = 9;   // legacy 'PLAN-CUOTA'
    public const short Ajuste        = 10;
    public const short SaldoInicial  = 11;  // legacy 'SALDO_ANTERIOR'
}

/// <summary>
/// Estados del plan de pago (cln_plan_pago_hdr.estado_id → adm_estado_plan,
/// unificación cobranza F6, 2026-07). Las cuotas (cln_plan_pago_dtl.estado_id)
/// usan EstadoDocumentoComercial: la cuota es un documento cobrable.
/// </summary>
public static class EstadoPlan
{
    public const short Activo     = 1;
    public const short Completado = 2;
    public const short Anulado    = 3;
}

public static class EstadoCorrelativoCai
{
    public const short PendingOffline = 1;
    public const short PendingSync    = 2;
    public const short Confirmado     = 3;
    public const short SyncConflict   = 4;
    public const short Anulado        = 5;
}

public static class EstadoBloqueCai
{
    public const short Reservado = 1;
    public const short Agotado   = 2;
    public const short Expirado  = 3;
}

public static class EstadoConflictoSync
{
    public const short Pendiente = 1;
    public const short Revisado  = 2;
    public const short Cerrado   = 3;
}

public static class CodigoConflicto
{
    public const short SyncConfirmError  = 1;
    public const short SyncConflictTotal = 2;
    public const short FacturaYaEmitida  = 3;
    public const short CaiVencido        = 4;
    public const short CaiNoEncontrado   = 5;
    public const short Otro              = 99;
}

public static class CondicionLectura
{
    public const short SinCondicion = 0;   // ''
    public const short Normal       = 1;   // 'N'
    public const short Minimo       = 2;   // 'MIN'
    public const short Pendiente    = 3;   // 'PND'
    public const short Promedio     = 4;   // 'PD'
    public const short Reposicion   = 5;   // 'R'
}

// Período comercial F7 (adm_periodo_comercial / adm_periodo_comercial_ciclo).
// Espejo legacy en historialmes: Abierto → cerrado='A'/cerrarperiodo='P';
// Cerrado → 'C'/'C'.
// (El período CONTABLE ya tiene su fuente única en EstadoPeriodoHelper /
// ContabilidadEnums.cs — no duplicar aquí.)
public static class EstadoPeriodoComercial
{
    public const short Abierto = 1;
    public const short Cerrado = 2;
}

// WS bancario F8 (ban_ws_pago.status_id) — contrato SIMAFI congelado:
// una referencia APLICADA se puede reversar una sola vez; una referencia
// REVERSADA no se puede volver a pagar.
public static class EstadoBanWsPago
{
    public const short Aplicado  = 1;
    public const short Reversado = 2;
}

// Orden de compra (alm_orden_compra.estado). CHECK en BD: estado IN (1,2,3,4,9).
// Borrador se edita; Aprobada ya no; la recepción la lleva a RecibidaParcial/Cerrada;
// Anulada solo antes de recibir.
public static class EstadoOrdenCompra
{
    public const short Borrador        = 1;
    public const short Aprobada        = 2;
    public const short RecibidaParcial = 3;
    public const short Cerrada         = 4;

    /// <summary>Rechazada desde Borrador. No llega a comprometer presupuesto.</summary>
    public const short Rechazada       = 5;

    /// <summary>
    /// Cancelada con recepciones parciales: lo pedido y no recibido ya no se va a recibir.
    /// Libera el saldo comprometido pendiente. Distinta de <see cref="Anulada"/>, que exige
    /// que no se haya recibido nada.
    /// </summary>
    public const short Cancelada       = 6;

    /// <summary>
    /// En aprobación: la orden salió de Borrador (ya NO es editable) pero todavía le faltan
    /// firmas de la escalera. Solo existe con la aprobación por niveles encendida
    /// (<c>cfg_aprobacion_control.modo = 1</c>); con el control apagado, Borrador pasa
    /// directo a <see cref="Aprobada"/> como siempre.
    /// </summary>
    public const short EnAprobacion    = 7;

    public const short Anulada         = 9;
}

// Nivel de la escalera de aprobación (alm_orden_compra_aprobacion.estado).
// CHECK en BD: estado IN (1,2,3,4). El nivel N solo pasa a Pendiente cuando el N-1 quedó
// Aprobado: la secuencia la impone el motor, no la base.
public static class EstadoAprobacionNivel
{
    /// <summary>Todavía no le toca: espera a que firme el nivel anterior.</summary>
    public const short Bloqueado = 1;

    /// <summary>Firmable ahora por cualquiera de sus aprobadores.</summary>
    public const short Pendiente = 2;

    public const short Aprobado  = 3;
    public const short Rechazado = 4;
}

// Recepción de compra (alm_compra_hdr.estado). CHECK en BD: estado IN (1,9).
// Una factura recibida no tiene estados intermedios: se registra y, si estuvo mal, se anula
// (y el kardex se corrige con REVERSA, nunca con UPDATE).
public static class EstadoRecepcionCompra
{
    public const short Registrada = 1;
    public const short Anulada    = 9;
}

// Movimiento de almacén (alm_movimiento_hdr.estado). CHECK en BD: estado IN (1,9).
// Mismo criterio que la recepción de compra: el documento nace posteado, sin estados
// intermedios. No hay Borrador porque un movimiento sin asiento no significa nada, y no
// hay flujo de aprobación (eso sería la Fase 6, requisición → descargo).
public static class EstadoMovimientoAlmacen
{
    public const short Registrado = 1;

    /// <summary>Traslado con recepción: enviado, con algo aún por recibir. Espejo del CHECK ampliado.</summary>
    public const short EnTransito = 2;

    /// <summary>Traslado con todo recibido. Un traslado directo nace aquí (recepción automática).</summary>
    public const short Recibido = 3;

    public const short Anulado = 9;
}

// Cabecera de requisición (alm_requisicion_hdr.estado). CHECK en BD: estado IN (1,2,3,4,5,6,8,9).
// La máquina de estados vive en el servicio (Fase 6.2); los estados 4/5 son DERIVADOS de
// cantidad_despachada por el descargo, no se capturan a mano.
public static class EstadoRequisicionHdr
{
    public const short Borrador          = 1;
    public const short EnRevision        = 2;   // aprobable
    public const short Aprobada          = 3;
    public const short DespachadaParcial = 4;   // derivado
    public const short DespachadaTotal   = 5;   // derivado
    public const short CerradaEnOC       = 6;   // solo reabastecimiento (fuera de alcance 1ª entrega)
    public const short Rechazada         = 8;
    public const short Anulada           = 9;
}

// Cabecera de descargo (alm_descargo_hdr.estado). CHECK en BD: estado IN (1,9).
// El descargo nace posteado; se corrige por anulación con reversa, nunca por UPDATE del kardex.
public static class EstadoDescargoHdr
{
    public const short Registrado = 1;
    public const short Anulado    = 9;
}

// Retención fiscal (prv_retencion_hdr.estado_id). CHECK en BD: estado_id IN (1,9).
// La retención nace Vigente; se corrige por anulación del pago (estado_id=9 + motivo), nunca por
// DELETE. El reverso de la partida lo hace el motor (F0); F4 solo marca el hdr.
public static class EstadoRetencion
{
    public const short Vigente = 1;
    public const short Anulada = 9;

    /// <summary>Descripción para el usuario final. Único punto de traducción en C#.</summary>
    public static string Descripcion(short? id) => id switch
    {
        Anulada => "ANULADA",
        _       => "VIGENTE"
    };
}

// Origen del pago retenido (prv_retencion_hdr.origen). CHECK en BD: coherencia origen↔referencia
// (origen=1 ⇒ numero_orden y sin cxp; origen=2 ⇒ cxp_id y sin orden). Permite que un mismo libro
// fiscal (constancia + declaración) cubra las retenciones de compromisos y las de facturas de compra.
public static class OrigenRetencion
{
    public const short Compromiso = 1;   // orden de pago directo (prv_compromiso_hdr)
    public const short Compra     = 2;   // factura de compra (alm_compra_cxp)

    /// <summary>Etiqueta legible del origen (nunca el código).</summary>
    public static string Descripcion(short? id) => id switch
    {
        Compra => "Compra",
        _      => "Compromiso"
    };
}

// Período de evaluación de proveedores (prv_evaluacion_periodo.estado). CHECK en BD: IN (1,2).
// Abierto se puede recalcular cuantas veces se quiera; cerrado queda congelado como historia.
public static class EstadoEvaluacionPeriodo
{
    public const short Abierto = 1;
    public const short Cerrado = 2;

    public static string Descripcion(short? id) => id switch
    {
        Cerrado => "Cerrado",
        _       => "Abierto"
    };
}

// Evaluación de un proveedor en un período (prv_evaluacion_hdr.estado). CHECK en BD: IN (1,2).
public static class EstadoEvaluacionProveedor
{
    public const short Calculada = 1;
    public const short Cerrada   = 2;

    public static string Descripcion(short? id) => id switch
    {
        Cerrada => "Cerrada",
        _       => "Calculada"
    };
}

// Origen del criterio (prv_evaluacion_criterio.origen). CHECK en BD: IN (1,2).
// Automático lo calcula fn_prv_evaluacion_metricas; manual lo captura el comprador en la ficha.
public static class OrigenCriterioEvaluacion
{
    public const short Automatico = 1;
    public const short Manual     = 2;

    public static string Descripcion(short? id) => id switch
    {
        Manual => "Manual",
        _      => "Automático"
    };
}

// Métricas que alimentan a los criterios automáticos (prv_evaluacion_criterio.metrica).
// Son las columnas que devuelve fn_prv_evaluacion_metricas: si aquí se agrega una, hay que
// agregarla también en la función y en el servicio.
public static class MetricaEvaluacion
{
    public const string Entrega    = "ENTREGA";
    public const string Completo   = "COMPLETO";
    public const string Precio     = "PRECIO";
    public const string Calidad    = "CALIDAD";
    public const string Documento  = "DOCUMENTO";
}

// Incidencia detectada al recibir (prv_recepcion_incidencia.tipo). CHECK en BD: IN (1,2,3,4,9).
public static class TipoIncidenciaRecepcion
{
    public const short Devolucion     = 1;
    public const short Dano           = 2;
    public const short Especificacion = 3;
    public const short Faltante       = 4;
    public const short Otro           = 9;

    public static string Descripcion(short? id) => id switch
    {
        Devolucion     => "Devolución",
        Dano           => "Daño",
        Especificacion => "Especificación distinta",
        Faltante       => "Faltante",
        Otro           => "Otro",
        _              => "—"
    };
}
