using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Xunit;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.DTOs.Configuracion;
using SIAD.Services.Almacen;
using SIAD.Services.Configuracion;

namespace SIAD.Tests.Almacen;

/// <summary>
/// Núcleo que empuja las alertas de stock por correo al área ALMACÉN. `IArticulosService` (detección)
/// y `ICorreoNotificador` (envío) se mockean con NSubstitute — sin BD.
/// </summary>
public class AlertasStockNotificadorTests
{
    private static AlertaStockDto Alerta(int id, int bodegaId, string codigo, string descripcion,
        string severidad, decimal existencia = 0m, decimal minimo = 0m) => new()
    {
        Id = id,
        BodegaId = bodegaId,
        BodegaNombre = "Bodega Central",
        Codigo = codigo,
        Descripcion = descripcion,
        Severidad = severidad,
        Existencia = existencia,
        ExistenciaMinima = minimo
    };

    [Fact]
    public async Task EnviarResumen_ConAlertas_NotificaAlmacenConLosArticulos()
    {
        var articulos = Substitute.For<IArticulosService>();
        articulos.GetAlertasStockAsync(null, Arg.Any<CancellationToken>())
            .Returns(new List<AlertaStockDto> { Alerta(1, 1, "ART-1", "Tornillo 1/2", StockSeveridad.BajoMinimo, 5m, 10m) });

        var correo = Substitute.For<ICorreoNotificador>();
        string? html = null;
        correo.NotificarAreaAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Do<string>(h => html = h), Arg.Any<CancellationToken>())
              .Returns(CorreoEnvioResultado.Ok(202));

        var sut = new AlertasStockNotificador(articulos, correo);
        var r = await sut.EnviarResumenAsync("motivo de prueba");

        Assert.True(r.Exito);
        await correo.Received(1).NotificarAreaAsync(TipoNotificacion.Almacen, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        // El correo identifica el artículo por ID interno + descripción (commit ad987d1, "correo de
        // stock por ID"); el código de negocio (ART-1) ya no se imprime.
        Assert.Contains("Tornillo 1/2", html);
    }

    [Fact]
    public async Task EnviarResumen_SinAlertas_OmiteSinNotificar()
    {
        var articulos = Substitute.For<IArticulosService>();
        articulos.GetAlertasStockAsync(null, Arg.Any<CancellationToken>())
            .Returns(new List<AlertaStockDto>());
        var correo = Substitute.For<ICorreoNotificador>();

        var sut = new AlertasStockNotificador(articulos, correo);
        var r = await sut.EnviarResumenAsync("x");

        Assert.True(r.Omitido);
        await correo.DidNotReceive().NotificarAreaAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnviarCruces_FiltraAlosParesQueSiguenEnAlerta()
    {
        var articulos = Substitute.For<IArticulosService>();
        articulos.GetAlertasStockAsync(null, Arg.Any<CancellationToken>())
            .Returns(new List<AlertaStockDto>
            {
                Alerta(1, 1, "A-1", "Uno", StockSeveridad.BajoMinimo),
                Alerta(2, 1, "A-2", "Dos", StockSeveridad.SinStock),
                Alerta(3, 2, "A-3", "Tres", StockSeveridad.BajoMinimo)
            });

        var correo = Substitute.For<ICorreoNotificador>();
        string? html = null;
        correo.NotificarAreaAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Do<string>(h => html = h), Arg.Any<CancellationToken>())
              .Returns(CorreoEnvioResultado.Ok(202));

        var sut = new AlertasStockNotificador(articulos, correo);
        var r = await sut.EnviarCrucesAsync(new[] { (2, 1) }, "Descargo 00007");

        Assert.True(r.Exito);
        // El correo imprime la descripción del artículo (no el código, ver commit ad987d1):
        // "Dos" (id 2) cruzó y sigue en alerta; "Uno" (id 1) y "Tres" (id 3) quedaron fuera.
        Assert.Contains("Dos", html);       // el que cruzó
        Assert.DoesNotContain("Uno", html); // los otros no
        Assert.DoesNotContain("Tres", html);
    }

    [Fact]
    public async Task EnviarCruces_NingunoSigueEnAlerta_Omite()
    {
        var articulos = Substitute.For<IArticulosService>();
        articulos.GetAlertasStockAsync(null, Arg.Any<CancellationToken>())
            .Returns(new List<AlertaStockDto> { Alerta(1, 1, "A-1", "Uno", StockSeveridad.BajoMinimo) });
        var correo = Substitute.For<ICorreoNotificador>();

        var sut = new AlertasStockNotificador(articulos, correo);
        var r = await sut.EnviarCrucesAsync(new[] { (9, 9) }, "Descargo 00008");

        Assert.True(r.Omitido);
        await correo.DidNotReceive().NotificarAreaAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
