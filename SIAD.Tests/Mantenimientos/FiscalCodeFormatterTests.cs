using Xunit;
using SIAD.Core.Utilities;

namespace SIAD.Tests.Mantenimientos;

/// <summary>
/// Formateador de códigos fiscales (No. de factura SAR, CAI). Sin base de datos: es una
/// utilidad pura de SIAD.Core. La regla de oro es que NUNCA lanza, pase lo que pase con
/// la máscara o el valor — quien decide si algo es válido es <c>EsValido</c>.
/// </summary>
public class FiscalCodeFormatterTests
{
    private const string MascaraSar = "###-###-##-########";
    private const string MascaraCai = "HHHHHH-HHHHHH-HHHHHH-HHHHHH-HHHHHH-HH";

    // ------------------------------------------------------------- normalizar

    [Fact]
    public void Normalizar_QuitaSeparadores_YSubeAMayusculas()
    {
        Assert.Equal("0000010100000123", FiscalCodeFormatter.Normalizar("000-001-01-00000123"));
        Assert.Equal("A1B2C3", FiscalCodeFormatter.Normalizar("a1b2-c3"));
    }

    [Fact]
    public void Normalizar_RespetaMinusculasCuandoSePide()
    {
        Assert.Equal("a1b2c3", FiscalCodeFormatter.Normalizar("a1b2-c3", mayusculas: false));
    }

    [Fact]
    public void Normalizar_ValorVacio_DevuelveCadenaVacia()
    {
        Assert.Equal(string.Empty, FiscalCodeFormatter.Normalizar(null));
        Assert.Equal(string.Empty, FiscalCodeFormatter.Normalizar("   "));
    }

    // -------------------------------------------------------------- formatear

    [Fact]
    public void Formatear_PoneLosLiteralesDeLaMascara()
    {
        Assert.Equal("000-001-01-00000123", FiscalCodeFormatter.Formatear("0000010100000123", MascaraSar));
    }

    [Fact]
    public void Formatear_EsIdempotente_SiElValorYaVieneConSeparadores()
    {
        Assert.Equal("000-001-01-00000123", FiscalCodeFormatter.Formatear("000-001-01-00000123", MascaraSar));
    }

    [Fact]
    public void Formatear_ValorCorto_CortaDondeAlcanza_SinDejarLiteralesColgando()
    {
        Assert.Equal("000-001", FiscalCodeFormatter.Formatear("000001", MascaraSar));
    }

    [Fact]
    public void Formatear_ValorLargo_AnexaElSobranteAlFinal()
    {
        // Se ve que algo no cuadra en vez de perder caracteres en silencio.
        Assert.Equal("000-001-01-0000012399", FiscalCodeFormatter.Formatear("000001010000012399", MascaraSar));
    }

    [Fact]
    public void Formatear_SinMascara_DevuelveElValorNormalizado()
    {
        Assert.Equal("0000010100000123", FiscalCodeFormatter.Formatear("000-001-01-00000123", null));
        Assert.Equal("0000010100000123", FiscalCodeFormatter.Formatear("000-001-01-00000123", "sin metacaracteres"));
    }

    // ------------------------------------------------------------------ regex

    [Fact]
    public void ToRegex_AgrupaLasCorridasDelMismoMetacaracter()
    {
        Assert.Equal(@"^\d{3}-\d{3}-\d{2}-\d{8}$", FiscalCodeFormatter.ToRegex(MascaraSar));
    }

    [Fact]
    public void ToRegex_Hexadecimal_UsaLaClaseDeHex()
    {
        Assert.Equal("^[0-9A-F]{6}-[0-9A-F]{6}-[0-9A-F]{6}-[0-9A-F]{6}-[0-9A-F]{6}-[0-9A-F]{2}$",
            FiscalCodeFormatter.ToRegex(MascaraCai));
    }

    [Fact]
    public void ToRegex_SinMetacaracteres_DevuelveCadenaVacia()
    {
        Assert.Equal(string.Empty, FiscalCodeFormatter.ToRegex(""));
        Assert.Equal(string.Empty, FiscalCodeFormatter.ToRegex("----"));
    }

    // ------------------------------------------------------ máscara DevExpress

    [Fact]
    public void ToDevExpressMask_TraduceLosMetacaracteres()
    {
        Assert.Equal("000-000-00-00000000", FiscalCodeFormatter.ToDevExpressMask(MascaraSar));
        Assert.Equal("AAAAAA-AAAAAA-AAAAAA-AAAAAA-AAAAAA-AA", FiscalCodeFormatter.ToDevExpressMask(MascaraCai));
    }

