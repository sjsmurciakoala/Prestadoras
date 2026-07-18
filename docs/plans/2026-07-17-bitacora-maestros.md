# Bitácora de Maestros — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Auditar automáticamente crear/editar/eliminar sobre las tablas maestras (clientes, almacén, proveedores) en `public.bitacora_maestros`, con un mantenimiento para configurar qué entidades se auditan.

**Architecture:** Un `ISaveChangesInterceptor` de EF Core registrado **solo en `apc`** captura los cambios del `ChangeTracker` sobre una whitelist de entidades (definida en código `AuditableMaestros` + habilitada por empresa en `bitacora_maestro_config`, cacheada) y escribe filas de auditoría con diff campo-a-campo. Dos tablas nuevas tenant-scoped. Vista de consulta solo-lectura (calca `HistorialAccionesCobranza`) y mantenimiento CRUD (calca `alm_categoria_unidad`).

**Tech Stack:** .NET 9, EF Core + Npgsql (PostgreSQL), Blazor (InteractiveServer + WASM), DevExpress 25.1.7, xUnit (integración con `BEGIN…ROLLBACK`).

**Diseño de referencia:** `docs/plans/2026-07-17-bitacora-maestros-auditoria-design.md`.

**Convenciones del repo a respetar:**
- Multi-tenancy: `SiadDbContext` estampa/filtra `company_id`; las entidades implementan `SIAD.Core.Tenancy.ICompanyScopedEntity`. Ver `SIAD.Data/SiadDbContext.Tenancy.cs`.
- Fechas: `DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)` (columnas `timestamp without time zone`).
- Cambios de esquema = script SQL timestamped en `Database/` aplicado por el usuario (NO migraciones EF, NO conectarse al server).
- Mapeo inline con `.Select(...)` (NO AutoMapper en catálogos).
- Nombre técnico de entidad = **nombre de tabla** (`entry.Metadata.GetTableName()`).

**Permisos (simplificación aprobada):** la vista de bitácora usa `[ModuleAuthorize(PermissionModules.Configuracion)]` (GET → `module.configuracion.view`). El mantenimiento de config usa `[Authorize(Policy = AuthorizationPolicies.SuperAdmin)]`. No se crea un permiso granular nuevo (se puede añadir a futuro).

---

## Fase 0 — Base de datos

### Task 1: Script SQL de las dos tablas

**Files:**
- Create: `Database/2026-07-17_bitacora_maestros.sql`

**Step 1: Escribir el script (idempotente)**

```sql
-- =============================================================================
-- Bitácora de maestros: auditoría de acciones de usuario sobre tablas maestras.
-- Fecha: 2026-07-17
-- Regla DB Mirror: aplicar en siad_v3_restore (localhost) y en el servidor.
-- IDEMPOTENTE: CREATE TABLE IF NOT EXISTS + índices IF NOT EXISTS.
-- =============================================================================
BEGIN;

CREATE TABLE IF NOT EXISTS public.bitacora_maestros (
    id             BIGSERIAL PRIMARY KEY,
    company_id     BIGINT       NOT NULL,
    entidad        VARCHAR(100) NOT NULL,
    accion         VARCHAR(10)  NOT NULL,
    registro_id    VARCHAR(60)  NOT NULL,
    registro_desc  VARCHAR(250) NULL,
    cambios        JSONB        NULL,
    usuario        VARCHAR(100) NOT NULL,
    ip             VARCHAR(45)  NULL,
    fecha          TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS ix_bitacora_maestros_company_fecha    ON public.bitacora_maestros (company_id, fecha);
CREATE INDEX IF NOT EXISTS ix_bitacora_maestros_company_entidad  ON public.bitacora_maestros (company_id, entidad);
CREATE INDEX IF NOT EXISTS ix_bitacora_maestros_company_usuario  ON public.bitacora_maestros (company_id, usuario);
CREATE INDEX IF NOT EXISTS ix_bitacora_maestros_company_registro ON public.bitacora_maestros (company_id, entidad, registro_id);

CREATE TABLE IF NOT EXISTS public.bitacora_maestro_config (
    id                  SERIAL PRIMARY KEY,
    company_id          BIGINT       NOT NULL,
    entidad             VARCHAR(100) NOT NULL,
    habilitado          BOOLEAN      NOT NULL DEFAULT TRUE,
    audita_crear        BOOLEAN      NOT NULL DEFAULT TRUE,
    audita_editar       BOOLEAN      NOT NULL DEFAULT TRUE,
    audita_eliminar     BOOLEAN      NOT NULL DEFAULT TRUE,
    usuariocreacion     VARCHAR(100) NULL,
    fechacreacion       TIMESTAMP WITHOUT TIME ZONE NULL,
    usuariomodificacion VARCHAR(100) NULL,
    fechamodificacion   TIMESTAMP WITHOUT TIME ZONE NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS uq_bitacora_maestro_config_company_entidad
    ON public.bitacora_maestro_config (company_id, entidad);

COMMENT ON TABLE public.bitacora_maestros IS 'Auditoría append-only de acciones de usuario sobre tablas maestras.';
COMMENT ON TABLE public.bitacora_maestro_config IS 'Configura, por empresa, qué entidades maestras se auditan.';

COMMIT;
```

**Step 2: Avisar al usuario que lo aplique**

Este script lo aplica el usuario en el mirror (`siad_v3_restore` @ localhost) y en prod. NO conectarse al server. La implementación de las tablas de test (`SIAD_TEST_DB`) requiere que el mismo DDL esté aplicado en esa base — indicarlo al usuario antes de correr los tests de integración.

---

## Fase 1 — Entidades, DbContext y catálogo

### Task 2: Entidades EF

**Files:**
- Create: `SIAD.Core/Entities/bitacora_maestros.cs`
- Create: `SIAD.Core/Entities/bitacora_maestro_config.cs`

**Step 1: `bitacora_maestros.cs`**

```csharp
using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class bitacora_maestros : ICompanyScopedEntity
{
    public long id { get; set; }
    public long company_id { get; set; }
    public string entidad { get; set; } = null!;
    public string accion { get; set; } = null!;
    public string registro_id { get; set; } = null!;
    public string? registro_desc { get; set; }
    public string? cambios { get; set; }      // JSON serializado (columna jsonb)
    public string usuario { get; set; } = null!;
    public string? ip { get; set; }
    public DateTime fecha { get; set; }
}
```

**Step 2: `bitacora_maestro_config.cs`**

```csharp
using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class bitacora_maestro_config : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }
    public string entidad { get; set; } = null!;
    public bool habilitado { get; set; }
    public bool audita_crear { get; set; }
    public bool audita_editar { get; set; }
    public bool audita_eliminar { get; set; }
    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }
    public string? usuariomodificacion { get; set; }
    public DateTime? fechamodificacion { get; set; }
}
```

**Step 3: Verificar que compila** — `dotnet build SIAD.Core/SIAD.Core.csproj`. Expected: PASS.

---

### Task 3: DbSets + configuración Fluent

**Files:**
- Modify: `SIAD.Data/SiadDbContext.cs` (agregar los dos `DbSet`)
- Create: `SIAD.Data/SiadDbContext.Auditoria.cs` (partial con la config Fluent + llamada desde `OnModelCreatingPartial`)

**Step 1: Agregar DbSets** en `SiadDbContext.cs` (junto a los demás `public virtual DbSet<...>`):

