namespace SIAD.Core.Constants;

/// <summary>
/// Vocabulario cerrado de <c>cfg_notificacion_destinatario.clase</c>. Espejo del CHECK
/// <c>ck_cfg_notif_dest_clase</c> — si se agrega un valor aquí, hay que ampliar el CHECK.
/// </summary>
public static class ClaseDestinatario
{
    /// <summary>Destinatario principal (Para).</summary>
    public const string To = "TO";

    /// <summary>Con copia.</summary>
    public const string Cc = "CC";

    /// <summary>Todos los valores válidos, en el mismo orden que el CHECK de la BD.</summary>
    public static readonly string[] Todas =
    [
        To,
        Cc
    ];

    public static bool EsValida(string? valor) =>
        !string.IsNullOrWhiteSpace(valor) && Array.IndexOf(Todas, valor) >= 0;
}
