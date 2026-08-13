using SIAD.Core.Constants;
using SIAD.Core.Retenciones;

namespace SIAD.Tests.Retenciones;

/// <summary>
/// Tests PUROS (sin BD) del autocálculo de retenciones de F2: base sin ISV, monto = base×%/100 con
/// redondeo AwayFromZero, y el caso "sin tasa vigente" (no se inventa %).
/// </summary>
public class RetencionCalculatorTests
{
    // ----- base SIN_ISV: le quita el ISV general al bruto -----

    [Fact]
    public void SinIsv_ConIsv15_QuitaElIsvDelBruto()
    {
        // 1150 con ISV 15% => subtotal 1000.
        var r = RetencionCalculator.Calcular(1150m, BaseRetencion.SinIsv, porcentaje: 12.5m, tasaIsvPorcentaje: 15m);

        Assert.True(r.PuedeCalcularMonto);
        Assert.Equal(1000.00m, r.Base);
        Assert.Equal(125.00m, r.Monto); // ISR 12.5% sobre 1000
    }

    [Fact]
    public void SinIsv_Isr1PorCiento_CalculaSobreSubtotal()
    {
        var r = RetencionCalculator.Calcular(1150m, BaseRetencion.SinIsv, porcentaje: 1m, tasaIsvPorcentaje: 15m);

        Assert.Equal(1000.00m, r.Base);
        Assert.Equal(10.00m, r.Monto);
    }

    [Fact]
    public void SinIsv_BaseSeRedondeaA2_AwayFromZero()
    {
        // 1000 / 1.15 = 869.5652... => 869.57 (AwayFromZero en el 3er decimal '5').
        var r = RetencionCalculator.Calcular(1000m, BaseRetencion.SinIsv, porcentaje: 12.5m, tasaIsvPorcentaje: 15m);

        Assert.Equal(869.57m, r.Base);
    }

    // ----- base TOTAL: el bruto tal cual -----

    [Fact]
    public void Total_BaseEsElBruto()
    {
        var r = RetencionCalculator.Calcular(1000m, BaseRetencion.Total, porcentaje: 5m, tasaIsvPorcentaje: 15m);

        Assert.Equal(1000.00m, r.Base);
        Assert.Equal(50.00m, r.Monto);
    }

    // ----- redondeo del monto AwayFromZero (no bancario) -----

    [Fact]
    public void Monto_RedondeaAwayFromZero_NoBancario()
    {
        // 101 * 2.5% = 2.525 => 2.53 (AwayFromZero). El redondeo bancario daría 2.52.
        var r = RetencionCalculator.Calcular(101m, BaseRetencion.Total, porcentaje: 2.5m, tasaIsvPorcentaje: null);

        Assert.Equal(2.53m, r.Monto);
    }

    // ----- caso "sin tasa vigente" (ISV-RET): NO se inventa % -----

    [Fact]
    public void SinTasaVigente_NoCalculaMonto_PeroProponeBase()
    {
        var r = RetencionCalculator.Calcular(1150m, BaseRetencion.SinIsv, porcentaje: null, tasaIsvPorcentaje: 15m);

        Assert.False(r.PuedeCalcularMonto);
        Assert.Equal(0m, r.Monto);
        Assert.Equal(1000.00m, r.Base); // la base se propone para referencia; el monto lo pone el usuario
    }

    [Fact]
    public void PorcentajeCero_TratadoComoSinTasa()
    {
        var r = RetencionCalculator.Calcular(1000m, BaseRetencion.Total, porcentaje: 0m, tasaIsvPorcentaje: 15m);

        Assert.False(r.PuedeCalcularMonto);
        Assert.Equal(0m, r.Monto);
    }

    // ----- fallback: SIN_ISV sin tasa ISV vigente => divisor 1 (base = bruto) + bandera de aviso -----

    [Fact]
    public void SinIsv_SinTasaIsvVigente_NoReduceLaBase()
    {
        var r = RetencionCalculator.Calcular(1150m, BaseRetencion.SinIsv, porcentaje: 12.5m, tasaIsvPorcentaje: null);

        Assert.Equal(1150.00m, r.Base);     // no se pudo quitar el ISV: base = bruto
        Assert.Equal(143.75m, r.Monto);     // 1150 * 12.5%
        Assert.True(RetencionCalculator.RequiereTasaIsvYNoHay(BaseRetencion.SinIsv, null));
    }

    [Theory]
    [InlineData(BaseRetencion.SinIsv, null, true)]   // sin ISV vigente -> avisar
    [InlineData(BaseRetencion.SinIsv, 0.0, true)]    // ISV 0 -> avisar
    [InlineData(BaseRetencion.SinIsv, 15.0, false)]  // hay ISV -> ok
    [InlineData(BaseRetencion.Total, null, false)]   // TOTAL no necesita ISV
    public void RequiereTasaIsvYNoHay_SegunBaseYTasa(string baseCalculo, double? isv, bool esperado)
    {
        var tasa = isv.HasValue ? (decimal?)(decimal)isv.Value : null;
        Assert.Equal(esperado, RetencionCalculator.RequiereTasaIsvYNoHay(baseCalculo, tasa));
    }

    // ----- MontoDesdeBase: recálculo cuando el usuario edita la base a mano -----

    [Theory]
    [InlineData(1000.0, 12.5, 125.00)]
    [InlineData(1000.0, 1.0, 10.00)]
    [InlineData(1000.0, null, 0.0)]
    [InlineData(1000.0, 0.0, 0.0)]
    public void MontoDesdeBase_CalculaOCero(double baseImp, double? pct, double esperado)
    {
        var porcentaje = pct.HasValue ? (decimal?)(decimal)pct.Value : null;
        var monto = RetencionCalculator.MontoDesdeBase((decimal)baseImp, porcentaje);
        Assert.Equal((decimal)esperado, monto);
    }

    // ----- guardas -----

    [Fact]
    public void BrutoCeroONegativo_BaseCero()
    {
        Assert.Equal(0m, RetencionCalculator.CalcularBase(0m, BaseRetencion.SinIsv, 15m));
        Assert.Equal(0m, RetencionCalculator.CalcularBase(-100m, BaseRetencion.Total, 15m));
    }
}
