# Checkbox "Generar partida contable al crear" — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Que al crear un compromiso de proveedor un checkbox decida si se genera la partida contable de creación (GEN); si no se genera, poder generarla después manualmente o automáticamente al procesar.

**Architecture:** Se agrega un flag `GenerarPartida` (default `true`) al DTO de upsert. `CreateAsync` solo genera la GEN cuando el flag es `true`. Un nuevo método/endpoint `generar-partida` genera la GEN sobre un compromiso existente sin partida, reconstruyendo el `PreparedOrder` desde lo almacenado y reutilizando `BuildCreatePartidaLineasAsync` + `RegisterPartidaContableAsync`. `MarkAsProcessedAsync` genera la GEN si falta antes de la PRC. La UI muestra el checkbox (solo al crear) y un botón "Generar partida contable" (en edición sin partida).

**Tech Stack:** .NET 9, ASP.NET Core, Blazor WebAssembly, DevExpress.Blazor 25.1.7, EF Core 9 + Npgsql, xUnit (integration tests con `SIAD_TEST_DB`).

**Diseño de referencia:** `docs/plans/2026-06-30-compromiso-proveedor-checkbox-partida-design.md`

**Notas clave del código existente (verificadas):**
- `RegisterPartidaContableAsync` **ignora** el `partidaNumber` que recibe (el `poliza_number` real lo genera `GenerateMonthlyPartidaNumberAsync`). Por eso NO se puede distinguir GEN/PRC por `poliza_number`. La detección de "ya hay partida" se hace con el helper existente `HasPartidaContableRegistradaAsync(numeroOrden, ct)` (envuelve `LoadPartidaContableAsync`). Antes de procesar, la única partida posible es la GEN, así que ese helper es suficiente.
- `CreateAsync` siempre llama a `ApplyCompromisoPresupuestoAsync` (afectación presupuestaria) e inserta hdr/dtl: eso NO cambia.
- El bloque de generación de la GEN en `CreateAsync` está en `SIAD.Services/Presupuesto/OrdenesPagoDirectoService.cs` líneas ~273-286.

---

### Task 1: Agregar flag `GenerarPartida` al DTO

**Files:**
- Modify: `SIAD.Core/DTOs/Presupuesto/OrdenesPagoDirectoDtos.cs:149` (dentro de `OrdenPagoDirectoUpsertDto`, después de `PagarA`, antes de `Detalles`)

**Step 1: Agregar la propiedad**

En `OrdenPagoDirectoUpsertDto`, antes de `public List<OrdenPagoDirectoUpsertLineaDto> Detalles { get; set; } = new();`:

```csharp
    /// <summary>
    /// Cuando es true (default) se genera la partida contable GEN al crear el compromiso.
    /// Cuando es false el compromiso se crea sin partida, para generarla despues.
    /// </summary>
    public bool GenerarPartida { get; set; } = true;
```

**Step 2: Compilar**

Run: `dotnet build SIAD.Core/SIAD.Core.csproj`
Expected: Build succeeded.

---

### Task 2: Condicionar la generación de la GEN en `CreateAsync`

**Files:**
- Modify: `SIAD.Services/Presupuesto/OrdenesPagoDirectoService.cs:273-286`

**Step 1: Envolver el bloque de la partida GEN en `if (dto.GenerarPartida)`**

Reemplazar el bloque actual (desde `var lineasContables = await BuildCreatePartidaLineasAsync(preparedOrder, ct);` hasta la llamada a `RegisterPartidaContableAsync(...)`) por:

```csharp
        if (dto.GenerarPartida)
        {
            var lineasContables = await BuildCreatePartidaLineasAsync(preparedOrder, ct);
            var connection = (NpgsqlConnection)_context.Database.GetDbConnection();
            var transaction = _context.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction
                ?? throw new InvalidOperationException("No se pudo obtener la transaccion activa para registrar la partida contable.");
            await RegisterPartidaContableAsync(
                connection,
                transaction,
                BuildDocumentNumber(numeroOrden),
                BuildPartidaNumber(numeroOrden, "GEN"),
                preparedOrder.FechaCompromiso,
                preparedOrder.Concepto,
                usuarioActual,
                lineasContables,
                ct);
        }
```

**Step 2: Compilar**

Run: `dotnet build SIAD.Services/SIAD.Services.csproj`
Expected: Build succeeded. (Si `usuarioActual` queda sin uso cuando `GenerarPartida=false` no importa: sigue declarado arriba y se usa dentro del `if`.)

---

### Task 3: Helper para construir las líneas GEN desde un compromiso almacenado

