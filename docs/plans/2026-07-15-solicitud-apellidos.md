# Campo Apellidos en solicitudes — Plan de implementación

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Capturar los apellidos del cliente en las solicitudes de servicio (columna nueva `cliente_apellidos`) y precargarlos correctamente al crear el cliente desde la solicitud.

**Architecture:** Slice estándar del repo: script SQL aditivo en `Database/` → entidad scaffolded → DTOs → perfil AutoMapper → formularios Blazor (alta y edición) → prefill en `ClienteCreatePage`. La columna es NULL en BD (histórico sin apellidos); la obligatoriedad se exige solo en la validación de UI. Diseño aprobado en `docs/plans/2026-07-15-solicitud-apellidos-design.md`.

**Tech Stack:** .NET 9, EF Core/Npgsql, AutoMapper, Blazor WASM + DevExpress 25.1.7, xUnit + Dapper (SIAD.Tests).

---

### Task 1: Script SQL y aplicación al mirror local

**Files:**
- Create: `Database/2026-07-15_solicitud_servicio_cliente_apellidos.sql`

**Step 1: Invocar la skill `guardia-estructura-bd`** (obligatoria antes de crear/aplicar DDL). Es un cambio aditivo (tarjeta verde).

**Step 2: Crear el script** con este contenido (ajustar al estilo de los scripts recientes de `Database/` si difiere):

```sql
-- 2026-07-15  Agrega apellidos del cliente a solicitud_servicio.
-- Aditivo: columna NULL; las solicitudes históricas quedan sin apellidos separados.
ALTER TABLE public.solicitud_servicio
    ADD COLUMN IF NOT EXISTS cliente_apellidos varchar NULL;

COMMENT ON COLUMN public.solicitud_servicio.cliente_apellidos
    IS 'Apellidos del cliente solicitante; cliente_nombre pasa a contener solo los nombres para registros nuevos.';
```

**Step 3: Aplicarlo SOLO al mirror local `siad_v3_restore`** usando la skill `psql-runner`. **NUNCA a producción: la aplica Emilio.** Recordárselo en el resumen final.

**Step 4: Verificar** con `\d solicitud_servicio` (o `information_schema.columns`) que la columna existe en el mirror.

---

### Task 2: Test de integración (escribirlo primero — debe fallar)

**Files:**
- Create: `SIAD.Tests/SolicitudApellidosTests.cs`

**Step 1: Escribir el test.** Modelarlo sobre `SIAD.Tests/ClienteDesdeSolicitudTests.cs` (misma infraestructura `PostgresFixture`/`IntegrationTestBase`, transacción con rollback). El servicio necesita `IMapper`:

```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.DTOs.Solicitudes;
using SIAD.Data;
using SIAD.Services.Solicitudes;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

[Collection("Postgres")]
public class SolicitudApellidosTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private SolicitudesService? _service;

    public SolicitudApellidosTests(PostgresFixture fixture) : base(fixture)
    {
    }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();

        if (Fixture.Available)
        {
            var options = new DbContextOptionsBuilder<SiadDbContext>()
                .UseNpgsql(Connection)
                .Options;

            var mockCompanyService = new TestCurrentCompanyService(CompanyId);
            _context = new SiadDbContext(options, mockCompanyService);
            _context.Database.UseTransaction(Transaction);

            var mapper = new MapperConfiguration(cfg => cfg.AddProfile<SolicitudMappings>())
                .CreateMapper();
            _service = new SolicitudesService(_context, mapper);
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    [SkippableFact]
    public async Task CrearSolicitud_ConApellidos_PersisteYMapea()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var categoriaId = await Connection.ExecuteScalarAsync<int>(
            "SELECT categoria_servicio_id FROM categoria_servicio WHERE estado LIMIT 1",
            transaction: Transaction);

        var dto = new SolicitudCreateDto
        {
            IdentificacionCliente = "0801199912345",
            NombreCliente = "Juan Carlos",
            ApellidosCliente = "García López",
            CategoriaServicioId = categoriaId,
            Telefono = "50422223333",
            Movil = "50499887777",
            Direccion = "Col. Test, casa 1"
        };

        var id = await _service!.CreateSolicitudAsync(dto, "test-user");

        var apellidos = await Connection.ExecuteScalarAsync<string?>(
            "SELECT cliente_apellidos FROM solicitud_servicio WHERE solicitud_servicio_id = @id",
            new { id }, Transaction);
        Assert.Equal("García López", apellidos);

        var detalle = await _service.GetSolicitudAsync(id);
        Assert.NotNull(detalle);
        Assert.Equal("Juan Carlos", detalle!.NombreCliente);
        Assert.Equal("García López", detalle.ApellidosCliente);

        var lista = await _service.GetSolicitudesAsync("0801199912345");
        var item = Assert.Single(lista);
        Assert.Equal("Juan Carlos García López", item.NombreCliente);
    }
}
```

