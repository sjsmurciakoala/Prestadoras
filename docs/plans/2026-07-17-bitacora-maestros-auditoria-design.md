# Diseño — Bitácora de maestros + configuración de auditoría

**Fecha:** 2026-07-17
**Rama:** Cambios_almacen1.0
**Estado:** Diseño aprobado por el usuario (2026-07-17). Pendiente: plan de implementación.

## 1. Contexto y objetivo

Se requiere una **auditoría de las acciones de los usuarios sobre los maestros** (crear / editar / eliminar
en el maestro de clientes, almacén, proveedores, etc.), persistida en `public.bitacora_maestros`, más un
**mantenimiento** para configurar **a qué entidades** se les hace auditoría.

Antecedente: la tabla `public.bitacora_maestros` ya existía en la BD como WIP de Emilio (módulo caja 0.9),
documentado como decisión pendiente en `docs/INFORME_RAMA_MODULO_CAJA_EMILIO_2026-07-09.md` §4.1. **Ese código
nunca se integró al repo** (verificado: `git log --all -S "bitacora_maestros"` = 0 commits; no hay entidad,
servicio, interceptor ni vista). Este diseño es una **implementación limpia**, no una recuperación.

## 2. Decisiones tomadas (con el usuario, 2026-07-17)

| # | Decisión | Elección |
|---|---|---|
| 1 | Granularidad de la configuración | **Por entidad/tabla maestra** (no por página ni por módulo) |
| 2 | Nivel de detalle del registro | **Diff campo a campo** (JSON antes/después) |
| 3 | Alcance de hosts | **Solo el portal `apc`** (no `apc.BancosWs` ni `apc.MobileApi`) |
| 4 | Menú / acceso | Sección **"Auditoría"**; mantenimiento de config **solo SuperAdmin** |
| 5 | Baja lógica (`activo=false`) | Se registra como **ELIMINAR**; reactivar (`activo=true`) como EDITAR |
| 6 | Set inicial del catálogo auditable | **Clientes, Almacén, Proveedores** (Servicios/Tarifario queda fuera del set inicial) |
| 7 | Retención | **Sin purga** por ahora (YAGNI) |

## 3. Enfoque de captura — decisión de arquitectura

**Elegido (A): `ISaveChangesInterceptor` dedicado**, registrado **solo en `apc/Program.cs`**.

- **Descartado (B): extender `ApplyCompanyInformation` en `SiadDbContext.Tenancy.cs`.** Mezclaría auditoría con
  tenancy, correría en los 3 hosts (no deseado) y ensuciaría el archivo más crítico del sistema.

El interceptor no existe hoy en el repo (campo verde: `AddInterceptors` = 0 usos). El único override actual de
`SaveChanges` está en `SiadDbContext.Tenancy.cs` y **corre antes** del interceptor (estampa `company_id` en el
override; el interceptor lee dentro de `base.SaveChanges`), lo que nos permite copiar el `company_id` ya sellado.

## 4. Modelo de datos

Ambas tablas son **tenant-scoped** (`ICompanyScopedEntity`) y se crean con un **script SQL timestamped** en
`Database/` (aplicado por el usuario en mirror/prod — no se toca el servidor por iniciativa propia). Luego se
escanean/mapean en `SiadDbContext`. No hay migraciones EF para el contexto SIAD.

### 4.1 `public.bitacora_maestros` (append-only)

