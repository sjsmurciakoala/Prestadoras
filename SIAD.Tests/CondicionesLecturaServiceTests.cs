using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.CondicionesLectura;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.CondicionesLectura;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

// ABM de condiciones de lectura por empresa (portal). Verifica lectura del
// catálogo (tipos + condiciones), upsert/borrado del conjunto y validaciones.
// Todo dentro de la transacción del fixture (GuardarAsync reusa la transacción
// ambiente en lugar de abrir una propia).
[Collection("Postgres")]
public sealed class CondicionesLecturaServiceTests : IntegrationTestBase, IDisposable
{
    private SiadDbContext? _context;

    public CondicionesLecturaServiceTests(PostgresFixture fixture) : base(fixture) { }

    public void Dispose() => _context?.Dispose();

    private CondicionesLecturaService CrearServicio()
    {
        var options = new DbContextOptionsBuilder<SiadDbContext>()
            .UseNpgsql(Connection)
            .Options;
        _context = new SiadDbContext(options, new CompanyFija(CompanyId));
        _context.Database.UseTransaction(Transaction);
        return new CondicionesLecturaService(_context);
    }

    [SkippableFact]
    public async Task Obtener_devuelve_tipos_globales_y_condiciones_de_la_empresa()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");
        var servicio = CrearServicio();

        var catalogo = await servicio.ObtenerAsync(CompanyId);

        // Los 4 tipos globales del vocabulario fijo.
        Assert.Contains(catalogo.Tipos, t => t.Tipo == "N" && t.RequiereLectura);
        Assert.Contains(catalogo.Tipos, t => t.Tipo == "MIN" && !t.RequiereLectura);
        Assert.Contains(catalogo.Tipos, t => t.Tipo == "PND");
        Assert.Contains(catalogo.Tipos, t => t.Tipo == "PD");

        // El seed por empresa (script DDL) dejó las 4 estándar.
        Assert.Contains(catalogo.Condiciones, c => c.Codigo == "N");
    }

    [SkippableFact]
    public async Task Guardar_inserta_actualiza_y_borra_el_conjunto()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");
        var servicio = CrearServicio();

        var inicial = await servicio.ObtenerAsync(CompanyId);
        var existente = inicial.Condiciones.First();
        existente.Descripcion = "MODIFICADA POR TEST";

        // Solo dejamos la existente (modificada) + una nueva → el resto se borra.
        var nueva = new CondicionLecturaAdminDto
        {
            CondicionLecturaId = 0,
            Codigo = "NUEVA1",
            Descripcion = "Condición nueva de test",
            Tipo = "PD",
            Facturacion = "N",
            AplicaDescuento = "S",
            Orden = 50,
            Activo = true,
        };

        var resultado = await servicio.GuardarAsync(CompanyId,
            new List<CondicionLecturaAdminDto> { existente, nueva }, "test-abm");

        // Quedaron exactamente 2: la modificada y la nueva.
        Assert.Equal(2, resultado.Condiciones.Count);
        Assert.Contains(resultado.Condiciones, c => c.Codigo == existente.Codigo && c.Descripcion == "MODIFICADA POR TEST");
        var creada = resultado.Condiciones.Single(c => c.Codigo == "NUEVA1");
        Assert.True(creada.CondicionLecturaId > 0, "La nueva debe recibir un id real.");
        Assert.Equal("PD", creada.Tipo);
        Assert.Equal("N", creada.Facturacion);
        Assert.Equal("S", creada.AplicaDescuento);
    }

    [SkippableFact]
    public async Task Guardar_permite_intercambiar_codigos_en_un_solo_guardado()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");
        var servicio = CrearServicio();

        // Parte de dos condiciones conocidas y les intercambia el código en un solo
        // Guardar: el UNIQUE(company_id, codigo) es DEFERRABLE, así que el duplicado
        // transitorio se resuelve al commit (antes tiraba 23505).
        var baseCat = await servicio.GuardarAsync(CompanyId, new List<CondicionLecturaAdminDto>
        {
            new() { Codigo = "SWAPA", Descripcion = "A", Tipo = "N", Orden = 1 },
            new() { Codigo = "SWAPB", Descripcion = "B", Tipo = "MIN", Orden = 2 },
        }, "test-abm");

        var a = baseCat.Condiciones.Single(c => c.Codigo == "SWAPA");
        var b = baseCat.Condiciones.Single(c => c.Codigo == "SWAPB");
        a.Codigo = "SWAPB";
        b.Codigo = "SWAPA";

        var resultado = await servicio.GuardarAsync(CompanyId,
            new List<CondicionLecturaAdminDto> { a, b }, "test-abm");

        // Los códigos quedaron intercambiados (mismos ids, códigos cruzados).
        Assert.Equal("SWAPB", resultado.Condiciones.Single(c => c.CondicionLecturaId == a.CondicionLecturaId).Codigo);
        Assert.Equal("SWAPA", resultado.Condiciones.Single(c => c.CondicionLecturaId == b.CondicionLecturaId).Codigo);
    }

    [SkippableFact]
    public async Task Guardar_rechaza_codigo_duplicado()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");
        var servicio = CrearServicio();

        var lista = new List<CondicionLecturaAdminDto>
        {
            new() { Codigo = "DUP", Descripcion = "Uno", Tipo = "N", Orden = 1 },
            new() { Codigo = "dup", Descripcion = "Dos", Tipo = "N", Orden = 2 }, // colisiona (case-insensitive)
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.GuardarAsync(CompanyId, lista, "test-abm"));
    }

    [SkippableFact]
    public async Task Guardar_rechaza_tipo_invalido()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");
        var servicio = CrearServicio();

        var lista = new List<CondicionLecturaAdminDto>
        {
            new() { Codigo = "XX", Descripcion = "Tipo inválido", Tipo = "NOEXISTE", Orden = 1 },
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.GuardarAsync(CompanyId, lista, "test-abm"));
    }

    private sealed class CompanyFija : ICurrentCompanyService
    {
        private readonly long _companyId;
        public CompanyFija(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
