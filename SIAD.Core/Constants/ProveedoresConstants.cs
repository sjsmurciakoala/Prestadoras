namespace SIAD.Core.Constants;

public static class ProveedoresConstants
{
    public const string CodigoProveedorGenericoCompromisos = "PRVGEN";
    public const string CodigoProveedorGenericoCompromisosLegacy = "PRV-GENERICO";
    public const string NombreProveedorGenericoCompromisos = "Proveedor generico";
    public const string NombreTipoProveedorGenerico = "Generico";
    public const string DireccionProveedorGenericoCompromisos = "Proveedor generico para compromisos directos";

    public const string TipoCuentaBancariaCheques = "CHEQUES";
    public const string TipoCuentaBancariaAhorro = "AHORRO";

    public static readonly IReadOnlyList<string> TiposCuentaBancaria = new[]
    {
        TipoCuentaBancariaCheques,
        TipoCuentaBancariaAhorro
    };
}
