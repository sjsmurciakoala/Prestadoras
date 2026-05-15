# Auditoría — Corte Legacy 2026-04-28

**Contexto:** El usuario dropeó 12 tablas legacy de la BD PROD. Este documento inventaría TODOS los archivos del repo que aún las referencian, **sin corregir nada**. Es la lista de trabajo a atacar por dominio cuando se decida.

## Tablas dropeadas en BD PROD

```sql
DROP TABLE public.tarifas;
DROP TABLE public.tarifas_contador;
DROP TABLE public.configuracion_tasas;
DROP TABLE public.configuracion_tasas_detalle;
DROP TABLE public.servicios_roles_ws;
DROP TABLE public.condicion_lectura;        -- (también la variante con typo: condicon_lectura)
DROP TABLE public.informativo;
DROP TABLE public.configuracion_app_lectura_medidores;
DROP TABLE public.cai;
DROP TABLE public.letras;
DROP TABLE public.letracodigo;
```

⚠️ **Cualquier query EF/SQL que referencie estas tablas falla con `relation does not exist` en runtime.**

---

## Resumen ejecutivo

| Frente | Archivos rotos | Severidad |
|---|---|---|
| WS WCF (`WSappLectores`) | 1 archivo (APCService.svc.cs) con múltiples endpoints/SP rotos | 🔴 Alta |
| Portal Backend C# (controllers + services) | ~12 archivos | 🔴 Alta |
| Portal Frontend Blazor (`apc.Client`) | ~15 archivos | 🔴 Alta |
| Entidades EF (`SIAD.Core/Entities`) | 8 entidades a borrar | 🔴 Alta — bloquea compilación |
| `SiadDbContext` y mapeos | 1 archivo principal + migraciones | 🔴 Alta |
| Migraciones EF generadas | ~15 archivos | 🟡 Media — no se ejecutan pero confunden |
| App Android | 2 archivos Java | 🟢 Baja — endpoints legacy ya no son fuente de verdad |
| SQL legacy en `Database/` | ~10 scripts ya aplicados | 🟢 Baja — históricos, no rompen |

---

## 1. 🔴 WS WCF — `WSappLectores/WS_APC/APCService.svc.cs` + `IAPCService.cs`

### Endpoints WCF que consultan tablas/SP dropeados

| Endpoint público (consumido por app) | SP/tabla que usa | Estado runtime |
|---|---|---|
| `GET /Tarifa` | `sp_tarifas_ws` → tabla `tarifas` | 🔴 Rompe |
| `GET /TarifaContador` | `sp_tarifas_contador_ws` → tabla `tarifas_contador` | 🔴 Rompe |
| `GET /GetCobrosAdicionales` | `sp_cobros_adicionales_ws` (v1 y v2) → `configuracion_tasas*` | 🔴 Rompe |
| `GET /GetCobrosAdicionalesV2` | Idem | 🔴 Rompe |
| `GET /CondicionesLectura` | tabla `condicion_lectura` | 🔴 Rompe |
| `GET /Informativos` | `sp_informativo_ws` → tabla `informativo` | 🔴 Rompe |
| `POST /ActualizarLecturaV2` | `sp_lectura_v2` (consulta `tarifas`, `configuracion_tasas`) | 🔴 Rompe |
| `GET /GetCAI/{ruta}` | tabla `cai` legacy | 🔴 Rompe |
| `GET /ServiciosApp` | `sp_servicios_app_ws` → tabla `servicios` | 🟡 Verificar (servicios no fue dropeada pero está deprecada) |

### SP referenciado dentro del WS que JOIN-ea legacy

| SP/Función SQL | Tablas legacy que JOIN-ea | Estado |
|---|---|---|
| `sp_medidores_por_ruta_ws` | `servicios_roles_ws` (4 JOINs), `configuracion_tasas`, `configuracion_tasas_detalle`, `servicios` | 🔴 **CRÍTICO — usado por GetRuta del flujo V3** |
| `sp_lectura_v2` | `tarifas`, `configuracion_tasas` | 🔴 Rompe (pero V2 se va a retirar de todas formas) |
| `sp_cobros_adicionales_ws` (v1 y v2) | `configuracion_tasas*` | 🔴 Rompe |

### Acción al respecto del WS

