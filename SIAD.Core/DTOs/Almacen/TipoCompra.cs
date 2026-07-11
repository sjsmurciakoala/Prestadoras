namespace SIAD.Core.DTOs.Almacen;

/// <summary>
/// Mapeo del código legacy tipo_compra de alm_compra. Inferido de los datos:
/// 0 = contado (sin plazo ni cuenta por pagar); 1 y 2 = crédito (con cuenta
/// por pagar; el 2 con plazo promedio ~30 días). El plazo real se muestra
/// aparte en la columna plazo_dias.
/// </summary>
public static class TipoCompra
{
    public const short Contado = 0;

    public static string Describir(short tipo) => tipo switch
    {
        0 => "Contado",
        _ => "Crédito"
    };
}
