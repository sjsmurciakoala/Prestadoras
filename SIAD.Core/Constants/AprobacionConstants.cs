namespace SIAD.Core.Constants;

/// <summary>
/// Documentos que puede gobernar el motor de aprobación por niveles. Es el valor de
/// <c>cfg_aprobacion_control.documento</c> y de <c>apr_bitacora.documento</c>, y está
/// respaldado por un CHECK en la base: agregar uno aquí exige ampliar también ese CHECK.
/// <para>
/// La primera entrega solo implementa <see cref="OrdenCompra"/>; los otros tres existen en el
/// catálogo para que la configuración y la bitácora no cambien de forma cuando se enganchen.
/// </para>
/// </summary>
public static class DocumentosAprobacion
{
    public const string OrdenCompra = "COMPRAS_OC";
    public const string FacturaCompra = "COMPRAS_FACTURA";
    public const string PagoProveedor = "PROVEEDORES_PAGO";
    public const string Requisicion = "ALMACEN_REQUISICION";
}

/// <summary>
/// Modo del control por empresa y documento (<c>cfg_aprobacion_control.modo</c>).
/// Nace <see cref="Apagado"/> en toda empresa: encenderlo es una decisión deliberada.
/// </summary>
public static class ModoAprobacion
{
    /// <summary>El documento se aprueba como siempre, de un clic. Comportamiento histórico.</summary>
    public const short Apagado = 0;

    /// <summary>Exige la escalera configurada en <c>cfg_aprobacion_nivel</c>.</summary>
    public const short Encendido = 1;
}

/// <summary>Tipo de aprobador (<c>cfg_aprobacion_aprobador.tipo</c>), decisión D3.</summary>
public static class TipoAprobador
{
    /// <summary>Un usuario nominal. <c>valor</c> es el user_name (email) en MINÚSCULAS.</summary>
    public const short Usuario = 1;

    /// <summary>Un rol de Identity. <c>valor</c> es el nombre del rol, con sus mayúsculas.</summary>
    public const short Rol = 2;
}

/// <summary>
/// Acciones de <c>apr_bitacora.accion</c>. Respaldadas por un CHECK en la base.
/// La bitácora es append-only: estas son las únicas cosas que puede haber pasado.
/// </summary>
public static class AccionAprobacion
{
    /// <summary>El documento salió de Borrador y entró a la escalera.</summary>
    public const string Enviada = "ENVIADA";

    /// <summary>Un nivel firmó a favor.</summary>
    public const string Aprobada = "APROBADA";

    /// <summary>Un nivel rechazó; el documento queda terminal.</summary>
    public const string Rechazada = "RECHAZADA";

    /// <summary>Vuelta a Borrador: borra las firmas y libera lo comprometido (D4).</summary>
    public const string Devuelta = "DEVUELTA";

    public const string Anulada = "ANULADA";

    /// <summary>Reenvío a la escalera después de una devolución.</summary>
    public const string Reiniciada = "REINICIADA";
}
