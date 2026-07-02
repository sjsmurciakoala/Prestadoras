# Cartera vencida — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Nueva vista en Cobranza que lista clientes con facturas vencidas, con antigüedad por tramos (0–30 / 31–60 / 61–120 / +120) a una fecha de corte, filtros (fecha, clave/nombre, tramo, ciclo) y acciones de cobranza masivas reutilizando la infraestructura existente.

**Architecture:** Slice estándar del proyecto: DTO (`SIAD.Core`) → servicio Dapper (`SIAD.Services`) → endpoint thin (`apc`) → cliente HTTP (`apc.Client`) → página Blazor/DevExpress (`apc.Client`). La antigüedad se calcula sobre la tabla `factura` (única con `fechavence`). Las acciones masivas (acción en lote, cartas) reusan los endpoints existentes `acciones/lote` y `cartas-cobro`.

**Tech Stack:** .NET 9, Dapper + Npgsql (PostgreSQL), Blazor WebAssembly, DevExpress 25.1.7, xUnit (SIAD.Tests, integración con `BEGIN … ROLLBACK`).

**Diseño aprobado:** `docs/plans/2026-06-24-cartera-vencida-design.md`.

**Convenciones del repo a respetar:**
- Multiempresa: todas las consultas filtran `company_id` (regla CLAUDE.md "Multi-tenancy — non-negotiable").
- Cambios de BD = script con fecha en `Database/` y replicar en SRV (regla mirror, skill `hodsoft-db-mirror`).
- Tests requieren `SIAD_TEST_DB`; sin la env var quedan `Skipped`.

---

## Task 1: Verificar el dominio de `factura.estado` (anuladas)

Antes de escribir el SQL hay que confirmar qué valor marca una factura anulada, para excluirla. La lógica de "pagada" ya se cubre con `fechapago`, pero una anulada puede tener `saldototal > 0`.

**Step 1: Consultar el dominio contra el mirror**

Usar el skill `psql-runner` (o `psql` directo) contra el mirror local `siad_v3_restore`:

```sql
SELECT estado, COUNT(*) FROM factura GROUP BY estado ORDER BY 2 DESC;
```

**Step 2: Anotar el resultado**

Esperado: identificar el/los códigos de anulada (p. ej. `'A'`, `'AN'`, `'X'`) frente a `'C'` (cobrada) y el estado pendiente.

**Step 3: Ajustar la constante del SQL**

En el Task 4, en el `WHERE`, fijar la lista real de estados anulados en el predicado
`AND COALESCE(f.estado, '') <> ANY(@EstadosExcluidos)`. Si NO existe estado de anulada (solo cobrada/pendiente), eliminar ese predicado: `fechapago` + `saldototal > 0` ya bastan.

> Si el VPN/mirror no está disponible, dejar el predicado de anuladas comentado con un `TODO:` y registrar el pendiente; la consulta sigue siendo correcta para cobradas (via `fechapago`).

---

## Task 2: DTOs de cartera vencida

**Files:**
- Create: `Prestadoras/SIAD.Core/DTOs/Cobranza/CarteraVencidaDtos.cs`

**Step 1: Crear el archivo de DTOs**

```csharp
namespace SIAD.Core.DTOs.Cobranza;

public class CarteraVencidaFiltroDto
{
    /// <summary>Fecha de corte (as-of). Si es null, el servicio usa la fecha actual.</summary>
    public DateOnly? FechaCorte { get; set; }
    public string? Busqueda { get; set; }
    /// <summary>Tramo de antigüedad: 1=0–30, 2=31–60, 3=61–120, 4=+120. Null = todos.</summary>
    public int? Tramo { get; set; }
    public int? CicloId { get; set; }
}

public record CarteraVencidaClienteDto(
    string Clave,
    string? Nombre,
    int? CicloId,
    string? Ruta,
    decimal B0_30,
    decimal B31_60,
    decimal B61_120,
    decimal BMas120,
    decimal TotalVencido,
    int FacturasVencidas,
    int? DiasMaxVencido,
    bool Bloqueado,
    bool NoCortable,
    int? AbogadoId);
```

**Step 2: Compilar el proyecto Core**

Run: `dotnet build Prestadoras/SIAD.Core/SIAD.Core.csproj`
Expected: build correcto (0 errores).

---

## Task 3: Declarar el método en `ICobranzaService`

**Files:**
- Modify: `Prestadoras/SIAD.Services/Cobranza/ICobranzaService.cs`

**Step 1: Agregar la firma**

Justo después de la línea de `ListarClientesCobroAsync` (zona "Clientes para cobros"), agregar:

```csharp
    // Cartera vencida (antigüedad por tramos)
    Task<IReadOnlyList<CarteraVencidaClienteDto>> ListarCarteraVencidaAsync(
        CarteraVencidaFiltroDto filtro, CancellationToken ct = default);
```

**Step 2: Compilar (debe fallar la implementación, no la interfaz)**

Run: `dotnet build Prestadoras/SIAD.Services/SIAD.Services.csproj`
Expected: FAIL — `CobranzaService` no implementa el nuevo miembro de la interfaz. (Se resuelve en Task 5.)

---

## Task 4: Test de integración (rojo) para `ListarCarteraVencidaAsync`

