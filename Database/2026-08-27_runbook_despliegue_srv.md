# Runbook de despliegue a SRV — Control presupuestario con compromiso en la O/C

**Base destino:** `siad_v4` @ `172.16.0.9`
**Fecha:** 2026-08-27
**Alcance:** 5 scripts. Capa de datos del control presupuestario (fases F1 y F8).
**Diseño:** [docs/plans/2026-08-27-presupuesto-compromiso-oc-design.md](../docs/plans/2026-08-27-presupuesto-compromiso-oc-design.md)

> ⚠️ **La tanda anterior sigue PENDIENTE.** `2026-08-22_runbook_despliegue_srv.md`
> (`fn_prv_cxp_documentos` + `fn_prv_cxp_resumen`) **no se ha aplicado** en `siad_v4`. Las dos
> tandas son **independientes** entre sí —ninguna depende de la otra— pero ambas siguen abiertas:
> no cerrar ninguna de las dos por haber aplicado la otra.
>
> ⚠️ La base **ACTIVA es `siad_v4`**, no `siad_v3`.

---

## 1. Qué cubre este runbook

La capa de datos completa del control presupuestario que se dispara al **aprobar una orden de
compra**: valida disponibilidad por partida, compromete, y libera exactamente el saldo pendiente
cuando la orden se anula o se cancela.

| Pieza | Qué aporta |
|---|---|
| `valor_comprometido` / `valor_pagado` | Los dos montos que le faltaban al modelo presupuestario |
| `pst_compromiso` | Saldo vivo del compromiso por documento, renglón y partida |
| `pst_compromiso_aplicacion` | Qué factura consumió qué compromiso |
| `pst_movimiento` | Kardex presupuestario inmutable, con los 8 saldos antes/después |
| `cfg_presupuesto_control` | El interruptor por empresa y módulo. **Nace apagado** |
| 5 funciones + 7 procedimientos | Comprometer, liberar, ajustar, devengar, revertir, pagar |
| 4 vistas | Ejecución presupuestaria, compromisos pendientes, kardex, por centro de costo |

**El portal NO necesita esto para arrancar.** La fase F2 (backend) todavía no existe: aplicar esta
tanda deja los objetos listos y el control apagado, sin cambiar el comportamiento de ninguna
pantalla.

## 2. Antes de empezar (obligatorio)

**Backup de `siad_v4`:**

```bash
pg_dump -h 172.16.0.9 -U postgres -d siad_v4 -Fc -f siad_v4_antes_pst_compromiso.backup
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

**Comprobar los prerrequisitos** — esta tanda se apoya en objetos que deben existir arriba:

```sql
SELECT to_regclass('public.pst_config_presupuesto_hdr') AS pst_hdr,
       to_regclass('public.pst_config_presupuesto_dtl') AS pst_dtl,
       to_regclass('public.alm_orden_compra')           AS oc,
       to_regclass('public.alm_orden_compra_detalle')   AS oc_dtl,
       to_regclass('public.alm_compra_hdr')             AS compra,
       to_regclass('public.con_centro_costo')           AS centro_costo,
       to_regclass('public.con_plan_cuentas')           AS plan_cuentas,
       to_regclass('public.cfg_compra_isv')             AS cfg_isv;
```

Los ocho deben devolver el nombre de la tabla (no `NULL`).

**Comprobar que el presupuesto ya es multitenant** — es un prerrequisito duro:

```sql
SELECT count(*) FILTER (WHERE column_name = 'company_id') AS tiene_company_id
  FROM information_schema.columns
 WHERE table_name = 'pst_config_presupuesto_dtl';
