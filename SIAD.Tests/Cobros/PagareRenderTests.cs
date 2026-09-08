using System.Text;
using System.Text.RegularExpressions;
using DevExpress.XtraPrinting;
using SIAD.Core.DTOs.Cobranza;
using SIAD.Reports;
using Xunit;

namespace SIAD.Tests.Cobros;

/// <summary>
/// Render del pagaré a la vista. Puro, sin BD: arma el DTO, exporta el PDF y
/// lee el texto resultante para comprobar que sale el documento completo, con
/// el texto legal y los datos del convenio integrados en la redacción.
/// </summary>
public class PagareRenderTests
{
    private static PagareImpresionDto Muestra(string? identidad = "0506-1981-01719", string? firmante = null)
        => new()
        {
            PlanId = 39,
            Correlativo = "39",
            FechaSuscripcion = new DateTime(2026, 8, 27),
            DeudorNombre = "Bayron José Ortiz Oseguera",
            DeudorIdentidad = identidad,
            NumeroCuenta = "090807355",
            EmpresaNombre = "Aguas de Puerto Cortés S.A. de C.V.",
            FirmanteCobranza = firmante,
            MontoTotal = 8672.85m,
            CantidadMeses = 4,
            FechaDesde = new DateTime(2026, 9, 10),
            FechaHasta = new DateTime(2026, 12, 10)
        };

    /// <summary>
    /// Documento renderizado, con los espacios normalizados y sin las marcas de
    /// resaltado, para poder buscar las frases completas. Exporta también a PDF
    /// para probar la ruta real del endpoint.
    /// </summary>
    private static string TextoRenderizado(PagareImpresionDto pagare)
    {
        using var report = new Rpt_Dev_Pagare(pagare);

        using var pdf = new MemoryStream();
        report.ExportToPdf(pdf);
        Assert.True(pdf.Length > 1024, "El PDF salió vacío.");

        using var texto = new MemoryStream();
        report.ExportToText(texto, new TextExportOptions
        {
            Separator = " ",
            QuoteStringsWithSeparators = false,
            Encoding = Encoding.UTF8   // por defecto exporta en ANSI y se pierden las tildes
        });

        var plano = Regex.Replace(Encoding.UTF8.GetString(texto.ToArray()), "</?b>", string.Empty);
        return Regex.Replace(plano, @"\s+", " ");
    }

    [Fact]
    public void Imprime_el_texto_legal_del_formato()
    {
        var texto = TextoRenderizado(Muestra());

        Assert.Contains("AGUAS DE PUERTO CORTÉS S.A. DE C.V.", texto);
        Assert.Contains("PAGARÉ", texto);
        Assert.Contains("POR L. 8,672.85", texto);
        Assert.Contains("PAGARÉ A LA VISTA", texto);
        Assert.Contains("HAGO CONSTAR", texto);
        Assert.Contains("debo y pagaré incondicionalmente", texto);
        Assert.Contains("la suma del valor real más intereses y recargos", texto);
        Assert.Contains("Recuperación por Morosidad de esta Institución.", texto);
        Assert.Contains("Y para constancia y efectos legales firmo el presente pagaré", texto);

        // El formato no lleva tabla de cuotas: el plazo se expresa en meses.
        Assert.DoesNotContain("DETALLE DE VENCIMIENTOS", texto);
    }

    [Fact]
    public void Integra_los_datos_del_convenio_en_la_redaccion()
    {
        var texto = TextoRenderizado(Muestra());

        Assert.Contains("BAYRON JOSÉ ORTIZ OSEGUERA", texto);
        Assert.Contains("0506-1981-01719", texto);
        Assert.Contains("8,672.85", texto);
        Assert.Contains("Puerto Cortés", texto);
        Assert.Contains("Cortés", texto);

        // Plazo en meses y fechas en palabras.
        Assert.Contains("4", texto);
        Assert.Contains("10 de septiembre de 2026", texto);
        Assert.Contains("10 de diciembre de 2026", texto);

        // Fecha de firma partida en día, mes y año.
        Assert.Contains("27", texto);
        Assert.Contains("agosto", texto);
        Assert.Contains("2026", texto);

        // Pie de firmas.
        Assert.Contains("Representante Legal", texto);
        Assert.Contains("Unidad de Cobranza", texto);
        Assert.Contains("Identidad No. 0506-1981-01719", texto);
        Assert.Contains("Cuenta No. 090807355", texto);
    }

    [Fact]
    public void El_firmante_de_cobranza_sale_cuando_esta_configurado()
    {
        var conNombre = TextoRenderizado(Muestra(firmante: "Indra Cruz"));
        Assert.Contains("INDRA CRUZ", conNombre);
        Assert.Contains("Representante Legal", conNombre);
        Assert.Contains("Unidad de Cobranza", conNombre);

        // Sin configurar queda el rótulo genérico y ningún nombre inventado.
        var sinNombre = TextoRenderizado(Muestra());
        Assert.DoesNotContain("INDRA CRUZ", sinNombre);
        Assert.Contains("Representante Legal", sinNombre);
        Assert.Contains("Unidad de Cobranza", sinNombre);
    }

    [Fact]
    public void Sin_identidad_deja_la_linea_para_llenar_a_mano()
    {
        var texto = TextoRenderizado(Muestra(identidad: null));

        Assert.Contains("____", texto);
        Assert.Contains("con domicilio en el Municipio de", texto);
        Assert.Contains("PAGARÉ A LA VISTA", texto);
        Assert.Contains("8,672.85", texto);
    }
}
