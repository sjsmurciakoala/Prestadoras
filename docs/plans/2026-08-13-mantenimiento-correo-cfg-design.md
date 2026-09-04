# Mantenimiento de correo y notificaciones (SendGrid) por empresa — Diseño

Fecha: 2026-08-13 · Rev: 2 (separación conexión / notificaciones) · Rama: `feat/almacen-integracion-contable` · Estado: **propuesta (plan corto), sin implementar**

---

## Requerimiento

Hoy el envío de correo de Identity está cableado a un **No-Op** que no envía nada
([`IdentityNoOpEmailSender.cs`](../../apc/Components/Account/IdentityNoOpEmailSender.cs), registrado en [`Program.cs:196`](../../apc/Program.cs)).
No existe SendGrid en la solución: ni paquete NuGet, ni claves en `appsettings`, ni servicio.
La única mención está en [`docs/readme_inventario.md`](../readme_inventario.md), que documenta el sistema **legacy**, no este.

Se quiere un **mantenimiento por empresa** que guarde la configuración de correo en la base de datos,
configurable desde pantalla, **con varias áreas de notificación** (administración, almacén, cobranza…),
cada una con **su propio remitente y sus propios destinatarios**, pero **una sola conexión (API key)**.

## Decisiones ya tomadas

- **La API key vive en la BD, cifrada con `IDataProtection`.** (Descartadas: híbrido con la key en
  variable de entorno, y texto plano.) La BD nunca ve la key en claro; el descifrado ocurre solo en
  memoria, al enviar.
- **Separación conexión / enrutamiento.** La **conexión** (API key) es **una por empresa**: la key
  autentica la *cuenta* de SendGrid; el `de:` y el `para:` van por mensaje, no por credencial. Lo que
  se multiplica por área es el **enrutamiento** (remitente + destinatarios), no la credencial. Por más
  áreas que se agreguen, **el secreto a cifrar sigue siendo uno solo**.
- **El remitente y los destinatarios cambian por área** (decisión del usuario). Una sola API key.
  Requiere que cada remitente (o el dominio) esté **verificado en SendGrid**.
- **Los tipos de notificación los define el código**, no el usuario: el sistema dispara el evento con
  un tipo concreto; la pantalla solo asigna remitente y destinatarios a cada tipo. El catálogo se
  siembra; no hay alta libre de tipos.

## ⚠️ Prerrequisito crítico — key-ring de DataProtection en producción

Cifrar en BD **obliga** a un key-ring estable en producción. Hoy `AddDataProtection()` solo se
configura en el bloque `if (IsDevelopment())` ([`Program.cs:56-59`](../../apc/Program.cs)); fuera de
desarrollo, IIS usa el comportamiento por defecto, que **no es estable**: al reciclar el App Pool,
redeployar o cambiar de máquina, las llaves pueden regenerarse y **la API key cifrada deja de
descifrarse** (envío caído en silencio).

**Trabajo obligatorio (Fase 0):** configurar DataProtection también fuera de desarrollo en
[`Program.cs`](../../apc/Program.cs):

- Persistir llaves a una carpeta fija **fuera del publish** (que sobreviva a redeploys).
- Protegerlas en reposo con **DPAPI** (`ProtectKeysWithDpapi()`, Windows/IIS).
- `SetApplicationName` estable (p. ej. `HODSOFT.Prestadoras`) — el mismo entre redeploys.
- Solo `apc` cifra/descifra (es quien envía); `apc.BancosWs` y `apc.MobileApi` no se tocan.

> El ciphertext queda atado al key-ring de cada entorno: un backup de `siad_v3` restaurado en otra
> máquina **no** descifra la key. Es un rasgo buscado (la credencial no viaja utilizable en respaldos
> ni en el mirror), no un defecto: cada entorno configura su propia key en su propia pantalla.

---

## Modelo de datos — tres tablas (multi-tenant, aditivas)

Molde de mapeo: `cfg_compra_isv` (tenant-scoped, `ICompanyScopedEntity`, upsert vía filtro global).

### 1. `cfg_correo` — la conexión (1 por empresa)

| Columna | Tipo | Nota |
|---|---|---|
| `id` | `SERIAL` PK | |
| `company_id` | `BIGINT NOT NULL` | Tenant (lo estampa `SaveChanges`) |
| `proveedor` | `VARCHAR(20) NOT NULL DEFAULT 'SENDGRID'` | Deja abierto SMTP u otros |
| `api_key_cifrada` | `TEXT NULL` | **Ciphertext de DataProtection**. Nunca en claro |
| `remitente_email_default` | `VARCHAR(200) NULL` | Remitente por defecto (fallback si un área no lo sobreescribe) |
| `remitente_nombre_default` | `VARCHAR(150) NULL` | |
| `activo` | `BOOLEAN NOT NULL DEFAULT false` | Interruptor **global** de envío |
| auditoría | | `usuariocreacion`, `fechacreacion`, `usuariomodificacion`, `fechamodificacion` |
| `UNIQUE (company_id, proveedor)` | | Una conexión por proveedor por empresa |

