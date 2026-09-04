# Runbook de despliegue a SRV — Aprobación por niveles (estructura, F1)

**Base destino:** `siad_v4` @ `172.16.0.9`
**Fecha:** 2026-08-31
**Alcance:** 1 script. Capa de datos de la aprobación por niveles (fase F1 de 7).
**Diseño:** [docs/plans/2026-08-31-aprobacion-niveles-compras-plan.md](../docs/plans/2026-08-31-aprobacion-niveles-compras-plan.md)

> ⚠️ **DEPENDENCIA DE ORDEN DURA con la tanda del 2026-08-27.**
> Este script y `2026-08-27_pst_compromiso_01_estructura.sql` **reemplazan el mismo CHECK**
> (`ck_alm_orden_compra_estado`). Aquel lo deja en `IN (1,2,3,4,5,6,9)`; este lo deja en
> `IN (1,2,3,4,5,6,7,9)`.
> **Este script va DESPUÉS.** Si se aplica antes, el paso 1 de la tanda del 27 borra
> silenciosamente el estado 7 y el módulo de aprobación deja de poder escribirlo.
> Si eso llegara a pasar: re-ejecutar este script (es re-ejecutable y lo repara).
>
> ⚠️ **Dos tandas anteriores siguen PENDIENTES:** `2026-08-22_runbook_despliegue_srv.md`
> (`fn_prv_cxp_documentos` + `fn_prv_cxp_resumen`) y `2026-08-27_runbook_despliegue_srv.md`
> (control presupuestario, 5 scripts). Esta tanda **no las reemplaza** y no cierra ninguna.
>
> ⚠️ La base **ACTIVA es `siad_v4`**, no `siad_v3`.

---

## 1. Qué cubre este runbook

La estructura completa de la aprobación por niveles configurable: escalera por monto, aprobadores
por usuario o por rol, flujo por documento y bitácora de auditoría.

| Pieza | Qué aporta |
|---|---|
| `cfg_aprobacion_control` | Interruptor por empresa y documento. **Nace apagado** |
| `cfg_aprobacion_nivel` | La escalera acumulativa por monto (D1) |
| `cfg_aprobacion_aprobador` | Quién firma cada nivel: usuario o rol de Identity (D3) |
| `alm_orden_compra_aprobacion` | Flujo vivo: una fila por nivel exigido de cada orden |
| `apr_bitacora` | Historial append-only de auditoría |
| Estado `7` en `alm_orden_compra` | "En aprobación": el documento ya no es editable y aún no está aprobado |

**El portal NO necesita esto para arrancar.** Las fases F2 (motor) y F3 (enganche en la O/C)
todavía no existen: aplicar esta tanda deja los objetos listos y el control apagado, sin cambiar
el comportamiento de ninguna pantalla.

## 2. Antes de empezar (obligatorio)

**Backup de `siad_v4`:**

```bash
pg_dump -h 172.16.0.9 -U postgres -d siad_v4 -Fc -f siad_v4_antes_apr_niveles.backup
```

**Definir la conexión** (la clave no va en el repo):

```bash
export SRV="postgresql://USUARIO:CLAVE@172.16.0.9:5432/siad_v4"
```

**Confirmar la base antes de escribir nada:**

```bash
psql "$SRV" -c "SELECT current_database(), pg_size_pretty(pg_database_size(current_database()));"
```

Debe decir `siad_v4`. Si dice `siad_v3`, **parar**.

**Comprobar los prerrequisitos:**

```sql
SELECT to_regclass('public.alm_orden_compra') AS oc,
       to_regclass('public.cfg_company')      AS empresas;
```

Los dos deben devolver el nombre de la tabla (no `NULL`).

**Comprobar el estado del CHECK** — decide el orden respecto de la tanda del 2026-08-27:

```sql
SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'ck_alm_orden_compra_estado';
```

- Si dice `IN (1, 2, 3, 4, 5, 6, 9)` → la tanda del 27 **ya se aplicó**: se puede seguir.
- Si dice `IN (1, 2, 3, 4, 9)` → la tanda del 27 **no se ha aplicado**. Aplicarla primero, o
  aceptar que habrá que **re-ejecutar este script después** de aplicarla.

## 3. Advertencias clave (leer antes de aplicar)

- La tanda es **aditiva y re-ejecutable**: `CREATE TABLE IF NOT EXISTS`, `CREATE INDEX IF NOT EXISTS`
  e `INSERT … ON CONFLICT DO NOTHING`. No hay `DROP TABLE`, `DELETE`, `TRUNCATE` ni `UPDATE` de datos.
- **El control nace APAGADO** (`cfg_aprobacion_control.modo = 0` en todas las empresas) y con la
  **autoaprobación prohibida** (`permite_autoaprobacion = false`). Aplicar el script **no cambia el
  comportamiento de ninguna pantalla**. Encenderlo es una decisión posterior y deliberada (ver §6).
