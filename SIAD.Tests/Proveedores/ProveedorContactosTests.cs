using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.DTOs.Proveedores;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Data.Auditoria;
using SIAD.Services.Auditoria;
using SIAD.Services.Proveedores;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Proveedores;

/// <summary>
/// Contactos de proveedor y catálogo de tipos, contra la base real.
///
/// Lo que estos tests cuidan y los unitarios no pueden:
///   · el aislamiento entre empresas de la tabla hija (query filter global sobre
///     company_id), que es LA razón por la que prv_proveedor_contacto lleva
///     company_id propio: cod_proveedor se repite entre tenants;
///   · la unicidad del nombre del tipo tal como la evalúa Postgres (btrim + upper),
///     no como la evaluaría LINQ en memoria;
///   · la guarda de borrado de un tipo en uso.
/// </summary>
[Collection("Postgres")]
public class ProveedorContactosTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private ProveedoresService? _service;

    // Empresa que NO es la del fixture. Sirve para sembrar filas "ajenas" y comprobar
    // que ninguna consulta las ve.
    private long OtraEmpresa => CompanyId + 9000;

    // Sufijo único por corrida: aunque todo se revierte con ROLLBACK, evita chocar
    // contra los datos reales del mirror (el índice de nombre del catálogo es único
    // por empresa).
    private readonly string _sufijo = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    public ProveedorContactosTests(PostgresFixture fixture) : base(fixture) { }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (!Fixture.Available) return;

        var options = new DbContextOptionsBuilder<SiadDbContext>()
            .UseNpgsql(Connection).Options;

        var company = new TestCurrentCompanyService(CompanyId);
        _context = new SiadDbContext(options, company);
        _context.Database.UseTransaction(Transaction);
        _service = new ProveedoresService(_context, company, new NoopBitacoraWriter());
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    // ---------------------------------------------------------------------
    // Siembra
    // ---------------------------------------------------------------------

    /// <summary>
    /// Inserta un tipo de contacto por SQL directo. Hace falta para dos cosas que EF
    /// impide a propósito: sembrar en OTRA empresa (SaveChanges estampa siempre la
    /// del tenant actual) y guardar un nombre con espacios al borde tal cual.
    /// </summary>
    private async Task<long> SeedTipoSqlAsync(long companyId, string nombre, bool activo = true)
    {
        await _context!.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO public.prv_tipo_contacto (company_id, nombre, activo, usuario_creo)
            VALUES ({companyId}, {nombre}, {activo}, 'test')");

        return await _context.prv_tipo_contactos
            .IgnoreQueryFilters()
            .Where(t => t.company_id == companyId && t.nombre == nombre)
            .OrderByDescending(t => t.tipo_contacto_id)
            .Select(t => t.tipo_contacto_id)
            .FirstAsync();
    }

    /// <summary>
    /// prv_proveedores es una entidad keyless persistida con SQL crudo por el servicio;
    /// acá se siembra igual. cod_tipoproveedor / cuenta_contable no tienen FK.
    /// </summary>
    private Task SeedProveedorSqlAsync(long companyId, string codigo, string nombre)
        => _context!.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO public.prv_proveedores
                (cod_proveedor, cod_tipoproveedor, nombre, cuenta_contable, direccion,
                 fecha_creacion, usuario_creo, status, company_id)
            VALUES ({codigo}, 1, {nombre}, '1', 'Dirección de prueba',
                    now(), 'test', TRUE, {(int)companyId})");

    private Task SeedContactoSqlAsync(
        long companyId, string codProveedor, string nombre, int orden, long? tipoId = null)
        => _context!.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO public.prv_proveedor_contacto
                (company_id, cod_proveedor, tipo_contacto_id, nombre, orden, usuario_creo)
            VALUES ({companyId}, {codProveedor}, {tipoId}, {nombre}, {orden}, 'test')");

    // ---------------------------------------------------------------------
    // Catálogo de tipos
    // ---------------------------------------------------------------------

    [SkippableFact]
    public async Task GetTiposContactoCatalogo_DevuelveSoloLosDeLaEmpresa()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var mio = $"ZZ Propio {_sufijo}";
        var ajeno = $"ZZ Ajeno {_sufijo}";

        var idMio = await SeedTipoSqlAsync(CompanyId, mio);
        var idAjeno = await SeedTipoSqlAsync(OtraEmpresa, ajeno);

        // Guarda: si la siembra ajena no hubiera entrado, el test pasaría por la razón
        // equivocada (no habría nada que filtrar).
        var ajenoEnBd = await _context!.prv_tipo_contactos.IgnoreQueryFilters()
            .AnyAsync(t => t.tipo_contacto_id == idAjeno && t.company_id == OtraEmpresa);
        Assert.True(ajenoEnBd, "La siembra del tipo de la otra empresa no se persistió.");

        var catalogo = await _service!.GetTiposContactoCatalogoAsync();

        Assert.Contains(catalogo, t => t.Id == idMio && t.Nombre == mio);
        Assert.DoesNotContain(catalogo, t => t.Id == idAjeno);
        Assert.DoesNotContain(catalogo, t => t.Nombre == ajeno);
    }

    [SkippableFact]
    public async Task CreateTipoContacto_NombreRepetido_Falla()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var nombre = $"ZZ Repetido {_sufijo}";
        await _service!.CreateTipoContactoAsync(new TipoContactoUpsertDto { Nombre = nombre }, "tester");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateTipoContactoAsync(new TipoContactoUpsertDto { Nombre = nombre }, "tester"));

        Assert.Contains(nombre, ex.Message);
    }

    /// <summary>
    /// El índice único de la BD compara upper(btrim(nombre)). La validación del servicio
    /// tiene que comparar igual, o un nombre sembrado con espacios al borde (migración,
    /// SQL directo) se escaparía hasta reventar contra el índice con un error crudo.
    /// </summary>
    [SkippableFact]
    public async Task CreateTipoContacto_NombreConEspacios_DetectaElRepetido()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var nombre = $"ZZ Ventas {_sufijo}";
        await SeedTipoSqlAsync(CompanyId, $"  {nombre}  ");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service!.CreateTipoContactoAsync(new TipoContactoUpsertDto { Nombre = nombre }, "tester"));

        Assert.Contains(nombre, ex.Message);
    }

    [SkippableFact]
    public async Task DeleteTipoContacto_EnUso_Falla()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var tipoId = await _service!.CreateTipoContactoAsync(
            new TipoContactoUpsertDto { Nombre = $"ZZ En uso {_sufijo}" }, "tester");

        await SeedContactoSqlAsync(CompanyId, $"ZZP{_sufijo}", "Contacto que usa el tipo", 1, tipoId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.DeleteTipoContactoAsync(tipoId));

        Assert.Contains("desactivarlo", ex.Message);

        var sigueVivo = await _context!.prv_tipo_contactos.AnyAsync(t => t.tipo_contacto_id == tipoId);
        Assert.True(sigueVivo, "El tipo en uso no debió eliminarse.");
    }

    [SkippableFact]
    public async Task DeleteTipoContacto_SinUso_Borra()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var tipoId = await _service!.CreateTipoContactoAsync(
            new TipoContactoUpsertDto { Nombre = $"ZZ Sin uso {_sufijo}" }, "tester");

        await _service.DeleteTipoContactoAsync(tipoId);

        var catalogo = await _service.GetTiposContactoCatalogoAsync();
        Assert.DoesNotContain(catalogo, t => t.Id == tipoId);

        var sigueVivo = await _context!.prv_tipo_contactos.IgnoreQueryFilters()
            .AnyAsync(t => t.tipo_contacto_id == tipoId);
        Assert.False(sigueVivo, "El tipo sin uso sí debió eliminarse.");
    }

    // ---------------------------------------------------------------------
    // Contactos del proveedor
    // ---------------------------------------------------------------------

    /// <summary>
    /// EL test que justifica company_id en prv_proveedor_contacto: el correlativo de
    /// proveedor se genera POR EMPRESA, así que el mismo cod_proveedor existe en varios
    /// tenants. Si la tabla hija colgara solo de cod_proveedor, el detalle mezclaría los
    /// contactos de ambas empresas — LoadContactosAsync filtra únicamente por código y
    /// delega el aislamiento al query filter global.
    /// </summary>
    [SkippableFact]
    public async Task GetProveedorAsync_NoDevuelveContactosDeOtraEmpresa()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var codigo = $"ZZX{_sufijo}";   // el MISMO código en las dos empresas

        await SeedProveedorSqlAsync(CompanyId, codigo, "Proveedor empresa propia");
        await SeedProveedorSqlAsync(OtraEmpresa, codigo, "Proveedor empresa ajena");

        await SeedContactoSqlAsync(CompanyId, codigo, "Contacto propio", 1);
        await SeedContactoSqlAsync(OtraEmpresa, codigo, "Contacto ajeno", 1);

        // Guarda: ambos contactos existen y comparten cod_proveedor. Sin esto, el test
        // podría pasar simplemente porque la fila ajena nunca se insertó.
        var filas = await _context!.prv_proveedor_contactos.IgnoreQueryFilters()
            .Where(c => c.cod_proveedor == codigo)
            .Select(c => new { c.company_id, c.nombre })
            .ToListAsync();
        Assert.Equal(2, filas.Count);
        Assert.Contains(filas, f => f.company_id == OtraEmpresa && f.nombre == "Contacto ajeno");

        var detalle = await _service!.GetProveedorAsync(codigo);

        Assert.NotNull(detalle);
        Assert.Equal("Proveedor empresa propia", detalle!.Nombre);
        var contacto = Assert.Single(detalle.Contactos);
        Assert.Equal("Contacto propio", contacto.Nombre);
    }

    [SkippableFact]
    public async Task GetProveedorAsync_DevuelveContactosOrdenadosConSuTipo()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var codigo = $"ZZO{_sufijo}";
        var nombreTipo = $"ZZ Cobros {_sufijo}";
        var tipoId = await SeedTipoSqlAsync(CompanyId, nombreTipo);

        await SeedProveedorSqlAsync(CompanyId, codigo, "Proveedor con dos contactos");

        // Se insertan al revés del orden esperado: si la consulta no ordenara por
        // "orden", saldrían por proveedor_contacto_id y el assert fallaría.
        await SeedContactoSqlAsync(CompanyId, codigo, "Beto sin tipo", orden: 2);
        await SeedContactoSqlAsync(CompanyId, codigo, "Ana con tipo", orden: 1, tipoId: tipoId);

        var detalle = await _service!.GetProveedorAsync(codigo);

        Assert.NotNull(detalle);
        Assert.Equal(2, detalle!.Contactos.Count);

        Assert.Equal("Ana con tipo", detalle.Contactos[0].Nombre);
        Assert.Equal(1, detalle.Contactos[0].Orden);
        Assert.Equal(tipoId, detalle.Contactos[0].TipoContactoId);
        Assert.Equal(nombreTipo, detalle.Contactos[0].TipoContacto);

        Assert.Equal("Beto sin tipo", detalle.Contactos[1].Nombre);
        Assert.Equal(2, detalle.Contactos[1].Orden);
        Assert.Null(detalle.Contactos[1].TipoContactoId);
        Assert.Null(detalle.Contactos[1].TipoContacto);
    }

    /// <summary>
    /// El upsert completo del proveedor con sus contactos (normalización, diff por id,
    /// borrado de los ausentes y sincronía de las columnas legacy).
    ///
    /// SKIP: ProveedoresService.CreateAsync / UpdateAsync abren su propia transacción
    /// (BeginTransactionAsync), incompatible con el fixture de rollback — IntegrationTestBase
    /// enlaza una transacción externa vía UseTransaction y Npgsql no admite transacciones
    /// anidadas ("The connection is already in a transaction and cannot participate in
    /// another transaction"). Mismo motivo documentado en ProveedorAuditTests.cs:80-85.
    ///
    /// Dónde queda cubierto: los 11 tests unitarios de ProveedorContactosNormalizerTests
    /// (filas vacías, nombre obligatorio, orden consecutivo, recorte, email, duplicados,
    /// límites y las columnas legacy con su truncado a 20) más la verificación manual en
    /// el navegador del Task 9 (alta, edición y borrado de contactos desde el formulario).
    /// </summary>
    [SkippableFact]
    public Task CreateAsync_ConContactos_LosPersisteYSincronizaLegacy()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        Skip.If(true, "CreateAsync/UpdateAsync abren transacción propia; incompatible con el fixture de rollback. Cubierto por ProveedorContactosNormalizerTests + verificación en el navegador.");

        return Task.CompletedTask;
    }

    private sealed class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }

    // La bitácora no es objeto de estos tests (tiene los suyos): un writer no-op deja
    // al servicio construible sin arrastrar la config de auditoría.
    private sealed class NoopBitacoraWriter : IBitacoraMaestrosWriter
    {
        public Task RegistrarAsync(string tabla, string accion, string? registroId, string entidad,
            string descripcion, IReadOnlyList<AuditDiff.Campo>? campos, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