### 2. `cfg_notificacion` — el área/tipo (N por empresa)

| Columna | Tipo | Nota |
|---|---|---|
| `id` | `SERIAL` PK | |
| `company_id` | `BIGINT NOT NULL` | Tenant |
| `tipo` | `VARCHAR(30) NOT NULL` | CHECK contra el catálogo de código (`ADMINISTRACION`, `ALMACEN`, `COBRANZA`, `SISTEMA`…) |
| `nombre` | `VARCHAR(120) NULL` | Etiqueta editable (solo presentación) |
| `remitente_email` | `VARCHAR(200) NULL` | **Override** del `de:`. `NULL` → usa el default de `cfg_correo` |
| `remitente_nombre` | `VARCHAR(150) NULL` | Override del nombre |
| `activo` | `BOOLEAN NOT NULL DEFAULT true` | Encender/apagar ese tipo sin borrarlo |
| auditoría | | Igual bloque |
| `UNIQUE (company_id, tipo)` | | Un renglón por tipo por empresa |

### 3. `cfg_notificacion_destinatario` — los destinatarios (N por notificación)

| Columna | Tipo | Nota |
|---|---|---|
| `id` | `SERIAL` PK | |
| `company_id` | `BIGINT NOT NULL` | Tenant |
| `notificacion_id` | `INT NOT NULL` FK `cfg_notificacion(id)` **CASCADE** | La FK compuesta incluye `company_id` (patrón tenant del repo) |
| `correo` | `VARCHAR(200) NOT NULL` | Dirección destino |
| `clase` | `VARCHAR(4) NOT NULL DEFAULT 'TO'` | CHECK ∈ `TO` \| `CC` |
| `activo` | `BOOLEAN NOT NULL DEFAULT true` | |
| `UNIQUE (notificacion_id, correo, clase)` | | Sin duplicar un destino en el mismo canal |

> Precedente de 1‑a‑N tenant-scoped: los **contactos de proveedor** (tabla + tipos). Se reusa ese patrón.

Las tres nacen **vacías** (salvo la siembra del catálogo de tipos): sin envío hasta que el usuario
configure y active (fail-closed, coherente con los demás `cfg_*`).

### Catálogo de tipos (código)

`SIAD.Core/Constants/TipoNotificacion.cs` — constantes `Administracion`, `Almacen`, `Cobranza`,
`Sistema`, … + `Todos` + `EsValido()`. Espejo del CHECK de `cfg_notificacion.tipo`. **Agregar un tipo
= tocar la constante + el CHECK** (queda escrito en el comentario del archivo). El seed de filas por
empresa es opcional (la pantalla puede crearlas al vuelo desde el catálogo).

### Script SQL — `Database/2026-08-13_cfg_correo_notificaciones.sql` (aditivo)

`CREATE TABLE IF NOT EXISTS` de las tres, con `UNIQUE`/`CHECK`/FK y `COMMENT` explicando el porqué
(por qué la key es ciphertext, por qué el remitente del canal es override, por qué `activo` global vs
por tipo). Encabezado con **Fecha** + **Regla DB Mirror** (`aplicar también en siad_v3_restore
@localhost`), idempotencia, `BEGIN … COMMIT`, bloque **VERIFICACIÓN** comentado. Pasa por
**guardia-estructura-bd** (tarjeta verde, aditivo) y se registra en el runbook SRV vía
**runbook-despliegue-srv**. **El usuario lo aplica**: mirror primero, SRV pendiente.

---

## Capas afectadas (slice estándar)

**Entidades / contexto** — `SIAD.Core/Entities/cfg_correo.cs`, `cfg_notificacion.cs`,
`cfg_notificacion_destinatario.cs` (las tres `ICompanyScopedEntity`); partial nuevo
`SIAD.Data/SiadDbContext.Correo.cs` con los `DbSet` y el mapeo (FK compuesta con `company_id`,
`DeleteBehavior.Cascade` en destinatarios; sin `HasDefaultValue` en los `activo`).

**Constantes** — `SIAD.Core/Constants/TipoNotificacion.cs` y `ClaseDestinatario.cs` (`TO`/`CC`).