- ⚠️ **Se amplía el CHECK `ck_alm_orden_compra_estado`** de `IN (1,2,3,4,5,6,9)` a
  `IN (1,2,3,4,5,6,7,9)`. Es la única operación que hace `DROP CONSTRAINT`. Solo **agrega** un valor
  admitido: ninguna fila existente queda inválida. El estado 7 todavía no lo escribe nadie; el código
  que lo usa llega en la fase F3.
- **Sin FK a los usuarios.** `cfg_aprobacion_aprobador.valor` y
  `alm_orden_compra_aprobacion.usuario_firma` son texto: la identidad vive en ASP.NET Identity
  (schema `identity`, otro DbContext) y una FK cruzaría el límite del contexto y del filtro
  multitenant. La existencia del usuario o rol la valida la pantalla de configuración (F4).
- Los usuarios se guardan **en minúsculas** (`CHECK (tipo <> 1 OR valor = lower(valor))`). Es lo que
  hace comparables la elegibilidad y la regla "nadie aprueba su propia orden".
- **Sin `company_id` fijo:** la semilla recorre `cfg_company`. Las verificaciones de abajo usan la
  empresa `2` (MERENDON).
- Las cinco tablas **nacen vacías** salvo `cfg_aprobacion_control`, que recibe una fila por empresa
  y documento. La reversa completa está documentada al pie del propio script.

## 4. Orden de aplicación (resumen)

| Paso | Script | Naturaleza | ¿Re-ejecutable? | Depende de |
|---:|---|---|:--:|---|
| 1 | `2026-08-31_apr_niveles_01_estructura.sql` | Aditivo (5 tablas, 8 índices, 1 semilla) + 1 CHECK ampliado | Sí | Paso 1 de la tanda `2026-08-27` (ver la advertencia de cabecera) |
| 2 | `2026-08-31_apr_niveles_02_funciones.sql` | Objetos (3 funciones de solo lectura) | Sí | Paso 1 |
| 3 | `2026-08-31_apr_niveles_03_requisicion.sql` | Aditivo (1 tabla, 2 índices) | Sí | Paso 1 |
| 4 | `2026-09-01_apr_niveles_04_limite_por_aprobador.sql` | **Cambio de modelo**: 1 rename + 1 columna anulable + 1 CHECK + 5 columnas nuevas + 5 funciones | Sí | Pasos 1, 2 y 3 |

Los cuatro traen su propio `BEGIN … COMMIT`.

> ⚠️ **El paso 4 cambia la regla de negocio.** La aprobación deja de ser en cascada: cada nivel
> declara **hasta cuánto** autoriza y quien alcanza el monto aprueba **de una sola firma**. Sin él,
> el código nuevo no funciona (busca `monto_hasta`, que solo existe después de aplicarlo).

## 5. Detalle por paso

### Paso 1 — Estructura (`2026-08-31_apr_niveles_01_estructura.sql`)

Crea las 5 tablas nuevas con sus índices, amplía el CHECK de estado de la orden de compra con el 7
y siembra `cfg_aprobacion_control` (todas las empresas × 4 documentos, en modo 0).

```bash
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-31_apr_niveles_01_estructura.sql
```

**¿Ya aplicado?**

```sql
SELECT to_regclass('public.cfg_aprobacion_control')      AS control,
       to_regclass('public.cfg_aprobacion_nivel')        AS nivel,
       to_regclass('public.cfg_aprobacion_aprobador')    AS aprobador,
       to_regclass('public.alm_orden_compra_aprobacion') AS flujo,
       to_regclass('public.apr_bitacora')                AS bitacora;
```

Esperado: los cinco con nombre. Todos en `NULL` = falta aplicar.

**Verificación:**

```sql
-- a) El control nace APAGADO y sin autoaprobación: una sola fila, (0, false)
SELECT modo, permite_autoaprobacion, count(*)
  FROM public.cfg_aprobacion_control GROUP BY 1, 2;

-- b) Hay una fila por empresa y documento (debe dar 4 por empresa)
SELECT company_id, count(*) AS documentos
  FROM public.cfg_aprobacion_control GROUP BY company_id ORDER BY company_id;

-- c) El CHECK admite el 7
SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'ck_alm_orden_compra_estado';

-- d) Ninguna O/C quedó fuera del CHECK ampliado (debe salir vacío)
SELECT id, numero, estado FROM public.alm_orden_compra
 WHERE estado NOT IN (1, 2, 3, 4, 5, 6, 7, 9);

-- e) Las cuatro tablas de trabajo nacen vacías
SELECT 'nivel' t, count(*) FROM public.cfg_aprobacion_nivel
UNION ALL SELECT 'aprobador',    count(*) FROM public.cfg_aprobacion_aprobador
UNION ALL SELECT 'flujo',        count(*) FROM public.alm_orden_compra_aprobacion
UNION ALL SELECT 'bitacora',     count(*) FROM public.apr_bitacora;
```

