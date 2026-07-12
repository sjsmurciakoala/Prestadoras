namespace SIAD.Core.DTOs.Almacen;

/// <summary>
/// Mapeo de los códigos legacy de tipo de transacción de bodega (alm_kardex
/// heredado de MySQL inventariotra) a etiquetas legibles.
/// 102 = entrada/inventario inicial · 202 = salida · 103 = ajuste.
/// </summary>
public static class TipoMovimientoKardex
{
    public static string Describir(string? codigo) => codigo switch
    {
        "102" => "Entrada",
        "103" => "Ajuste",
        "202" => "Salida",
        null or "" => "—",
        _ => codigo
    };
}