    [Fact]
    public void ToDevExpressMask_EscapaLosLiteralesQueDevExpressInterpretaria()
    {
        // '/' es separador de fecha para DevExpress: como literal va escapado.
        Assert.Equal(@"000\/000", FiscalCodeFormatter.ToDevExpressMask("###/###"));
    }

    // ---------------------------------------------------------------- ejemplo

    [Fact]
    public void Ejemplo_MuestraLaFormaDeLaMascara()
    {
        Assert.Equal("000-000-00-00000000", FiscalCodeFormatter.Ejemplo(MascaraSar));
        Assert.Equal("AAAAAA-AAAAAA-AAAAAA-AAAAAA-AAAAAA-AA", FiscalCodeFormatter.Ejemplo(MascaraCai));
    }

    [Fact]
    public void Longitud_CuentaSoloLosMetacaracteres()
    {
        Assert.Equal(16, FiscalCodeFormatter.Longitud(MascaraSar));
        Assert.Equal(0, FiscalCodeFormatter.Longitud("---"));
    }

    // --------------------------------------------------------------- validar

    [Fact]
    public void EsValido_AceptaElValorConSeparadoresYSinEllos()
    {
        Assert.True(FiscalCodeFormatter.EsValido("000-001-01-00000123", MascaraSar));
        Assert.True(FiscalCodeFormatter.EsValido("0000010100000123", MascaraSar));
    }

    [Fact]
    public void EsValido_RechazaLoQueNoCuadraConLaMascara()
    {
        Assert.False(FiscalCodeFormatter.EsValido("000-001-01-000012", MascaraSar));      // faltan dígitos
        Assert.False(FiscalCodeFormatter.EsValido("000-001-01-000001239", MascaraSar));   // sobran
        Assert.False(FiscalCodeFormatter.EsValido("00A-001-01-00000123", MascaraSar));    // letra donde va dígito
    }

    [Fact]
    public void EsValido_Cai_ExigeHexadecimal()
    {
        Assert.True(FiscalCodeFormatter.EsValido("A1B2C3-D4E5F6-071829-3A4B5C-6D7E8F-90", MascaraCai));
        Assert.False(FiscalCodeFormatter.EsValido("A1B2C3-D4E5F6-071829-3A4B5C-6D7E8G-90", MascaraCai)); // 'G' no es hex
    }

    [Fact]
    public void EsValido_ValorVacio_EsValido_LoObligatorioLoDecideElCatalogo()
    {
        Assert.True(FiscalCodeFormatter.EsValido(null, MascaraSar));
        Assert.True(FiscalCodeFormatter.EsValido("  ", MascaraSar));
    }

    [Fact]
    public void EsValido_PatronPropio_Manda_SobreLaMascara()
    {
        // Patrón más laxo que la máscara: acepta cualquier cosa de 3 grupos.
        Assert.True(FiscalCodeFormatter.EsValido("000-001-01-00000123", MascaraSar, @"^.+$"));
    }

    [Fact]
    public void EsValido_PatronInvalido_NoBloqueaLaCaptura()
    {
        // Una expresión mal escrita en el mantenimiento no puede trabar la factura del día.
        Assert.True(FiscalCodeFormatter.EsValido("cualquier cosa", MascaraSar, "([sin cerrar"));
    }

    // --------------------------------------------------------- no lanza nunca

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("valor", null)]
    [InlineData(null, "###")]
    [InlineData("!!!", "###-###")]
    public void NingunMetodo_Lanza_ConEntradasDegeneradas(string? valor, string? mascara)
    {
        var ex = Record.Exception(() =>
        {
            _ = FiscalCodeFormatter.Normalizar(valor);
            _ = FiscalCodeFormatter.Formatear(valor, mascara);
            _ = FiscalCodeFormatter.ToRegex(mascara);
            _ = FiscalCodeFormatter.ToDevExpressMask(mascara);
            _ = FiscalCodeFormatter.Ejemplo(mascara);
            _ = FiscalCodeFormatter.Longitud(mascara);
            _ = FiscalCodeFormatter.TieneMetacaracteres(mascara);
            _ = FiscalCodeFormatter.EsValido(valor, mascara);
        });

        Assert.Null(ex);
    }
}