**Files:**
- Modify: `SIAD.Services/Presupuesto/OrdenesPagoDirectoService.cs` (agregar método privado, junto a `BuildCreatePartidaLineasAsync`, ~línea 729)

**Step 1: Agregar método que reconstruye el `OrdenPagoDirectoUpsertDto` y arma las líneas**

`GetByNumeroOrdenAsync` ya carga hdr+dtl en `OrdenPagoDirectoDetalleDto`. Reutilizarlo para reconstruir el DTO de upsert y pasar por `PrepareOrderAsync` + `BuildCreatePartidaLineasAsync`:

```csharp
    private async Task<(List<PartidaLineaOrden> Lineas, DateTime FechaCompromiso, string Concepto)>
        BuildCreatePartidaFromStoredAsync(int numeroOrden, CancellationToken ct)
    {
        var detalle = await GetByNumeroOrdenAsync(numeroOrden, ct)
            ?? throw new ArgumentException($"No se encontro el compromiso {numeroOrden}.", nameof(numeroOrden));

        var upsert = new OrdenPagoDirectoUpsertDto
        {
            FechaCompromiso = detalle.FechaCompromiso ?? DateTime.Today,
            CodigoProveedor = detalle.CodigoProveedor,
            Rtn = detalle.Rtn,
            Concepto = detalle.Concepto,
            CuentaContable = detalle.CuentaContable ?? string.Empty,
            PagarA = detalle.PagarA,
            Detalles = detalle.Detalles
                .Select(x => new OrdenPagoDirectoUpsertLineaDto
                {
                    CodigoPresupuestario = string.IsNullOrWhiteSpace(x.CodigoPresupuestario) ? "0" : x.CodigoPresupuestario!,
                    Descripcion = x.Descripcion ?? detalle.Concepto,
                    ConceptoDetalle = x.ConceptoDetalle,
                    Monto = x.Monto,
                    Programa = x.Programa,
                    Actividad = x.Actividad,
                    ObjetoGasto = x.ObjetoGasto,
                    CuentaContable = x.CuentaContable
                })
                .ToList()
        };

        var preparedOrder = await PrepareOrderAsync(upsert, ct);
        var lineas = await BuildCreatePartidaLineasAsync(preparedOrder, ct);
        return (lineas, preparedOrder.FechaCompromiso, preparedOrder.Concepto);
    }
```

**Step 2: Compilar**

Run: `dotnet build SIAD.Services/SIAD.Services.csproj`
Expected: Build succeeded.

---

### Task 4: Método público `GenerarPartidaCreacionAsync` + interfaz

**Files:**
- Modify: `SIAD.Services/Presupuesto/IOrdenesPagoDirectoService.cs` (agregar firma)
- Modify: `SIAD.Services/Presupuesto/OrdenesPagoDirectoService.cs` (agregar implementación, junto a `CreateAsync`)

**Step 1: Agregar la firma en la interfaz** (después de `CreateAsync`, ~línea 31):

```csharp
    Task<OrdenPagoDirectoOperacionResultadoDto> GenerarPartidaCreacionAsync(
        int numeroOrden,
        CancellationToken ct = default);
```

**Step 2: Implementar el método en el servicio:**

```csharp
    public async Task<OrdenPagoDirectoOperacionResultadoDto> GenerarPartidaCreacionAsync(
        int numeroOrden,
        CancellationToken ct = default)
    {
        if (numeroOrden <= 0)
            throw new ArgumentException("El numero de orden no es valido.", nameof(numeroOrden));

        var orden = await _context.prv_compromiso_hdrs
            .AsNoTracking()
            .Where(x => x.numero_orden == numeroOrden)
            .Select(x => new { x.numero_orden, x.status_transacc, x.anulado })
            .FirstOrDefaultAsync(ct);

        if (orden is null)
            return new OrdenPagoDirectoOperacionResultadoDto { Success = false, NumeroOrden = numeroOrden, Message = "El compromiso no existe." };

        if (orden.status_transacc == true)
            throw new InvalidOperationException("El compromiso ya fue procesado.");

        if (orden.anulado)
            throw new InvalidOperationException("El compromiso esta anulado.");

        if (await HasPartidaContableRegistradaAsync(numeroOrden, ct))
            throw new InvalidOperationException("El compromiso ya tiene una partida contable registrada.");

        var (lineas, fechaCompromiso, concepto) = await BuildCreatePartidaFromStoredAsync(numeroOrden, ct);
        var usuarioActual = GetCurrentUser();

        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var connection = (NpgsqlConnection)_context.Database.GetDbConnection();
        var transaction = _context.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction
            ?? throw new InvalidOperationException("No se pudo obtener la transaccion activa para registrar la partida contable.");

        await RegisterPartidaContableAsync(
            connection,
            transaction,
            BuildDocumentNumber(numeroOrden),
            BuildPartidaNumber(numeroOrden, "GEN"),
            fechaCompromiso,
            concepto,
            usuarioActual,
            lineas,
            ct);

        await tx.CommitAsync(ct);

        return new OrdenPagoDirectoOperacionResultadoDto
        {
            Success = true,
            NumeroOrden = numeroOrden,
            Message = "Se genero la partida contable del compromiso."
        };
    }
```