- **Reescribir `sp_medidores_por_ruta_ws`** sin JOINs a `servicios_roles_ws` ni `configuracion_tasas*`. Devolver los campos legacy (ser1..ser10, saldos por rol, recargos) en `false`/`0` literales o eliminar de la firma del SP. **Bloqueante para que el flujo V3 siga funcionando** porque GetRuta sigue dependiendo de este SP.
- **Eliminar endpoints WCF**: `Tarifa`, `TarifaContador`, `GetCobrosAdicionales(V2)`, `CondicionesLectura`, `Informativos`, `ActualizarLecturaV2`, `GetCAI`, `ServiciosApp`. Tanto en `APCService.svc.cs` (implementación) como en `IAPCService.cs` (contrato WCF).
- Recompilar `WS_APC.dll` y redesplegar a IIS PROD.

---

## 2. 🔴 Portal Backend C# — Controllers, Services, DTOs

### Controllers a eliminar enteros

| Archivo | Razón |
|---|---|
| `apc/Controllers/TarifasBaseController.cs` | Lee/escribe `tarifas` |
| `apc/Controllers/TarifasContadorController.cs` | Lee/escribe `tarifas_contador` |
| `apc/Controllers/LetrasController.cs` | Lee/escribe `letras` |

### Controllers a depurar parcialmente

| Archivo | Cambio |
|---|---|
| `apc/Controllers/CatalogosController.cs` | Quitar endpoint que devuelve catálogo de letras |
| `apc/Controllers/CobranzaController.cs` | Verificar si referencia letras como filtro |

### Services a eliminar enteros

| Archivo | Razón |
|---|---|
| `SIAD.Services/TarifasContador/TarifasContadorService.cs` | CRUD de `tarifas_contador` |
| `SIAD.Services/Letras/LetrasService.cs` (si existe) | CRUD de `letras` |
| `SIAD.Services/AppLectores/ServiciosRolesWsService.cs` | CRUD de `servicios_roles_ws` |
| `SIAD.Services/AppLectores/ConfiguracionAppService.cs` | CRUD de `configuracion_app_lectura_medidores` |

### Services a depurar parcialmente

| Archivo | Cambio |
|---|---|
| `SIAD.Services/Catalogos/CatalogosService.cs` | Quitar lectura de `tarifas_contador` |
| `SIAD.Services/Clientes/ClientesServices.cs` | Quitar referencias a `configuracion_tasas`, `configuracion_tasas_detalle`, `tarifas_contador`, `letracodigo` |
| `SIAD.Services/NotasCreditoDebito/NotasCreditoDebitoService.cs` | Verificar si calcula notas usando `configuracion_tasas*` |
| `SIAD.Services/Tarifario/PruebaCalculoService.cs` | Verificar referencia a `condicion_lectura` (probablemente para combo) |
| `SIAD.Services/Medidores/MedidoresService.cs` | Quitar referencia a `configuracion_app_lectura_medidores` |

### DTOs a revisar

| Archivo | Cambio |
|---|---|
| `SIAD.Core/DTOs/AppLectores/ConfiguracionAppDtos.cs` | Eliminar (DTOs de configuración legacy) |
| `SIAD.Core/DTOs/Bancos/BancoCuentaCreateDto.cs` | Verificar referencia a `letras` (si existe filtro) |
| `SIAD.Core/Constants/PermissionEndpointCatalog.cs` | Quitar permisos de endpoints `tarifas` y `letras` |

---

## 3. 🔴 Portal Frontend Blazor — `apc.Client`

### Páginas/módulos a eliminar enteros

| Carpeta/archivo | Razón |
|---|---|
| `apc.Client/Pages/TarifasBase/` (TarifasBaseList.razor + TarifasBaseGridDataSource.cs) | Módulo legacy de tarifas |
| `apc.Client/Pages/TarifasContador/` (TarifaContadorForm.razor + TarifasContadorList.razor + TarifasContadorGridDataSource.cs) | Módulo legacy de tarifas con medidor |
| `apc.Client/Pages/Letras/` (LetrasList.razor + LetrasGridDataSource.cs) | Módulo de letras |
| `apc.Client/Pages/MiApp/` (ConfiguracionAppForm.razor + ConfiguracionAppList.razor) | Configuración del app legacy |

