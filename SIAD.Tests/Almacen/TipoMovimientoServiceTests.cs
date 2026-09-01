using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.Tenancy;
using SIAD.Core.DTOs.Almacen;
using SIAD.Services.Almacen;
using SIAD.Data;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Almacen;

/// <summary>
/// Catálogo de tipos de movimiento de almacén (Fase 1 de
/// <c>docs/plans/2026-08-01-movimientos-almacen-catalogo-diseno.md</c>, plan de pruebas §7).
/// <para>
/// Requiere que <c>Database/2026-08-01_alm_tipo_movimiento.sql</c> esté aplicado en la base
/// apuntada por <c>SIAD_TEST_DB</c>; sin la tabla, estas pruebas fallan en vez de saltarse.
/// </para>
/// </summary>
[Collection("Postgres")]
public class TipoMovimientoServiceTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private ITipoMovimientoService? _service;

    public TipoMovimientoServiceTests(PostgresFixture fixture) : base(fixture)
    {
    }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();

        if (Fixture.Available)
        {
            _context = CrearContexto(CompanyId);
            _service = new TipoMovimientoService(_context);
        }
    }

    /// <summary>
    /// Un contexto más sobre la MISMA conexión y transacción del test (para que el ROLLBACK
    /// final lo limpie todo), pero con otra empresa: así se prueba el aislamiento multiempresa.
    /// </summary>
    private SiadDbContext CrearContexto(long companyId)
    {
        var options = new DbContextOptionsBuilder<SiadDbContext>()
            .UseNpgsql(Connection)
            .Options;

        var ctx = new SiadDbContext(options, new TestCurrentCompanyService(companyId));
        ctx.Database.UseTransaction(Transaction);
        return ctx;
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    private static TipoMovimientoAlmacenDto Nuevo(string codigo, string nombre, string clase, bool activo = true) => new()
    {
        Codigo = codigo,
        Nombre = nombre,
        Clase = clase,
        Activo = activo
    };

    // ── 1 ───────────────────────────────────────────────────────────────────────
    [SkippableFact]
    public async Task Crear_CodigoDuplicadoEnLaMismaEmpresa_Rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await _service!.CrearAsync(Nuevo("TM_DUP", "Merma por vencimiento", "SALIDA"), "tester");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CrearAsync(Nuevo("TM_DUP", "Otro nombre", "ENTRADA"), "tester"));

        Assert.Contains("TM_DUP", ex.Message);
    }

    // ── 2 ───────────────────────────────────────────────────────────────────────
    [SkippableFact]
    public async Task Crear_MismoCodigoEnDosEmpresas_AmbosSeCrean()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // La unicidad es (company_id, codigo): el mismo código en otra empresa es legítimo.
        var otraEmpresa = CompanyId + 90_000;
        using var contextOtra = CrearContexto(otraEmpresa);
        var servicioOtra = new TipoMovimientoService(contextOtra);

        var enEmpresaActual = await _service!.CrearAsync(Nuevo("TM_MULTI", "Donación", "SALIDA"), "tester");
        var enOtraEmpresa = await servicioOtra.CrearAsync(Nuevo("TM_MULTI", "Donación", "SALIDA"), "tester");

        Assert.NotNull(enEmpresaActual.Id);
        Assert.NotNull(enOtraEmpresa.Id);
        Assert.NotEqual(enEmpresaActual.Id, enOtraEmpresa.Id);

        // Y cada empresa sólo ve el suyo (el filtro global de tenant no se puede eludir).
        var deLaActual = await _service.GetAsync(soloActivos: false);
        Assert.Single(deLaActual.Where(t => t.Codigo == "TM_MULTI"));

        var deLaOtra = await servicioOtra.GetAsync(soloActivos: false);
        var unicoDeLaOtra = Assert.Single(deLaOtra.Where(t => t.Codigo == "TM_MULTI"));
        Assert.Equal(enOtraEmpresa.Id, unicoDeLaOtra.Id);
    }

    // ── 3 ───────────────────────────────────────────────────────────────────────
    [SkippableTheory]
    [InlineData("ENTRADA_X")]
    [InlineData("")]
    // Nota: 'TRASLADO' era un caso inválido antes de la Fase 5; el paso 29 la volvió una clase
    // VÁLIDA (widening de ck_alm_tipo_movimiento_clase + ClaseMovimientoInventario, hallazgo R-6),
    // así que ya no pertenece aquí.
    public async Task Crear_ClaseInvalida_Rechaza(string clase)
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // El servicio es la primera red; el CHECK ck_alm_tipo_movimiento_clase es la última.
        await Assert.ThrowsAnyAsync<Exception>(() =>
            _service!.CrearAsync(Nuevo("TM_CLASE", "Clase inválida", clase), "tester"));
    }

    // ── 4 ───────────────────────────────────────────────────────────────────────
    [SkippableFact]
    public async Task Desactivar_TipoSinMovimientos_QuedaInactivoYEsIdempotente()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var creado = await _service!.CrearAsync(Nuevo("TM_OFF", "Consumo interno", "SALIDA"), "tester");
        var id = creado.Id!.Value;

        Assert.True(await _service.DesactivarAsync(id, "tester"));

        var despues = await _service.GetByIdAsync(id);
        Assert.NotNull(despues);
        Assert.False(despues!.Activo);

        // Desactivar dos veces no falla ni cambia nada: la operación es idempotente.
        Assert.True(await _service.DesactivarAsync(id, "tester"));
    }

    [SkippableFact]
    public async Task Desactivar_IdInexistente_DevuelveFalse()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        Assert.False(await _service!.DesactivarAsync(987_654_321, "tester"));
    }

    // ── 5 ───────────────────────────────────────────────────────────────────────
    /// <summary>
    /// ⚠️ FASE 2. El diseño (§7, caso 5) pide que cambiar la <c>clase</c> de un tipo con
    /// movimientos posteados sea rechazado. Esa guarda YA está escrita en
    /// <c>TipoMovimientoService.ActualizarAsync</c>, pero hoy es inalcanzable: no existe
    /// <c>alm_movimiento_dtl</c>, así que <c>TieneMovimientosPosteadosAsync</c> devuelve
    /// <c>false</c> fijo y ningún tipo puede estar en uso.
    /// <para>
    /// Esta prueba fija el comportamiento ACTUAL (la clase es editable mientras el tipo no se
    /// use) para que quede documentado y no se lea como un olvido. Cuando la Fase 2 conecte el
    /// conteo real, hay que agregar aquí la prueba negativa correspondiente.
    /// </para>
    /// </summary>
    [SkippableFact]
    public async Task Actualizar_CambiarClase_SinMovimientosPosteados_SePermite()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var creado = await _service!.CrearAsync(Nuevo("TM_CLS", "Reclasificable", "ENTRADA"), "tester");
        var id = creado.Id!.Value;

        var dto = await _service.GetByIdAsync(id);
        Assert.NotNull(dto);
        dto!.Clase = "SALIDA";

        await _service.ActualizarAsync(id, dto, "tester");

        var despues = await _service.GetByIdAsync(id);
        Assert.Equal("SALIDA", despues!.Clase);
    }

    [SkippableFact]
    public async Task Actualizar_CodigoQueYaUsaOtroTipo_Rechaza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        await _service!.CrearAsync(Nuevo("TM_A", "Primero", "ENTRADA"), "tester");
        var segundo = await _service.CrearAsync(Nuevo("TM_B", "Segundo", "ENTRADA"), "tester");

        var dto = await _service.GetByIdAsync(segundo.Id!.Value);
        dto!.Codigo = "TM_A";

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ActualizarAsync(segundo.Id!.Value, dto, "tester"));
    }

    // ── 6 ───────────────────────────────────────────────────────────────────────
    [SkippableFact]
    public async Task Get_TipoInactivo_SoloApareceCuandoNoSeFiltraPorActivos()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var creado = await _service!.CrearAsync(Nuevo("TM_VIS", "Para ocultar", "SALIDA"), "tester");
        var id = creado.Id!.Value;
        await _service.DesactivarAsync(id, "tester");

        var activos = await _service.GetAsync(soloActivos: true);
        Assert.DoesNotContain(activos, t => t.Id == id);

        var todos = await _service.GetAsync(soloActivos: false);
        Assert.Contains(todos, t => t.Id == id);
    }

    [SkippableFact]
    public async Task Crear_NormalizaElCodigoAMayusculas()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var creado = await _service!.CrearAsync(Nuevo("tm_min", "Minúsculas", "ENTRADA"), "tester");

        Assert.Equal("TM_MIN", creado.Codigo);

        var leido = await _service.GetByIdAsync(creado.Id!.Value);
        Assert.Equal("TM_MIN", leido!.Codigo);
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