Nota: verificar el nombre exacto de la columna de identidad/categoría en el mirror antes de dar por bueno el SQL del test (`categoria_servicio.estado` existe según `SolicitudesService.GetCategoriasAsync`).

**Step 2: Compilar y correr — debe fallar** (error de compilación: `ApellidosCliente` no existe):

```powershell
dotnet build SIAD.Tests/SIAD.Tests.csproj
```

Esperado: FAIL con CS1061 (`SolicitudCreateDto` no contiene `ApellidosCliente`).

---

### Task 3: Entidad, DTOs y mapeos (hacer pasar el test)

**Files:**
- Modify: `SIAD.Core/Entities/solicitud_servicio.cs` (después de `cliente_nombre`, línea ~16)
- Modify: `SIAD.Core/DTOs/Solicitudes/SolicitudCreateDto.cs`
- Modify: `SIAD.Core/DTOs/Solicitudes/SolicitudUpdateDto.cs`
- Modify: `SIAD.Core/DTOs/Solicitudes/SolicitudDetail.cs`
- Modify: `SIAD.Services/Solicitudes/SolicitudMappings.cs`

**Step 1: Entidad** — agregar tras `cliente_nombre`:

```csharp
    public string? cliente_apellidos { get; set; }
```

(No hace falta tocar `SiadDbContext.cs`: la columna es varchar sin longitud, igual que `cliente_nombre`, que no tiene configuración explícita.)

**Step 2: DTOs.**

`SolicitudCreateDto.cs` y `SolicitudUpdateDto.cs`, tras `NombreCliente`:

```csharp
    public string ApellidosCliente { get; set; } = "";
```

`SolicitudDetail.cs` (clase `SolicitudDetailDto`), tras `NombreCliente`:

```csharp
    public string? ApellidosCliente { get; set; }

    /// <summary>Nombres + apellidos para despliegue.</summary>
    public string NombreCompleto => string.IsNullOrWhiteSpace(ApellidosCliente)
        ? NombreCliente
        : $"{NombreCliente} {ApellidosCliente}";
```

**Step 3: Mapeos** en `SolicitudMappings.cs`:

- `solicitud_servicio → SolicitudDetailDto`: agregar
  `.ForMember(d => d.ApellidosCliente, o => o.MapFrom(s => s.cliente_apellidos))`
- `solicitud_servicio → SolicitudListDto`: **reemplazar** el MapFrom de `NombreCliente` por
  `.ForMember(d => d.NombreCliente, o => o.MapFrom(s => string.IsNullOrWhiteSpace(s.cliente_apellidos) ? s.cliente_nombre : s.cliente_nombre + " " + s.cliente_apellidos))`
- `SolicitudCreateDto → solicitud_servicio` y `SolicitudUpdateDto → solicitud_servicio`: agregar
  `.ForMember(d => d.cliente_apellidos, o => o.MapFrom(s => s.ApellidosCliente))`

**Step 4: Correr el test — debe pasar:**

```powershell
$env:SIAD_TEST_DB = '<cadena del mirror>'
dotnet test SIAD.Tests/SIAD.Tests.csproj --filter "FullyQualifiedName~SolicitudApellidosTests"
```

Esperado: PASS (o `Skipped` si no hay `SIAD_TEST_DB`; en ese caso conseguir la cadena del mirror antes de continuar).

---

### Task 4: Formularios de solicitud (alta y edición)

**Files:**
- Modify: `apc.Client/Pages/Solicitudes/Components/SolicitudForm.razor:25-27` y `:171-189`
- Modify: `apc.Client/Pages/Solicitudes/Components/SolicitudFormEdicion.razor:13-15` y `:75-93`

