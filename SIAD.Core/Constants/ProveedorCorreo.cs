namespace SIAD.Core.Constants;

/// <summary>
/// Vocabulario cerrado de <c>cfg_correo.proveedor</c>. Espejo del CHECK
/// <c>ck_cfg_correo_proveedor</c> — si se agrega un valor aquí, hay que ampliar el CHECK.
/// Hoy solo SENDGRID está implementado; SMTP queda declarado para dejar la puerta abierta.
/// </summary>
public static class ProveedorCorreo
{
    public const string SendGrid = "SENDGRID";

    public const string Smtp = "SMTP";

    /// <summary>Todos los valores válidos, en el mismo orden que el CHECK de la BD.</summary>
    public static readonly string[] Todos =
    [
        SendGrid,
        Smtp
    ];

    public static bool EsValido(string? valor) =>
        !string.IsNullOrWhiteSpace(valor) && Array.IndexOf(Todos, valor) >= 0;
}
