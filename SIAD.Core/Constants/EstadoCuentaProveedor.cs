namespace SIAD.Core.Constants;

/// <summary>
/// Módulo del que nace un documento del estado de cuenta del proveedor
/// (columna <c>origen</c> de <c>fn_prv_estado_cuenta_*</c>).
/// <para>
/// La deuda con un proveedor vive en dos módulos separados: las facturas de compra
/// (<c>alm_compra_cxp</c>) y los compromisos / órdenes de pago directo
/// (<c>prv_compromiso_hdr</c>). El código es de almacenamiento: en pantalla se
/// muestra siempre la descripción.
/// </para>
/// </summary>
public static class OrigenDocumentoProveedor
{
    public const short Compra = 1;
    public const short Compromiso = 2;

    public static string Describir(short origen) => origen switch
    {
        Compra => "Compra",
        Compromiso => "Compromiso",
        _ => "—"
    };
}

/// <summary>
/// Naturaleza de una línea del libro de movimientos del proveedor
/// (columna <c>tipo</c> de <c>fn_prv_estado_cuenta_movimientos</c>).
/// </summary>
public static class TipoMovimientoProveedor
{
    /// <summary>Nace la deuda: factura de compra o compromiso.</summary>
    public const short Cargo = 1;

    /// <summary>Se cancela la deuda: abono vigente.</summary>
    public const short Abono = 2;

    public static string Describir(short tipo) => tipo switch
    {
        Cargo => "Cargo",
        Abono => "Abono",
        _ => "—"
    };
}
