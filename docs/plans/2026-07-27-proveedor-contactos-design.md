# Contactos de proveedor — diseño

Fecha: 2026-07-27
Estado: aprobado, pendiente de implementar

## Problema

Un proveedor solo puede tener un contacto, guardado como campos sueltos en
`prv_proveedores`: `nombre_contacto`, `telefono`, `email` (más `fax` y
`pagina_web`, que no se editan desde el portal). En la práctica un proveedor
tiene varias personas de contacto — ventas, cobros, soporte — y hoy no hay
dónde registrarlas.

## Alcance

- Tabla hija de contactos por proveedor (N por proveedor, opcional).
- Catálogo de tipos de contacto con vista de mantenimiento propia.
- Los campos legacy de `prv_proveedores` se conservan y se mantienen
  sincronizados con el primer contacto.

Fuera de alcance: `fax` y `pagina_web` (siguen como están), y el hueco de
multiempresa de `prv_proveedor_cuenta_bancaria` descrito más abajo.

## Modelo de datos

Dos tablas nuevas en Postgres, en un script aditivo
(`Database/2026-07-27_proveedor_contactos.sql`).

### `prv_tipo_contacto`

Catálogo por empresa, con mantenimiento propio.

| columna | tipo | nota |
|---|---|---|
| `tipo_contacto_id` | BIGSERIAL | PK |
| `company_id` | INT NOT NULL | |
| `nombre` | VARCHAR(60) NOT NULL | UNIQUE `(company_id, upper(nombre))` |
| `observaciones` | VARCHAR(250) NULL | |
| `activo` | BOOLEAN NOT NULL DEFAULT true | permite retirar un tipo sin borrarlo |
| `fecha_creacion` | TIMESTAMP NOT NULL DEFAULT now() | |
| `usuario_creo` | VARCHAR(100) NOT NULL | |
| `fecha_modificacion` | TIMESTAMP NULL | |
| `usuario_modifica` | VARCHAR(100) NULL | |
| `rowid` | UUID DEFAULT gen_random_uuid() | |

Semilla por cada empresa existente: Ventas, Cobros, Gerencia, Soporte técnico,
Administración.

### `prv_proveedor_contacto`

| columna | tipo | nota |
|---|---|---|
| `proveedor_contacto_id` | BIGSERIAL | PK |
| `company_id` | INT NOT NULL | |
| `cod_proveedor` | VARCHAR(20) NOT NULL | |
| `tipo_contacto_id` | BIGINT NULL | FK → `prv_tipo_contacto`, `ON DELETE RESTRICT` |
| `nombre` | VARCHAR(150) NOT NULL | |
| `cargo` | VARCHAR(100) NULL | |
| `telefono` | VARCHAR(30) NULL | |
| `extension` | VARCHAR(10) NULL | |
| `celular` | VARCHAR(30) NULL | |
| `email` | VARCHAR(150) NULL | |
| `observaciones` | VARCHAR(500) NULL | |
| `orden` | INT NOT NULL DEFAULT 1 | posición en el grid |
| auditoría | igual que el catálogo | |

Índice `(company_id, cod_proveedor, orden)`.

No lleva columna `activo`: los contactos se quitan del grid, no se desactivan.

### Por qué `company_id` en la tabla hija

El correlativo del proveedor se genera por empresa
([ProveedoresService.cs:1443](../../SIAD.Services/Proveedores/ProveedoresService.cs)),
de modo que `cod_proveedor` se repite entre empresas. Colgar los contactos
únicamente de `cod_proveedor` los volvería visibles entre tenants.

`prv_proveedor_cuenta_bancaria` tiene exactamente ese hueco hoy: se carga
filtrando solo por código
([ProveedoresService.cs:849](../../SIAD.Services/Proveedores/ProveedoresService.cs)).
Queda anotado; no se corrige en este trabajo.

### Migración de datos

Un `INSERT ... SELECT` crea el contacto `orden = 1` para cada proveedor con
`nombre_contacto` no vacío, copiando `nombre_contacto`, `telefono` y `email`.
Idempotente: no inserta si el proveedor ya tiene contactos.

