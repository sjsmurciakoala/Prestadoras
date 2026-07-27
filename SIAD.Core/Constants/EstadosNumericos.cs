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