```csharp
public virtual DbSet<bitacora_maestros> bitacora_maestros { get; set; } = null!;
public virtual DbSet<bitacora_maestro_config> bitacora_maestro_configs { get; set; } = null!;
```

**Step 2: Verificar el hook parcial de `OnModelCreating`.** Buscar en `SiadDbContext.cs` cómo se llaman los parciales (p. ej. `OnModelCreatingPartial(modelBuilder)` o una lista de `partial void`). Replicar el mismo mecanismo que usan `SiadDbContext.Almacen.cs` / `*.Cobranza.cs`. Si el patrón es `partial void OnModelCreatingPartial(ModelBuilder modelBuilder)`, entonces:

**Step 3: Crear `SiadDbContext.Auditoria.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using SIAD.Core.Entities;

namespace SIAD.Data;

public partial class SiadDbContext
{
    partial void ConfigureAuditoria(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<bitacora_maestros>(entity =>
        {
            entity.HasKey(e => e.id).HasName("bitacora_maestros_pkey");
            entity.ToTable("bitacora_maestros", "public");
            entity.HasIndex(e => new { e.company_id, e.fecha }, "ix_bitacora_maestros_company_fecha");
            entity.HasIndex(e => new { e.company_id, e.entidad }, "ix_bitacora_maestros_company_entidad");
            entity.Property(e => e.entidad).HasMaxLength(100);
            entity.Property(e => e.accion).HasMaxLength(10);
            entity.Property(e => e.registro_id).HasMaxLength(60);
            entity.Property(e => e.registro_desc).HasMaxLength(250);
            entity.Property(e => e.cambios).HasColumnType("jsonb");
            entity.Property(e => e.usuario).HasMaxLength(100);
            entity.Property(e => e.ip).HasMaxLength(45);
            entity.Property(e => e.fecha).HasColumnType("timestamp without time zone");
        });

        modelBuilder.Entity<bitacora_maestro_config>(entity =>
        {
            entity.HasKey(e => e.id).HasName("bitacora_maestro_config_pkey");
            entity.ToTable("bitacora_maestro_config", "public");
            entity.HasIndex(e => new { e.company_id, e.entidad }, "uq_bitacora_maestro_config_company_entidad").IsUnique();
            entity.Property(e => e.entidad).HasMaxLength(100);
            entity.Property(e => e.habilitado).HasDefaultValue(true);
            entity.Property(e => e.audita_crear).HasDefaultValue(true);
            entity.Property(e => e.audita_editar).HasDefaultValue(true);
            entity.Property(e => e.audita_eliminar).HasDefaultValue(true);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");
        });
    }
}
```

**Step 4: Enganchar `ConfigureAuditoria`.** Si el patrón del repo es `partial void OnModelCreatingPartial(...)` implementado una sola vez, agregar `ConfigureAuditoria(modelBuilder);` en el cuerpo existente (donde se llaman las demás configs). Si cada módulo declara su propio `partial void`, declarar `partial void ConfigureAuditoria(ModelBuilder modelBuilder);` en `SiadDbContext.cs` y llamarlo. **Verificar el patrón real antes de escribir** — no inventar.

**Step 5: Verificar build** — `dotnet build SIAD.Data/SIAD.Data.csproj`. Expected: PASS.

---

### Task 4: Catálogo `AuditableMaestros`

**Files:**
- Create: `SIAD.Core/Constants/AuditableMaestros.cs`

**Step 1: Escribir el catálogo**

```csharp
using System.Collections.Generic;
using System.Linq;

namespace SIAD.Core.Constants;

/// <summary>
/// Lista blanca de tablas maestras candidatas a auditoría (bitácora de maestros).
/// La clave es el nombre de TABLA (entry.Metadata.GetTableName()).
/// El interceptor solo audita las que además estén habilitadas en bitacora_maestro_config.
/// </summary>
public static class AuditableMaestros
{
    public sealed record Item(string Tabla, string Nombre, string Modulo);

    public static readonly IReadOnlyList<Item> All =
    [
        new("cliente_maestro",                "Maestro de clientes",            "Clientes"),
        new("alm_articulo",                   "Artículos",                      "Almacén"),
        new("alm_grupo",                      "Grupos de artículo",             "Almacén"),
        new("alm_tipo_articulo",              "Tipos de artículo",              "Almacén"),
        new("alm_bodega",                     "Bodegas",                        "Almacén"),
        new("alm_categoria_unidad",           "Categorías de unidad",           "Almacén"),
        new("alm_unidad_medida",              "Unidades de medida",             "Almacén"),
        new("prv_proveedor",                  "Maestro de proveedores",         "Proveedores"),
        new("prv_proveedor_cuenta_bancaria",  "Cuentas bancarias de proveedor", "Proveedores"),
    ];

    private static readonly HashSet<string> _tablas =
        All.Select(x => x.Tabla).ToHashSet(System.StringComparer.OrdinalIgnoreCase);

    public static bool EsAuditable(string? tabla) => tabla is not null && _tablas.Contains(tabla);

    public static string NombreDe(string tabla) =>
        All.FirstOrDefault(x => string.Equals(x.Tabla, tabla, System.StringComparison.OrdinalIgnoreCase))?.Nombre ?? tabla;
}
```

**Step 2: Verificar los nombres de tabla reales.** Antes de dar por buena la lista, confirmar cada `ToTable(...)` en los parciales de `SiadDbContext` (p. ej. `cliente_maestro`, `prv_proveedor`). Ajustar si alguno difiere.

**Step 3: Build** `dotnet build SIAD.Core/SIAD.Core.csproj`. Expected: PASS.

---

## Fase 2 — Interceptor (núcleo, TDD)

### Task 5: Utilidad de diff (TDD, test unitario puro)

**Files:**
- Create: `SIAD.Data/Auditoria/AuditDiff.cs`
- Create: `SIAD.Tests/Auditoria/AuditDiffTests.cs`

**Step 1: Escribir el test que falla**

```csharp
using System.Text.Json;
using SIAD.Data.Auditoria;
using Xunit;

namespace SIAD.Tests.Auditoria;

public class AuditDiffTests
{
    [Fact]
    public void Serializa_solo_campos_cambiados()
    {
        var cambios = new List<AuditDiff.Campo>
        {
            new("nombre", "viejo", "nuevo"),
            new("activo", true, false),
        };
        var json = AuditDiff.Serialize(cambios);

        using var doc = JsonDocument.Parse(json!);
        Assert.Equal(2, doc.RootElement.GetArrayLength());
        Assert.Equal("nombre", doc.RootElement[0].GetProperty("campo").GetString());
    }

    [Fact]
    public void Lista_vacia_devuelve_null()
    {
        Assert.Null(AuditDiff.Serialize(new List<AuditDiff.Campo>()));
    }
}
```

**Step 2: Correr y verificar que falla** — `dotnet test SIAD.Tests/SIAD.Tests.csproj --filter "FullyQualifiedName~AuditDiffTests"`. Expected: FAIL (no compila / tipo inexistente). Nota: estos tests son unitarios puros (no requieren `SIAD_TEST_DB`).

**Step 3: Implementar `AuditDiff`**

