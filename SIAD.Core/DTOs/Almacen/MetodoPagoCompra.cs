using System;

namespace SIAD.Core.DTOs.Almacen;

/// <summary>
/// Método de pago de un abono a una cuenta por pagar de compra. Los bancarios (cheque,
/// transferencia) mueven el kardex de la cuenta bancaria; el efectivo sólo baja el saldo de
/// la CxP (no toca banco). Alineado con los tipos de transacción bancaria de salida.
/// </summary>
public static class MetodoPagoCompra
{
    public const string Efectivo = "EFECTIVO";
    public const string Cheque = "CHEQUE";
    public const string Transferencia = "TRANSFERENCIA";

    /// <summary>Los métodos que generan un movimiento en el kardex bancario.</summary>
    public static bool EsBancario(string? metodo) =>
        string.Equals(metodo, Cheque, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(metodo, Transferencia, StringComparison.OrdinalIgnoreCase);

    /// <summary>Abreviatura para la referencia del movimiento bancario.</summary>
    public static string Abrev(string? metodo) => (metodo ?? string.Empty).ToUpperInvariant() switch
    {
        Cheque => "CHQ",
        Transferencia => "TRF",
        Efectivo => "EFE",
        _ => "PAG"
    };
}
