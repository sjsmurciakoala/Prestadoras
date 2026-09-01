# Runbook de despliegue a SRV — Cuentas por pagar unificadas (ago 2026)

**Base destino:** `siad_v4` @ `172.16.0.9`
**Fecha:** 2026-08-22
**Alcance:** 1 script, 2 funciones de lectura nuevas. Tanda mínima y aditiva.

> La tanda anterior (`2026-08-20_runbook_despliegue_srv.md`, Fases A/B/C + fix del
> 2026-08-21) quedó **aplicada y cerrada**. Este runbook arranca limpio.
>
> ⚠️ La base **ACTIVA es `siad_v4`**, no `siad_v3` — ver la corrección del runbook anterior.

---

## 1. Qué cubre este runbook

La capa de datos de la pantalla única de **cuentas por pagar** (`/proveedores/cuentas-por-pagar`),
donde las **facturas de compra** y los **compromisos** se ven en un mismo listado:

- `fn_prv_cxp_documentos` — el listado unificado de todos los proveedores, con filtros.
- `fn_prv_cxp_resumen` — los totales del encabezado de esa pantalla.

Las dos son de **lectura**. No hay tablas, columnas, índices ni datos nuevos.

## 2. Antes de empezar (obligatorio)

**Backup de `siad_v4`:**

```bash
pg_dump -h 172.16.0.9 -U postgres -d siad_v4 -Fc -f siad_v4_antes_cxp_unificada.backup
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

**Comprobar los prerrequisitos** — el script se apoya en objetos que deben existir arriba:

```sql
SELECT to_regclass('public.alm_compra_cxp')       AS cxp,
       to_regclass('public.prv_compromiso_hdr')   AS compromisos,
       to_regclass('public.prv_proveedores')      AS proveedores,
       (SELECT count(*) FROM pg_proc
         WHERE proname = 'fn_prv_estado_cuenta_documentos') AS fn_base;
```

Los tres primeros deben devolver el nombre de la tabla (no `NULL`) y `fn_base` debe ser `1`.
Si `fn_base` es `0`, aplicar primero `Database/2026-08-13_prv_estado_cuenta.sql`, que es de
donde este script hereda las reglas de vigencia.

## 3. Advertencias clave (leer antes de aplicar)

- La tanda es **aditiva y re-ejecutable**: solo `CREATE OR REPLACE FUNCTION` con **nombres
  nuevos**. No hay `DROP`, `DELETE`, `TRUNCATE` ni `UPDATE`.
- **No reemplaza nada existente.** `fn_prv_estado_cuenta_*` y `fn_prv_antiguedad_saldos`
  quedan intactas: el estado de cuenta del proveedor y la antigüedad de saldos siguen
  comportándose igual.
- Reversible con `DROP FUNCTION fn_prv_cxp_documentos(...), fn_prv_cxp_resumen(...);`
  (firmas completas en la verificación del paso).
- **El compromiso se devuelve sin vencimiento** (`fecha_vencimiento` y `dias_vencido` en
  `NULL`), porque no tiene plazo propio — decisión D1 del plan. La función base sí devuelve
  su fecha de emisión ahí; el cambio es solo de esta vista.
- Sin `company_id` fijo: la empresa es parámetro. Las verificaciones de abajo usan `2`
  (MERENDON).
- **El portal no necesita esto para arrancar**, pero la pantalla de cuentas por pagar
  unificadas falla si el script no está aplicado. Aplicarlo **antes** de publicar el binario
  que la incluye.

## 4. Orden de aplicación (resumen)

| Paso | Script | Naturaleza | ¿Re-ejecutable? | Depende de |
|---:|---|---|:--:|---|
| 1 | `2026-08-22_prv_cxp_unificada.sql` | Objetos (2 funciones de lectura) | Sí | `2026-08-13_prv_estado_cuenta.sql` |

## 5. Detalle por paso

### Paso 1 — `fn_prv_cxp_documentos` + `fn_prv_cxp_resumen`

Crea las dos funciones de lectura de la pantalla unificada. Corre dentro de su propia
transacción (`BEGIN … COMMIT` dentro del script).

```bash
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-22_prv_cxp_unificada.sql
```

**¿Ya aplicado?**

```sql
SELECT proname, pg_get_function_identity_arguments(oid)
  FROM pg_proc
 WHERE proname IN ('fn_prv_cxp_documentos', 'fn_prv_cxp_resumen');
