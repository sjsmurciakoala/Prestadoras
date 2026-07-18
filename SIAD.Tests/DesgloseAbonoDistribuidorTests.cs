using SIAD.Services.Clientes;
using static SIAD.Services.Clientes.DesgloseAbonoDistribuidor;

namespace SIAD.Tests;

/// <summary>
/// Unit tests (sin BD) del reparto de pagos/abonos del desglose por servicio.
/// Invariante central: la suma de las filas devueltas == suma de ítems + pagos +
/// ajustes, para que el TOTAL del desglose siempre cuadre con el saldo del cliente.
/// </summary>
public sealed class DesgloseAbonoDistribuidorTests
{
    private static readonly IReadOnlyList<ItemDesglose> Items =
    [
        new("AGUA_POTABLE", "Agua potable", 400m, 1),
        new("ALCANTARILLADO", "Alcantarillado", 240m, 2),
        new("TASA_AMBIENTAL", "Tasa ambiental", 12m, 3)
    ];

    private static decimal Total(IEnumerable<SIAD.Core.DTOs.Clientes.SaldoServicioDto> filas)
        => filas.Sum(f => f.Saldo);

    [Fact]
    public void Reparte_por_porcentaje_exacto()
    {
        var pct = new Dictionary<string, decimal>
        {
            ["AGUA_POTABLE"] = 60m,
            ["ALCANTARILLADO"] = 40m
        };

        var filas = Distribuir(Items, pagos: -100m, ajustes: 0m, pct);

        var agua = filas.Single(f => f.Servicio == "Agua potable");
        Assert.Equal(400m - 60m, agua.Saldo);
        Assert.Equal(400m, agua.Deuda);      // deuda antes del reparto
        Assert.Equal(60m, agua.Porcentaje);  // % aplicado

        Assert.Equal(240m - 40m, filas.Single(f => f.Servicio == "Alcantarillado").Saldo);

        var tasa = filas.Single(f => f.Servicio == "Tasa ambiental");
        Assert.Equal(12m, tasa.Saldo);
        Assert.Equal(12m, tasa.Deuda);
        Assert.Null(tasa.Porcentaje);        // no participa del reparto

        Assert.DoesNotContain(filas, f => f.Servicio == "Pagos y ajustes");
        Assert.Equal(652m - 100m, Total(filas));
    }

    [Fact]
    public void Residuo_de_redondeo_cae_en_el_item_de_mayor_porcentaje()
    {
        var pct = new Dictionary<string, decimal>
        {
            ["AGUA_POTABLE"] = 33.34m,
            ["ALCANTARILLADO"] = 33.33m,
            ["TASA_AMBIENTAL"] = 33.33m
        };

        var filas = Distribuir(Items, pagos: -100m, ajustes: 0m, pct);

        // Los dos de 33.33 reciben -33.33; el de mayor peso absorbe el residuo (-33.34).
        Assert.Equal(240m - 33.33m, filas.Single(f => f.Servicio == "Alcantarillado").Saldo);
        Assert.Equal(12m - 33.33m, filas.Single(f => f.Servicio == "Tasa ambiental").Saldo);
        Assert.Equal(400m - 33.34m, filas.Single(f => f.Servicio == "Agua potable").Saldo);
        Assert.Equal(652m - 100m, Total(filas));
    }

    [Fact]
    public void Configuracion_incompleta_no_distribuye()
    {
        var pct = new Dictionary<string, decimal> { ["AGUA_POTABLE"] = 60m }; // suma 60 ≠ 100

        var filas = Distribuir(Items, pagos: -100m, ajustes: 0m, pct);

        var agua = filas.Single(f => f.Servicio == "Agua potable");
        Assert.Equal(400m, agua.Saldo);
        Assert.Equal(400m, agua.Deuda);   // sin reparto: deuda == saldo
        Assert.Null(agua.Porcentaje);     // el % no se aplicó (config incompleta)
        Assert.Equal(-100m, filas.Single(f => f.Servicio == "Pagos y ajustes").Saldo);
        Assert.Equal(652m - 100m, Total(filas));
    }

    [Fact]
    public void Sin_configuracion_conserva_fila_de_pagos()
    {
        var filas = Distribuir(Items, pagos: -50m, ajustes: -10m, new Dictionary<string, decimal>());

        Assert.Equal(-60m, filas.Single(f => f.Servicio == "Pagos y ajustes").Saldo);
        Assert.Equal(652m - 60m, Total(filas));
    }

    [Fact]
    public void Item_configurado_ausente_renormaliza_entre_los_presentes()
    {
        var pct = new Dictionary<string, decimal>
        {
            ["AGUA_POTABLE"] = 50m,
            ["ALCANTARILLADO"] = 25m,
            [CodigoSaldoAnterior] = 25m // el cliente no tiene saldo anterior
        };

        var filas = Distribuir(Items, pagos: -90m, ajustes: 0m, pct);

        // Pesos renormalizados: 50/75 y 25/75 → -60 y -30.
        Assert.Equal(400m - 60m, filas.Single(f => f.Servicio == "Agua potable").Saldo);
        Assert.Equal(240m - 30m, filas.Single(f => f.Servicio == "Alcantarillado").Saldo);
        Assert.Equal(652m - 90m, Total(filas));
    }

    [Fact]
    public void Ningun_item_configurado_presente_cae_a_pagos_y_ajustes()
    {
        var pct = new Dictionary<string, decimal> { [CodigoSaldoAnterior] = 100m };

        var filas = Distribuir(Items, pagos: -100m, ajustes: 0m, pct);

        Assert.Equal(-100m, filas.Single(f => f.Servicio == "Pagos y ajustes").Saldo);
        Assert.Equal(652m - 100m, Total(filas));
    }

    [Fact]
    public void Sin_pagos_solo_ajustes_no_distribuye()
    {
        var pct = new Dictionary<string, decimal> { ["AGUA_POTABLE"] = 100m };

        var filas = Distribuir(Items, pagos: 0m, ajustes: -25m, pct);

        Assert.Equal(400m, filas.Single(f => f.Servicio == "Agua potable").Saldo);
        Assert.Equal(-25m, filas.Single(f => f.Servicio == "Pagos y ajustes").Saldo);
        Assert.Equal(652m - 25m, Total(filas));
    }

    [Fact]
    public void Todo_en_cero_no_agrega_fila_de_pagos()
    {
        var filas = Distribuir(Items, pagos: 0m, ajustes: 0m, new Dictionary<string, decimal>());

        Assert.Equal(3, filas.Count);
        Assert.Equal(652m, Total(filas));
    }
}
