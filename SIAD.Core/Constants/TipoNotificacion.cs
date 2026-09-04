namespace SIAD.Core.Constants;

/// <summary>
/// Catálogo cerrado de tipos de notificación (áreas). Espejo del CHECK
/// <c>ck_cfg_notificacion_tipo</c> — si se agrega un valor aquí, hay que ampliar el CHECK.
/// <para>
/// Los define el CÓDIGO, no el usuario: el sistema dispara cada notificación con un tipo
/// concreto; el mantenimiento solo asigna remitente y destinatarios a cada tipo.
/// </para>
/// </summary>
public static class TipoNotificacion
{
    /// <summary>Avisos administrativos / gerenciales.</summary>
    public const string Administracion = "ADMINISTRACION";

    /// <summary>Avisos de bodega/almacén (stock bajo, recepciones, …).</summary>
    public const string Almacen = "ALMACEN";

    /// <summary>Avisos de cobranza.</summary>
    public const string Cobranza = "COBRANZA";

    /// <summary>Correos del sistema (Identity): confirmación de cuenta, reseteo de contraseña.</summary>
    public const string Sistema = "SISTEMA";

    /// <summary>Todos los valores válidos, en el mismo orden que el CHECK de la BD.</summary>
    public static readonly string[] Todos =
    [
        Administracion,
        Almacen,
        Cobranza,
        Sistema
    ];

    public static bool EsValido(string? valor) =>
        !string.IsNullOrWhiteSpace(valor) && Array.IndexOf(Todos, valor) >= 0;
}