```sql
CREATE TABLE IF NOT EXISTS public.bitacora_maestros (
    id             BIGSERIAL PRIMARY KEY,
    company_id     BIGINT       NOT NULL,
    entidad        VARCHAR(100) NOT NULL,   -- nombre de TABLA: 'cliente_maestro', 'alm_articulo', ...
    accion         VARCHAR(10)  NOT NULL,   -- 'CREAR' | 'EDITAR' | 'ELIMINAR'
    registro_id    VARCHAR(60)  NOT NULL,   -- PK del registro afectado (string: soporta int/uuid/compuesta)
    registro_desc  VARCHAR(250) NULL,       -- descriptor legible (ej. nombre del cliente), best-effort
    cambios        JSONB        NULL,       -- [{ "campo": "...", "antes": ..., "despues": ... }]
    usuario        VARCHAR(100) NOT NULL,   -- HttpContext.User.Identity.Name ?? 'system'
    ip             VARCHAR(45)  NULL,       -- RemoteIpAddress (IPv4/IPv6)
    fecha          TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS ix_bitacora_maestros_company_fecha    ON public.bitacora_maestros (company_id, fecha);
CREATE INDEX IF NOT EXISTS ix_bitacora_maestros_company_entidad  ON public.bitacora_maestros (company_id, entidad);
CREATE INDEX IF NOT EXISTS ix_bitacora_maestros_company_usuario  ON public.bitacora_maestros (company_id, usuario);
CREATE INDEX IF NOT EXISTS ix_bitacora_maestros_company_registro ON public.bitacora_maestros (company_id, entidad, registro_id);
```

Sin operaciones de UPDATE/DELETE desde la aplicación (solo INSERT por el interceptor y SELECT por la vista).

### 4.2 `public.bitacora_maestro_config` (CRUD)

```sql
CREATE TABLE IF NOT EXISTS public.bitacora_maestro_config (
    id                  SERIAL PRIMARY KEY,
    company_id          BIGINT       NOT NULL,
    entidad             VARCHAR(100) NOT NULL,   -- nombre de TABLA, del catálogo AuditableMaestros
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
```

## 5. Catálogo de entidades auditables (lista blanca en código)

Constante `AuditableMaestros` en `SIAD.Core` — define **qué tablas son candidatas** a auditoría. El mantenimiento
solo muestra estas; el interceptor solo audita las que estén en `bitacora_maestro_config` con `habilitado=true`.
Esto impide auditar tablas transaccionales pesadas (facturas, kardex) por error.

Clave técnica = **nombre de tabla** (`GetTableName()`), más estable que el nombre de entidad scaffold.

Set inicial (Clientes + Almacén + Proveedores):

| tabla | nombre amigable | módulo |
|---|---|---|
| `cliente_maestro` | Maestro de clientes | Clientes |
| `alm_articulo` | Artículos | Almacén |
| `alm_grupo` | Grupos de artículo | Almacén |
| `alm_tipo_articulo` | Tipos de artículo | Almacén |
| `alm_bodega` | Bodegas | Almacén |
| `alm_categoria_unidad` | Categorías de unidad | Almacén |
| `alm_unidad_medida` | Unidades de medida | Almacén |
| `prv_proveedor` | Maestro de proveedores | Proveedores |
| `prv_proveedor_cuenta_bancaria` | Cuentas bancarias de proveedor | Proveedores |

(Los nombres de tabla se confirman contra el modelo EF en implementación. Nota: la entidad CLR de proveedor es
`prv_proveedore` pero la tabla es `prv_proveedor`.)

## 6. Motor de captura — `BitacoraMaestrosInterceptor`

`SaveChangesInterceptor` registrado **scoped** y enganchado solo en `apc/Program.cs`:

```csharp
builder.Services.AddScoped<BitacoraMaestrosInterceptor>();
builder.Services.AddDbContext<SiadDbContext>((sp, options) =>
    options.UseNpgsql(connectionString)
           .AddInterceptors(sp.GetRequiredService<BitacoraMaestrosInterceptor>()));
```

Dependencias: `IHttpContextAccessor` (usuario + IP) y un `IAuditConfigProvider` (config cacheada en
`IMemoryCache` por empresa, invalidada al guardar el mantenimiento).

Flujo:

1. **`SavingChanges` / `SavingChangesAsync`** (antes del commit, dentro de `base.SaveChanges`):
   - Recorre `ChangeTracker.Entries()`.
   - **Excluye siempre** `bitacora_maestros` y `bitacora_maestro_config` (anti-recursión).
   - Filtra por whitelist: tabla ∈ catálogo **y** `habilitado` **y** la acción concreta habilitada
     (`audita_crear/editar/eliminar`).
   - Determina la acción:
     - `Added` → **CREAR**
     - `Deleted` → **ELIMINAR** (borrado físico)
     - `Modified` → **EDITAR**, salvo que la propiedad `activo` pase de `true`→`false` ⇒ **ELIMINAR** (baja
       lógica); `false`→`true` ⇒ EDITAR.
   - Construye el diff: para `Modified`, compara `OriginalValue` vs `CurrentValue` por propiedad escalar (ignora
     navegaciones) y guarda solo las que cambiaron; `Added` → solo valores nuevos; `Deleted` → solo valores previos.
   - `company_id` de la fila = `company_id` de la entidad auditada (ya estampado por Tenancy).
   - `usuario` = `HttpContext.User?.Identity?.Name ?? "system"`; `ip` = `RemoteIpAddress`.
   - `registro_id`: si la PK ya existe (`Modified`/`Deleted`, o `Added` con PK asignada por app) se materializa
     ya; si es identity store-generated (`Added`), se deja **pendiente**.
2. **`SavedChanges` / `SavedChangesAsync`** (post-commit): completa `registro_id` de los pendientes leyendo la PK
   real, hace `AddRange` + `SaveChanges` de las filas de bitácora. Ese segundo save re-dispara el interceptor pero
   `bitacora_maestros` está excluida ⇒ no genera nada (sin recursión).

Notas:
- Fechas: `DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)` (columnas `timestamp without time zone`).
- SuperAdmin sin empresa (`company_id = 0`): la fila queda con `company_id=0` (consistente con el comportamiento
  actual de tenancy; visible solo en ese scope).
- `registro_desc`: best-effort, leyendo una propiedad "nombre"/"descripcion"/"codigo" si existe; si no, null.

## 7. Mantenimiento de configuración (`/auditoria/configuracion`, solo SuperAdmin)

Calca el patrón `alm_categoria_unidad` (entidad → DTO → servicio → controller `[ModuleAuthorize]` → client → página),
con mapeo **inline** (sin AutoMapper), como el resto de catálogos.

UX: grilla con una fila por entidad del catálogo `AuditableMaestros`, con switches **Habilitado / Crear / Editar /
Eliminar** y guardar. Al cargar, hace upsert de filas faltantes contra el catálogo (para empresas nuevas). Guardar
invalida el `IMemoryCache` de config.

## 8. Vista de bitácora (`/auditoria/bitacora-maestros`, solo lectura)

Calca `HistorialAccionesCobranza`:
- Filtros: **Desde / Hasta** (`DxDateEdit`), **Entidad** (`DxComboBox` del catálogo), **Usuario** (`DxTextBox`),
  **Acción** (`DxComboBox`: Crear/Editar/Eliminar). Botón **Buscar** (no auto).
- Grid: Fecha, Usuario, Entidad (nombre amigable), Acción (badge), Registro (`registro_desc` ?? `registro_id`), IP.
- **Detalle del diff**: popup / fila expandible que renderiza `cambios` (campo · antes · después).
- Export Excel/PDF (`ExportToXlsxAsync`/`ExportToPdfAsync`); tope `MaxFilas = 5000` con aviso, igual que cobranza.
- Filtrado/paginación en servidor (una llamada con querystring), como el patrón de referencia.

## 9. Permisos y menú

- **Bitácora:** permiso nuevo `module.configuracion.auditoria.view` (recurso `auditoria` bajo módulo
  `configuracion`). Registrar en `PermissionNames`, `PermissionResources.Configuracion` y (endpoint-específico)
  `PermissionEndpointCatalog`.
- **Configuración:** policy **SuperAdmin** (`AuthorizationPolicies.SuperAdmin`) en página y controller.
- **Menú** (`SidebarNavigationDefinition.cs`, el menú real):
  - Sección nueva **"Auditoría"** → ítem "Bitácora de maestros" (`/auditoria/bitacora-maestros`).
  - Ítem "Configuración de auditoría" (`/auditoria/configuracion`) bajo la sección **"Parámetros"** (ya
    `RequiredPolicy = SuperAdmin`), reforzado server-side. Motivo: el gating de menú hoy es por sección, no por
    ítem; así la bitácora es visible con su permiso y la config solo la ve SuperAdmin.