```csharp
using System.Collections.Generic;
using System.Text.Json;

namespace SIAD.Data.Auditoria;

public static class AuditDiff
{
    public sealed record Campo(string campo, object? antes, object? despues);

    private static readonly JsonSerializerOptions _opts = new() { WriteIndented = false };

    public static string? Serialize(IReadOnlyList<Campo> campos)
        => campos is { Count: > 0 } ? JsonSerializer.Serialize(campos, _opts) : null;
}
```

**Step 4: Correr y verificar que pasa** — mismo filtro. Expected: PASS.

---

### Task 6: `IAuditConfigProvider` (whitelist cacheada)

**Files:**
- Create: `SIAD.Services/Auditoria/IAuditConfigProvider.cs`
- Create: `SIAD.Services/Auditoria/AuditConfigProvider.cs`

**Step 1: Interfaz**

```csharp
namespace SIAD.Services.Auditoria;

public interface IAuditConfigProvider
{
    /// <summary>¿Se audita esta acción sobre esta tabla, para esta empresa?</summary>
    bool DebeAuditar(long companyId, string tabla, string accion); // accion: CREAR|EDITAR|ELIMINAR
    void Invalidar(long companyId);
}
```

**Step 2: Implementación** (cache por empresa con `IMemoryCache`; lee la config con un scope corto para no atarse al `DbContext` de la request):

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using SIAD.Core.Constants;

namespace SIAD.Services.Auditoria;

public sealed class AuditConfigProvider : IAuditConfigProvider
{
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;
    public AuditConfigProvider(IMemoryCache cache, IServiceScopeFactory scopeFactory)
        => (_cache, _scopeFactory) = (cache, scopeFactory);

    private sealed record Flags(bool Crear, bool Editar, bool Eliminar);

    private static string Key(long companyId) => $"auditcfg:{companyId}";

    public bool DebeAuditar(long companyId, string tabla, string accion)
    {
        if (companyId <= 0 || !AuditableMaestros.EsAuditable(tabla)) return false;
        var map = _cache.GetOrCreate(Key(companyId), e =>
        {
            e.AbsoluteExpirationRelativeToNow = System.TimeSpan.FromMinutes(30);
            return Load(companyId);
        })!;
        if (!map.TryGetValue(tabla, out var f)) return false;
        return accion switch
        {
            "CREAR" => f.Crear,
            "EDITAR" => f.Editar,
            "ELIMINAR" => f.Eliminar,
            _ => false
        };
    }

    public void Invalidar(long companyId) => _cache.Remove(Key(companyId));

    private Dictionary<string, Flags> Load(long companyId)
    {
        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<SIAD.Data.SiadDbContext>();
        // IgnoreQueryFilters: el provider corre fuera del scope del usuario; filtra explícito por companyId.
        return ctx.bitacora_maestro_configs
            .IgnoreQueryFilters()
            .Where(c => c.company_id == companyId && c.habilitado)
            .AsNoTracking()
            .ToDictionary(c => c.entidad, c => new Flags(c.audita_crear, c.audita_editar, c.audita_eliminar),
                System.StringComparer.OrdinalIgnoreCase);
    }
}
```

**Step 3: Build** `dotnet build SIAD.Services/SIAD.Services.csproj`. Expected: PASS.

> Nota de test: en los tests de integración se usará un **fake** de `IAuditConfigProvider` que devuelva `true` para la tabla bajo prueba, evitando depender del cache/estado real.

---

### Task 7: `BitacoraMaestrosInterceptor` (TDD, integración)

**Files:**
- Create: `SIAD.Data/Auditoria/BitacoraMaestrosInterceptor.cs`
- Create: `SIAD.Data/Auditoria/ICurrentUserAudit.cs` (abstracción de usuario/IP para poder testear)
- Create: `SIAD.Tests/Auditoria/BitacoraMaestrosInterceptorTests.cs`

**Step 1: Abstracción de usuario/IP**

```csharp
namespace SIAD.Data.Auditoria;

public interface ICurrentUserAudit
{
    string Usuario { get; }   // User.Identity.Name ?? "system"
    string? Ip { get; }
}
```

Implementación en `apc` (Task 8) sobre `IHttpContextAccessor`. En tests, un fake fijo.

**Step 2: Escribir los tests de integración que fallan**

```csharp
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Data.Auditoria;
using SIAD.Services.Almacen;
using SIAD.Core.DTOs.Almacen;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.Auditoria;

