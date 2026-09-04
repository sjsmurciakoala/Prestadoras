using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Proveedores;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Proveedores;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Proveedores;

/// <summary>
/// Incidencias de recepción (F4) y su efecto sobre el scorecard: la primera incidencia
/// registrada **enciende** el criterio CALIDAD, que hasta entonces se reporta sin datos.
/// </summary>
[Collection("Postgres")]
public class RecepcionIncidenciaTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private RecepcionIncidenciaService? _service;
    private EvaluacionProveedorService? _evaluacion;

    private int _compraHdrId;
    private string _codProveedor = string.Empty;
    private DateOnly _fechaRecepcion;

    public RecepcionIncidenciaTests(PostgresFixture fixture) : base(fixture)
    {
    }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (!Fixture.Available)
        {
            return;
        }

        var options = new DbContextOptionsBuilder<SiadDbContext>()
            .UseNpgsql(Connection)
            .Options;

        _context = new SiadDbContext(options, new TestCurrentCompanyService(CompanyId));
        _context.Database.UseTransaction(Transaction);
        _service = new RecepcionIncidenciaService(_context, new TestCurrentCompanyService(CompanyId));
        _evaluacion = new EvaluacionProveedorService(_context, new TestCurrentCompanyService(CompanyId));

        // Una recepción NO anulada cualquiera de la empresa de pruebas.
        var recepcion = await Connection.QuerySingleOrDefaultAsync<(int Id, string Cod, DateOnly Fecha)?>(
            new CommandDefinition(@"
                SELECT h.id, h.cod_proveedor, h.fecha
                  FROM alm_compra_hdr h
                 WHERE h.company_id = @CompanyId AND h.estado <> 9
                 ORDER BY h.fecha DESC
                 LIMIT 1",
                new { CompanyId }, Transaction));

        if (recepcion.HasValue)
        {
            _compraHdrId = recepcion.Value.Id;
            _codProveedor = recepcion.Value.Cod;
            _fechaRecepcion = recepcion.Value.Fecha;
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    // ── Alta y validaciones ──────────────────────────────────────────────────

    [SkippableFact]
    public async Task Crear_GuardaLaIncidenciaConLosDatosDeLaRecepcion()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        ExigirRecepcion();

        var creada = await _service!.CrearAsync(NuevaIncidencia(), "bodeguero");

        Assert.True(creada.Id > 0);
        Assert.Equal(_compraHdrId, creada.CompraHdrId);
        Assert.Equal(_codProveedor, creada.CodProveedor);
        Assert.Equal(TipoIncidenciaRecepcion.Devolucion, creada.Tipo);
        Assert.Equal("bodeguero", creada.UsuarioCreacion);

        // Sin fecha explícita, hereda la de la recepción.
        Assert.Equal(_fechaRecepcion, creada.Fecha);
    }

    [SkippableFact]
    public async Task Crear_SinDescripcion_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        ExigirRecepcion();

        var dto = NuevaIncidencia();
        dto.Descripcion = "   ";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service!.CrearAsync(dto, "bodeguero"));
        Assert.Contains("Describa la incidencia", ex.Message);
    }

    [SkippableFact]
    public async Task Crear_RecepcionInexistente_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        ExigirRecepcion();

        var dto = NuevaIncidencia();
        dto.CompraHdrId = int.MaxValue;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service!.CrearAsync(dto, "bodeguero"));
        Assert.Contains("no existe", ex.Message);
    }

    [SkippableFact]
    public async Task Crear_FechaAnteriorALaRecepcion_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        ExigirRecepcion();

        var dto = NuevaIncidencia();
        dto.Fecha = _fechaRecepcion.AddDays(-1);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service!.CrearAsync(dto, "bodeguero"));
        Assert.Contains("anterior a la de la recepción", ex.Message);
    }

    [SkippableFact]
    public async Task Crear_TipoInvalido_Lanza()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        ExigirRecepcion();

        var dto = NuevaIncidencia();
        dto.Tipo = 7;   // fuera del CHECK de la BD

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service!.CrearAsync(dto, "bodeguero"));
    }

    // ── Edición, borrado y consulta ──────────────────────────────────────────

    [SkippableFact]
    public async Task Actualizar_CambiaTipoYDescripcion()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        ExigirRecepcion();

        var creada = await _service!.CrearAsync(NuevaIncidencia(), "bodeguero");

        var cambios = NuevaIncidencia();
        cambios.Tipo = TipoIncidenciaRecepcion.Faltante;
        cambios.Descripcion = "Faltaron 3 cajas del pedido.";
        cambios.Cantidad = 3m;

        var actualizada = await _service.ActualizarAsync(creada.Id, cambios, "bodeguero");

        Assert.Equal(TipoIncidenciaRecepcion.Faltante, actualizada.Tipo);
        Assert.Equal("Faltaron 3 cajas del pedido.", actualizada.Descripcion);
        Assert.Equal(3m, actualizada.Cantidad);
    }

    [SkippableFact]
    public async Task Eliminar_QuitaLaIncidencia()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        ExigirRecepcion();

        var creada = await _service!.CrearAsync(NuevaIncidencia(), "bodeguero");

        Assert.True(await _service.EliminarAsync(creada.Id));
        Assert.Null(await _service.GetByIdAsync(creada.Id));
        Assert.False(await _service.EliminarAsync(creada.Id));   // ya no está
    }

    [SkippableFact]
    public async Task Get_FiltraPorTipoYProveedor()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        ExigirRecepcion();

        var dano = NuevaIncidencia();
        dano.Tipo = TipoIncidenciaRecepcion.Dano;
        dano.Descripcion = "Material con golpes de transporte.";
        var creadaDano = await _service!.CrearAsync(dano, "bodeguero");
        await _service.CrearAsync(NuevaIncidencia(), "bodeguero");   // devolución

        var soloDano = await _service.GetAsync(new RecepcionIncidenciaFilterDto
        {
            Tipo = TipoIncidenciaRecepcion.Dano,
            CodProveedor = _codProveedor
        });

        Assert.Contains(soloDano, i => i.Id == creadaDano.Id);
        Assert.All(soloDano, i => Assert.Equal(TipoIncidenciaRecepcion.Dano, i.Tipo));
    }

    [SkippableFact]
    public async Task BuscarRecepciones_TraeLasDelProveedorSinAnuladas()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        ExigirRecepcion();

        var recepciones = await _service!.BuscarRecepcionesAsync(_codProveedor);

        Assert.NotEmpty(recepciones);
        Assert.Contains(recepciones, r => r.Id == _compraHdrId);
    }

    // ── Efecto en el scorecard ───────────────────────────────────────────────

    [SkippableFact]
    public async Task PrimeraIncidencia_EnciendeElCriterioDeCalidad()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        ExigirRecepcion();

        var sinIncidencias = await Connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM prv_recepcion_incidencia WHERE company_id = @c",
            new { c = CompanyId }, Transaction);
        Skip.If(sinIncidencias > 0, "La base ya tiene incidencias: el criterio ya estaba encendido.");

        var criterios = await Connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM prv_evaluacion_criterio WHERE company_id = @c",
            new { c = CompanyId }, Transaction);
        Skip.If(criterios == 0, "Falta aplicar Database/2026-08-14_prv_evaluacion.sql.");

        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var periodo = await _evaluacion!.CrearPeriodoAsync(new EvaluacionPeriodoUpsertDto
        {
            Codigo = "TEST-CALIDAD",
            Nombre = "Período de prueba de calidad",
            FechaDesde = hoy.AddYears(-1),
            FechaHasta = hoy
        }, "tester");

        // Antes: sin ninguna incidencia registrada, CALIDAD no puntúa.
        await _evaluacion.CalcularAsync(periodo.Id, "tester");
        var antes = await FichaDelProveedorAsync(periodo.Id);
        Skip.If(antes is null, "El proveedor de la recepción no quedó evaluado en el período.");

        var calidadAntes = antes!.Criterios.Single(c =>
            string.Equals(c.Metrica, MetricaEvaluacion.Calidad, StringComparison.OrdinalIgnoreCase));
        Assert.True(calidadAntes.SinDatos);
        Assert.Null(calidadAntes.Puntos);

        // Después de registrar una: el criterio empieza a medir.
        await _service!.CrearAsync(NuevaIncidencia(), "bodeguero");
        await _evaluacion.CalcularAsync(periodo.Id, "tester");

        var despues = await FichaDelProveedorAsync(periodo.Id);
        var calidadDespues = despues!.Criterios.Single(c =>
            string.Equals(c.Metrica, MetricaEvaluacion.Calidad, StringComparison.OrdinalIgnoreCase));

        Assert.False(calidadDespues.SinDatos);
        Assert.NotNull(calidadDespues.Denominador);
        Assert.True(calidadDespues.Denominador > 0);

        // Y la recepción con incidencia NO cuenta como buena.
        Assert.True(calidadDespues.Numerador < calidadDespues.Denominador,
            "La recepción con incidencia debería quedar fuera del numerador.");
    }

    // ── Apoyo ────────────────────────────────────────────────────────────────

    private void ExigirRecepcion()
        => Skip.If(_compraHdrId == 0, "La base de pruebas no tiene recepciones de compra registradas.");

    private RecepcionIncidenciaUpsertDto NuevaIncidencia() => new()
    {
        CompraHdrId = _compraHdrId,
        Tipo = TipoIncidenciaRecepcion.Devolucion,
        Descripcion = "Se devolvió material dañado en el transporte.",
        Cantidad = 2m,
        Monto = 150m
    };

    private async Task<EvaluacionFichaDto?> FichaDelProveedorAsync(int periodoId)
        => await _evaluacion!.GetFichaAsync(periodoId, _codProveedor);

    private sealed class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