**No-regresión:** con el control en modo 0, `AprobarAsync` sigue siendo el flujo de un clic de hoy.
Aprobar una orden en el portal después de aplicar esto debe comportarse **exactamente igual** que
antes: pasa de Borrador (1) a Aprobada (2) sin pedir firmas.

### Paso 2 — Funciones (`2026-08-31_apr_niveles_02_funciones.sql`)

Tres funciones de **solo lectura** que consume el motor (`SIAD.Services/Aprobaciones/`):
`fn_apr_escalera` (qué niveles exige un monto), `fn_apr_es_aprobador` (si alguien puede firmar un
nivel, por usuario o por rol) y `fn_apr_oc_pendientes` (la bandeja "Mis aprobaciones").

```bash
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-31_apr_niveles_02_funciones.sql
```

**¿Ya aplicado?**

```sql
SELECT count(*) FROM pg_proc
 WHERE proname IN ('fn_apr_escalera', 'fn_apr_es_aprobador', 'fn_apr_oc_pendientes');
```

Esperado: `3`.

**Verificación** — sin escalera configurada, las tres son inocuas:

```sql
-- a) Ningún monto exige nada mientras no haya niveles (0 filas)
SELECT * FROM public.fn_apr_escalera(2::bigint, 'COMPRAS_OC', 999999::numeric);

-- b) Nadie es aprobador de un nivel que no existe (false)
SELECT public.fn_apr_es_aprobador(2::bigint, 'COMPRAS_OC', 1::smallint, 'quien@sea.com', ARRAY['Admin']::varchar[]);

-- c) La bandeja está vacía mientras no haya flujos abiertos (0 filas)
SELECT * FROM public.fn_apr_oc_pendientes(2::bigint, 'quien@sea.com', ARRAY['Admin']::varchar[]);
```

### Paso 3 — Flujo de la requisición (`2026-08-31_apr_niveles_03_requisicion.sql`)

Crea `alm_requisicion_aprobacion`, gemela de la de la orden de compra, para que la requisición use
el mismo motor. **No amplía ningún CHECK**: la requisición ya admite el estado 2 (En revisión).

```bash
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-31_apr_niveles_03_requisicion.sql
```

**¿Ya aplicado?**

```sql
SELECT to_regclass('public.alm_requisicion_aprobacion') AS tabla;
```

**Verificación:**

```sql
-- a) Nace vacía
SELECT count(*) FROM public.alm_requisicion_aprobacion;

-- b) La FK es compuesta y apunta a la requisición
SELECT conname, pg_get_constraintdef(oid) FROM pg_constraint
 WHERE conrelid = 'alm_requisicion_aprobacion'::regclass AND contype = 'f';

-- c) Las requisiciones existentes no se tocaron
SELECT estado, count(*) FROM public.alm_requisicion_hdr GROUP BY estado ORDER BY estado;
```

**No-regresión:** con `cfg_aprobacion_control.modo = 0` para `ALMACEN_REQUISICION`, aprobar una
requisición sigue siendo de un clic para quien tenga `module.inventario.requisiciones.aprobar`.

### Paso 4 — Límite de autorización (`2026-09-01_apr_niveles_04_limite_por_aprobador.sql`)

Convierte el umbral de entrada de cada nivel en un **límite máximo de autorización**, agrega el
registro que exige el requerimiento (con qué límite se autorizó, y el cambio de estado) y rehace
las funciones con la regla nueva.

```bash
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-09-01_apr_niveles_04_limite_por_aprobador.sql
```

**¿Ya aplicado?**

```sql
SELECT column_name FROM information_schema.columns
 WHERE table_name = 'cfg_aprobacion_nivel' AND column_name IN ('monto_desde', 'monto_hasta');
```

Esperado: `monto_hasta`. Si devuelve `monto_desde`, falta aplicarlo.

**Verificación:**

```sql
-- a) La columna admite NULL (= sin tope)
SELECT is_nullable FROM information_schema.columns
 WHERE table_name = 'cfg_aprobacion_nivel' AND column_name = 'monto_hasta';   -- YES

-- b) Las cinco funciones de la regla nueva, y ninguna de la vieja
SELECT proname FROM pg_proc WHERE proname LIKE 'fn_apr%' ORDER BY proname;
-- esperado: fn_apr_autorizadores, fn_apr_oc_capacidad, fn_apr_oc_pendientes,
--           fn_apr_puede_autorizar, fn_apr_tramo_de   (5, y NO fn_apr_escalera)

-- c) El registro nuevo de la bitácora
SELECT column_name FROM information_schema.columns
 WHERE table_name = 'apr_bitacora'
   AND column_name IN ('estado_anterior', 'estado_nuevo', 'limite_utilizado')
 ORDER BY column_name;   -- 3 filas
```