[Collection("Postgres")]
public class BitacoraMaestrosInterceptorTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private IBodegaService? _bodegas;

    public BitacoraMaestrosInterceptorTests(PostgresFixture fixture) : base(fixture) { }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        if (!Fixture.Available) return;

        var interceptor = new BitacoraMaestrosInterceptor(
            new FakeAuditConfig(),                    // audita todo lo auditable
            new FakeUser("tester", "10.0.0.1"));

        var options = new DbContextOptionsBuilder<SiadDbContext>()
            .UseNpgsql(Connection)
            .AddInterceptors(interceptor)
            .Options;

        _context = new SiadDbContext(options, new TestCurrentCompanyService(CompanyId));
        _context.Database.UseTransaction(Transaction);
        _bodegas = new BodegaService(_context);
    }

    public new Task DisposeAsync() { _context?.Dispose(); return base.DisposeAsync(); }

    [SkippableFact]
    public async Task Crear_bodega_genera_una_fila_CREAR_con_registro_id()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var creado = await _bodegas!.CreateAsync(new BodegaEditDto { Codigo = "AUD1", Nombre = "X" }, "tester");

        var filas = await _context!.bitacora_maestros.IgnoreQueryFilters()
            .Where(b => b.entidad == "alm_bodega" && b.company_id == CompanyId)
            .OrderByDescending(b => b.id).Take(1).ToListAsync();
        var fila = Assert.Single(filas);
        Assert.Equal("CREAR", fila.accion);
        Assert.Equal(creado.Id!.Value.ToString(), fila.registro_id);
        Assert.Equal("tester", fila.usuario);
    }

    [SkippableFact]
    public async Task Baja_logica_activo_false_se_registra_como_ELIMINAR()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
        var creado = await _bodegas!.CreateAsync(new BodegaEditDto { Codigo = "AUD2", Nombre = "Y" }, "tester");
        await _bodegas.DeactivateAsync(creado.Id!.Value, "tester");

        var fila = await _context!.bitacora_maestros.IgnoreQueryFilters()
            .Where(b => b.entidad == "alm_bodega" && b.registro_id == creado.Id.Value.ToString())
            .OrderByDescending(b => b.id).FirstAsync();
        Assert.Equal("ELIMINAR", fila.accion);
    }

    // Fakes
    private sealed class FakeAuditConfig : SIAD.Services.Auditoria.IAuditConfigProvider
    {
        public bool DebeAuditar(long companyId, string tabla, string accion)
            => SIAD.Core.Constants.AuditableMaestros.EsAuditable(tabla);
        public void Invalidar(long companyId) { }
    }
    private sealed class FakeUser : ICurrentUserAudit
    {
        public FakeUser(string u, string? ip) { Usuario = u; Ip = ip; }
        public string Usuario { get; } public string? Ip { get; }
    }
    private sealed class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _id; public TestCurrentCompanyService(long id) => _id = id;
        public long GetCompanyId() => _id;
    }
}
```

**Step 3: Correr y verificar que fallan** — `dotnet test ... --filter "FullyQualifiedName~BitacoraMaestrosInterceptorTests"` (requiere `SIAD_TEST_DB` con el DDL de la Task 1 aplicado). Expected: FAIL (interceptor inexistente).

**Step 4: Implementar el interceptor**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SIAD.Core.Entities;
using SIAD.Services.Auditoria;

namespace SIAD.Data.Auditoria;

public sealed class BitacoraMaestrosInterceptor : SaveChangesInterceptor
{
    private readonly IAuditConfigProvider _config;
    private readonly ICurrentUserAudit _user;

    // Pendientes de completar registro_id tras el commit (altas con PK identity).
    private readonly List<Pendiente> _pendientes = new();
    private bool _reentrada; // evita auditar el propio SaveChanges de la bitácora

    public BitacoraMaestrosInterceptor(IAuditConfigProvider config, ICurrentUserAudit user)
        => (_config, _user) = (config, user);

    private sealed record Pendiente(EntityEntry Entry, bitacora_maestros Fila);

    public override InterceptionResult<int> SavingChanges(DbContextEventData e, InterceptionResult<int> r)
    { Capturar(e.Context); return base.SavingChanges(e, r); }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData e, InterceptionResult<int> r, CancellationToken ct = default)
    { Capturar(e.Context); return base.SavingChangesAsync(e, r, ct); }

    public override int SavedChanges(SaveChangesCompletedEventData e, int result)
    { Completar(e.Context); return base.SavedChanges(e, result); }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData e, int result, CancellationToken ct = default)
    { await CompletarAsync(e.Context, ct); return await base.SavedChangesAsync(e, result, ct); }

    private void Capturar(DbContext? ctx)
    {
        if (ctx is null || _reentrada) return;
        _pendientes.Clear();
        var nuevas = new List<bitacora_maestros>();

        foreach (var entry in ctx.ChangeTracker.Entries().ToList())
        {
            var tabla = entry.Metadata.GetTableName();
            if (tabla is null) continue;
            if (tabla is "bitacora_maestros" or "bitacora_maestro_config") continue; // anti-recursión
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;

            var accion = ResolverAccion(entry);
            var companyId = LeerCompanyId(entry);
            if (!_config.DebeAuditar(companyId, tabla, accion)) continue;

            var (campos, pk) = LeerCamposYPk(entry, accion);
            var fila = new bitacora_maestros
            {
                company_id = companyId,
                entidad = tabla,
                accion = accion,
                registro_id = pk ?? string.Empty,
                registro_desc = LeerDescriptor(entry),
                cambios = AuditDiff.Serialize(campos),
                usuario = _user.Usuario,
                ip = _user.Ip,
                fecha = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
            };

            if (string.IsNullOrEmpty(pk)) _pendientes.Add(new Pendiente(entry, fila)); // alta identity
            else nuevas.Add(fila);
        }

        if (nuevas.Count > 0) ctx.Set<bitacora_maestros>().AddRange(nuevas); // se insertan en la misma transacción
    }

    private void Completar(DbContext? ctx) => CompletarAsync(ctx, CancellationToken.None).AsTask().GetAwaiter().GetResult();

    private async ValueTask CompletarAsync(DbContext? ctx, CancellationToken ct)
    {
        if (ctx is null || _reentrada || _pendientes.Count == 0) return;
        foreach (var p in _pendientes)
            p.Fila.registro_id = LeerPk(p.Entry) ?? string.Empty;

        var filas = _pendientes.Select(p => p.Fila).ToList();
        _pendientes.Clear();
        try
        {
            _reentrada = true;
            ctx.Set<bitacora_maestros>().AddRange(filas);
            await ctx.SaveChangesAsync(ct); // no vuelve a auditar (bitacora_maestros excluida) y _reentrada=true
        }
        finally { _reentrada = false; }
    }

    private static string ResolverAccion(EntityEntry entry)
    {
        if (entry.State == EntityState.Added) return "CREAR";
        if (entry.State == EntityState.Deleted) return "ELIMINAR";
        // Modified: baja lógica activo true->false => ELIMINAR
        var activo = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "activo");
        if (activo is not null && activo.IsModified
            && activo.OriginalValue is true && activo.CurrentValue is false)
            return "ELIMINAR";
        return "EDITAR";
    }

    private static long LeerCompanyId(EntityEntry entry)
    {
        var p = entry.Properties.FirstOrDefault(x => x.Metadata.Name == "company_id");
        return p?.CurrentValue is long l ? l : 0;
    }

    private static (List<AuditDiff.Campo> campos, string? pk) LeerCamposYPk(EntityEntry entry, string accion)
    {
        var campos = new List<AuditDiff.Campo>();
        foreach (var prop in entry.Properties)
        {
            var name = prop.Metadata.Name;
            if (name is "company_id") continue;
            switch (entry.State)
            {
                case EntityState.Added:
                    if (prop.CurrentValue is not null) campos.Add(new(name, null, prop.CurrentValue));
                    break;
                case EntityState.Deleted:
                    campos.Add(new(name, prop.OriginalValue, null));
                    break;
                default: // Modified
                    if (prop.IsModified && !Equals(prop.OriginalValue, prop.CurrentValue))
                        campos.Add(new(name, prop.OriginalValue, prop.CurrentValue));
                    break;
            }
        }
        return (campos, LeerPk(entry));
    }

    private static string? LeerPk(EntityEntry entry)
    {
        var pk = entry.Metadata.FindPrimaryKey();
        if (pk is null) return null;
        var vals = pk.Properties.Select(p => entry.Property(p.Name).CurrentValue?.ToString()).ToArray();
        if (vals.Any(v => string.IsNullOrEmpty(v) || v == "0")) return null; // identity aún no asignada
        return string.Join("|", vals);
    }

    private static string? LeerDescriptor(EntityEntry entry)
    {
        foreach (var cand in new[] { "nombre", "descripcion", "razon_social", "codigo" })
        {
            var p = entry.Properties.FirstOrDefault(x => x.Metadata.Name == cand);
            var v = p?.CurrentValue ?? p?.OriginalValue;
            if (v is string s && !string.IsNullOrWhiteSpace(s)) return s.Length > 250 ? s[..250] : s;
        }
        return null;
    }
}
```

**Step 5: Correr los tests y verificar que pasan** — mismo filtro. Expected: PASS. Si el orden `SavedChanges` sync no aplica (EF usa la ruta async), el `Completar` sync se cubre igual; mantener ambas rutas.

**Step 6: Agregar tests restantes** (mismo archivo): editar → 1 fila `EDITAR` con diff solo de campos cambiados; entidad no auditable (usar un servicio transaccional, p. ej. crear algo fuera del catálogo) → 0 filas; verificar que no se auto-audita `bitacora_maestros`. Correr y verificar PASS.

---

### Task 8: Registro DI + interceptor en `apc`

**Files:**
- Create: `apc/Security/CurrentUserAudit.cs` (impl de `ICurrentUserAudit` sobre `IHttpContextAccessor`)
- Modify: `apc/Program.cs:183-184` (AddDbContext con interceptor)
- Modify: `SIAD.Services/ServiceRegistration.cs` (registrar `IAuditConfigProvider`, `IMemoryCache`)

**Step 1: `CurrentUserAudit`**