```

Esperado: **2 filas**. Sin filas = falta aplicar.

**Verificación** (las cinco comprobaciones vienen escritas al pie del propio script):

```sql
-- a) El listado responde y trae las dos ramas
SELECT origen, count(*) AS documentos, sum(saldo) AS saldo
  FROM fn_prv_cxp_documentos(2)
 GROUP BY origen ORDER BY origen;

-- b) Los compromisos salen SIN plazo (D1): con_vencimiento debe ser 0
SELECT count(*) AS compromisos, count(fecha_vencimiento) AS con_vencimiento
  FROM fn_prv_cxp_documentos(2, NULL, 2::SMALLINT);

-- c) El resumen cuadra con sus partes: diferencia = 0
SELECT saldo_total, saldo_compras, saldo_compromisos,
       saldo_total - (saldo_compras + saldo_compromisos) AS diferencia
  FROM fn_prv_cxp_resumen(2);

-- d) Cuadre con el estado de cuenta de un proveedor cualquiera (misma función base):
--    las dos cifras deben ser iguales
WITH uno AS (SELECT cod_proveedor FROM fn_prv_cxp_documentos(2) LIMIT 1)
SELECT (SELECT sum(saldo) FROM fn_prv_cxp_documentos(2, NULL, 0::SMALLINT, NULL,
            (SELECT cod_proveedor FROM uno)))                        AS unificada,
       (SELECT sum(saldo) FROM fn_prv_estado_cuenta_documentos(2,
            (SELECT cod_proveedor FROM uno), NULL, TRUE))            AS estado_cuenta;
```

**Rollback** si hiciera falta:

```sql
DROP FUNCTION IF EXISTS public.fn_prv_cxp_resumen(BIGINT, VARCHAR, SMALLINT, SMALLINT, VARCHAR, BOOLEAN, BOOLEAN);
DROP FUNCTION IF EXISTS public.fn_prv_cxp_documentos(BIGINT, VARCHAR, SMALLINT, SMALLINT, VARCHAR, BOOLEAN, BOOLEAN);
```

(En ese orden: el resumen se apoya en el listado.)

## 6. Estado presunto

| Script | Local (`siad_v3_restore`) | `siad_v4` |
|---|:--:|:--:|
| `2026-08-22_prv_cxp_unificada.sql` | ✅ **APLICADO 2026-08-22** | **pendiente** |

Prerrequisito `2026-08-13_prv_estado_cuenta.sql`: confirmado presente en el mirror (2026-08-22);
en `siad_v4` **sin confirmar** — correr la consulta de prerrequisitos de la §2 al momento de
desplegar.

### 6-bis. ✅ Aplicado en el mirror — 2026-08-22

`psql -v ON_ERROR_STOP=1` sobre `siad_v3_restore`, exit 0: `BEGIN / CREATE FUNCTION / COMMENT /
CREATE FUNCTION / COMMENT / COMMIT`. Antes de aplicar se comprobó que las dos funciones **no
existían** y que la función base sí. Verificación posterior con datos reales de la empresa 2:

```
 origen | documentos |   saldo
--------+------------+-----------
      1 |         63 | 873014.49     (facturas de compra)
      2 |          7 |  45500.00     (compromisos)

 saldo_total | saldo_compras | saldo_compromisos | diferencia | pendientes | vencidos
   918514.49 |     873014.49 |          45500.00 |       0.00 |         70 |       41
```

- **D1 comprobado:** los 7 compromisos salen con `fecha_vencimiento` y `dias_vencido` en NULL
  (`con_vencimiento = 0`, `con_dias = 0`), así que ninguno entra al conteo de vencidos.
- **Cuadre con el estado de cuenta:** diferencia `0.00` en los 5 proveedores comprobados
  (`0001`, `001001`, `001003`, `0087`, `0088`).
- **Tests:** los 21 de `SIAD.Tests/Proveedores/CuentasPorPagarTests.cs` pasan contra el mirror;
  la suite completa queda en **841 correctas / 0 con error / 48 omitidas**.

Nunca verificar el estado del SRV conectándose por iniciativa propia: correr la consulta
«¿ya aplicado?» del paso al momento de desplegar.

## 7. Versionado (git)

Archivos nuevos de esta tanda, pendientes de commit:

- `Database/2026-08-22_prv_cxp_unificada.sql`
- `Database/2026-08-22_runbook_despliegue_srv.md`