```

Debe ser `1`. Si es `0`, aplicar antes `Database/2026-07-24_presupuesto_multitenant_company_id.sql`
y `Database/2026-07-28_presupuesto_completar_ddl_valor_real.sql` (pasos 14, 15 y 18 del registro
`Database/2026-07-30_pendientes_srv.md`, que siguen pendientes).

## 3. Advertencias clave (leer antes de aplicar)

- La tanda es **aditiva y re-ejecutable**: `CREATE TABLE IF NOT EXISTS`, `ADD COLUMN IF NOT EXISTS`
  y `CREATE OR REPLACE`. No hay `DROP TABLE`, `DELETE`, `TRUNCATE` ni `UPDATE` de datos.
- **El control nace APAGADO** (`cfg_presupuesto_control.modo = 0` en todas las empresas). Aplicar
  estos scripts **no cambia el comportamiento de ninguna pantalla**. Encenderlo es una decisión
  posterior y deliberada (ver §7).
- ⚠️ **Cambia la fórmula de `valor_disponible`** del detalle: pasa de
  `MAX(proyeccion − real, 0)` a `MAX(proyeccion − comprometido − real, 0)`. **Sin efecto al
  aplicar**, porque `valor_comprometido` nace en 0.
- ⚠️ **Se amplía el CHECK `ck_alm_orden_compra_estado`** de `IN (1,2,3,4,9)` a `IN (1,2,3,4,5,6,9)`.
  Es la única operación que hace `DROP CONSTRAINT`. Solo **agrega** valores admitidos: ninguna fila
  existente queda inválida. Los estados nuevos (5 Rechazada, 6 Cancelada) todavía no los escribe
  nadie: el código que los usa llega en la fase F3.
- El script 01 crea la clave alterna `uq_con_centro_costo_tenant` sobre `con_centro_costo`. No
  puede fallar: `cost_center_id` ya es PK, así que el par `(company_id, cost_center_id)` es único
  por construcción.
- `pst_movimiento` queda **inmutable por trigger**: `UPDATE` y `DELETE` fallan por diseño. Si hace
  falta corregir un movimiento, se registra una reversa. Precedente: `trg_transaccion_abonado_congelada`.
- **Sin `company_id` fijo:** los scripts recorren `cfg_company`. Las verificaciones de abajo usan
  la empresa `2` (MERENDON).
- ⚠️ **Riesgo de doble conteo, preexistente:** una misma compra registrada como O/C *y* como
  compromiso de proveedor (OPD) consumiría presupuesto dos veces. El **paso 5** hace que los dos
  compartan el mismo motor y el mismo pool, pero solo descuentan mutuamente cuando se enciende el
  módulo PROVEEDORES; con todo apagado el riesgo sigue siendo el de hoy.
- ⚠️ **El paso 5 reemplaza una función que producción ya usa** (`fn_pst_afectar_saldo_real_credito`,
  la que llama bancos). Misma firma y mismos códigos de retorno; ver su detalle abajo.

## 4. Orden de aplicación (resumen)

El orden es **por dependencia**, no por nombre: las funciones usan las tablas del 01, los
procedimientos usan las funciones del 02, y las vistas usan todo lo anterior.

| Paso | Script | Naturaleza | ¿Re-ejecutable? | Depende de |
|---:|---|---|:--:|---|
| 1 | `2026-08-27_pst_compromiso_01_estructura.sql` | Aditivo (4 tablas, 5 columnas, 1 tipo, 9 índices, 1 trigger) + 1 CHECK ampliado | Sí | Presupuesto multitenant (pasos 14/15/18) |
| 2 | `2026-08-27_pst_compromiso_02_funciones.sql` | Objetos (5 funciones de lectura) | Sí | Paso 1 |
| 3 | `2026-08-27_pst_compromiso_03_procedimientos.sql` | Objetos (7 procedimientos + 1 helper) | Sí | Pasos 1 y 2 |
| 4 | `2026-08-27_pst_compromiso_04_vistas.sql` | Objetos (4 vistas) | Sí | Pasos 1, 2 y 3 |
| 5 | `2026-08-27_pst_compromiso_05_proveedores_bancos.sql` | Objetos (1 función nueva) + **reemplazo de una función viva** + CHECK/índice ampliados | Sí | Pasos 1, 2 y 3 |

Los cinco traen su propio `BEGIN … COMMIT`.

## 5. Detalle por paso

### Paso 1 — Estructura (`..._01_estructura.sql`)

Crea las 4 tablas nuevas, agrega las 5 columnas, el tipo compuesto, los índices, el trigger de
inmutabilidad y la semilla de `cfg_presupuesto_control` (todas las empresas en modo 0).

```bash
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-27_pst_compromiso_01_estructura.sql
```

**¿Ya aplicado?**

```sql
SELECT to_regclass('public.pst_compromiso')             AS compromiso,
       to_regclass('public.pst_compromiso_aplicacion')  AS aplicacion,
       to_regclass('public.pst_movimiento')             AS movimiento,
       to_regclass('public.cfg_presupuesto_control')    AS control;
