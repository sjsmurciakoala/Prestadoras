namespace SIAD.Core.Constants;

/// <summary>
/// Estado de una cuenta por pagar de compra (<c>alm_compra_cxp.estado_id</c>).
/// </summary>
public static class EstadoCompraCxp
{
    public const short Pendiente = 1;
    public const short Parcial = 2;
    public const short Pagada = 3;
    public const short Anulada = 9;

    public static string Describir(short estado) => estado switch
    {
        Pendiente => "Pendiente",
        Parcial => "Parcial",
        Pagada => "Pagada",
        Anulada => "Anulada",
        _ => "—"
    };
}