**Step 3: Compilar**

Run: `dotnet build SIAD.Services/SIAD.Services.csproj`
Expected: Build succeeded.

---

### Task 5: Auto-generar la GEN al procesar si falta

**Files:**
- Modify: `SIAD.Services/Presupuesto/OrdenesPagoDirectoService.cs` (dentro de `MarkAsProcessedAsync`, en el bloque de la transacción ~líneas 502-544)

**Step 1: Determinar si falta la GEN antes de abrir la transacción de procesamiento**

Después de la validación inicial (tras `if (orden.status_transacc == true) ...`, ~línea 443) y antes de armar las líneas contra, agregar:

```csharp
        var faltaPartidaCreacion = !await HasPartidaContableRegistradaAsync(numeroOrden, ct);
        List<PartidaLineaOrden>? lineasCreacion = null;
        if (faltaPartidaCreacion)
        {
            (lineasCreacion, _, _) = await BuildCreatePartidaFromStoredAsync(numeroOrden, ct);
        }
```

**Step 2: Registrar la GEN dentro de la misma `dbTransaction`, antes de la PRC**

Dentro del `try` (después de `await using var dbTransaction = ...` y antes de `var partidaId = await RegisterPartidaContableAsync(... "PRC" ...)`), agregar:

```csharp
            if (faltaPartidaCreacion && lineasCreacion is not null)
            {
                await RegisterPartidaContableAsync(
                    connection,
                    dbTransaction,
                    BuildDocumentNumber(numeroOrden),
                    BuildPartidaNumber(numeroOrden, "GEN"),
                    fechaOrden,
                    descripcion,
                    usuarioProceso,
                    lineasCreacion,
                    ct);
            }
```

**Step 3: Compilar**

Run: `dotnet build SIAD.Services/SIAD.Services.csproj`
Expected: Build succeeded.

---

### Task 6: Endpoint `POST {numeroOrden}/generar-partida`

**Files:**
- Modify: `apc/Controllers/Presupuesto/OrdenesPagoDirectoController.cs` (agregar acción, después de `ProcessOrder`, ~línea 179)

**Step 1: Agregar la acción**

```csharp
    [HttpPost("{numeroOrden:int}/generar-partida")]
    public async Task<IActionResult> GenerarPartida(int numeroOrden, CancellationToken ct)
    {
        if (numeroOrden <= 0)
            return BadRequest(new { message = "El numero de orden no es valido." });

        try
        {
            var result = await _service.GenerarPartidaCreacionAsync(numeroOrden, ct);
            return result.Success ? Ok(result) : Conflict(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
```

**Step 2: Compilar**

Run: `dotnet build apc/apc.csproj`
Expected: Build succeeded.

---

### Task 7: Cliente HTTP `GenerarPartidaAsync`

**Files:**
- Modify: `apc.Client/Services/Presupuesto/OrdenesPagoDirectoClient.cs` (después de `ProcesarAsync`, ~línea 169)

**Step 1: Agregar el método**

```csharp
    public async Task<OrdenPagoDirectoOperacionResultadoDto> GenerarPartidaAsync(
        int numeroOrden,
        CancellationToken ct = default)
    {
        if (numeroOrden <= 0)
            throw new ArgumentException("El numero de orden no es valido.", nameof(numeroOrden));

        var response = await _http.PostAsJsonAsync(
            $"api/presupuesto/ordenes-pago-directo/{numeroOrden}/generar-partida",
            new { },
            ct);
        return await ReadOperationResultAsync(response, ct);
    }
```

> Nota: verifica que `ReadOperationResultAsync` exista y sea el patrón usado por `CrearAsync` (lo es). Si requiere un cuerpo no-vacío, `new { }` sirve como payload mínimo.

**Step 2: Compilar**

Run: `dotnet build apc.Client/apc.Client.csproj`
Expected: Build succeeded.

---

### Task 8: UI — checkbox al crear y botón "Generar partida" en edición

**Files:**
- Modify: `apc.Client/Pages/Proveedores/CompromisoProveedorEdit.razor`

