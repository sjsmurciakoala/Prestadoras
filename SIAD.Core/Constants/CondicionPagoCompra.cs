namespace SIAD.Core.Constants;

/// <summary>
/// Condición de pago de la factura de compra (<c>alm_compra_hdr.condicion_pago</c>). Reemplaza
/// la inferencia por texto/plazo por un campo explícito. La deriva el servidor del término de
/// pago (0 días → Contado, &gt; 0 → Crédito) y decide la generación de la cuenta por pagar.
/// </summary>
public static class CondicionPagoCompra
{
    public const short Contado = 1;
    public const short Credito = 2;
    public const short Prepagado = 3;

    public static string Describir(short condicion) => condicion switch
    {
        Contado => "Contado",
        Credito => "Crédito",
        Prepagado => "Prepagado",
        _ => "—"
    };
}
