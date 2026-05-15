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
