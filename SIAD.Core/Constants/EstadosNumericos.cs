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
    public const short Activa  = 1;   // 'A'
    public const short Cobrada = 2;   // 'C'
    public const short Anulada = 3;   // 'N'

    public static string ToCodigo(short id) => id switch
    {
        Activa  => "A",
        Cobrada => "C",
        Anulada => "N",
        _ => string.Empty
    };
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
    public const short Anulada         = 9;
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