```

Esperado: los cuatro con nombre. Todos en `NULL` = falta aplicar.

**Verificación:**

```sql
-- a) El control nace APAGADO: la única fila del group by debe ser modo = 0
SELECT modo, count(*) FROM public.cfg_presupuesto_control GROUP BY modo;

-- b) Las columnas nuevas existen y están en cero
SELECT count(*) AS partidas,
       count(*) FILTER (WHERE valor_comprometido <> 0) AS comprometido_no_cero,
       count(*) FILTER (WHERE valor_pagado <> 0)       AS pagado_no_cero
  FROM public.pst_config_presupuesto_dtl;

-- c) El CHECK de estado admite los 7 valores
SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'ck_alm_orden_compra_estado';

-- d) Ninguna O/C quedó fuera del CHECK ampliado (debe salir vacío)
SELECT id, numero, estado FROM public.alm_orden_compra WHERE estado NOT IN (1,2,3,4,5,6,9);
```

### Paso 2 — Funciones (`..._02_funciones.sql`)

`fn_pst_resolver_partida`, `fn_pst_disponible`, `fn_pst_recalcular_cabecera`,
`fn_alm_oc_distribucion_partidas`, `fn_alm_compra_distribucion_partidas`.

```bash
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-27_pst_compromiso_02_funciones.sql
```

**¿Ya aplicado?**

```sql
SELECT count(*) FROM pg_proc
 WHERE proname IN ('fn_pst_resolver_partida', 'fn_pst_disponible', 'fn_pst_recalcular_cabecera',
                   'fn_alm_oc_distribucion_partidas', 'fn_alm_compra_distribucion_partidas');
```

Esperado: `5`.

**Verificación** — la distribución de una O/C debe cuadrar contra su total (diferencia `0.00`):

```sql
WITH oc AS (SELECT id, total FROM public.alm_orden_compra WHERE company_id = 2 AND total > 0 LIMIT 1)
SELECT o.id, o.total,
       (SELECT SUM(monto) FROM public.fn_alm_oc_distribucion_partidas(2, o.id)) AS distribuido,
       o.total - (SELECT SUM(monto) FROM public.fn_alm_oc_distribucion_partidas(2, o.id)) AS diferencia,
       (SELECT count(*) FROM public.fn_alm_oc_distribucion_partidas(2, o.id)
         WHERE con_cuenta_code IS NULL) AS renglones_sin_cuenta
  FROM oc o;
```

`renglones_sin_cuenta > 0` **no es un fallo del script**: son artículos cuyo tipo no tiene
`cuenta_inventario` configurada. Es configuración pendiente que el control reportará al encenderse.

### Paso 3 — Procedimientos (`..._03_procedimientos.sql`)

Los 7 `sp_pst_*` más el helper `fn_pst_aplicar_movimiento`, que es donde vive el
`SELECT … FOR UPDATE` sobre la partida.

```bash
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-27_pst_compromiso_03_procedimientos.sql
```

**¿Ya aplicado?**

```sql
SELECT count(*) FROM pg_proc
 WHERE proname IN ('fn_pst_aplicar_movimiento', 'sp_pst_comprometer_documento',
                   'sp_pst_liberar_compromiso', 'sp_pst_ajustar_compromiso',
                   'sp_pst_devengar_documento', 'sp_pst_revertir_devengo',
                   'sp_pst_registrar_pago', 'sp_pst_revertir_pago');
```

Esperado: `8`.

**Verificación — NO-REGRESIÓN (la más importante de esta tanda).** Con el control apagado no debe
hacer absolutamente nada:

```sql
SELECT count(*) AS avisos FROM public.sp_pst_comprometer_documento(
       2, 'COMPRAS', 'ORDEN_COMPRA', -1, 'PRUEBA', CURRENT_DATE, 'prueba', 'prueba', NULL,
       ARRAY[ROW('00000000000', NULL, NULL, 100)::public.pst_linea_afectacion]);

SELECT count(*) AS movimientos FROM public.pst_movimiento WHERE documento_id = -1;
```

Esperado: **0 avisos y 0 movimientos**. Si sale algo, el control **no** nació apagado: revisar la
semilla de `cfg_presupuesto_control` antes de continuar.

### Paso 4 — Vistas (`..._04_vistas.sql`)

```bash
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-27_pst_compromiso_04_vistas.sql
```

**¿Ya aplicado?**

```sql
SELECT count(*) FROM information_schema.views
 WHERE table_schema = 'public'
   AND table_name IN ('vw_pst_compromiso_saldo', 'vw_pst_ejecucion_presupuestaria',
                      'vw_pst_movimiento_detalle', 'vw_pst_ejecucion_centro_costo');
