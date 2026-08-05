# Entradas y salidas de almacén — Traspaso (2026-08-04)

Documento de handoff: qué está hecho, dónde está aplicado, qué falta y **cuál es el siguiente paso**.
Autocontenido para retomar en otra sesión o cuenta.

**Encargo original:** incorporar al módulo de almacén las *entradas y salidas de almacén* del legacy
Centura (`GA_IN.APT`, pantalla `dlgTransaccionesGenericasINV`).

**Documentos relacionados (no duplicar, leer si hace falta):**
- Análisis del legacy: [`docs/centura-flujos/README_entradas_salidas_almacen.md`](../centura-flujos/README_entradas_salidas_almacen.md)
- Diseño (con §10 hallazgos QA y decisiones de vocabulario): [`docs/plans/2026-08-01-movimientos-almacen-catalogo-diseno.md`](2026-08-01-movimientos-almacen-catalogo-diseno.md)
- Diseño de costeo (dependencia): [`docs/plans/2026-08-01-costeo-articulo-diseno.md`](2026-08-01-costeo-articulo-diseno.md)
- Runbook de despliegue (pasos 27 y 28): [`Database/2026-07-23_runbook_despliegue_srv.md`](../../Database/2026-07-23_runbook_despliegue_srv.md)

---

## 1. Resumen en una línea

El módulo de entradas y salidas manuales de almacén está **implementado y verificado de punta a punta
en código** (backend + UI), **aplicado al mirror** `siad_v3_restore`, con **220/220 pruebas de Almacén
en verde**. Falta la **prueba de humo logueada en el navegador** y la decisión de **aplicar al SRV**.

---

## 2. Arquitectura (dos capas)

| Capa | Qué es | En el código | En la UI (vocabulario del usuario) |
|---|---|---|---|
| 1 | Naturaleza que ejecuta el motor: suma / resta / corrige costo | `clase` = `ENTRADA`/`SALIDA`/`VALOR` (`ClaseAjusteInventario`) | se rotula **«Tipo»** |
| 2 | Catálogo configurable (Merma, Donación, Sobrante) | tabla `alm_tipo_movimiento` | se llama **«Concepto de movimiento»** |
| — | Documento multi-renglón de captura | `alm_movimiento_hdr` / `_dtl` / `_correlativo` | «Movimiento de almacén» |

El comportamiento de inventario lo ejecuta **el único motor** `IInventarioPostingService` (compartido con
compras, carga inicial y ajustes), sin cambios. Cada renglón postea un asiento en `alm_kardex`
(inmutable); la anulación es por **reversa**, nunca por UPDATE/DELETE.

> **⚠️ Divergencia deliberada código ↔ UI.** En el código y la base todo se llama `TipoMovimiento` /
> `alm_tipo_movimiento` / `clase`. En **rutas, permisos y etiquetas** se usa «concepto» y «tipo». No es
> un error: fue decisión del usuario (2026-08-04). Ver §6.

---

## 3. Estado por fase

| Fase | Qué | Estado |
|---|---|---|
| **1 — Catálogo** (conceptos) | tabla, entidad, servicio, controller, permisos, cliente, 2 pantallas, menú, 11 tests | ✅ completa y verificada |
| **2 — Documento** (entradas/salidas) | `hdr`/`dtl`/`correlativo`, servicio (crear+postear, anular), controller, permisos, 20 tests | ✅ completa y verificada |
| **3 — UI** | lista de movimientos + formulario de captura (costo deshabilitado en salida), cliente, menú | ✅ implementada · falta prueba de humo logueada |
| **Vocabulario** | «Clase»→«Tipo», «Tipo de movimiento»→«Concepto»; rutas y permisos renombrados; formulario del catálogo rediseñado | ✅ 2026-08-04 |
| **4 — Deprecar `alm_ajuste_inventario`** | congelar el ajuste de una línea a favor del documento nuevo | ⬜ no empezada |
| **5 — Traslado entre bodegas** | clase `TRASLADO`, `bodega_destino_id`, tránsito | ⬜ fuera de alcance (era lo MÁS usado en Centura: 62.599 asientos) |
| **6 — Requisición→aprobación→descargo** | flujo de dos actores | ⬜ fuera de alcance |

---

## 4. Base de datos — qué está aplicado dónde

Ambos scripts **aplicados y verificados en el mirror `siad_v3_restore`**, **pendientes en el SRV**
(`siad_v3` @ 172.16.0.9). Registrados como pasos 27 y 28 del runbook. **No hay migración de datos**: son
aditivos (tablas nuevas + semilla).