## Sincronía con los campos legacy

El contacto de `orden = 1` se escribe en `prv_proveedores.nombre_contacto`,
`.telefono` y `.email` en cada alta y edición del proveedor — el mismo
mecanismo que hoy replica la primera cuenta bancaria en las columnas viejas
(`BuildLegacyCompatibilityFields`). Si el proveedor queda sin contactos, esas
tres columnas quedan en NULL.

Así los reportes, vistas y consultas que leen las columnas legacy siguen
funcionando sin cambios.

## Backend

- DTOs en `SIAD.Core/DTOs/Proveedores/`: `ProveedorContactoDto`,
  `TipoContactoListItemDto`, `TipoContactoUpsertDto`, `TipoContactoLookupDto`.
  `ProveedorUpsertDto` y `ProveedorDetailDto` reciben `Contactos`.
- `ProveedoresService`: `LoadContactosAsync`, `PrepareContactos` y
  `SyncContactosAsync`, calcados de sus equivalentes de cuentas bancarias
  (diff por id: borra los ausentes, actualiza los existentes, inserta los
  nuevos). Más el CRUD del catálogo, espejo del de tipos de proveedor.
- Validación en `PrepareContactos`: nombre obligatorio por fila, email con
  formato válido cuando viene, sin nombres repetidos dentro del mismo
  proveedor, `tipo_contacto_id` existente y activo.
- Borrar un tipo de contacto en uso se rechaza con mensaje explícito.
- Entidades EF y configuración en `SiadDbContext.cs`, siguiendo lo hecho con
  `prv_proveedor_cuenta_bancaria`.
- `ProveedoresController`: solo endpoints del catálogo
  (`contactos/tipos` — GET, POST, PUT, DELETE). Los contactos del proveedor
  viajan dentro del upsert del proveedor y no tienen endpoint propio. Heredan
  el `[ModuleAuthorize(PermissionModules.Proveedores)]` de la clase.
- Auditoría: ambas tablas entran en `AuditableMaestros` — son entidades con
  clave, así que las ve el interceptor de `SaveChanges` — más su fila en
  `bitacora_maestro_config`.

## UI

- `ProveedorForm.razor`: se elimina el grupo "Contacto" (los tres campos
  sueltos) y entra un grupo **Contactos** con grid inline idéntico al de
  cuentas bancarias. Columnas: Tipo (combo del catálogo), Nombre, Cargo,
  Teléfono, Ext., Celular, Email, Observaciones y botón de quitar; abajo,
  "Agregar contacto". A diferencia de las cuentas bancarias, **no** se agrega
  una fila vacía automáticamente: los contactos son opcionales.
- `ProveedorDetailGeneral.razor`: bloque de solo lectura con los contactos.
- Página nueva `/mantenimientos/tipos-contacto` (List + Edit + Form), clon de
  Tipos de proveedor y conforme al estándar de grids, con su entrada en
  `SidebarNavigationDefinition` junto a "Tipos de proveedor".

## Pruebas

Integración en `SIAD.Tests` (requieren `SIAD_TEST_DB`):

- Alta de proveedor con varios contactos; lectura devuelve el mismo orden.
- Edición: alta, modificación y borrado de filas en la misma operación.
- Los campos legacy quedan sincronizados con el contacto `orden = 1`.
- Dos empresas con el mismo `cod_proveedor` no se ven los contactos.
- Borrar un tipo de contacto en uso falla.
- El proveedor se guarda sin contactos (son opcionales).

## Decisiones tomadas

| Decisión | Resuelto |
|---|---|
| Campos del contacto | nombre, cargo, teléfono, extensión, celular, email, observaciones, tipo |
| Tipo de contacto | tabla catálogo + vista de mantenimiento; opcional en cada contacto |
| Campos legacy | se migran y se mantienen sincronizados con el primer contacto |
| Contacto obligatorio | no; N es opcional |
| Flag "principal" | no se agrega; la fila que alimenta los campos legacy es la primera del grid |
| `activo` en el contacto | no; solo en el catálogo |