**DTOs** — `SIAD.Core/DTOs/Configuracion/CorreoConfigDtos.cs`:
- `ConexionCorreoDto` (lectura, **sin la key**): proveedor, remitente default, activo, `TieneApiKey`.
- `ConexionCorreoUpsertDto` (escritura): + `NuevaApiKey` (opcional).
- `NotificacionDto` / `NotificacionUpsertDto`: tipo, nombre, remitente override, activo, y la lista de
  destinatarios (`DestinatarioDto` = correo + clase + activo).

**Servicio** — `SIAD.Services/Configuracion/ICorreoConfigService.cs` + impl (molde `IsvCompraConfigService`):
- Conexión: `ObtenerConexionAsync` (nunca devuelve la key), `GuardarConexionAsync` (cifra `NuevaApiKey`
  si viene; si vacía, conserva). Depende de `IDataProtectionProvider`, protector `"cfg_correo.apikey"`.
- Notificaciones: `ListarNotificacionesAsync`, `GuardarNotificacionAsync` (upsert canal + reemplazo de
  su lista de destinatarios en la misma transacción).
- `ResolverEnvioAsync(tipo)` (interno, para el futuro sender): descifra la key, resuelve el remitente
  efectivo (override del canal → default de la conexión) y la lista de destinatarios activos.
- Registrar en [`ServiceRegistration.cs`](../../SIAD.Services/ServiceRegistration.cs).

**Controller** — `apc/Controllers/Configuracion/CorreoConfigController.cs` (`api/configuracion/correo`):
GET/PUT conexión, GET/PUT notificaciones; `[ModuleAuthorize(...)]`. El GET nunca trae la key.

**Cliente HTTP** — `apc.Client/Services/Configuracion/CorreoConfigClient.cs` (extensiones auth-aware),
registrado en [`CommonServices.cs`](../../apc.Client/CommonServices.cs).

**Pantalla** — `apc.Client/Pages/Configuracion/CorreoConfig.razor` (carpeta nueva). Dos zonas:
1. **Conexión**: proveedor, API key (solo escritura), remitente por defecto, activo global.
2. **Áreas de notificación**: grilla de tipos; por fila, remitente propio (opcional) y sus
   destinatarios (TO/CC). `TenantState.EnsureCompanyAsync()` antes de cargar.

## Seguridad de la pantalla (secreto de solo escritura)

- Estado **`API key: ✔ Configurada / ✖ No configurada`** — nunca el valor.
- Campo *password* "Nueva API key": vacío al guardar = **no** cambia la key almacenada.
- El resto (remitentes, destinatarios, activos) se edita libremente.

## Autorización

Módulo `configuracion`: `PermissionResources.Correo` + `module.configuracion.correo.view` / `.edit`,
entradas en `Policies` y `PermissionEndpointCatalog`. Restringido (idealmente `SuperAdministrador` o
rol admin acotado).

---

## Pruebas (`SIAD.Tests`, integración Postgres; `BEGIN … ROLLBACK`)

1. Conexión: upsert crea/actualiza una sola fila; round-trip de cifrado (`ResolverEnvioAsync` recupera
   la key); `ObtenerConexionAsync` nunca trae la key; `NuevaApiKey` vacío conserva la previa.
2. Notificación: upsert de un canal reemplaza su lista de destinatarios; `UNIQUE (company_id, tipo)`.
3. Resolución de remitente: override del canal gana; si es `NULL`, cae al default de la conexión.
4. Destinatarios: se listan solo los `activo`, separados por clase TO/CC; CASCADE al borrar el canal.
5. Aislamiento por tenant en las tres tablas.

> Los tests usan un `IDataProtectionProvider` real (o `EphemeralDataProtectionProvider`); la
> persistencia va contra `SIAD_TEST_DB`.

## Orden de trabajo

- **F0** — DataProtection en producción ([`Program.cs`](../../apc/Program.cs)). *Prerrequisito duro.*
- **F1** — Tres tablas + entidades + catálogo de tipos + script SQL (mirror; SRV pendiente).
- **F2** — Servicio + DTOs + controller + cliente + DI.
- **F3** — Pantalla `/configuracion/correo` (conexión + grilla de áreas) + permisos.
- **F4** — Tests.

## Fuera de alcance (siguiente iteración)

- El **`EmailSender` real de SendGrid** que consume `ResolverEnvioAsync` y reemplaza al No-Op (paquete
  NuGet, implementación de `IEmailSender<ApplicationUser>`, cambio del registro en `Program.cs`).
- Botón **"Probar conexión"** (correo de prueba) — depende del sender real.
- Plantillas de correo, multi-proveedor SMTP, cola de reintentos.

Este plan deja la **configuración persistida y cifrada, con enrutamiento por área**; encender el envío
es un paso posterior que solo lee de `ICorreoConfigService`.