```

Esperado: `4`.

**Verificación** — la ejecución responde y comprometido/pagado salen en 0:

```sql
SELECT count(*) AS partidas, SUM(presupuesto) AS presupuesto,
       SUM(comprometido) AS comprometido, SUM(ejecutado) AS ejecutado,
       SUM(pagado) AS pagado, SUM(disponible) AS disponible
  FROM public.vw_pst_ejecucion_presupuestaria
 WHERE company_id = 2 AND estado_aprobado;
```

**⚠️ Y la consulta que decide si todo esto servirá** (decisión D1 del diseño): ¿están
presupuestadas las cuentas de inventario contra las que hoy debita el asiento de compra?

```sql
SELECT t.codigo, t.nombre, t.cuenta_inventario,
       pc.allows_budget AS marcada_presupuestable,
       e.presupuesto    AS presupuestada_en
  FROM public.alm_tipo_articulo t
  LEFT JOIN public.con_plan_cuentas pc
         ON pc.company_id = t.company_id
        AND upper(btrim(pc.code)) = upper(btrim(t.cuenta_inventario))
  LEFT JOIN public.vw_pst_ejecucion_presupuestaria e
         ON e.company_id = t.company_id
        AND upper(btrim(e.con_cuenta_code)) = upper(btrim(t.cuenta_inventario))
        AND e.estado_aprobado
 WHERE t.company_id = 2
 ORDER BY t.codigo;
```

Si `marcada_presupuestable` sale `false` o `presupuestada_en` sale `NULL` en todas las filas,
**encender el control contra la cuenta de inventario no bloquearía nada**. Es el riesgo R1 del
diseño; hay que resolverlo con el contador (decisión D1) antes de la fase F7.

### Paso 5 — Proveedores y bancos (`..._05_proveedores_bancos.sql`)

⚠️ **Es el único paso que reemplaza una función que producción ya usa.**

Crea `sp_pst_afectar_valor_real` (nueva) y **reemplaza** `fn_pst_afectar_saldo_real_credito`, que
llama `BanTransaccionesService` en cada partida bancaria con crédito. La firma
`(bigint, bigint, date, numeric)` y los códigos de retorno (0 excede · 1 ok · 2 sin aprobar) **no
cambian**, así que el C# de bancos queda intacto. También amplía el CHECK y el índice de
idempotencia de `pst_movimiento` con los tipos 14 y 15.

**Por qué este paso no es opcional:** el paso 1 cambió la fórmula del disponible a
`proyección − comprometido − ejecutado`, pero la función de bancos recalculaba la cabecera con
`valor_global − Σ valor_real` y la dejaba **inflada** en cuanto hubiera un compromiso de compras.
Este paso lo corrige.

```bash
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-27_pst_compromiso_05_proveedores_bancos.sql
```

**¿Ya aplicado?**

```sql
SELECT count(*) FROM pg_proc WHERE proname = 'sp_pst_afectar_valor_real';
```

Esperado: `1`.

**Verificación — la no-regresión de bancos es lo que hay que mirar:**

```sql
-- a) La firma y el tipo de retorno son los de siempre
SELECT pg_get_function_identity_arguments(oid) AS args, pg_get_function_result(oid) AS res
  FROM pg_proc WHERE proname = 'fn_pst_afectar_saldo_real_credito';

-- b) Una cuenta sin presupuesto sigue devolviendo 1 (no bloquea nada)
SELECT public.fn_pst_afectar_saldo_real_credito(2, -1, CURRENT_DATE, 100);