```csharp
using Microsoft.AspNetCore.Http;
using SIAD.Data.Auditoria;

namespace apc.Security;

public sealed class CurrentUserAudit : ICurrentUserAudit
{
    private readonly IHttpContextAccessor _http;
    public CurrentUserAudit(IHttpContextAccessor http) => _http = http;
    public string Usuario => _http.HttpContext?.User?.Identity?.Name ?? "system";
    public string? Ip => _http.HttpContext?.Connection?.RemoteIpAddress?.ToString();
}
```

**Step 2: DI en `ServiceRegistration.cs`** (dentro de `AddSiadServices`):

```csharp
services.AddMemoryCache();
services.AddScoped<IAuditConfigProvider, AuditConfigProvider>();
services.AddScoped<IBitacoraMaestrosService, BitacoraMaestrosService>(); // Task 9
services.AddScoped<IAuditoriaConfigService, AuditoriaConfigService>();   // Task 10
```

**Step 3: Interceptor + `ICurrentUserAudit` en `apc/Program.cs`.** `IHttpContextAccessor` ya suele estar registrado (lo usa `CurrentCompanyService`); si no, agregar `builder.Services.AddHttpContextAccessor();`. Luego:

```csharp
builder.Services.AddScoped<ICurrentUserAudit, apc.Security.CurrentUserAudit>();
builder.Services.AddScoped<BitacoraMaestrosInterceptor>();
builder.Services.AddDbContext<SiadDbContext>((sp, options) =>
    options.UseNpgsql(connectionString)
           .AddInterceptors(sp.GetRequiredService<BitacoraMaestrosInterceptor>()));
```

(Reemplaza el `AddDbContext<SiadDbContext>` actual de las líneas 183-184. **No** tocar los registros de `apc.BancosWs` ni `apc.MobileApi` — la auditoría es solo del portal.)

**Step 4: Build de la solución** — `dotnet build HODSOFT_DEVEXPRESS.sln`. Expected: PASS.

---

## Fase 3 — Servicios de aplicación

### Task 9: `IBitacoraMaestrosService` + DTOs (consulta)

**Files:**
- Create: `SIAD.Core/DTOs/Auditoria/BitacoraMaestroFilterDto.cs`
- Create: `SIAD.Core/DTOs/Auditoria/BitacoraMaestroListItemDto.cs`
- Create: `SIAD.Services/Auditoria/IBitacoraMaestrosService.cs`
- Create: `SIAD.Services/Auditoria/BitacoraMaestrosService.cs`
- Create: `SIAD.Services/Auditoria/BitacoraMaestrosConstantes.cs` (MaxFilas = 5000)

**Step 1: DTOs**

```csharp
// BitacoraMaestroFilterDto.cs
using System;
namespace SIAD.Core.DTOs.Auditoria;
public sealed class BitacoraMaestroFilterDto
{
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
    public string? Entidad { get; set; }   // nombre de tabla
    public string? Usuario { get; set; }
    public string? Accion { get; set; }    // CREAR|EDITAR|ELIMINAR
}
```

```csharp
// BitacoraMaestroListItemDto.cs
using System;
namespace SIAD.Core.DTOs.Auditoria;
public sealed class BitacoraMaestroListItemDto
{
    public long Id { get; init; }
    public DateTime Fecha { get; init; }
    public string Usuario { get; init; } = string.Empty;
    public string Entidad { get; init; } = string.Empty;        // tabla
    public string EntidadNombre { get; init; } = string.Empty;  // amigable (AuditableMaestros.NombreDe)
    public string Accion { get; init; } = string.Empty;
    public string RegistroId { get; init; } = string.Empty;
    public string? RegistroDesc { get; init; }
    public string? Cambios { get; init; }                        // JSON crudo (lo formatea la UI)
    public string? Ip { get; init; }
}
```

**Step 2: Interfaz + constantes**

```csharp
// IBitacoraMaestrosService.cs
using SIAD.Core.DTOs.Auditoria;
namespace SIAD.Services.Auditoria;
public interface IBitacoraMaestrosService
{
    Task<IReadOnlyList<BitacoraMaestroListItemDto>> BuscarAsync(BitacoraMaestroFilterDto filtro, CancellationToken ct = default);
}
// BitacoraMaestrosConstantes.cs
namespace SIAD.Services.Auditoria;
public static class BitacoraMaestrosConstantes { public const int MaxFilas = 5000; }
```

**Step 3: Servicio** (mapeo inline; resuelve nombre amigable con `AuditableMaestros`):

```csharp
using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Auditoria;
using SIAD.Data;

namespace SIAD.Services.Auditoria;

public sealed class BitacoraMaestrosService : IBitacoraMaestrosService
{
    private readonly SiadDbContext _context;
    public BitacoraMaestrosService(SiadDbContext context) => _context = context;

    public async Task<IReadOnlyList<BitacoraMaestroListItemDto>> BuscarAsync(BitacoraMaestroFilterDto filtro, CancellationToken ct = default)
    {
        var q = _context.bitacora_maestros.AsNoTracking().AsQueryable();
        if (filtro.Desde is { } d) q = q.Where(b => b.fecha >= d.Date);
        if (filtro.Hasta is { } h) q = q.Where(b => b.fecha < h.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(filtro.Entidad)) q = q.Where(b => b.entidad == filtro.Entidad);
        if (!string.IsNullOrWhiteSpace(filtro.Accion)) q = q.Where(b => b.accion == filtro.Accion);
        if (!string.IsNullOrWhiteSpace(filtro.Usuario))
        {
            var like = $"%{filtro.Usuario.Trim()}%";
            q = q.Where(b => EF.Functions.ILike(b.usuario, like));
        }

        var rows = await q.OrderByDescending(b => b.fecha).Take(BitacoraMaestrosConstantes.MaxFilas)
            .Select(b => new
            {
                b.id, b.fecha, b.usuario, b.entidad, b.accion, b.registro_id, b.registro_desc, b.cambios, b.ip
            }).ToListAsync(ct);

        return rows.Select(b => new BitacoraMaestroListItemDto
        {
            Id = b.id, Fecha = b.fecha, Usuario = b.usuario,
            Entidad = b.entidad, EntidadNombre = AuditableMaestros.NombreDe(b.entidad),
            Accion = b.accion, RegistroId = b.registro_id, RegistroDesc = b.registro_desc,
            Cambios = b.cambios, Ip = b.ip
        }).ToList();
    }
}
```

**Step 4: Build** `dotnet build SIAD.Services/SIAD.Services.csproj`. Expected: PASS.

---

### Task 10: `IAuditoriaConfigService` + DTO (CRUD config)

**Files:**
- Create: `SIAD.Core/DTOs/Auditoria/AuditoriaConfigItemDto.cs`
- Create: `SIAD.Services/Auditoria/IAuditoriaConfigService.cs`
- Create: `SIAD.Services/Auditoria/AuditoriaConfigService.cs`

**Step 1: DTO**

```csharp
namespace SIAD.Core.DTOs.Auditoria;
public sealed class AuditoriaConfigItemDto
{
    public string Entidad { get; set; } = string.Empty;   // tabla
    public string Nombre { get; set; } = string.Empty;    // amigable
    public string Modulo { get; set; } = string.Empty;
    public bool Habilitado { get; set; }
    public bool AuditaCrear { get; set; } = true;
    public bool AuditaEditar { get; set; } = true;
    public bool AuditaEliminar { get; set; } = true;
}
```

**Step 2: Interfaz**

