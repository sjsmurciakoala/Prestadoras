using System.Text;
using System.Text.RegularExpressions;
using DevExpress.XtraPrinting;
using SIAD.Core.DTOs.Cobranza;
using SIAD.Reports;
using Xunit;

namespace SIAD.Tests.Cobros;

/// <summary>
/// Render del compromiso de pago. Puro, sin BD: arma el DTO, exporta el PDF y
/// lee el texto para comprobar que sale el formato de la unidad de cobranza,
/// con el desglose de la mora y el plan de cuotas con saldo corrido.
/// </summary>
public class ConvenioRenderTests
{
    private static ConvenioImpresionDto Muestra(int meses = 4, decimal cuota = 2168.21m, string? firmante = null)
    {
        var cuotas = new List<ConvenioCuotaImpresionDto>();
        for (var i = 0; i < meses; i++)
        {
            cuotas.Add(new ConvenioCuotaImpresionDto
            {
                Numero = i + 1,
                FechaVencimiento = new DateTime(2026, 9, 10).AddMonths(i),
                Monto = cuota,
                Saldo = cuota,
                EstadoTexto = "PENDIENTE"
            });
        }

        return new ConvenioImpresionDto
        {
            PlanId = 39,
            Correlativo = "39",
            Codigo = "0000039-2026",
            EstadoTexto = "ACTIVO",
            FechaCreacion = new DateTime(2026, 8, 27),
            FechaPrimerPago = new DateTime(2026, 9, 10),
            ClienteClave = "090135102",
            ClienteNombre = "Ortiz Oseguera Bayron José",
            ClienteDireccion = "COL. MODELO MUNICIPAL LOTE B-18 L-08",
            DocRepresentante = "0506-1981-01719",
            MontoTotal = 8672.85m,
            Prima = 0m,
            MontoFinanciado = 8672.85m,
            Tasa = 0m,
            Meses = meses,
            ValorCuota = cuota,
            Comentario = "Es el dueño de la propiedad 3211-7895",
            EmpresaNombre = "Aguas de Puerto Cortés S.A. de C.V.",
            FirmanteCobranza = firmante,
            Conceptos = new List<ConvenioConceptoDto>
            {
                new() { Descripcion = "Agua Potable", Valor = 6799.51m },
                new() { Descripcion = "Alcantarillado Sanitario", Valor = 1873.34m }
            },
            Cuotas = cuotas,
            SaldoPendiente = 8672.85m,
            ElaboradoPor = "MARTAP",
            FechaElaboracion = new DateTime(2026, 8, 27, 13, 33, 38)
        };
    }

    private static string TextoRenderizado(ConvenioImpresionDto convenio)
    {
        using var report = new Rpt_Dev_Convenio(convenio);

        using var pdf = new MemoryStream();
        report.ExportToPdf(pdf);
        Assert.True(pdf.Length > 1024, "El PDF salió vacío.");

        using var texto = new MemoryStream();
        report.ExportToText(texto, new TextExportOptions
        {
            Separator = " ",
            QuoteStringsWithSeparators = false,
            Encoding = Encoding.UTF8
        });

        var plano = Regex.Replace(Encoding.UTF8.GetString(texto.ToArray()), "</?b>", string.Empty);
        return Regex.Replace(plano, @"\s+", " ");
    }

    [Fact]
    public void Imprime_el_encabezado_y_el_desglose_de_la_mora()
    {
        var texto = TextoRenderizado(Muestra());

        Assert.Contains("COMPROMISO DE PAGO", texto);
        Assert.Contains("Clave: 090135102", texto);
        Assert.Contains("0000039-2026", texto);
        Assert.Contains("ORTIZ OSEGUERA BAYRON JOSÉ", texto);
        Assert.Contains("27/08/26", texto);
        Assert.Contains("COL. MODELO MUNICIPAL LOTE B-18 L-08", texto);

        // Desglose por concepto y total de la mora.
        Assert.Contains("Agua Potable", texto);
        Assert.Contains("6,799.51", texto);
        Assert.Contains("Alcantarillado Sanitario", texto);
        Assert.Contains("1,873.34", texto);
        Assert.Contains("TOTAL MORA L.", texto);
        Assert.Contains("8,672.85", texto);

        // Condiciones pactadas.
        Assert.Contains("PRIMA L.", texto);
        Assert.Contains("MONTO A FINANCIAR L.", texto);
        Assert.Contains("TASA %", texto);
        Assert.Contains("CUOTA L.", texto);

        // Texto legal y pie.
        Assert.Contains("HAGO CONSTAR", texto);
        Assert.Contains("renunciando expresamente al fuero de mi domicilio", texto);
        Assert.Contains("ES EL DUEÑO DE LA PROPIEDAD 3211-7895", texto);
        Assert.Contains("UNIDAD DE COBRANZA", texto);
        Assert.Contains("Elaborado por: MARTAP", texto);
    }

    [Fact]
    public void El_plan_arranca_en_la_cuota_cero_y_el_saldo_baja_hasta_liquidar()
    {
        var texto = TextoRenderizado(Muestra());

        // Fila 00: no cobra nada y muestra la deuda completa.
        Assert.Contains("00 10/09/26 0.00 0.00 0.00 8,672.85", texto);

        // El saldo corrido baja con cada cuota.
        Assert.Contains("6,504.64", texto);
        Assert.Contains("4,336.43", texto);
        Assert.Contains("2,168.22", texto);
    }

    [Fact]
    public void El_firmante_de_cobranza_sale_cuando_esta_configurado()
    {
        var conNombre = TextoRenderizado(Muestra(firmante: "Indra Cruz"));
        Assert.Contains("INDRA CRUZ", conNombre);
        Assert.Contains("UNIDAD DE COBRANZA", conNombre);

        // Sin configurar queda el rótulo genérico.
        var sinNombre = TextoRenderizado(Muestra());
        Assert.DoesNotContain("INDRA CRUZ", sinNombre);
        Assert.Contains("Representante", sinNombre);
        Assert.Contains("UNIDAD DE COBRANZA", sinNombre);
    }

    [Fact]
    public void Sin_desglose_de_conceptos_muestra_el_saldo_trasladado()
    {
        var convenio = Muestra() with { Conceptos = Array.Empty<ConvenioConceptoDto>() };
        var texto = TextoRenderizado(convenio);

        Assert.Contains("Saldo trasladado al convenio", texto);
        Assert.Contains("TOTAL MORA L.", texto);
        Assert.Contains("8,672.85", texto);
    }
}