| Paso | Script | Qué crea |
|---|---|---|
| 27 | `Database/2026-08-01_alm_tipo_movimiento.sql` | `alm_tipo_movimiento` + semilla de **12 conceptos reales importados de `dbo.INV_TIPOSTRANSACC` de MERENDON** (4 activos: AIE/AIS/NPG/APL; 8 inactivos) |
| 28 | `Database/2026-08-03_alm_movimiento.sql` | `alm_movimiento_hdr` + `_dtl` + `_correlativo` (4 FK compuestas tenant-safe) |

Verificación en vivo (mirror): catálogo con 12 filas, clases 7 ENTRADA / 5 SALIDA; las 3 tablas del
documento con sus 4 FK compuestas; pruebas negativas de constraints OK; mirror sin residuos tras los tests.

---

## 5. Archivos entregados (todos sin commitear en la rama `Cambios_almacen2.0`)

**Nuevos — backend**
- `SIAD.Core/Entities/`: `alm_tipo_movimiento.cs`, `alm_movimiento_hdr.cs`, `alm_movimiento_dtl.cs`, `alm_movimiento_correlativo.cs`
- `SIAD.Core/DTOs/Almacen/`: `TipoMovimientoAlmacenDto.cs`, `TipoMovimientoAlmacenListItemDto.cs`, `MovimientoAlmacenDtos.cs`
- `SIAD.Services/Almacen/`: `ITipoMovimientoService.cs` + impl, `IMovimientoAlmacenService.cs` + impl
- `apc/Controllers/Almacen/`: `TiposMovimientoController.cs`, `MovimientosAlmacenController.cs`
- `Database/`: `2026-08-01_alm_tipo_movimiento.sql`, `2026-08-03_alm_movimiento.sql`

**Nuevos — cliente/UI**
- `apc.Client/Services/Almacen/`: `TiposMovimientoClient.cs`, `MovimientosAlmacenClient.cs`
- `apc.Client/Pages/Almacen/`: `TiposMovimientoList.razor`, `TipoMovimientoForm.razor` (+ `.css`), `MovimientosAlmacenList.razor`, `MovimientoAlmacenFormPage.razor`

**Nuevos — tests**
- `SIAD.Tests/Almacen/`: `TipoMovimientoServiceTests.cs` (11), `MovimientoAlmacenTests.cs` (14), `MovimientoAlmacenAnulacionTests.cs` (6)

**Modificados** (aditivo): `PermissionNames.cs`, `PermissionEndpointCatalog.cs`, `EstadosNumericos.cs`,
`SiadDbContext.Almacen.cs`, `ServiceRegistration.cs`, `CommonServices.cs`, `SidebarNavigationDefinition.cs`,
`ArticuloListItemDto.cs` (propiedad `ComboTexto`), `2026-07-23_runbook_despliegue_srv.md`.

> Nota de nombres: por decisión de alcance, los **nombres de clase/archivo C#** siguen diciendo
> `TipoMovimiento` aunque la UI diga «concepto». Renombrarlos a `ConceptoMovimiento` es un pendiente
> opcional, NO hecho.

---

## 6. Decisiones tomadas (para no re-litigar)

1. **Semilla = 12 conceptos reales de Centura** (no inventados). Las 7 cuentas contables de MERENDON
   **no se importaron** (no existen en `con_plan_cuentas` de SIAD; quedan NULL = hereda del artículo).
2. **`requiere_autorizacion` es booleano.** NO se portó la matriz usuario×tipo de Centura
   (`AXL_USUARIOS_TRN`): la evidencia mostró que dejó de mantenerse.
3. **Salida se valoriza al promedio vigente**; el costo tecleado se ignora (= `PIDE_COSTO` de Centura).
4. **Buscador y grilla de artículos muestran el ID interno**, no el código SIMAFI (el código se sigue
   guardando como snapshot en `alm_movimiento_dtl.codigo_articulo`).
5. **Vocabulario:** «Tipo» = Entrada/Salida/Valor; «Concepto de movimiento» = el catálogo. Alcance del
   renombrado: etiquetas + rutas (`/almacen/conceptos-movimiento`) + permisos
   (`module.inventario.conceptos_movimiento.*`). Código y base sin cambios.
6. **Menú:** «Movimientos de almacén» va después de «Artículos» (primer nivel); «Conceptos de
   movimiento» va en Almacén → Mantenimiento.