```csharp
using SIAD.Core.DTOs.Auditoria;
namespace SIAD.Services.Auditoria;
public interface IAuditoriaConfigService
{
    /// <summary>Devuelve el catálogo AuditableMaestros combinado con la config guardada.</summary>
    Task<IReadOnlyList<AuditoriaConfigItemDto>> GetAsync(CancellationToken ct = default);
    Task GuardarAsync(IReadOnlyList<AuditoriaConfigItemDto> items, string user, CancellationToken ct = default);
}
```

**Step 3: Servicio** (upsert por entidad del catálogo; invalida cache). Inyecta `IAuditConfigProvider` para invalidar y `ICurrentCompanyService` para el companyId a invalidar:

```csharp
using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Auditoria;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.Auditoria;

public sealed class AuditoriaConfigService : IAuditoriaConfigService
{
    private readonly SiadDbContext _context;
    private readonly IAuditConfigProvider _provider;
    private readonly ICurrentCompanyService _company;

    public AuditoriaConfigService(SiadDbContext context, IAuditConfigProvider provider, ICurrentCompanyService company)
        => (_context, _provider, _company) = (context, provider, company);

    public async Task<IReadOnlyList<AuditoriaConfigItemDto>> GetAsync(CancellationToken ct = default)
    {
        var guardadas = await _context.bitacora_maestro_configs.AsNoTracking()
            .ToDictionaryAsync(c => c.entidad, c => c, System.StringComparer.OrdinalIgnoreCase, ct);

        return AuditableMaestros.All.Select(m =>
        {
            guardadas.TryGetValue(m.Tabla, out var cfg);
            return new AuditoriaConfigItemDto
            {
                Entidad = m.Tabla, Nombre = m.Nombre, Modulo = m.Modulo,
                Habilitado = cfg?.habilitado ?? false,
                AuditaCrear = cfg?.audita_crear ?? true,
                AuditaEditar = cfg?.audita_editar ?? true,
                AuditaEliminar = cfg?.audita_eliminar ?? true,
            };
        }).ToList();
    }

    public async Task GuardarAsync(IReadOnlyList<AuditoriaConfigItemDto> items, string user, CancellationToken ct = default)
    {
        var validas = items.Where(i => AuditableMaestros.EsAuditable(i.Entidad)).ToList();
        var existentes = await _context.bitacora_maestro_configs
            .ToDictionaryAsync(c => c.entidad, c => c, System.StringComparer.OrdinalIgnoreCase, ct);
        var ahora = System.DateTime.SpecifyKind(System.DateTime.UtcNow, System.DateTimeKind.Unspecified);

        foreach (var i in validas)
        {
            if (existentes.TryGetValue(i.Entidad, out var cfg))
            {
                cfg.habilitado = i.Habilitado;
                cfg.audita_crear = i.AuditaCrear; cfg.audita_editar = i.AuditaEditar; cfg.audita_eliminar = i.AuditaEliminar;
                cfg.usuariomodificacion = user; cfg.fechamodificacion = ahora;
            }
            else
            {
                _context.bitacora_maestro_configs.Add(new bitacora_maestro_config
                {
                    entidad = i.Entidad, habilitado = i.Habilitado,
                    audita_crear = i.AuditaCrear, audita_editar = i.AuditaEditar, audita_eliminar = i.AuditaEliminar,
                    usuariocreacion = user, fechacreacion = ahora
                });
            }
        }
        await _context.SaveChangesAsync(ct);
        _provider.Invalidar(_company.GetCompanyId());
    }
}
```

**Step 4: Build** `dotnet build SIAD.Services/SIAD.Services.csproj`. Expected: PASS.

---

## Fase 4 — Controladores API

### Task 11: `BitacoraMaestrosController`

**Files:**
- Create: `apc/Controllers/Auditoria/BitacoraMaestrosController.cs`

```csharp
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Auditoria;
using SIAD.Services.Auditoria;
using apc.Security;

namespace apc.Controllers.Auditoria;

[ApiController]
[Route("api/auditoria/bitacora-maestros")]
[ModuleAuthorize(PermissionModules.Configuracion)]
public sealed class BitacoraMaestrosController : ControllerBase
{
    private readonly IBitacoraMaestrosService _service;
    public BitacoraMaestrosController(IBitacoraMaestrosService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Buscar([FromQuery] BitacoraMaestroFilterDto filtro, CancellationToken ct)
        => Ok(await _service.BuscarAsync(filtro, ct));
}
```

**Step 1: Build** `dotnet build apc/apc.csproj`. Expected: PASS.

---

### Task 12: `AuditoriaConfigController` (solo SuperAdmin)

**Files:**
- Create: `apc/Controllers/Auditoria/AuditoriaConfigController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Auditoria;
using SIAD.Services.Auditoria;

namespace apc.Controllers.Auditoria;

[ApiController]
[Route("api/auditoria/configuracion")]
[Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
public sealed class AuditoriaConfigController : ControllerBase
{
    private readonly IAuditoriaConfigService _service;
    public AuditoriaConfigController(IAuditoriaConfigService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await _service.GetAsync(ct));

    [HttpPut]
    public async Task<IActionResult> Guardar([FromBody] List<AuditoriaConfigItemDto> items, CancellationToken ct)
    {
        await _service.GuardarAsync(items, User?.Identity?.Name ?? "system", ct);
        return Ok(new { success = true });
    }
}
```

**Step 1: Build** `dotnet build apc/apc.csproj`. Expected: PASS.

---

## Fase 5 — Clientes HTTP

### Task 13: Clients + registro

**Files:**
- Create: `apc.Client/Services/Auditoria/BitacoraMaestrosClient.cs`
- Create: `apc.Client/Services/Auditoria/AuditoriaConfigClient.cs`
- Modify: `apc.Client/CommonServices.cs` (registrar ambos `AddScoped`)

**Step 1: `BitacoraMaestrosClient`** (usa extensiones auth-aware de `HttpClientExtensions`):

```csharp
using System.Globalization;
using apc.Client.Services; // HttpClientExtensions
using SIAD.Core.DTOs.Auditoria;

namespace apc.Client.Services.Auditoria;

public sealed class BitacoraMaestrosClient
{
    private readonly HttpClient _http;
    public BitacoraMaestrosClient(HttpClient http) => _http = http;

    public async Task<List<BitacoraMaestroListItemDto>> BuscarAsync(BitacoraMaestroFilterDto f, CancellationToken ct = default)
    {
        var qs = new List<string>();
        if (f.Desde is { } d) qs.Add($"desde={d:yyyy-MM-dd}");
        if (f.Hasta is { } h) qs.Add($"hasta={h:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(f.Entidad)) qs.Add($"entidad={Uri.EscapeDataString(f.Entidad)}");
        if (!string.IsNullOrWhiteSpace(f.Accion)) qs.Add($"accion={Uri.EscapeDataString(f.Accion)}");
        if (!string.IsNullOrWhiteSpace(f.Usuario)) qs.Add($"usuario={Uri.EscapeDataString(f.Usuario)}");
        var url = "api/auditoria/bitacora-maestros" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");
        return await _http.GetFromJsonAsyncWithAuthCheck<List<BitacoraMaestroListItemDto>>(url, ct) ?? new();
    }
}
```

**Step 2: `AuditoriaConfigClient`**

