using Dapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.MobileApi;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.MobileApi;

// Catálogo de condiciones de lectura (app_lectores, spec §4/§5). Verifica:
//   - GET /api/condiciones (GetCondicionesAsync): scopeado por empresa, solo
//     activas, ordenado por `orden`, requiereLectura derivado del tipo.
//   - La columna destino historicomedicion.condicion admite el código completo
//     sin truncar (era varchar(3), ahora varchar(10)) — spec §5.3.
// Todo corre dentro de la transacción del fixture (rollback al final).
[Collection("Postgres")]
public sealed class CondicionesLecturaMobileTests : IntegrationTestBase, IDisposable
{
    private SiadDbContext? _context;

    public CondicionesLecturaMobileTests(PostgresFixture fixture) : base(fixture) { }

    public void Dispose() => _context?.Dispose();

    private LectoresMobileService CrearServicio()
    {
        var options = new DbContextOptionsBuilder<SiadDbContext>()
            .UseNpgsql(Connection)
            .Options;
        _context = new SiadDbContext(options, new CompanyFija(CompanyId));
        _context.Database.UseTransaction(Transaction);
        return new LectoresMobileService(_context);
    }

    private async Task InsertarCondicionAsync(string codigo, string tipo, int orden, bool activo,
        string facturacion = "S", string aplicaDescuento = "N")
    {
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.adm_condicion_lectura
                (company_id, codigo, descripcion, tipo, facturacion, aplica_descuento, orden, activo, created_by)
            VALUES (@CompanyId, @Codigo, @Codigo || ' desc', @Tipo, @Fact, @Desc, @Orden, @Activo, 'test-cond')
            ON CONFLICT (company_id, codigo) DO UPDATE
              SET tipo = EXCLUDED.tipo, orden = EXCLUDED.orden, activo = EXCLUDED.activo,
                  facturacion = EXCLUDED.facturacion, aplica_descuento = EXCLUDED.aplica_descuento;",
            new { CompanyId, Codigo = codigo, Tipo = tipo, Fact = facturacion, Desc = aplicaDescuento, Orden = orden, Activo = activo },
            Transaction));
    }

    [SkippableFact]
    public async Task GetCondiciones_scopeado_activas_ordenadas()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");
        // Dos activas (orden distinto) y una inactiva que NO debe aparecer.
        await InsertarCondicionAsync("ZZB", "MIN", 92, activo: true);
        await InsertarCondicionAsync("ZZA", "N", 91, activo: true);
        await InsertarCondicionAsync("ZZC", "PD", 93, activo: false);
        var servicio = CrearServicio();

        var condiciones = await servicio.GetCondicionesAsync(CompanyId);

        // La inactiva quedó fuera.
        Assert.DoesNotContain(condiciones, c => c.Codigo == "ZZC");
        Assert.Contains(condiciones, c => c.Codigo == "ZZA");
        Assert.Contains(condiciones, c => c.Codigo == "ZZB");

        // Ordenado por `orden` ascendente (ZZA orden 91 antes que ZZB orden 92).
        var idxA = condiciones.FindIndex(c => c.Codigo == "ZZA");
        var idxB = condiciones.FindIndex(c => c.Codigo == "ZZB");
        Assert.True(idxA < idxB, "Las condiciones deben venir ordenadas por `orden`.");
    }

    [SkippableFact]
    public async Task GetCondiciones_requiereLectura_derivado_del_tipo()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");
        await InsertarCondicionAsync("ZZN", "N", 94, activo: true);    // requiere lectura
        await InsertarCondicionAsync("ZZM", "MIN", 95, activo: true);  // no requiere
        await InsertarCondicionAsync("ZZP", "PD", 96, activo: true);   // no requiere
        var servicio = CrearServicio();

        var condiciones = await servicio.GetCondicionesAsync(CompanyId);

        Assert.True(condiciones.Single(c => c.Codigo == "ZZN").RequiereLectura);
        Assert.False(condiciones.Single(c => c.Codigo == "ZZM").RequiereLectura);
        Assert.False(condiciones.Single(c => c.Codigo == "ZZP").RequiereLectura);
    }

    [SkippableFact]
    public async Task Condicion_lectura_columna_destino_es_varchar10()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");

        var ancho = await Connection.ExecuteScalarAsync<int?>(new CommandDefinition(@"
            SELECT character_maximum_length
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'historicomedicion' AND column_name = 'condicion';",
            transaction: Transaction));

        Assert.True(ancho >= 10,
            $"historicomedicion.condicion debe ser varchar(10)+ para no truncar los códigos; hoy es varchar({ancho}).");
    }

    [SkippableFact]
    public async Task Historicomedicion_condicion_persiste_codigo_completo_sin_truncar()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");

        // Toma una fila de medición cualquiera de la empresa y le escribe un código
        // multichar (como haría el MobileApi con el código completo). Antes del
        // ensanche (varchar(3)) esto habría truncado 'PROMEDIO10' → 'PRO'.
        var ide = await Connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT ide FROM public.historicomedicion WHERE company_id = @CompanyId ORDER BY ide DESC LIMIT 1;",
            new { CompanyId }, Transaction));
        Skip.If(ide is null, "No hay filas en historicomedicion para esta empresa.");

        const string codigoCompleto = "PROMEDIO10"; // 10 caracteres
        await Connection.ExecuteAsync(new CommandDefinition(
            "UPDATE public.historicomedicion SET condicion = @Cond WHERE ide = @Ide;",
            new { Cond = codigoCompleto, Ide = ide }, Transaction));

        var leido = await Connection.ExecuteScalarAsync<string>(new CommandDefinition(
            "SELECT condicion FROM public.historicomedicion WHERE ide = @Ide;",
            new { Ide = ide }, Transaction));

        Assert.Equal(codigoCompleto, leido);
    }

    private sealed class CompanyFija : ICurrentCompanyService
    {
        private readonly long _companyId;
        public CompanyFija(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
