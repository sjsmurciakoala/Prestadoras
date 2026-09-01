using System;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Retenciones;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Retenciones;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Retenciones;

/// <summary>
/// Ejercita <see cref="RetencionesService"/> contra el SiadDbContext real (EF) sobre el mirror.
/// Valida en runtime: el modelo EF (ConfigureRetencionesModel), las proyecciones LINQ, el filtro
/// tenant automático de prv_retencion_cuenta y el estampado de company_id en SaveChanges.
/// (CambiarTasaAsync no se prueba acá: abre su propia transacción, incompatible con la transacción
/// compartida del test; su no-solape ya se cubre en RetencionesCatalogoTests vía el EXCLUDE.)
/// </summary>
[Collection("Postgres")]
public sealed class RetencionesServiceTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private RetencionesService? _service;

    public RetencionesServiceTests(PostgresFixture fixture) : base(fixture) { }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (Fixture.Available)
        {
            var options = new DbContextOptionsBuilder<SiadDbContext>().UseNpgsql(Connection).Options;
            var company = new TestCurrentCompanyService(CompanyId);
            _context = new SiadDbContext(options, company);
            _context.Database.UseTransaction(Transaction);
            _service = new RetencionesService(_context);
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    private Task<long?> CuentaPosteableAsync(long? distintaDe = null) =>
        Connection.ExecuteScalarAsync<long?>(new CommandDefinition(@"
            SELECT account_id FROM public.con_plan_cuentas
            WHERE company_id = @CompanyId AND allows_posting AND (@Distinta IS NULL OR account_id <> @Distinta)
            LIMIT 1",
            new { CompanyId, Distinta = distintaDe }, Transaction));

    [SkippableFact]
    public async Task Servicio_crea_lista_actualiza_y_desactiva()
    {
        var svc = _service!;

        var creada = await svc.CreateAsync(new RetencionEditDto
        {
            Codigo = "TEST-SVC-1",
            Nombre = "Retención por servicio",
            BaseCalculo = BaseRetencion.SinIsv,
            TipoImpuesto = TipoImpuestoRetencion.Isr
        }, "test");
        Assert.NotNull(creada.Id);
        var id = creada.Id!.Value;

        await svc.CreateTasaAsync(new RetencionTasaDto
        {
            RetencionId = id,
            Porcentaje = 12.50m,
            VigenciaDesde = new DateOnly(2020, 1, 1)
        }, "test");

        // Lista: valida las proyecciones (PorcentajeVigente, TasasVigentes).
        var lista = await svc.GetAsync(new RetencionFilterDto { Search = "TEST-SVC-1" });
        var item = Assert.Single(lista);
        Assert.Equal(12.50m, item.PorcentajeVigente);
        Assert.Equal(1, item.TasasVigentes);
        Assert.False(item.CuentaConfigurada);

        // Detalle: retención + tasas.
        var detalle = await svc.GetDetalleAsync(id);
        Assert.NotNull(detalle);
        Assert.Single(detalle!.Tasas);

        // Update.
        var actualizada = await svc.UpdateAsync(id, new RetencionEditDto
        {
            Id = id,
            Codigo = "TEST-SVC-1B",
            Nombre = "Renombrada",
            BaseCalculo = BaseRetencion.SinIsv,
            TipoImpuesto = TipoImpuestoRetencion.Isr,
            Activo = true
        }, "test");
        Assert.Equal("TEST-SVC-1B", actualizada.Codigo);

        // Baja lógica.
        var ok = await svc.DeactivateAsync(id, "test");
        Assert.True(ok);
        var trasBaja = await svc.GetByIdAsync(id);
        Assert.False(trasBaja!.Activo);
    }

    [SkippableFact]
    public async Task Servicio_rechaza_tasa_con_vigencia_solapada()
    {
        var svc = _service!;

        var creada = await svc.CreateAsync(new RetencionEditDto
        {
            Codigo = "TEST-SVC-OVL",
            Nombre = "x",
            BaseCalculo = BaseRetencion.SinIsv,
            TipoImpuesto = TipoImpuestoRetencion.Isr
        }, "test");
        var id = creada.Id!.Value;

        await svc.CreateTasaAsync(new RetencionTasaDto
        {
            RetencionId = id,
            Porcentaje = 12.50m,
            VigenciaDesde = new DateOnly(2020, 1, 1)
        }, "test");

        // Segunda tasa que se pisa: el servicio la rechaza con mensaje humano (antes del EXCLUDE).
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CreateTasaAsync(new RetencionTasaDto
            {
                RetencionId = id,
                Porcentaje = 13.00m,
                VigenciaDesde = new DateOnly(2021, 1, 1)
            }, "test"));

        Assert.Contains("solapa", ex.Message);
    }

    [SkippableFact]
    public async Task Servicio_configura_cuenta_del_pasivo_por_empresa()
    {
        var svc = _service!;

        var accountId = await CuentaPosteableAsync();
        Skip.If(accountId is null, "No hay cuenta posteable en la empresa de prueba.");

        var creada = await svc.CreateAsync(new RetencionEditDto
        {
            Codigo = "TEST-SVC-CTA",
            Nombre = "x",
            BaseCalculo = BaseRetencion.SinIsv,
            TipoImpuesto = TipoImpuestoRetencion.Isr
        }, "test");
        var id = creada.Id!.Value;

        // Cuentas posteables: valida el filtro tenant sobre con_plan_cuentas.
        var cuentas = await svc.GetCuentasPosteablesAsync();
        Assert.NotEmpty(cuentas);

        // Alta: valida el mapeo EF de prv_retencion_cuenta y el estampado de company_id.
        var set = await svc.SetCuentaAsync(id, new RetencionCuentaDto
        {
            RetencionId = id,
            AccountId = accountId!.Value,
            Activo = true
        }, "test");
        Assert.Equal(accountId.Value, set.AccountId);

        // Se refleja en la lista (subconsulta correlacionada, filtrada por empresa).
        var lista = await svc.GetAsync(new RetencionFilterDto { Search = "TEST-SVC-CTA" });
        var item = Assert.Single(lista);
        Assert.True(item.CuentaConfigurada);
        Assert.False(string.IsNullOrWhiteSpace(item.CuentaCodigo));

        // Update por el mismo path (upsert): otra cuenta si existe.
        var otra = await CuentaPosteableAsync(distintaDe: accountId.Value);
        if (otra is not null)
        {
            var upd = await svc.SetCuentaAsync(id, new RetencionCuentaDto
            {
                RetencionId = id,
                AccountId = otra.Value,
                Activo = true
            }, "test");
            Assert.Equal(otra.Value, upd.AccountId);
        }

        // Y hay exactamente una fila por (empresa, retención): el upsert no duplicó.
        var filas = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(@"
            SELECT count(*) FROM public.prv_retencion_cuenta
            WHERE company_id = @CompanyId AND retencion_id = @Id",
            new { CompanyId, Id = id }, Transaction));
        Assert.Equal(1, filas);
    }

    private sealed class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