-- c) El CHECK y el índice admiten los tipos nuevos
SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'ck_pst_movimiento_tipo';
```

⚠️ **Cambio de comportamiento, bajo interruptor.** La validación de proveedores y bancos sigue
midiendo contra `proyección − ejecutado`, igual que hoy, **mientras esos módulos estén en modo 0**,
que es como nacen. Encenderlos hace que también descuenten lo comprometido por las órdenes de
compra: es lo correcto —los dos consumen el mismo presupuesto— pero puede rechazar operaciones que
hoy pasan. No encenderlos en el mismo despliegue.

### Rollback

Cada script trae su bloque de rollback comentado al pie. En orden inverso: 04 → 03 → 02 → 01.
Como el control nace apagado y las tablas nacen vacías, la reversión es un `DROP` de objetos sin
datos. El único punto de atención es restaurar el CHECK de estado:

```sql
ALTER TABLE public.alm_orden_compra DROP CONSTRAINT IF EXISTS ck_alm_orden_compra_estado;
ALTER TABLE public.alm_orden_compra
    ADD CONSTRAINT ck_alm_orden_compra_estado CHECK (estado IN (1, 2, 3, 4, 9));
```

Alternativa sin desplegar nada: dejar los objetos y poner `cfg_presupuesto_control.modo = 0`.
Eso desactiva el control por completo.

## 6. Estado presunto

| Script | Local (`siad_v3_restore`) | `siad_v4` |
|---|:--:|:--:|
| `2026-08-27_pst_compromiso_01_estructura.sql` | ✅ **APLICADO 2026-08-28** | **pendiente** |
| `2026-08-27_pst_compromiso_02_funciones.sql` | ✅ **APLICADO 2026-08-28** | **pendiente** |
| `2026-08-27_pst_compromiso_03_procedimientos.sql` | ✅ **APLICADO 2026-08-28** | **pendiente** |
| `2026-08-27_pst_compromiso_04_vistas.sql` | ✅ **APLICADO 2026-08-28** | **pendiente** |
| `2026-08-27_pst_compromiso_05_proveedores_bancos.sql` | ✅ **APLICADO 2026-08-28** | **pendiente** |

### 6-bis. ✅ Aplicado y verificado en el mirror — 2026-08-28

`psql -v ON_ERROR_STOP=1` sobre `siad_v3_restore` (PostgreSQL 17.7, localhost), **exit 0 en los
cuatro**. Antes de aplicar se comprobó que los 8 prerrequisitos existían, que el presupuesto ya es
multitenant y que **ninguno de los 4 objetos nuevos existía**.

**Estado resultante:** 14 rutinas nuevas, 4 vistas nuevas, 4 filas en `cfg_presupuesto_control`
(empresa 2, **todas en modo 0**), 224 partidas con `valor_comprometido` y `valor_pagado` en 0,
`CHECK (estado = ANY (ARRAY[1,2,3,4,5,6,9]))` y **cero** órdenes fuera del CHECK ampliado.

**Verificaciones ejecutadas contra datos reales:**

| Prueba | Resultado |
|---|---|
| **No-regresión** — control apagado | 0 avisos, 0 movimientos. No hace nada |
| **Inmutabilidad del kardex** | `UPDATE` y `DELETE` fallan con P0001, como se diseñó |
| **Distribución de O/C** — 40 órdenes con total > 0 | Las 40 cuadran contra su total, **diferencia 0.0000**, ningún renglón sin cuenta |
| **Distribución de facturas** — 65 facturas | Las 65 cuadran, peor diferencia **0.00** |
| **★ Equivalencia con el asiento contable** | 56 facturas vigentes con partida: **56 pares, 0 solo en un lado, 0 montos distintos, peor diferencia 0.00** |
| **Ciclo completo** (presupuesto 1000) | Comprometer 897 → disponible 103 · Devengar 500 → comprometido 397, ejecutado 500, **disponible sigue en 103** · Cancelar → libera **397, no 897** → disponible 500 |
| **Rechazo por insuficiencia** | `La orden excede el presupuesto disponible para la cuenta 11401010101. Disponible: 103.00. Requerido: 900.00. Faltan: 797.00.` y **nada se escribe** |
| **Idempotencia** | El reintento de la misma O/C es un no-op: comprometido sigue en 897 |
| **★ Concurrencia (2 sesiones simultáneas)** | Disponible 1000. A compromete 897 y retiene el lock; B pide 900 y **queda bloqueada 4.72 s**; al liberarse el lock B lee el saldo ya actualizado y falla con **error de negocio**, no un 500. **Solo una de las dos órdenes quedó comprometida** |
| **Conciliación de invariantes** | Vacía |

**Paso 5 (F8), aplicado y verificado 2026-08-28** — exit 0. Verificado que la firma de
`fn_pst_afectar_saldo_real_credito` sigue siendo `(bigint, bigint, date, numeric) → integer` y que
una cuenta sin presupuesto sigue devolviendo `1` (no bloquea). CHECK e índice admiten los tipos
14/15. **Regresión de proveedores: los 32 tests preexistentes de OPD** (abonos, contabilidad,
retenciones) **pasan contra el motor nuevo**, más 8 tests nuevos de la ruta del ejecutado, que antes
no tenía ninguno. Las cabeceras de PRE-2025 y PRE-2026 quedan coherentes con la fórmula nueva
(diferencia 0.0000).

Los montajes de prueba corrieron dentro de `BEGIN … ROLLBACK`, salvo el de concurrencia —que
necesita dos sesiones viendo lo mismo— cuyo montaje se limpió explícitamente. **Estado final
verificado: 0 movimientos, 0 compromisos, 0 aplicaciones, control en modo 0, ninguna cuenta
marcada, ningún renglón de O/C tocado, ningún monto sucio.** Las cabeceras de presupuesto quedaron
coherentes (descuadre 0.0000 en PRE-2025 y PRE-2026).

**⚠️ Defecto encontrado y corregido durante la verificación:** el reintento de una aprobación
fallaba con «excede el presupuesto» en vez de ser un no-op, porque la validación corría antes del
`ON CONFLICT` y volvía a comparar el importe completo contra un disponible que ya descontaba ese
mismo compromiso. Se agregó una guarda de idempotencia **a nivel de documento** al inicio de
`sp_pst_comprometer_documento`. El script 03 en el repo ya la incluye y fue reaplicado.

Prerrequisito de presupuesto multitenant (pasos 14, 15 y 18 de
`Database/2026-07-30_pendientes_srv.md`): **pendiente en el SRV**, confirmado en el mirror.
Correr la consulta de prerrequisitos de la §2 al momento de desplegar.

Nunca verificar el estado del SRV conectándose por iniciativa propia: correr la consulta
«¿ya aplicado?» de cada paso al momento de desplegar.

## 7. Después de aplicar: cómo se enciende (NO es parte de esta tanda)

Aplicar los scripts **no enciende nada**. Encender exige tres cosas, en este orden:

1. **Cuentas presupuestables**: `con_plan_cuentas.allows_budget = true` en las cuentas que se
   quieren controlar (se marca a mano en el plan de cuentas del portal).
2. **Presupuesto vigente y aprobado** que cubra esas cuentas en la fecha de operación.
3. **El interruptor**, y primero en advertencia:

```sql
-- Advertencia: registra y deja pasar. Observar un ciclo mensual antes de bloquear.
UPDATE public.cfg_presupuesto_control SET modo = 1
 WHERE company_id = 2 AND modulo IN ('COMPRAS_OC', 'COMPRAS_FACTURA');