**Step 1: Inyectar nada nuevo** — `model.GenerarPartida` ya existe vía el DTO. `OrdenesPagoDirectoClient` ya está inyectado.

**Step 2: Agregar el checkbox y el botón en la tarjeta "Vista previa de partida contable"**

En el bloque `@if (!ordenProcesada && !TienePartidaContableRegistrada)` (línea ~340), dentro del `<div class="modern-card mt-3">`, justo después del `<div class="d-flex justify-content-between ...">` de cabecera (después del cierre de los `summary-pill`, ~línea 354), insertar:

```razor
                        @if (!IsEdit)
                        {
                            <div class="mb-3">
                                <DxCheckBox @bind-Checked="model.GenerarPartida">
                                    Generar partida contable al crear el compromiso
                                </DxCheckBox>
                                <small class="text-muted d-block">
                                    Si lo desmarca, el compromiso se crea sin partida y podra generarla despues.
                                </small>
                            </div>
                        }
                        else
                        {
                            <div class="mb-3 d-flex justify-content-end">
                                <DxButton Text="Generar partida contable"
                                          IconCssClass="bi bi-journal-plus"
                                          RenderStyle="ButtonRenderStyle.Success"
                                          CssClass="btn-modern shadow-sm"
                                          Click="GenerarPartidaAsync"
                                          Enabled="@(!isSaving && PuedeGenerarPartidaContable())" />
                            </div>
                        }
```

**Step 3: Agregar el handler `GenerarPartidaAsync` en el bloque `@code`** (junto a `GuardarAsync`):

```csharp
    private async Task GenerarPartidaAsync()
    {
        if (!IsEdit || ordenProcesada || TienePartidaContableRegistrada)
        {
            return;
        }

        if (!PuedeGenerarPartidaContable())
        {
            ShowToast("Validacion", "La partida contable no esta completa o no cuadra.", ToastRenderStyle.Warning);
            return;
        }

        try
        {
            isSaving = true;
            errorMessage = null;
            var result = await OrdenesPagoDirectoClient.GenerarPartidaAsync(NumeroOrden);
            ShowToast("Exito", result.Message, ToastRenderStyle.Success);
            await CargarAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error al generar la partida del compromiso {NumeroOrden}", NumeroOrden);
            errorNotice.ShowError("No se pudo generar la partida contable.", ex);
        }
        finally
        {
            isSaving = false;
        }
    }
```

**Step 4: Relajar las validaciones de partida al crear cuando el checkbox esta desmarcado** — en `ValidarDetalle()`:

Reemplazar el bloque de "cuenta de servicio o de gasto" (líneas ~929-934):

```csharp
        if (!TieneCuentaServicio() && !model.Detalles.Any(TieneCuentaGasto))
        {
            errorMessage = "Para emitir el compromiso, se debe elegir una cuenta de servicio o de gasto.";
            ShowToast("Validacion", errorMessage, ToastRenderStyle.Warning);
            return false;
        }
```

por:

```csharp
        if (!IsEdit && model.GenerarPartida && !TieneCuentaServicio() && !model.Detalles.Any(TieneCuentaGasto))
        {
            errorMessage = "Para generar la partida del compromiso, se debe elegir una cuenta de servicio o de gasto.";
            ShowToast("Validacion", errorMessage, ToastRenderStyle.Warning);
            return false;
        }
```

Y reemplazar el bloque `PuedeGenerarPartidaContable` (líneas ~936-941):

```csharp
        if (!IsEdit && !PuedeGenerarPartidaContable())
        {
```

por:

```csharp
        if (!IsEdit && model.GenerarPartida && !PuedeGenerarPartidaContable())
        {
```

**Step 5: Compilar el cliente**

Run: `dotnet build apc.Client/apc.Client.csproj`
Expected: Build succeeded.

> **DevExpress:** Antes de dar por buena la API de `DxCheckBox` (`@bind-Checked`), confírmala con el skill `hodsoft-devexpress-docs` / MCP `dxdocs` si hay duda. Es la API estándar en 25.1.7.

---

### Task 9: Build completo de la solución

**Step 1: Compilar todo**

Run: `dotnet build HODSOFT_DEVEXPRESS.sln`
Expected: Build succeeded, 0 errores.

---

### Task 10: Pruebas de integración (SIAD.Tests)

> Estas son pruebas de integración contra Postgres real (patrón `IntegrationTestBase` + `[SkippableFact]`, como `AccionDocumentoTests.cs`). Requieren `SIAD_TEST_DB` y seed contable/presupuestario (periodo abierto, diario, plan de cuentas, `sp_registrar_partida_contable`, proveedor). Si el seed completo no está disponible, prioriza el caso `GenerarPartida=false` (no toca `sp_registrar_partida_contable`).