**Step 1: `SolicitudForm.razor`** — reemplazar el item "Nombre Completo *" por dos items:

```razor
                        <DxFormLayoutItem Caption="Nombres *" ColSpanXs="12" ColSpanMd="6">
                            <DxTextBox @bind-Text="@Solicitud.NombreCliente" />
                        </DxFormLayoutItem>

                        <DxFormLayoutItem Caption="Apellidos *" ColSpanXs="12" ColSpanMd="6">
                            <DxTextBox @bind-Text="@Solicitud.ApellidosCliente" />
                        </DxFormLayoutItem>
```

Ojo: "Categoría *" queda en la siguiente fila; el grupo usa ColSpanMd=6, el layout fluye solo.

En `ValidarFormulario()` agregar la condición junto a `NombreCliente`:

```csharp
            string.IsNullOrWhiteSpace(Solicitud.NombreCliente) ||
            string.IsNullOrWhiteSpace(Solicitud.ApellidosCliente) ||
```

**Step 2: `SolicitudFormEdicion.razor`** — mismo cambio de caption/campo nuevo y misma condición en su `ValidarFormulario()`.

**Step 3: Compilar:** `dotnet build apc.Client/apc.Client.csproj` → sin errores.

---

### Task 5: SolicitudesIndex (alta, edición y ficha de detalle)

**Files:**
- Modify: `apc.Client/Pages/Solicitudes/SolicitudesIndex.razor:166` (hero del detalle), `:387` (nuevo), `:400-421` (edición)

**Step 1: Ficha de detalle** — línea 166, mostrar nombre completo:

```razor
                            <div class="detail-name">@solicitudActual.NombreCompleto</div>
```

**Step 2: `AbrirNuevaSolicitudAsync`** — inicializar el campo:

```csharp
        formularioNuevo = new SolicitudCreateDto { IdentificacionCliente = "", NombreCliente = "", ApellidosCliente = "", Telefono = "", Movil = "", Direccion = "" };
```

**Step 3: `AbrirEdicionAsync`** — en el objeto `SolicitudUpdateDto`, tras `NombreCliente`:

```csharp
                    ApellidosCliente = detalle.ApellidosCliente ?? "",
```

**Step 4:** No tocar la grilla (usa `SolicitudListDto.NombreCliente`, que ya llega concatenado) ni `SolicitudesIndex.razor.txt` (residuo, fuera de la solución).

**Step 5: Compilar:** `dotnet build apc.Client/apc.Client.csproj` → sin errores.

---

### Task 6: Prefill al crear cliente desde solicitud

**Files:**
- Modify: `apc.Client/Pages/Clientes/ClienteCreatePage.razor:45` y `:171`

**Step 1:** En `CargarDesdeSolicitudAsync`, línea 171:

```csharp
            m.Nombre = s.NombreCliente;
            m.Apellidos = NullIfEmpty(s.ApellidosCliente);
```

(Para solicitudes históricas `ApellidosCliente` viene null → `Apellidos` queda null y todo el texto sigue en `Nombre`, como hoy.)

**Step 2:** En el aviso (línea 45) usar el nombre completo:

```razor
                (@_solicitudOrigen.NombreCompleto). Complete los campos faltantes:
```

**Step 3: Compilar:** `dotnet build apc.Client/apc.Client.csproj` → sin errores.

---

### Task 7: Verificación final

**Step 1: Build de la solución completa:**

```powershell
dotnet build HODSOFT_DEVEXPRESS.sln
```

Esperado: 0 errores.

**Step 2: Suite de tests completa** (con `SIAD_TEST_DB` apuntando al mirror):

```powershell
dotnet test SIAD.Tests/SIAD.Tests.csproj
```

Esperado: todo verde (los tests existentes no se ven afectados: la columna nueva es NULL y ningún insert previo la menciona).

**Step 3: Verificación funcional en el navegador** (skill `verify` / preview): crear una solicitud con nombres y apellidos, verla en la grilla y la ficha, editar, y "Crear cliente" desde ella comprobando que Nombres/Apellidos llegan separados al formulario de cliente.

**Step 4: Recordatorio al usuario:** el script `Database/2026-07-15_solicitud_servicio_cliente_apellidos.sql` está aplicado solo en el mirror; **en producción lo aplica Emilio antes de desplegar**. Sin commit automático.