```csharp
using apc.Client.Services;
using SIAD.Core.DTOs.Auditoria;

namespace apc.Client.Services.Auditoria;

public sealed class AuditoriaConfigClient
{
    private readonly HttpClient _http;
    public AuditoriaConfigClient(HttpClient http) => _http = http;

    public async Task<List<AuditoriaConfigItemDto>> GetAsync(CancellationToken ct = default)
        => await _http.GetFromJsonAsyncWithAuthCheck<List<AuditoriaConfigItemDto>>("api/auditoria/configuracion", ct) ?? new();

    public async Task GuardarAsync(List<AuditoriaConfigItemDto> items, CancellationToken ct = default)
    {
        var r = await _http.PutAsJsonAsyncWithAuthCheck("api/auditoria/configuracion", items, ct);
        if (!r.IsSuccessStatusCode)
            throw new InvalidOperationException(await HttpClientExtensions.ObtenerMensajeErrorAsync(r, ct));
    }
}
```

**Step 3: Registrar en `CommonServices.cs`** (junto a los demás clients):

```csharp
services.AddScoped<BitacoraMaestrosClient>();
services.AddScoped<AuditoriaConfigClient>();
```

**Step 4: Verificar las firmas exactas** de `GetFromJsonAsyncWithAuthCheck` / `PutAsJsonAsyncWithAuthCheck` / `ObtenerMensajeErrorAsync` en `apc.Client/Services/HttpClientExtensions.cs` y ajustar si difieren (namespace/parametros).

**Step 5: Build** `dotnet build apc.Client/apc.Client.csproj`. Expected: PASS.

---

## Fase 6 — UI Blazor

### Task 14: Vista de bitácora (`BitacoraMaestrosList.razor`)

**Files:**
- Create: `apc.Client/Pages/Auditoria/BitacoraMaestrosList.razor`

**Referencia:** calcar la estructura de `apc.Client/Pages/Facturacion/Cobranza/HistorialAccionesCobranza.razor` (filtros fecha/entidad/usuario/acción, botón Buscar, `DxGrid @ref` con export Excel/PDF, tope `MaxFilas`). Diferencias:
- `@page "/auditoria/bitacora-maestros"`, `@attribute [Authorize]`.
- Combo Entidad alimentado por `AuditableMaestros.All` (Value = `Tabla`, Text = `Nombre`).
- Combo Acción con `CREAR/EDITAR/ELIMINAR`.
- Columnas: Fecha (`g`), Usuario, Entidad (`EntidadNombre`), Acción (badge), Registro (`RegistroDesc ?? RegistroId`), IP.
- **Detalle de cambios:** botón por fila que abre un `DxPopup` mostrando `Cambios` (parsear el JSON con `System.Text.Json` a `[{campo,antes,despues}]` y renderizar tabla). Manejar `Cambios == null`.

**Step 1: Implementar la página** siguiendo esa referencia.

**Step 2: Verificación en navegador** (ver sección Verificación): iniciar `apc`, navegar a `/auditoria/bitacora-maestros`, hacer una alta en un maestro auditado, buscar y confirmar la fila + el popup de cambios.

---

### Task 15: Mantenimiento de config (`AuditoriaConfigList.razor`, solo SuperAdmin)

**Files:**
- Create: `apc.Client/Pages/Auditoria/AuditoriaConfigList.razor`

**Referencia:** grilla editable con switches (no popup de alta, porque las filas provienen del catálogo fijo). Estructura:
- `@page "/auditoria/configuracion"`, `@attribute [Authorize(Policy = "CanSuperAdmin")]` (usar la constante `AuthorizationPolicies.SuperAdmin` = `"CanSuperAdmin"`).
- Carga `AuditoriaConfigClient.GetAsync()` a una `List<AuditoriaConfigItemDto>`.
- `DxGrid` con columnas: Módulo, Nombre, y 4 columnas con `DxCheckBox`/switch bindeados a `Habilitado`, `AuditaCrear`, `AuditaEditar`, `AuditaEliminar` (usar `CellDisplayTemplate` con `@bind-Checked` sobre el item del contexto).
- Botón "Guardar" → `AuditoriaConfigClient.GuardarAsync(items)` + toast de éxito/error.

**Step 1: Implementar la página** siguiendo el estándar de grid (`siad-grid.css`, `grid-wrapper` + `grid-solicitudes`).

**Step 2: Verificación en navegador:** navegar, activar/desactivar auditoría de una entidad, guardar, y confirmar que un cambio posterior en esa entidad respeta la config (auditado / no auditado).

---

### Task 16: Menú lateral

**Files:**
- Modify: `apc.Client/Layout/Navigation/SidebarNavigationDefinition.cs`

**Step 1: Nueva sección "Auditoría"** (antes de "Configuración"), con el ítem de bitácora:

```csharp
new SidebarNavSection
{
    Label = "Auditoría",
    Items =
    [
        new SidebarNavItem
        {
            Id = "auditoria-bitacora-maestros",
            Text = "Bitácora de maestros",
            IconCssClass = "bi bi-clock-history",
            NavigateUrl = "/auditoria/bitacora-maestros",
            MatchPrefixes = ["/auditoria/bitacora-maestros"]
        }
    ]
},
```

**Step 2: Ítem de configuración en la sección "Parámetros"** (ya `RequiredPolicy = SuperAdmin`), junto a `param-roles`:

```csharp
new SidebarNavItem
{
    Id = "auditoria-config",
    Text = "Configuración de auditoría",
    IconCssClass = "bi bi-sliders",
    NavigateUrl = "/auditoria/configuracion",
    MatchPrefixes = ["/auditoria/configuracion"]
},
```

**Step 3: Build** `dotnet build apc.Client/apc.Client.csproj`. Expected: PASS.

---

## Fase 7 — Verificación final

### Task 17: Build, tests y smoke en navegador

**Step 1: Build de solución** — `dotnet build HODSOFT_DEVEXPRESS.sln`. Expected: 0 errores.

**Step 2: Tests** (con el DDL de Task 1 aplicado en la base de test):
`$env:SIAD_TEST_DB = '<conn>'; dotnet test SIAD.Tests/SIAD.Tests.csproj --filter "FullyQualifiedName~Auditoria"`. Expected: PASS (los unitarios de `AuditDiff` corren siempre; los de integración requieren `SIAD_TEST_DB`).

**Step 3: Smoke en navegador** (usar preview_start `apc`, ver `<preview_tools>`):
1. Configurar auditoría (SuperAdmin) → habilitar "Bodegas".
2. Crear/editar/desactivar una bodega.
3. Abrir `/auditoria/bitacora-maestros`, buscar, confirmar filas CREAR/EDITAR/ELIMINAR y el popup de diff.
4. Deshabilitar "Bodegas" en config, repetir un cambio y confirmar que **no** se audita.
5. Revisar `read_console_messages` / `preview_logs` sin errores.

**Step 4: Recordatorios al usuario**
- Aplicar `Database/2026-07-17_bitacora_maestros.sql` en el mirror y en prod (regla: no toco el server).
- Confirmar nombres de tabla reales del catálogo (Task 4, Step 2).

---

## AJUSTE 2026-07-17 — Auditoría de proveedores vía writer (post-hallazgo)