**Files:**
- Create: `SIAD.Tests/OrdenPagoDirectoPartidaTests.cs`

**Step 1: Escribir el esqueleto del test que falla**

Modelar sobre `AccionDocumentoTests.cs`: construir `OrdenesPagoDirectoService` con `SiadDbContext` ligado a la transacción del test, `TestCurrentCompanyService`, un `IProveedoresService` (real o fake mínimo) y un `IHttpContextAccessor` (`new HttpContextAccessor()`). Sembrar proveedor + presupuesto mínimos.

Caso 1 — crear sin partida:

```csharp
[SkippableFact]
public async Task Crear_SinGenerarPartida_NoRegistraPartida()
{
    Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
    await SembrarMinimoAsync();

    var dto = NuevoCompromisoDto();
    dto.GenerarPartida = false;

    var result = await _service!.CreateAsync(dto);
    Assert.True(result.Success);

    var hayPartida = await Connection.ExecuteScalarAsync<bool>(
        @"SELECT EXISTS(SELECT 1 FROM con_partida_hdr
                        WHERE company_id=@c AND module='PROV' AND document_type='OPD'
                          AND document_number=@d)",
        new { c = CompanyId, d = $"OPD-{result.NumeroOrden}" }, Transaction);
    Assert.False(hayPartida);
}
```

Caso 2 — generar partida después; segunda llamada rechazada:

```csharp
[SkippableFact]
public async Task GenerarPartida_DosVeces_SegundaRechazada()
{
    Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");
    await SembrarMinimoAsync();

    var dto = NuevoCompromisoDto();
    dto.GenerarPartida = false;
    var creado = await _service!.CreateAsync(dto);

    var gen = await _service.GenerarPartidaCreacionAsync(creado.NumeroOrden);
    Assert.True(gen.Success);

    await Assert.ThrowsAsync<InvalidOperationException>(
        () => _service.GenerarPartidaCreacionAsync(creado.NumeroOrden));
}
```

(El helper `NuevoCompromisoDto()` arma un compromiso con una línea con cuenta de gasto válida del seed; `SembrarMinimoAsync()` inserta proveedor + cuentas + periodo/diario.)

**Step 2: Ejecutar para ver fallar** (antes de que el seed/servicio estén listos)

Run: `$env:SIAD_TEST_DB = '<conn>'; dotnet test SIAD.Tests/SIAD.Tests.csproj --filter OrdenPagoDirectoPartidaTests`
Expected: FAIL (o Skipped si no hay `SIAD_TEST_DB`).

**Step 3: Ajustar seed/servicio hasta que pasen**

Run: el mismo comando.
Expected: PASS (2 pruebas) cuando `SIAD_TEST_DB` apunta a una BD con el seed contable.

---

### Task 11: Verificación manual en el portal (opcional pero recomendada)

**Step 1:** `dotnet run --project apc/apc.csproj`, ir a *Nuevo compromiso de proveedor*.

**Step 2:** Verificar:
- Checkbox marcado por defecto → al guardar se crea con partida (igual que antes).
- Checkbox desmarcado → se crea sin partida; en la pantalla de edición aparece "Generar partida contable"; al pulsarlo, se genera y la pantalla pasa a mostrar la partida.
- Procesar un compromiso creado sin partida → queda con partida (GEN) + PRC.

---

## Resumen de archivos tocados

- `SIAD.Core/DTOs/Presupuesto/OrdenesPagoDirectoDtos.cs` — flag `GenerarPartida`.
- `SIAD.Services/Presupuesto/IOrdenesPagoDirectoService.cs` — firma nueva.
- `SIAD.Services/Presupuesto/OrdenesPagoDirectoService.cs` — condicional en `CreateAsync`, helper `BuildCreatePartidaFromStoredAsync`, método `GenerarPartidaCreacionAsync`, auto-gen en `MarkAsProcessedAsync`.
- `apc/Controllers/Presupuesto/OrdenesPagoDirectoController.cs` — endpoint `generar-partida`.
- `apc.Client/Services/Presupuesto/OrdenesPagoDirectoClient.cs` — `GenerarPartidaAsync`.
- `apc.Client/Pages/Proveedores/CompromisoProveedorEdit.razor` — checkbox, botón, handler, validaciones.
- `SIAD.Tests/OrdenPagoDirectoPartidaTests.cs` — pruebas.

Sin cambios de BD (DDL): la feature usa tablas y SP existentes.
