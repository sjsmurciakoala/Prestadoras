using SIAD.Services.Bancos;

namespace SIAD.Tests.Bancos;

public class ChequeNumeracionCalculatorTests
{
    [Fact]
    public void ProximoValido_AsignaEseNumero_YSiguienteEsMasUno()
    {
        var r = ChequeNumeracionCalculator.Compute(proximoCheque: 105m, chequeMaximo: 200m);
        Assert.False(r.Agotado);
        Assert.Equal(105m, r.NumeroAsignado);
        Assert.Equal(106m, r.SiguienteProximo);
    }

    [Fact]
    public void ProximoCeroONegativo_SeNormalizaAUno()
    {
        var r0 = ChequeNumeracionCalculator.Compute(0m, 0m);
        Assert.Equal(1m, r0.NumeroAsignado);
        Assert.Equal(2m, r0.SiguienteProximo);

        var rNeg = ChequeNumeracionCalculator.Compute(-5m, 0m);
        Assert.Equal(1m, rNeg.NumeroAsignado);
    }

    [Fact]
    public void MaximoCero_NoValidaAgotamiento()
    {
        var r = ChequeNumeracionCalculator.Compute(999999m, 0m);
        Assert.False(r.Agotado);
        Assert.Equal(999999m, r.NumeroAsignado);
    }

    [Fact]
    public void ProximoIgualAlMaximo_TodaviaEmite()
    {
        var r = ChequeNumeracionCalculator.Compute(200m, 200m);
        Assert.False(r.Agotado);
        Assert.Equal(200m, r.NumeroAsignado);
    }

    [Fact]
    public void ProximoSuperaElMaximo_Agotado()
    {
        var r = ChequeNumeracionCalculator.Compute(201m, 200m);
        Assert.True(r.Agotado);
    }

    [Fact]
    public void DecimalesDeSimafi_SeTruncanAEntero()
    {
        // proximo_cheque es NUMERIC(28,4) migrado de SIMAFI: puede traer decimales.
        var r = ChequeNumeracionCalculator.Compute(105.0000m, 200m);
        Assert.Equal(105m, r.NumeroAsignado);
        var r2 = ChequeNumeracionCalculator.Compute(105.7m, 200m);
        Assert.Equal(105m, r2.NumeroAsignado);
    }
}
