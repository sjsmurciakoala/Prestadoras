using Xunit;
using SIAD.Core.DTOs.Almacen;

namespace SIAD.Tests.Almacen;

/// <summary>
/// Regla de clasificación de severidad (base de la detección de cruce del motor de posteo). Pura,
/// sin BD.
/// </summary>
public class StockSeveridadTests
{
    [Fact]
    public void Clasificar_CubreLosCasos()
    {
        Assert.Equal(StockSeveridad.Negativa, StockSeveridad.Clasificar(-1m, 0m));
        Assert.Equal(StockSeveridad.SinStock, StockSeveridad.Clasificar(0m, 0m));
        Assert.Equal(StockSeveridad.BajoMinimo, StockSeveridad.Clasificar(5m, 10m));
        Assert.Null(StockSeveridad.Clasificar(10m, 10m));   // igual al mínimo = en orden
        Assert.Null(StockSeveridad.Clasificar(15m, 10m));
        Assert.Null(StockSeveridad.Clasificar(5m, 0m));     // sin mínimo definido, positivo = en orden
    }

    [Fact]
    public void Clasificar_Cruce_OrdenAAlerta()
    {
        // El motor marca cruce cuando antes = null (en orden) y después != null (alerta).
        Assert.Null(StockSeveridad.Clasificar(12m, 10m));                 // antes: en orden
        Assert.Equal(StockSeveridad.BajoMinimo, StockSeveridad.Clasificar(8m, 10m)); // después: alerta
        // Estando ya bajo, seguir bajando NO es un cruce nuevo (ambos != null).
        Assert.NotNull(StockSeveridad.Clasificar(8m, 10m));
        Assert.NotNull(StockSeveridad.Clasificar(3m, 10m));
    }
}