### Clientes HTTP a eliminar enteros

| Archivo | Razón |
|---|---|
| `apc.Client/Services/TarifasBase/TarifasBaseClient.cs` | — |
| `apc.Client/Services/TarifasContador/TarifasContadorClient.cs` | — |
| `apc.Client/Services/Letras/LetrasClient.cs` | — |

### Clientes HTTP / Pages a depurar parcialmente

| Archivo | Cambio |
|---|---|
| `apc.Client/Services/Catalogos/CatalogosClient.cs` | Quitar método `ObtenerLetrasServicioAsync` |
| `apc.Client/Services/Facturacion/CobranzaClient.cs` | Verificar referencia a letras |
| `apc.Client/Pages/Clientes/Components/ClienteForm.razor` | Quitar combo de letra (`Modelo.LetraCodigo`, `letras`) y método `LoadLetrasAsync` |
| `apc.Client/Pages/Clientes/ClienteDetail.razor` | Verificar si muestra `LetraCodigo` o tarifas |
| `apc.Client/Pages/Informes/ReportesDatasets.razor` | Verificar referencia a letras como dataset |
| `apc.Client/Layout/Navigation/SidebarNavigationDefinition.cs` | Quitar items "Tarifas Base", "Tarifas Contador", "Letras", "Configuración del App" |
| `apc.Client/Layout/NavMenu.razor` | Quitar items legacy |
| `apc/Data/seed_manual.sql` | Quitar inserts de letras y tarifas |

### `CommonServices.cs` (Blazor DI registration)

- Quitar `AddHttpClient<TarifasBaseClient>(...)`, `AddHttpClient<TarifasContadorClient>(...)`, `AddHttpClient<LetrasClient>(...)`.

---

## 4. 🔴 Entidades EF — `SIAD.Core/Entities/`

### Entidades a borrar enteras

| Archivo entidad | Mapeo en SiadDbContext |
|---|---|
| `tarifas_catalogo.cs` | DbSet a quitar |
| `tarifas_contador.cs` | DbSet a quitar |
| `configuracion_tasa.cs` | DbSet a quitar |
| `configuracion_tasas_detalle.cs` | DbSet a quitar |
| `servicios_roles_ws.cs` | DbSet a quitar |
| `condicion_lectura.cs` | DbSet a quitar |
| `informativo.cs` | DbSet a quitar |
| `configuracion_app_lectura_medidore.cs` | DbSet a quitar |
| `letra.cs` | DbSet a quitar |

### Entidades a depurar parcialmente

| Archivo | Cambio |
|---|---|
| `cliente_maestro.cs` | Quitar columna `letracodigo`, navegaciones a `configuracion_tasas` |

### `SiadDbContext.cs`

- Quitar `DbSet<Tarifa>`, `DbSet<TarifaContador>`, `DbSet<ConfiguracionTasa>`, `DbSet<ConfiguracionTasasDetalle>`, `DbSet<ServiciosRolesWs>`, `DbSet<CondicionLectura>`, `DbSet<Informativo>`, `DbSet<ConfiguracionAppLecturaMedidore>`, `DbSet<Letra>`.
- Quitar `OnModelCreating` configs asociadas.

---

## 5. 🟡 Migraciones EF — `SIAD.Data/Migrations/`

⚠️ **No se aplican (son históricas)** pero el `SiadDbContextModelSnapshot.cs` debe regenerarse después de quitar entidades para que un futuro `dotnet ef migrations add` no contradiga el estado real.

### Archivos afectados

| Archivo | Tabla legacy referenciada |
|---|---|
| `Migrations/20251124180137_AddAccountingTables.cs` + `.Designer.cs` | tarifas, tarifas_contador, configuracion_tasas, configuracion_tasas_detalle, condicion_lectura, informativo, configuracion_app_lectura_medidores |
| `Migrations/20251216221247_AddLogoToCompanies.cs` + `.Designer.cs` | tarifas_contador, condicion_lectura, etc. (snapshot) |
| `Migrations/20251220144750_AddPlanCuentaExtraFields.cs` + `.Designer.cs` | snapshot |
| `Migrations/20260214011322_AddLetraTable.cs` + `.Designer.cs` | letra |
| `Migrations/Contabilidad/*.Designer.cs` (varios) | snapshot |
| `Migrations/SiadDbContextModelSnapshot.cs` | snapshot global |