**Files:**
- Create: `Prestadoras/SIAD.Tests/CarteraVencidaTests.cs`

El test siembra facturas controladas dentro de la transacción (que se revierte) sobre un cliente real existente, y verifica la ubicación por tramo y el comportamiento *as-of*. Usa deltas (baseline vs. después de insertar) para aislarse de las facturas reales del cliente.

**Step 1: Escribir el test que falla**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.DTOs.Cobranza;
using SIAD.Core.Tenancy;
using SIAD.Services.Cobranza;
using SIAD.Data;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

[Collection("Postgres")]
public class CarteraVencidaTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private CobranzaService? _service;

    public CarteraVencidaTests(PostgresFixture fixture) : base(fixture) { }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (Fixture.Available)
        {
            var options = new DbContextOptionsBuilder<SiadDbContext>()
                .UseNpgsql(Connection).Options;
            var company = new TestCurrentCompanyService(CompanyId);
            _context = new SiadDbContext(options, company);
            _context.Database.UseTransaction(Transaction);
            _service = new CobranzaService(_context, company);
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    [SkippableFact]
    public async Task ListarCarteraVencida_SumasPorTramo_CuadranConTotal()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var result = await _service!.ListarCarteraVencidaAsync(
            new CarteraVencidaFiltroDto { FechaCorte = DateOnly.FromDateTime(DateTime.Today) });

        Assert.All(result, c =>
        {
            Assert.Equal(c.TotalVencido, c.B0_30 + c.B31_60 + c.B61_120 + c.BMas120);
            Assert.True(c.TotalVencido > 0);
        });
    }

    [SkippableFact]
    public async Task ListarCarteraVencida_FacturaVencida45Dias_CaeEnTramo31_60()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var clave = await ObtenerClaveExistenteAsync();
        Skip.If(clave is null, "No hay clientes en la BD de prueba");

        var corte = DateOnly.FromDateTime(DateTime.Today);

        var antes = await BucketDelCliente(clave!, corte);
        await InsertarFacturaAsync(clave!, fechaEmision: corte.AddDays(-60),
            fechaVence: corte.AddDays(-45), fechaPago: null, saldo: 123.45m);
        var despues = await BucketDelCliente(clave!, corte);

        Assert.Equal(123.45m, despues.B31_60 - antes.B31_60);
        Assert.Equal(0m, despues.B0_30 - antes.B0_30);
    }

    [SkippableFact]
    public async Task ListarCarteraVencida_AsOf_RespetaFechaDePago()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var clave = await ObtenerClaveExistenteAsync();
        Skip.If(clave is null, "No hay clientes en la BD de prueba");

        var corteHoy = DateOnly.FromDateTime(DateTime.Today);
        var cortePasado = corteHoy.AddDays(-20);

        // Factura que venció hace 45 días y se pagó hace 10 días.
        await InsertarFacturaAsync(clave!, fechaEmision: corteHoy.AddDays(-60),
            fechaVence: corteHoy.AddDays(-45), fechaPago: corteHoy.AddDays(-10), saldo: 77.00m);

        var hoy = await BucketDelCliente(clave!, corteHoy);      // pagada antes de hoy → no cuenta
        var pasado = await BucketDelCliente(clave!, cortePasado); // a esa fecha aún no estaba pagada → cuenta

        Assert.Equal(0m, hoy.TotalDelta);          // hoy no debe sumar la seeded
        Assert.Equal(77.00m, pasado.TotalDelta);   // 45-20=25 días → tramo 0–30
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<string?> ObtenerClaveExistenteAsync()
        => await Connection.QuerySingleOrDefaultAsync<string?>(
            "SELECT maestro_cliente_clave FROM cliente_maestro WHERE company_id = @c LIMIT 1",
            new { c = CompanyId }, Transaction);

    private async Task InsertarFacturaAsync(string clave, DateOnly fechaEmision,
        DateOnly fechaVence, DateOnly? fechaPago, decimal saldo)
        => await Connection.ExecuteAsync("""
            INSERT INTO factura (company_id, numrecibo, numfactura, clientecodigo,
                                 fechaemision, fechavence, fechapago, saldototal, estado)
            VALUES (@company_id, @numrecibo, @numfactura, @clave,
                    @fechaemision, @fechavence, @fechapago, @saldo, 'P')
            """,
            new
            {
                company_id = CompanyId,
                numrecibo = Random.Shared.Next(900_000_000, 999_999_999),
                numfactura = "TEST-" + Guid.NewGuid().ToString("N")[..8],
                clave,
                fechaemision = fechaEmision,
                fechavence = fechaVence,
                fechapago = fechaPago,
                saldo
            }, Transaction);

    private async Task<(decimal B0_30, decimal B31_60, decimal TotalDelta)> BucketDelCliente(
        string clave, DateOnly corte)
    {
        var fila = (await _service!.ListarCarteraVencidaAsync(
            new CarteraVencidaFiltroDto { FechaCorte = corte, Busqueda = clave }))
            .FirstOrDefault(c => c.Clave == clave);
        return fila is null ? (0m, 0m, 0m) : (fila.B0_30, fila.B31_60, fila.TotalVencido);
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
```

> Nota sobre el 3.º test: `TotalDelta` es el total del cliente; como se siembra una sola factura, el delta vs. la realidad existente no es directo. Si la BD de prueba tiene facturas reales del cliente, ajustar el 3.º test a la forma "baseline-then-insert" igual que el 2.º (capturar total antes, insertar, comparar). Mantener simple: si causa fricción, dejar los tests 1 y 2 (que son deterministas con delta) y marcar el as-of con baseline/después.

**Step 2: Ejecutar y verificar que NO compila / falla**

Run (PowerShell): `$env:SIAD_TEST_DB = '<connection string del mirror>'; dotnet test Prestadoras/SIAD.Tests/SIAD.Tests.csproj --filter FullyQualifiedName~CarteraVencidaTests`
Expected: FAIL de compilación (método inexistente) — esto confirma el rojo antes de implementar.

---

## Task 5: Implementar `ListarCarteraVencidaAsync` en `CobranzaService`

**Files:**
- Modify: `Prestadoras/SIAD.Services/Cobranza/CobranzaService.cs`

**Step 1: Agregar el método** (ubicarlo junto a `ListarClientesCobroAsync`; mismo patrón Dapper)

```csharp
    public async Task<IReadOnlyList<CarteraVencidaClienteDto>> ListarCarteraVencidaAsync(
        CarteraVencidaFiltroDto filtro, CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();
        var fechaCorte = filtro.FechaCorte ?? DateOnly.FromDateTime(DateTime.Today);

        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(ct);

        var sb = new StringBuilder("""
            SELECT
                f.clientecodigo                     AS Clave,
                cm.maestro_cliente_nombre           AS Nombre,
                cm.ciclos_id                        AS CicloId,
                cm.maestro_cliente_indicativo_ruta  AS Ruta,
                COALESCE(SUM(f.saldototal) FILTER (WHERE (@FechaCorte - f.fechavence) BETWEEN 1 AND 30), 0)   AS B0_30,
                COALESCE(SUM(f.saldototal) FILTER (WHERE (@FechaCorte - f.fechavence) BETWEEN 31 AND 60), 0)  AS B31_60,
                COALESCE(SUM(f.saldototal) FILTER (WHERE (@FechaCorte - f.fechavence) BETWEEN 61 AND 120), 0) AS B61_120,
                COALESCE(SUM(f.saldototal) FILTER (WHERE (@FechaCorte - f.fechavence) > 120), 0)              AS BMas120,
                COALESCE(SUM(f.saldototal), 0)      AS TotalVencido,
                COUNT(*)                            AS FacturasVencidas,
                MAX(@FechaCorte - f.fechavence)     AS DiasMaxVencido,
                COALESCE(cm.bloqueado_cobranza, FALSE) AS Bloqueado,
                COALESCE(cm.no_cortable, FALSE)        AS NoCortable,
                cm.abogado                          AS AbogadoId
            FROM factura f
            JOIN cliente_maestro cm
                ON cm.company_id = f.company_id
               AND cm.maestro_cliente_clave = f.clientecodigo
            WHERE f.company_id = @CompanyId
              AND f.fechaemision IS NOT NULL AND f.fechaemision <= @FechaCorte
              AND f.fechavence  IS NOT NULL AND f.fechavence  < @FechaCorte
              AND (f.fechapago IS NULL OR f.fechapago > @FechaCorte)
              AND COALESCE(f.saldototal, 0) > 0
            """);

        // TODO(Task 1): si existe estado de anulada, descomentar con el valor real:
        // sb.Append(" AND COALESCE(f.estado, '') <> 'AN'");

        if (filtro.CicloId.HasValue)
            sb.Append(" AND cm.ciclos_id = @CicloId");

        if (!string.IsNullOrWhiteSpace(filtro.Busqueda))
            sb.Append(" AND (f.clientecodigo ILIKE @Busqueda OR cm.maestro_cliente_nombre ILIKE @Busqueda)");

        sb.Append("""
             GROUP BY f.clientecodigo, cm.maestro_cliente_nombre, cm.ciclos_id,
                      cm.maestro_cliente_indicativo_ruta, cm.bloqueado_cobranza,
                      cm.no_cortable, cm.abogado
            """);

        // Filtro por tramo (HAVING): solo clientes con saldo en el tramo elegido.
        var havingTramo = filtro.Tramo switch
        {
            1 => " HAVING COALESCE(SUM(f.saldototal) FILTER (WHERE (@FechaCorte - f.fechavence) BETWEEN 1 AND 30), 0) > 0",
            2 => " HAVING COALESCE(SUM(f.saldototal) FILTER (WHERE (@FechaCorte - f.fechavence) BETWEEN 31 AND 60), 0) > 0",
            3 => " HAVING COALESCE(SUM(f.saldototal) FILTER (WHERE (@FechaCorte - f.fechavence) BETWEEN 61 AND 120), 0) > 0",
            4 => " HAVING COALESCE(SUM(f.saldototal) FILTER (WHERE (@FechaCorte - f.fechavence) > 120), 0) > 0",
            _ => string.Empty
        };
        sb.Append(havingTramo);

        sb.Append(" ORDER BY cm.maestro_cliente_nombre LIMIT 5000");

        var busqueda = string.IsNullOrWhiteSpace(filtro.Busqueda) ? null : $"%{filtro.Busqueda.Trim()}%";

        var rows = await connection.QueryAsync<CarteraVencidaRow>(
            new CommandDefinition(sb.ToString(),
                new
                {
                    CompanyId = companyId,
                    FechaCorte = fechaCorte,
                    CicloId = filtro.CicloId,
                    Busqueda = busqueda
                },
                cancellationToken: ct));

        return rows
            .Select(r => new CarteraVencidaClienteDto(
                r.Clave, r.Nombre, r.CicloId, r.Ruta,
                r.B0_30, r.B31_60, r.B61_120, r.BMas120,
                r.TotalVencido, r.FacturasVencidas, r.DiasMaxVencido,
                r.Bloqueado, r.NoCortable, r.AbogadoId))
            .ToList();
    }
```

**Step 2: Agregar la clase `Row`** (junto a `ClienteCobroRow`, al final de la clase)

```csharp
    private sealed class CarteraVencidaRow
    {
        public string Clave { get; init; } = string.Empty;
        public string? Nombre { get; init; }
        public int? CicloId { get; init; }
        public string? Ruta { get; init; }
        public decimal B0_30 { get; init; }
        public decimal B31_60 { get; init; }
        public decimal B61_120 { get; init; }
        public decimal BMas120 { get; init; }
        public decimal TotalVencido { get; init; }
        public int FacturasVencidas { get; init; }
        public int? DiasMaxVencido { get; init; }
        public bool Bloqueado { get; init; }
        public bool NoCortable { get; init; }
        public int? AbogadoId { get; init; }
    }
```

**Step 3: Compilar**

Run: `dotnet build Prestadoras/SIAD.Services/SIAD.Services.csproj`
Expected: build correcto (0 errores).

---

## Task 6: Ejecutar los tests (verde)

**Step 1: Correr los tests de cartera vencida**

Run: `$env:SIAD_TEST_DB = '<connection string del mirror>'; dotnet test Prestadoras/SIAD.Tests/SIAD.Tests.csproj --filter FullyQualifiedName~CarteraVencidaTests`
Expected: PASS (o `Skipped` si `SIAD_TEST_DB` no está; en ese caso correrlos donde haya mirror).

**Step 2: Si algún test falla**

Diagnosticar con el skill `systematic-debugging`. Causas típicas: tipo de `@FechaCorte` (DateOnly→`date`), join sin match en `cliente_maestro`, o facturas reales del cliente que rompen el delta exacto (ajustar a baseline/después).

---

## Task 7: Endpoint en `CobranzaController`

**Files:**
- Modify: `Prestadoras/apc/Controllers/CobranzaController.cs`

**Step 1: Agregar la acción** (junto a `ListarClientesCobro`, hereda `[ModuleAuthorize(Ventas, Cobranza)]` de la clase — sin permiso nuevo)

```csharp
    // GET api/Cobranza/cartera-vencida?fechaCorte=&busqueda=&tramo=&cicloId=
    [HttpGet("cartera-vencida")]
    public async Task<IActionResult> ListarCarteraVencida(
        [FromQuery] CarteraVencidaFiltroDto filtro, CancellationToken ct)
        => Ok(await _service.ListarCarteraVencidaAsync(filtro, ct));
```

**Step 2: Compilar el host**

Run: `dotnet build Prestadoras/apc/apc.csproj`
Expected: build correcto.

---

## Task 8: Método en el cliente HTTP

**Files:**
- Modify: `Prestadoras/apc.Client/Services/Cobranza/ClientesCobroClient.cs`

**Step 1: Agregar `ListarCarteraVencidaAsync`** (debajo de `ListarAsync`)

```csharp
    public Task<IReadOnlyList<CarteraVencidaClienteDto>?> ListarCarteraVencidaAsync(
        CarteraVencidaFiltroDto filtro, CancellationToken ct = default)
    {
        var query = new List<string>();

        if (filtro.FechaCorte is not null)
            Append(query, "fechaCorte", filtro.FechaCorte.Value.ToString("yyyy-MM-dd"));
        if (!string.IsNullOrWhiteSpace(filtro.Busqueda))
            Append(query, "busqueda", filtro.Busqueda);
        if (filtro.Tramo is not null)
            Append(query, "tramo", filtro.Tramo.Value.ToString(CultureInfo.InvariantCulture));
        if (filtro.CicloId is not null)
            Append(query, "cicloId", filtro.CicloId.Value.ToString(CultureInfo.InvariantCulture));

        var url = "api/Cobranza/cartera-vencida";
        if (query.Count > 0)
            url += "?" + string.Join("&", query);

        return _http.GetFromJsonAsyncWithAuthCheck<IReadOnlyList<CarteraVencidaClienteDto>>(url, ct);
    }
```

**Step 2: Compilar**

Run: `dotnet build Prestadoras/apc.Client/apc.Client.csproj`
Expected: build correcto.

---

## Task 9: Página `CarteraVencida.razor`

**Files:**
- Create: `Prestadoras/apc.Client/Pages/Facturacion/Cobranza/CarteraVencida.razor`

Reutiliza el patrón de `ClientesParaCobros.razor` (mismas acciones masivas y popup). Antes de tocar cualquier API de DevExpress, consultar `dxdocs` (skill `hodsoft-blazor-devexpress-ui`). Componentes usados ya existen en el proyecto: `DxGrid`, `DxDateEdit`, `DxComboBox`, `DxTextBox`, `DxFormLayout`, `DxButton`, `DxPopup`, `DxToastProvider`.

**Step 1: Crear la página**

```razor
@page "/facturacion/cobranza/cartera-vencida"
@attribute [Authorize]
@using System.Linq
@using DevExpress.Blazor
@using SIAD.Core.DTOs.Cobranza
@using SIAD.Core.DTOs.Rutas
@using apc.Client.Services.Cobranza
@using apc.Client.Services.Facturacion
@using apc.Client.Services.Informes
@inject ClientesCobroClient ClientesCobroClient
@inject CobranzaClient CobranzaClient
@inject InformesClient InformesClient
@inject IJSRuntime JS
@inject NavigationManager Navigation
@inject IToastNotificationService ToastService

<PageTitle>SIAD - Cartera vencida</PageTitle>

<div class="page-container">
    <DxToastProvider Name="CarteraVencidaToast" MaxToastCount="4" />

    <div class="header-section mb-3">
        <div>
            <h2 class="page-title">Cartera vencida</h2>
            <p class="page-subtitle">Antigüedad de facturas vencidas por cliente</p>
        </div>
    </div>

    <!-- FILTROS -->
    <div class="modern-card mb-4">
        <div class="section-header mb-3">
            <i class="bi bi-funnel"></i>
            <h5 class="m-0">Filtros</h5>
        </div>

        <DxFormLayout CaptionPosition="CaptionPosition.Vertical">
            <DxFormLayoutItem Caption="Fecha de corte" ColSpanXs="12" ColSpanMd="3" Context="f1">
                <DxDateEdit @bind-Date="fechaCorte" CssClass="w-100" />
            </DxFormLayoutItem>
            <DxFormLayoutItem Caption="Clave o nombre" ColSpanXs="12" ColSpanMd="3" Context="f2">
                <DxTextBox @bind-Text="busqueda"
                           Placeholder="Clave o nombre..."
                           ClearButtonDisplayMode="DataEditorClearButtonDisplayMode.Auto"
                           CssClass="w-100" />
            </DxFormLayoutItem>
            <DxFormLayoutItem Caption="Tramo de antigüedad" ColSpanXs="12" ColSpanMd="3" Context="f3">
                <DxComboBox Data="tramos"
                            @bind-Value="tramo"
                            ValueFieldName="Id"
                            TextFieldName="Texto"
                            NullText="— Todos —"
                            ClearButtonDisplayMode="DataEditorClearButtonDisplayMode.Auto"
                            CssClass="w-100" />
            </DxFormLayoutItem>
            <DxFormLayoutItem Caption="Ciclo" ColSpanXs="12" ColSpanMd="3" Context="f4">
                <DxComboBox Data="ciclos"
                            @bind-Value="cicloId"
                            ValueFieldName="Id"
                            TextFieldName="Descripcion"
                            NullText="— Todos —"
                            ClearButtonDisplayMode="DataEditorClearButtonDisplayMode.Auto"
                            CssClass="w-100" />
            </DxFormLayoutItem>
            <DxFormLayoutItem ColSpanXs="12" ColSpanMd="3" CssClass="align-self-end" Context="f5">
                <DxButton Text="Buscar"
                          IconCssClass="bi bi-search"
                          RenderStyle="ButtonRenderStyle.Primary"
                          Click="BuscarAsync"
                          Enabled="@(!isLoading)" />
            </DxFormLayoutItem>
        </DxFormLayout>
    </div>

    @if (!string.IsNullOrWhiteSpace(errorMsg))
    {
        <DxAlert AlertType="AlertType.Danger" CssClass="mb-3">@errorMsg</DxAlert>
    }

    <!-- RESULTADOS + ACCIONES MASIVAS -->
    <div class="modern-card position-relative">
        <DxLoadingPanel Visible="isLoading" IsContentBlocked="true" Text="Cargando cartera..." />

        <div class="d-flex flex-wrap gap-2 align-items-center mb-3">
            <DxButton Text="Exportar selección"
                      IconCssClass="bi bi-file-earmark-excel"
                      RenderStyle="ButtonRenderStyle.Secondary"
                      Click="ExportarSeleccionAsync"
                      Enabled="@(SeleccionadosClaves.Count > 0)" />
            <DxButton Text="Registrar acción en lote"
                      IconCssClass="bi bi-journal-plus"
                      RenderStyle="ButtonRenderStyle.Secondary"
                      Click="AbrirAccionLote"
                      Enabled="@(SeleccionadosClaves.Count > 0)" />
            <DxButton Text="Generar cartas"
                      IconCssClass="bi bi-envelope-paper"
                      RenderStyle="ButtonRenderStyle.Secondary"
                      Click="GenerarCartasAsync"
                      Enabled="@(SeleccionadosClaves.Count > 0 && !isGenerandoCartas)" />

            <div class="ms-auto text-muted small">
                Seleccionados: <span class="fw-bold">@SeleccionadosClaves.Count.ToString("N0")</span>
                <span class="mx-2">·</span>
                Total: <span class="fw-bold">@clientes.Count.ToString("N0")</span>
            </div>
        </div>

        <div class="grid-wrapper">
            <DxGrid @ref="grid"
                    Data="@clientes"
                    KeyFieldName="@nameof(CarteraVencidaClienteDto.Clave)"
                    @bind-SelectedDataItems="seleccionados"
                    SelectionMode="GridSelectionMode.Multiple"
                    PageSize="20"
                    CssClass="grid-modern">
                <Columns>
                    <DxGridSelectionColumn Width="45px" />
                    <DxGridDataColumn FieldName="@nameof(CarteraVencidaClienteDto.Clave)" Caption="Clave" Width="110px" />
                    <DxGridDataColumn FieldName="@nameof(CarteraVencidaClienteDto.Nombre)" Caption="Cliente" MinWidth="200" />
                    <DxGridDataColumn FieldName="@nameof(CarteraVencidaClienteDto.CicloId)" Caption="Ciclo" Width="80px" />
                    <DxGridDataColumn FieldName="@nameof(CarteraVencidaClienteDto.Ruta)" Caption="Ruta" Width="100px" />
                    <DxGridDataColumn FieldName="@nameof(CarteraVencidaClienteDto.B0_30)" Caption="0–30"
                                      Width="100px" DisplayFormat="N2" TextAlignment="GridTextAlignment.Right" />
                    <DxGridDataColumn FieldName="@nameof(CarteraVencidaClienteDto.B31_60)" Caption="31–60"
                                      Width="100px" DisplayFormat="N2" TextAlignment="GridTextAlignment.Right" />
                    <DxGridDataColumn FieldName="@nameof(CarteraVencidaClienteDto.B61_120)" Caption="61–120"
                                      Width="100px" DisplayFormat="N2" TextAlignment="GridTextAlignment.Right" />
                    <DxGridDataColumn FieldName="@nameof(CarteraVencidaClienteDto.BMas120)" Caption="+120"
                                      Width="100px" DisplayFormat="N2" TextAlignment="GridTextAlignment.Right" />
                    <DxGridDataColumn FieldName="@nameof(CarteraVencidaClienteDto.TotalVencido)" Caption="Total vencido"
                                      Width="130px" DisplayFormat="N2" TextAlignment="GridTextAlignment.Right" />
                </Columns>
                <TotalSummary>
                    <DxGridSummaryItem FieldName="@nameof(CarteraVencidaClienteDto.B0_30)" SummaryType="GridSummaryItemType.Sum" ValueDisplayFormat="N2" />
                    <DxGridSummaryItem FieldName="@nameof(CarteraVencidaClienteDto.B31_60)" SummaryType="GridSummaryItemType.Sum" ValueDisplayFormat="N2" />
                    <DxGridSummaryItem FieldName="@nameof(CarteraVencidaClienteDto.B61_120)" SummaryType="GridSummaryItemType.Sum" ValueDisplayFormat="N2" />
                    <DxGridSummaryItem FieldName="@nameof(CarteraVencidaClienteDto.BMas120)" SummaryType="GridSummaryItemType.Sum" ValueDisplayFormat="N2" />
                    <DxGridSummaryItem FieldName="@nameof(CarteraVencidaClienteDto.TotalVencido)" SummaryType="GridSummaryItemType.Sum" ValueDisplayFormat="N2" />
                </TotalSummary>
                <EmptyDataAreaTemplate>
                    <div class="p-4 text-center text-muted">
                        <i class="bi bi-inbox" style="font-size:2rem;opacity:.5"></i>
                        <p class="mt-2">No hay cartera vencida para mostrar. Ajuste los filtros y presione Buscar.</p>
                    </div>
                </EmptyDataAreaTemplate>
            </DxGrid>
        </div>
    </div>
</div>

<!-- POPUP: Registrar acción en lote (idéntico a ClientesParaCobros) -->
<DxPopup @bind-Visible="mostrarAccionLote"
         HeaderText="Registrar acción en lote"
         Width="min(520px, 95vw)"
         ShowFooter="true"
         CloseOnOutsideClick="false">
    <BodyTemplate>
        <p class="text-muted small mb-3">
            Se registrará la acción para <strong>@SeleccionadosClaves.Count</strong> cliente(s) seleccionado(s).
        </p>
        <DxFormLayout CaptionPosition="CaptionPosition.Vertical">
            <DxFormLayoutItem Caption="Tipo de acción" ColSpanXs="12" Context="a1">
                <DxComboBox Data="catalogoAcciones" @bind-Value="accionId"
                            ValueFieldName="CodAccion" TextFieldName="Nombre"
                            NullText="— Seleccione —" CssClass="w-100" />
            </DxFormLayoutItem>
            <DxFormLayoutItem Caption="Resultado" ColSpanXs="12" Context="a2">
                <DxComboBox Data="catalogoObservaciones" @bind-Value="observacionId"
                            ValueFieldName="Id" TextFieldName="Observacion"
                            NullText="— Seleccione resultado —"
                            ClearButtonDisplayMode="DataEditorClearButtonDisplayMode.Auto" CssClass="w-100" />
            </DxFormLayoutItem>
            <DxFormLayoutItem Caption="Observación" ColSpanXs="12" Context="a3">
                <DxMemo @bind-Text="observacion" Rows="3" CssClass="w-100" />
            </DxFormLayoutItem>
        </DxFormLayout>
        @if (!string.IsNullOrEmpty(accionLoteError))
        {
            <DxAlert AlertType="AlertType.Danger" CssClass="mt-2">@accionLoteError</DxAlert>
        }
    </BodyTemplate>
    <FooterTemplate>
        <div class="d-flex gap-2 justify-content-end">
            <DxButton Text="Cancelar" RenderStyle="ButtonRenderStyle.Secondary" Click="() => mostrarAccionLote = false" />
            <DxButton Text="Aplicar" RenderStyle="ButtonRenderStyle.Primary" Click="AplicarAccionLoteAsync" Enabled="@(!isAplicando)" />
        </div>
    </FooterTemplate>
</DxPopup>

@code {
    private DxGrid? grid;

    private List<CicloLookupDto> ciclos = [];
    private List<AccionCobranzaCatalogoDto> catalogoAcciones = [];
    private List<ObservacionCobranzaCatalogoDto> catalogoObservaciones = [];

    private readonly List<TramoOption> tramos =
    [
        new(1, "0–30 días"), new(2, "31–60 días"), new(3, "61–120 días"), new(4, "Más de 120 días")
    ];

    // Filtros
    private DateTime? fechaCorte = DateTime.Today;
    private string? busqueda;
    private int? tramo;
    private int? cicloId;

    // Datos
    private IReadOnlyList<CarteraVencidaClienteDto> clientes = Array.Empty<CarteraVencidaClienteDto>();
    private IReadOnlyList<object> seleccionados = Array.Empty<object>();

    private bool isLoading;
    private bool isGenerandoCartas;
    private string? errorMsg;

    // Popup acción en lote
    private bool mostrarAccionLote;
    private int? accionId;
    private int? observacionId;
    private string? observacion;
    private bool isAplicando;
    private string? accionLoteError;

    private List<string> SeleccionadosClaves =>
        seleccionados.OfType<CarteraVencidaClienteDto>().Select(c => c.Clave).ToList();

    protected override async Task OnInitializedAsync() => await CargarCatalogosAsync();

    private async Task CargarCatalogosAsync()
    {
        try
        {
            var tCiclos = InformesClient.GetCiclosAsync();
            var tAcciones = CobranzaClient.GetCatalogoAccionesAsync();
            var tObs = CobranzaClient.GetCatalogoObservacionesAsync();
            await Task.WhenAll(tCiclos, tAcciones, tObs);
            ciclos = tCiclos.Result?.ToList() ?? [];
            catalogoAcciones = tAcciones.Result?.ToList() ?? [];
            catalogoObservaciones = tObs.Result?.ToList() ?? [];
        }
        catch { /* catálogos opcionales */ }
    }

    private CarteraVencidaFiltroDto BuildFiltro() => new()
    {
        FechaCorte = fechaCorte.HasValue ? DateOnly.FromDateTime(fechaCorte.Value) : null,
        Busqueda = string.IsNullOrWhiteSpace(busqueda) ? null : busqueda,
        Tramo = tramo,
        CicloId = cicloId
    };

    private async Task BuscarAsync()
    {
        isLoading = true;
        errorMsg = null;
        try
        {
            var result = await ClientesCobroClient.ListarCarteraVencidaAsync(BuildFiltro());
            clientes = result ?? Array.Empty<CarteraVencidaClienteDto>();
            seleccionados = Array.Empty<object>();
        }
        catch (Exception ex)
        {
            errorMsg = $"Error al cargar cartera: {ex.Message}";
            clientes = Array.Empty<CarteraVencidaClienteDto>();
        }
        finally { isLoading = false; }
    }

    private async Task ExportarSeleccionAsync()
    {
        if (grid is null || SeleccionadosClaves.Count == 0) return;
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
        await grid.ExportToXlsxAsync($"cartera_vencida_{stamp}.xlsx",
            new GridXlExportOptions { ExportSelectedRowsOnly = true });
    }

    private void AbrirAccionLote()
    {
        accionId = null; observacionId = null; observacion = null; accionLoteError = null;
        mostrarAccionLote = true;
    }

    private async Task AplicarAccionLoteAsync()
    {
        if (accionId is null) { accionLoteError = "Seleccione un tipo de acción."; return; }
        var claves = SeleccionadosClaves;
        if (claves.Count == 0) { accionLoteError = "No hay clientes seleccionados."; return; }

        accionLoteError = null;
        isAplicando = true;
        try
        {
            var req = new RegistrarAccionLoteRequest(
                claves, accionId.Value, observacionId, null,
                string.IsNullOrWhiteSpace(observacion) ? null : observacion, null);
            var n = await ClientesCobroClient.RegistrarAccionLoteAsync(req);
            mostrarAccionLote = false;
            ShowToast("Acción registrada", $"Se registraron {n} acción(es) de cobranza.", ToastRenderStyle.Success);
        }
        catch (Exception ex) { accionLoteError = $"Error al registrar: {ex.Message}"; }
        finally { isAplicando = false; }
    }

    private async Task GenerarCartasAsync()
    {
        var claves = SeleccionadosClaves;
        if (claves.Count == 0) return;

        isGenerandoCartas = true;
        try
        {
            var hdr = await ClientesCobroClient.GenerarCartasAsync(new GenerarCartasCobroRequest(claves));
            if (hdr is null)
            {
                ShowToast("Generar cartas", "No se recibió el lote de cartas generado.", ToastRenderStyle.Warning);
                return;
            }
            ShowToast("Cartas generadas", $"Lote {hdr.Correlativo} con {hdr.TotalClientes} carta(s).", ToastRenderStyle.Success);
            var relativeUrl = ClientesCobroClient.GetImprimirCartasUrl(hdr.Id);
            var absoluteUrl = Navigation.ToAbsoluteUri(relativeUrl).ToString();
            await JS.InvokeVoidAsync("open", absoluteUrl, "_blank");
        }
        catch (Exception ex) { ShowToast("Error", $"No se pudieron generar las cartas: {ex.Message}", ToastRenderStyle.Danger); }
        finally { isGenerandoCartas = false; }
    }

    private void ShowToast(string title, string text, ToastRenderStyle style)
        => ToastService.ShowToast(new ToastOptions
        {
            Title = title, Text = text, RenderStyle = style, ProviderName = "CarteraVencidaToast"
        });

    private record TramoOption(int Id, string Texto);
}
```

**Step 2: Compilar el cliente**

Run: `dotnet build Prestadoras/apc.Client/apc.Client.csproj`
Expected: build correcto.

> Si `CobranzaClient`, `InformesClient` o `GetCiclosAsync` no resuelven, copiar los `@using`/inyecciones exactos de `ClientesParaCobros.razor` (ya validados ahí).

---

## Task 10: Ítem de menú

**Files:**
- Modify: `Prestadoras/apc.Client/Layout/NavMenu.razor` (junto al ítem "Clientes para cobros", ~línea 169)

**Step 1: Agregar el `DxMenuItem`**

```razor
            <DxMenuItem NavigateUrl="/facturacion/cobranza/cartera-vencida"
                        Text="Cartera vencida"
                        IconCssClass="bi bi-calendar-x"
                        CssClass="@ActiveClass("/facturacion/cobranza/cartera-vencida")"
                        Attributes="@ItemAttrs("Cartera vencida")" />
```

**Step 2: Compilar el cliente**

Run: `dotnet build Prestadoras/apc.Client/apc.Client.csproj`
Expected: build correcto.

---

## Task 11: Índice de BD (rendimiento) + regla mirror

**Files:**
- Create: `Prestadoras/Database/2026-06-24_idx_factura_cartera_vencida.sql`

**Step 1: Crear el script idempotente**

```sql
-- Índice de apoyo para la consulta de cartera vencida (antigüedad por vencimiento).
CREATE INDEX IF NOT EXISTS ix_factura_cartera_vencida
    ON factura (company_id, fechavence, fechapago);
```

**Step 2: Aplicar en el mirror local**

Con `psql-runner` / `psql` contra `siad_v3_restore`. Verificar con:
`\d+ factura` (que aparezca `ix_factura_cartera_vencida`).

**Step 3: Replicar en SRV (VPN)**

Por la regla `hodsoft-db-mirror`, aplicar el MISMO script en SRV `siad_v3`. Si el VPN está abajo (172.16.0.9 inalcanzable), dejar registrado el pendiente igual que con los scripts `2026-06-23_*`.

---

## Task 12: Build de solución + verificación final

**Step 1: Build completo**

Run: `dotnet build HODSOFT_DEVEXPRESS.sln`
Expected: 0 errores.

**Step 2: Suite de tests**

Run: `$env:SIAD_TEST_DB = '<mirror>'; dotnet test Prestadoras/SIAD.Tests/SIAD.Tests.csproj`
Expected: verde (o `Skipped` sin mirror). Aplicar skill `verification-before-completion` antes de declarar terminado.

**Step 3: Verificación manual de UI (opcional, skill `verify`)**

Levantar `dotnet run --project Prestadoras/apc/apc.csproj`, navegar a `/facturacion/cobranza/cartera-vencida`, probar filtros (fecha de corte, tramo, búsqueda) y una acción masiva (exportar).

---

## Resumen de archivos

| Acción | Archivo |
|--------|---------|
| Create | `SIAD.Core/DTOs/Cobranza/CarteraVencidaDtos.cs` |
| Modify | `SIAD.Services/Cobranza/ICobranzaService.cs` |
| Modify | `SIAD.Services/Cobranza/CobranzaService.cs` |
| Create | `SIAD.Tests/CarteraVencidaTests.cs` |
| Modify | `apc/Controllers/CobranzaController.cs` |
| Modify | `apc.Client/Services/Cobranza/ClientesCobroClient.cs` |
| Create | `apc.Client/Pages/Facturacion/Cobranza/CarteraVencida.razor` |
| Modify | `apc.Client/Layout/NavMenu.razor` |
| Create | `Database/2026-06-24_idx_factura_cartera_vencida.sql` |

**Reutilizado sin cambios:** `acciones/lote` y `cartas-cobro` (controller + servicio + cliente), popup de acción en lote, export Xlsx.