## 10. Archivos a crear / tocar

**Crear**
- `Database/2026-07-17_bitacora_maestros.sql` (las 2 tablas + índices; idempotente).
- `SIAD.Core/Entities/bitacora_maestros.cs`, `bitacora_maestro_config.cs` (partials, `ICompanyScopedEntity`).
- `SIAD.Core/Constants/AuditableMaestros.cs` (catálogo).
- `SIAD.Core/DTOs/Auditoria/` — `BitacoraMaestroFilterDto`, `BitacoraMaestroListItemDto`, `BitacoraCambioDto`,
  `AuditoriaConfigItemDto` (config editable).
- `SIAD.Services/Auditoria/` — `IBitacoraMaestrosService` + impl (consulta), `IAuditoriaConfigService` + impl
  (CRUD + cache), `IAuditConfigProvider` + impl (whitelist cacheada para el interceptor).
- `SIAD.Data/Interceptors/BitacoraMaestrosInterceptor.cs`.
- `apc/Controllers/Auditoria/BitacoraMaestrosController.cs`, `AuditoriaConfigController.cs`.
- `apc.Client/Services/Auditoria/BitacoraMaestrosClient.cs`, `AuditoriaConfigClient.cs`.
- `apc.Client/Pages/Auditoria/BitacoraMaestrosList.razor`, `AuditoriaConfigList.razor` (+ form si aplica).

**Tocar**
- `SIAD.Data/SiadDbContext.cs` (DbSets / scan de entidades nuevas).
- `SIAD.Data/SiadDbContext.*.cs` (config Fluent de las 2 tablas — archivo parcial adecuado, p. ej. uno nuevo
  `SiadDbContext.Auditoria.cs`).
- `apc/Program.cs` (registro del interceptor en `AddDbContext`).
- `SIAD.Services/ServiceRegistration.cs` (DI de servicios + interceptor + provider).
- `apc.Client/CommonServices.cs` (DI de los 2 clients).
- `apc.Client/Layout/Navigation/SidebarNavigationDefinition.cs` (sección Auditoría + ítem en Parámetros).
- `SIAD.Core/Constants/PermissionNames.cs` (+ `PermissionResources.Configuracion`, `PermissionEndpointCatalog`).

## 11. Riesgos / puntos de atención (de la exploración)

1. **Orden interceptor vs tenancy**: el override de Tenancy estampa `company_id` antes; el interceptor lo lee ya
   sellado. Verificar en pruebas que en `SavingChanges` la entidad tiene `company_id` correcto.
2. **PK identity en altas**: patrón dos fases (capturar en Saving, releer en Saved). Cubrir con test.
3. **Recursión**: excluir siempre las 2 tablas de auditoría del interceptor.
4. **Rendimiento**: config cacheada; el interceptor no debe consultar la BD por cada `SaveChanges`.
5. **Serialización del diff**: crear util propia (no hay existente); excluir binarios/navegaciones; cuidar tipos
   (fechas, decimales, bool) al serializar a JSON.
6. **Campos de auditoría por-fila** (usuariocreacion/…): la bitácora **no** depende de ellos; captura usuario/fecha
   por su cuenta.

## 12. Pruebas (SIAD.Tests, integración Postgres con BEGIN…ROLLBACK)

- Alta de un maestro habilitado ⇒ 1 fila CREAR con `registro_id` correcto y diff de valores nuevos.
- Edición ⇒ 1 fila EDITAR con diff solo de campos cambiados.
- `activo` true→false ⇒ fila **ELIMINAR** (baja lógica); false→true ⇒ EDITAR.
- Entidad con `habilitado=false` o acción deshabilitada ⇒ **no** genera bitácora.
- Cambios sobre tabla transaccional fuera del catálogo ⇒ **no** genera bitácora.
- No se auto-audita `bitacora_maestros` (sin recursión).
- Aislamiento por `company_id` (tenant scoping de la bitácora y de la config).