### Acción

- Tras quitar entidades, **regenerar snapshot** con `dotnet ef migrations add CleanupLegacyTables` o similar, que generará un migration con los `DropTable(...)` correspondientes (idempotente: las tablas ya están dropeadas en BD).
- Los migrations históricos (`20251124...`, `20260214...`) se quedan, no se borran (forman parte del historial).

---

## 6. 🟢 SQL en `Database/` — scripts ya aplicados o históricos

⚠️ **No rompen nada** porque son scripts incrementales. Pero conviene marcarlos como "ya no aplicar" o moverlos a una carpeta `Database/historico_legacy/` para claridad.

| Archivo SQL | Tabla legacy creada/modificada |
|---|---|
| `2026-02-13_create_letras_table.sql` | Creaba letras (dropeada) |
| `2026-02-14_add_servicio_base.sql` | Modificaba tarifas_contador, configuracion_tasas |
| `2026-02-14_add_tarifas_relations.sql` | FK entre tarifas, tarifas_contador |
| `2026-02-15_add_servicios_roles_ws.sql` | Creaba servicios_roles_ws |
| `2026-02-15_add_servicios_roles_ws_trigger.sql` | Trigger |
| `2026-02-15_fix_sp_medidores_por_ruta_ws.sql` | **🔴 Este SP HOY tiene JOIN a servicios_roles_ws + configuracion_tasas — debe reescribirse** |
| `2026-02-16_fix_sp_cobros_adicionales_ws.sql` | SP legacy |
| `2026-02-16_fix_sp_lectura_roles.sql` | SP legacy de lectura |
| `2026-02-26_add_sp_lectura_v2.sql` | sp_lectura_v2 (referenced from informativo + ActualizarLecturaV2) |
| `2026-02-28_add_sp_cobros_adicionales_ws_v2.sql` | SP legacy v2 |
| `2026-02-28_add_sp_servicios_app_ws.sql` | SP legacy |
| `2026-03-02_remove_tarifas_catalogo.sql` | Removía tarifas_catalogo (parcial) |
| `2026-03-05_update_sp_lectura_v2_contabilidad.sql` | SP legacy v2 |
| `2026-03-16_add_fn_generar_codigo_cliente.sql` | Función ya desconectada del backend pero sigue en BD |
| `2026-04-07_drop_fk_tarifas_contador_tarifa.sql` | Drop FK previo |

### Acción recomendada

- Crear `Database/historico_legacy/` y mover ahí estos scripts.
- Crear nuevo script `Database/2026-04-28_drop_legacy_tables.sql` documentando los DROP que ya ejecutó el usuario manualmente (para reproducir el estado en otro ambiente).
- Reescribir `sp_medidores_por_ruta_ws` y dejar el script en `Database/2026-04-28_rewrite_sp_medidores_sin_legacy.sql`.

---

## 7. 🟢 App Android — `AppLectoresAPC/app/src/main/java/...`

### Archivos con referencias a endpoints legacy del WS

| Archivo | Referencias |
|---|---|
| `Utilidades/Utilidades.java` | `URL_getTarifa`, `URL_getTarifaContador`, `URL_getCAI`, `URL_getCondicionesLectura`, `URL_getConfiguraciones`, `URL_getInformativos`, `URL_getCobrosAdicionales`, `URL_getCobrosAdicionalesV2`, `URL_ActualizarLecturaV2` — quitar todas |
| `Utilidades/UtilidadesBD.java` | Métodos `DescargarTarifasABD`, `DescargarTarifasContadorABD`, `DescargarCondicionesLecturaABD`, `DescargarInformativosABD`, `DescargarConfiguracionesABD`, etc. — quitar |
| `Utilidades/UtilidadesParaNumeros.java` | Algún uso de letras (verificar) |
| `data/LegacySyncRepository.java` | Hoy ya tiene esos catálogos como "avisos no bloqueantes". Puede simplificarse eliminando los `appendOptionalWarning` correspondientes. |

### Tablas SQLite locales del app