**Hallazgo:** un `SaveChangesInterceptor` solo captura entidades **keyed** que pasan por el `ChangeTracker`. `ProveedoresService` persiste el maestro `prv_proveedores` con **SQL crudo** (`ExecuteSqlRaw`) sobre una entidad **keyless** → el interceptor NO lo ve. (Clientes, artículos, catálogos de almacén y `prv_proveedor_cuenta_bancaria` sí pasan por `SaveChanges` y se auditan solos.) Decisión del usuario: **auditar el maestro de proveedores en la capa de servicio** con un writer compartido.

### Nueva pieza: `IBitacoraMaestrosWriter` + helper de construcción de fila

- Create: `SIAD.Services/Auditoria/IBitacoraMaestrosWriter.cs` + `BitacoraMaestrosWriter.cs`.
- Refactor menor: extraer la construcción de la entidad `bitacora_maestros` a un helper compartido por el interceptor y el writer (evitar duplicar el armado de fila: company/usuario/ip/fecha/serialización del diff). Ubicarlo donde ambos lo alcancen (p. ej. `SIAD.Data/Auditoria/BitacoraFilaFactory.cs` o un método del writer que el interceptor reutilice).

```csharp
// IBitacoraMaestrosWriter.cs
using SIAD.Data.Auditoria; // AuditDiff
namespace SIAD.Services.Auditoria;
public interface IBitacoraMaestrosWriter
{
    /// <summary>Registra una acción de auditoría respetando la config. Agrega la fila al
    /// DbContext y hace SaveChanges (para usar dentro de la transacción del servicio llamador).
    /// No-op si la config no audita esa (tabla, acción).</summary>
    Task RegistrarAsync(string tabla, string accion, string registroId, string? registroDesc,
                        IReadOnlyList<AuditDiff.Campo>? cambios, CancellationToken ct = default);
}
```

Implementación (`BitacoraMaestrosWriter`): inyecta `SiadDbContext`, `IAuditConfigProvider`, `ICurrentCompanyService`, `ICurrentUserAudit`. Chequea `_config.DebeAuditar(companyId, tabla, accion)`; si procede, `_context.bitacora_maestros.Add(fila)` + `await _context.SaveChangesAsync(ct)` (ese SaveChanges pasa por el interceptor pero `bitacora_maestros` está excluida → sin recursión).

### Nueva Task: auditar el maestro de proveedores en `ProveedoresService`

**Files:** Modify `SIAD.Services/Proveedores/ProveedoresService.cs` (inyectar `IBitacoraMaestrosWriter`).

- `CreateAsync` (tras el INSERT crudo del proveedor, dentro de la `tx`): armar `cambios` con los valores nuevos del DTO y `await _writer.RegistrarAsync("prv_proveedores", "CREAR", codigo, dto.Nombre, campos, ct)`. Ubicar la llamada inmediatamente después del INSERT del proveedor (antes de los syncs de bancos) para no mezclar.
- `UpdateAsync`: **cargar el proveedor previo** (`prv_proveedores.AsNoTracking()` por `cod_proveedor`) antes del UPDATE para armar el diff campo-a-campo (comparar DTO vs previo). Tras el UPDATE: `RegistrarAsync("prv_proveedores", "EDITAR", codigo, dto.Nombre, camposCambiados, ct)`.
- `DeleteAsync`: tras el DELETE crudo: `RegistrarAsync("prv_proveedores", "ELIMINAR", codigo, null, null, ct)`.
- Todo dentro de la transacción existente del método, para que la fila de bitácora sea atómica con la operación.

**Test (integración):** `SIAD.Tests/Auditoria/ProveedorAuditTests.cs` — crear/editar/eliminar un proveedor vía `ProveedoresService` genera las filas CREAR/EDITAR/ELIMINAR en `bitacora_maestros` con `entidad="prv_proveedores"`, `registro_id=codigo`, y (en EDITAR) diff solo de campos cambiados. Requiere `SIAD_TEST_DB` con el DDL.

### Registro DI (agregar a la Task 8)
`services.AddScoped<IBitacoraMaestrosWriter, BitacoraMaestrosWriter>();` en `ServiceRegistration.cs`.

## AJUSTE 2 (2026-07-17) — Esquema real de `bitacora_maestros` reutilizado

Al aplicar el DDL se descubrió que `public.bitacora_maestros` **ya existía** (WIP de Emilio) con su propio esquema y 3 filas reales. Decisión del usuario: **reutilizar ese esquema**. Se descarta el esquema del plan original (`id`/`cambios`/`ip`). Esquema real (mapear la entidad a esto):

| columna | tipo | nota |
|---|---|---|
| `bitacora_maestro_id` | bigint PK | IDENTITY BY DEFAULT (la BD genera el id) |
| `company_id` | bigint (nullable) | `ICompanyScopedEntity`; se estampa en insert |
| `modulo` | varchar(80) NOT NULL | nombre amigable ("Clientes","Almacen","Proveedores") = `AuditableMaestros.Modulo` |
| `tabla` | varchar(128) NOT NULL | nombre técnico = `entry.Metadata.GetTableName()` |
| `entidad` | varchar(256) NOT NULL | descriptor del registro (código-nombre; fallback FullName del tipo) |
| `registro_id` | varchar(500) nullable | PK del registro |
| `accion` | varchar(30) NOT NULL | **"Creacion" / "Actualizacion" / "Eliminacion"** |
| `descripcion` | varchar(500) NOT NULL | texto legible |
| `valores_anteriores` | jsonb nullable | objeto `{campo: valor_anterior}` |
| `valores_nuevos` | jsonb nullable | objeto `{campo: valor_nuevo}` |
| `fecha` | timestamp (without tz) NOT NULL | |
| `usuario` | varchar(256) NOT NULL | |

Convenciones (de las 3 filas de Emilio): acciones en español; **baja lógica `activo=false` = "Eliminacion"**; CREAR → `valores_anteriores=null`, `valores_nuevos={todos los campos}`; EDITAR → ambos = solo campos cambiados; ELIMINAR → `valores_anteriores={todos}`, `valores_nuevos=null`.

Cambios respecto al plan original:
- `AuditDiff` expone `SerializeAnteriores`/`SerializeNuevos` (dos objetos `{campo:valor}`), no un array.
- Constantes `AccionesBitacora` (Creacion/Actualizacion/Eliminacion) en `SIAD.Core/Constants`.
- `AuditConfigProvider.DebeAuditar` mapea esas acciones a `audita_crear/editar/eliminar`.
- DTOs de consulta y `BitacoraMaestrosService` mapean las columnas reales (modulo/tabla/entidad/descripcion/valores_*).
- El interceptor y el writer llenan las 12 columnas reales.
- `bitacora_maestro_config` (tabla nueva) se mantiene con el esquema del plan; su columna `entidad` guarda el **nombre técnico de tabla** (clave contra `AuditableMaestros.Tabla`).

## Notas de riesgo (recordatorio)
- El `company_id` de la bitácora se copia de la entidad auditada, ya estampada por `SiadDbContext.Tenancy.ApplyCompanyInformation` (corre antes del interceptor). SuperAdmin sin empresa ⇒ `company_id=0`.
- Anti-recursión: `bitacora_maestros`/`bitacora_maestro_config` excluidas + flag `_reentrada`.
- Altas con PK identity: se completa `registro_id` en `SavedChanges` (patrón dos fases).
- No auditar transaccionales: garantizado por la doble barrera catálogo `AuditableMaestros` + config habilitada.
- Rendimiento: config cacheada por empresa (`IMemoryCache`, invalidada al guardar).
```
