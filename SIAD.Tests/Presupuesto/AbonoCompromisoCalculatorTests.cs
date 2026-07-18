using SIAD.Services.Presupuesto;

namespace SIAD.Tests.Presupuesto;

public class AbonoCompromisoCalculatorTests
{
    private static AbonoLineaState L(int numeroAbono, decimal monto, string estado)
        => new(numeroAbono, monto, estado);

    [Fact]
    public void SinAbonos_NoProcesado_SaldoEsMontoCompleto_YProximoEsUno()
    {
        var s = AbonoCompromisoCalculator.Compute(1000m, procesadoLegacy: false, Array.Empty<AbonoLineaState>());
        Assert.Equal(1000m, s.SaldoActual);
        Assert.Equal(1, s.SiguienteNumeroAbono);
    }

    [Fact]
    public void SinAbonos_ProcesadoLegacy_SaldoCero_YaPagado()
    {
        var s = AbonoCompromisoCalculator.Compute(1000m, procesadoLegacy: true, Array.Empty<AbonoLineaState>());
        Assert.Equal(0m, s.SaldoActual);
        Assert.Equal(1, s.SiguienteNumeroAbono);
    }

    [Fact]
    public void SoloAbonosVigentesRestan_YAnuladosSeIgnoran()
    {
        var s = AbonoCompromisoCalculator.Compute(1000m, procesadoLegacy: false, new[]
        {
            L(1, 300m, "V"),
            L(2, 200m, "A"), // anulado: no resta
            L(3, 100m, "V"),
        });
        Assert.Equal(600m, s.SaldoActual);       // 1000 - 300 - 100
        Assert.Equal(4, s.SiguienteNumeroAbono); // max(1,2,3) + 1
    }

    [Fact]
    public void AbonosCubrenTotal_SaldoCero()
    {
        var s = AbonoCompromisoCalculator.Compute(500m, procesadoLegacy: true, new[] { L(1, 500m, "V") });
        Assert.Equal(0m, s.SaldoActual);
        Assert.Equal(2, s.SiguienteNumeroAbono);
    }
}