- `Tarifa`, `TarifaContador`, `CobroAdicional`, `CondicionLectura`, `Informativo`, `Configuraciones` — eliminar definiciones del `ConexionSQLiteHelper` y subir versión de la BD para que se aplique migración local.

---

## 8. 🟢 Función PL/pgSQL en BD — `fn_generar_codigo_cliente`

- Dropeada del flujo del portal en sesión previa pero **sigue existiendo en BD**.
- Si el corte legacy es total, dropearla también: `DROP FUNCTION IF EXISTS public.fn_generar_codigo_cliente()`.

---

## 9. 🟢 Scripts de validación (artifacts)

| Archivo | Comentario |
|---|---|
| `artifacts/validacion_ruta_00000.sql` | Hace queries a tarifas, tarifas_contador, condicion_lectura, configuracion_app_lectura_medidores, informativo, servicios_roles_ws — todas dropeadas. Lo dejaba como referencia histórica. |
| `artifacts/validacion_ruta_00000_v3.sql` | Idem (versión V3). Reescribir o borrar. |

---

## 📋 Plan de ejecución sugerido (cuando se decida arrancar)

Orden propuesto, basado en dependencias y bloqueos:

### Fase A — Desbloquear flujo V3 actual (urgente)
1. **Reescribir `sp_medidores_por_ruta_ws`** sin JOINs a `servicios_roles_ws` ni `configuracion_tasas*`. Aplicar a PROD. **Bloquea descargas de la app si no se hace.**

### Fase B — Limpieza WS WCF
2. Eliminar 9 endpoints WCF legacy (`Tarifa`, `TarifaContador`, `GetCobrosAdicionales(V2)`, `CondicionesLectura`, `Informativos`, `ActualizarLecturaV2`, `GetCAI`, `ServiciosApp`) en `APCService.svc.cs` + `IAPCService.cs`.
3. Recompilar `WS_APC.dll`, redesplegar IIS.

### Fase C — Limpieza Portal Backend
4. Borrar Controllers/Services/DTOs legacy listados en sección 2.
5. Quitar `DbSet`s en `SiadDbContext.cs` y borrar entidades EF.
6. Compilar — destapará usings rotos pendientes.

### Fase D — Limpieza Portal Frontend
7. Borrar páginas Blazor + clientes HTTP listados en sección 3.
8. Quitar items del sidebar.
9. Quitar registros DI en `CommonServices.cs`.
10. Compilar Blazor.

### Fase E — Limpieza migraciones EF
11. Generar migración `CleanupLegacyTables` con los `DropTable` resultantes.
12. Validar `SiadDbContextModelSnapshot.cs` sin entidades dropeadas.

### Fase F — Limpieza App Android
13. Quitar URLs y métodos legacy en `Utilidades.java` + `UtilidadesBD.java`.
14. Subir versión de SQLite local + migrar.
15. Recompilar APK + reinstalar en los 5 dispositivos.

### Fase G — Limpieza scripts y archivos auxiliares
16. Mover scripts SQL históricos a `Database/historico_legacy/`.
17. Crear `Database/2026-04-28_drop_legacy_tables.sql` documental.
18. Reescribir o borrar `artifacts/validacion_ruta_00000*.sql`.
19. Dropear `fn_generar_codigo_cliente()` en BD.

---

## ⚠️ Estado al 2026-04-28

**Si compilas el portal HOY:** falla porque las entidades EF apuntan a tablas dropeadas y EF valida el modelo al iniciar `SiadDbContext`.

**Si haces request al WS HOY a `/Tarifa`, `/CondicionesLectura`, `/Informativos`, `/GetCAI`, `/ActualizarLecturaV2`:** todas devuelven HTTP 500 (relation does not exist).

**Si la app pide `GetRuta` HOY:** falla con error de SQL porque `sp_medidores_por_ruta_ws` JOIN-ea `servicios_roles_ws` (dropeada). **🔴 Bloquea descarga de cualquier ruta.**

**Si abres la lista de Letras / Tarifas Base / Tarifas Contador / Configuración App en el portal HOY:** páginas crashean.

---

## 🎯 Decisión de prioridad

Recomiendo empezar por **Fase A** porque desbloquea las pruebas de los lectores. Las demás se pueden atacar dominio por dominio sin urgencia operativa.