**No-regresión:** con el control en modo 0 nada de esto interviene y los documentos se aprueban de
un clic, como siempre.

## 6. Después de aplicar — cómo se enciende (NO hacerlo todavía)

⚠️ **Al 2026-08-31 están F1, F2 y F3 (base, motor y API). Faltan F4 y F5: la pantalla de
configuración y los botones de enviar / firmar / devolver.** Encender el control ahora dejaría el
flujo sin interfaz: el botón «Aprobar» de la lista respondería *«La orden debe enviarse a
aprobación antes de poder firmarse»* y no habría dónde enviarla. **No encender todavía.**

Cuando llegue el momento, encenderlo es configurar la escalera y poner `modo = 1` para una empresa
y un documento:

```sql
-- Ejemplo para la empresa 2, órdenes de compra. NO ejecutar ahora.
UPDATE public.cfg_aprobacion_control
   SET modo = 1, usuariomodificacion = 'quien-configura',
       fechamodificacion = now() AT TIME ZONE 'utc'
 WHERE company_id = 2 AND documento = 'COMPRAS_OC';
```

Encenderlo **sin niveles configurados** deja las órdenes sin poder enviarse a aprobación (error
explícito de configuración, por diseño: nunca aprobación automática silenciosa).

## 7. Estado presunto

| Base | Estado |
|---|---|
| `siad_v3_restore` (mirror, localhost) | ✅ **APLICADOS LOS CUATRO PASOS** (los tres primeros el 2026-08-31, el cuarto el 2026-09-01), exit 0. Paso 4 verificado: `monto_hasta` anulable, 5 funciones nuevas y ninguna `fn_apr_escalera`, y las 3 columnas de registro en la bitácora. El paso 3 quedó verificado: tabla vacía, FK compuesta a la requisición y las 43 requisiciones existentes intactas. Paso 1 verificado: control con 4 filas en modo 0 y `permite_autoaprobacion = false` (empresa 2), CHECK en `(1,2,3,4,5,6,7,9)`, 0 órdenes fuera del CHECK, las otras 4 tablas vacías. Paso 2 verificado: las 3 funciones existen y devuelven vacío sin escalera configurada |
| `siad_v4` @ 172.16.0.9 (SRV) | ⏳ **Pendientes los cuatro** — los aplica el usuario, en orden 01 → 02 → 03 → 04 |

En el mirror se corrió además una batería de 14 pruebas de constraints dentro de `BEGIN … ROLLBACK`
(usuario con mayúsculas rechazado, rol duplicado case-insensitive rechazado, aprobador vacío
rechazado, firma incoherente rechazada en ambos sentidos, nivel duplicado rechazado, estado 7
escribible, acción fuera de catálogo rechazada, cascade de aprobadores). Las 14 pasaron y el
`ROLLBACK` dejó la base sin rastro.

**Prerrequisito confirmado en el mirror:** el CHECK estaba en `(1,2,3,4,5,6,9)` antes de aplicar,
o sea que la tanda del 2026-08-27 **ya está en el mirror**. En `siad_v4` hay que comprobarlo con la
consulta de §2 antes de aplicar.

Nunca se verifica conectándose a la BD desde aquí: cada paso trae su consulta «¿ya aplicado?».

## 8. Versionado (git)

Archivos nuevos, **untracked** al 2026-08-31:

- `Database/2026-08-31_apr_niveles_01_estructura.sql`
- `Database/2026-08-31_apr_niveles_02_funciones.sql`
- `Database/2026-09-01_apr_niveles_04_limite_por_aprobador.sql`
- `Database/2026-08-31_runbook_despliegue_srv.md`
- `docs/plans/2026-08-31-aprobacion-niveles-compras-plan.md`
- `SIAD.Core/Constants/AprobacionConstants.cs`, `SIAD.Core/DTOs/Aprobaciones/`,
  `SIAD.Core/Security/ICurrentUserService.cs`
- `SIAD.Services/Aprobaciones/`, `SIAD.Services/Security/CurrentUserService.cs`
- `SIAD.Tests/Aprobaciones/AprobacionMotorTests.cs`

Modificados: `SIAD.Core/Constants/EstadosNumericos.cs` (estado 7 + `EstadoAprobacionNivel`),
`SIAD.Services/ServiceRegistration.cs` (dos registros nuevos),
`SIAD.Tests/Almacen/OrdenCompraTests.cs` (aislamiento del control presupuestario).