---

## 7. Verificación

- **Build:** solución completa 0 errores (cuando el portal no está en debug bloqueando las DLL).
- **Tests contra el mirror** (`SIAD_TEST_DB` → `siad_v3_restore`, company 2): **Almacén 220/220 en verde**,
  incluye los 31 nuevos. Mirror limpio tras correr (todo en `BEGIN … ROLLBACK`).
- **Prueba destacada:** la de 3 líneas destapó y se corrigió un bug de idempotencia real (uuid de renglón
  derivado de un id inexistente antes del INSERT → ahora de `(company_id, hdr.uuid, posición)`).

Comando para re-verificar:
```
$env:SIAD_TEST_DB = 'Host=localhost;Port=5432;Database=siad_v3_restore;Username=postgres;Password=***;Timeout=30'
$env:SIAD_TEST_COMPANY_ID = '2'
dotnet test SIAD.Tests/SIAD.Tests.csproj --filter "FullyQualifiedName~SIAD.Tests.Almacen"
```

---

## 8. Deudas y limitaciones conocidas (NO bloqueantes)

1. **Anular un movimiento de clase VALOR no restituye el costo promedio anterior.** El motor no lo guarda;
   es el defecto de `2026-08-01-costeo-articulo-diseno.md` §3 corr.1, cuya solución es la **Fase B de ese
   diseño**. La prueba `Anular_CorreccionDeValor_..._LimitacionConocida` lo fija y avisa.
2. **`NPG` tiene 182 asientos en Centura pero no está concedido a nadie** en `AXL_USUARIOS_TRN` → hay un
   camino de posteo legacy no identificado. Curiosidad, no bloquea.
3. **Atajo sin re-apuntar:** `ArticuloUbicacionesTab.razor:396` sigue abriendo el ajuste de una línea, no
   el documento nuevo. Pendiente menor.
4. **Nombres de clase C#** siguen en `TipoMovimiento` (ver §5). Opcional.
5. **Permisos:** los strings cambiaron (`tipos_movimiento`→`conceptos_movimiento`). Si algún rol tenía el
   permiso viejo, quedó huérfano; siendo módulo nuevo, casi seguro nadie lo tenía (SuperAdmin hace bypass).

---

## 9. SIGUIENTE PASO

**Inmediato (cierra la entrega actual):**
> **Prueba de humo logueada en el navegador.** Es lo único que falta para dar por terminado el módulo.
> Requiere iniciar sesión (no la puede hacer el asistente: no ingresa credenciales). Verificar end-to-end:
> 1. Almacén → Mantenimiento → **Conceptos de movimiento**: alta/edición/desactivación; confirmar que el
>    formulario rediseñado se ve bien y los textos de ayuda ya no se parten en vertical.
> 2. Almacén → **Movimientos de almacén**: capturar un movimiento de **3 líneas** (una entrada con costo y
>    una salida), confirmar que postea, que el buscador/grilla muestran el ID, y que el costo se
>    deshabilita en salida.
> 3. Anular ese movimiento y confirmar que la existencia vuelve.

**Después, cuando el usuario decida:**
- **Aplicar al SRV** los pasos 27 y 28 (los aplica el usuario; el asistente no toca el servidor).
- **Commit/push** de la rama (el usuario decide cuándo).

**Backlog (fases siguientes, cuando se prioricen):** Fase 4 (deprecar `alm_ajuste_inventario`),
Fase 5 (traslado entre bodegas — el flujo más usado del legacy), Fase 6 (requisición→descargo con dos
actores), y la Fase B del costeo (que arregla la limitación 8.1).

---

## 10. Cómo retomar en otra máquina/cuenta

- Rama: `Cambios_almacen2.0`. Todo el trabajo está **sin commitear** (untracked/modified).
- Mirror local: `siad_v3_restore` @ localhost (PG 17). Ya tiene los pasos 27 y 28 aplicados.
- Fuente Centura para consultas: SQL Server local, base `MERENDON` (tablas `INV_TIPOSTRANSACC`,
  `INV_TRANSACC_AXL`, `AXL_USUARIOS_TRN`, `INV_KARDEX`).
- Build: `dotnet build HODSOFT_DEVEXPRESS.sln`. Si falla con `MSB302x` (copia de DLL), es que el portal
  está corriendo/en debug; usar `-t:Compile` para validar código, o cerrar el portal.