-- Solo después de revisar el informe de sobregiros:
-- UPDATE public.cfg_presupuesto_control SET modo = 2 WHERE company_id = 2 AND modulo = 'COMPRAS_OC';
```

⚠️ **No encender antes de la fase F2/F3** (el backend que llama a estos procedimientos). Mientras
el código no los invoque, el modo es irrelevante.

⚠️ **Defecto preexistente a corregir antes de F7:** `ImportPlanCuentasAsync`
(`ContabilidadCatalogosService.cs:1444`) pone `allows_budget = false` en **toda fila que procesa,
también las que actualiza**. Reimportar el plan de cuentas apagaría el control de las 329 cuentas
marcadas.

## 8. Versionado (git)

Archivos nuevos de esta tanda, pendientes de commit:

- `Database/2026-08-27_pst_compromiso_01_estructura.sql`
- `Database/2026-08-27_pst_compromiso_02_funciones.sql`
- `Database/2026-08-27_pst_compromiso_03_procedimientos.sql`
- `Database/2026-08-27_pst_compromiso_04_vistas.sql`
- `Database/2026-08-27_pst_compromiso_05_proveedores_bancos.sql`
- `Database/2026-08-27_runbook_despliegue_srv.md`
- `docs/plans/2026-08-27-presupuesto-compromiso-oc-design.md`

Sigue pendiente de commit, de la tanda anterior:

- `Database/2026-08-22_prv_cxp_unificada.sql`
- `Database/2026-08-22_runbook_despliegue_srv.md`
