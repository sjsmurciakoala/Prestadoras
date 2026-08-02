# Runbook de despliegue a SRV — tanda proveedores / bancos / almacén (jul 2026)

**Base destino:** `siad_v3` @ `172.16.0.9` (producción, "el servidor de la VPN")
**Rama:** `Cambios_almacen1.0`
**Preparado:** 2026-07-23

---

## 1. Qué cubre este runbook

Orden y guía para aplicar en el **SRV de producción** los scripts SQL de la tanda de
features desarrollada en `Cambios_almacen1.0` que —hasta donde está registrado— ya se
aplicaron en el **mirror `siad_v3_restore` (localhost)** pero **faltan en el SRV**.

Son **20 scripts a aplicar** en orden (pasos 1 a 19, con un 8b intercalado), más
**1 archivo de respaldo que NO se aplica** (es el rollback del paso 6).

> El **código** C#/Blazor de estas features ya está en `main`. Este runbook cubre
> **solo la parte de base de datos**. No incluye el despliegue del binario/portal.

> ⚠️ **No se verificó contra el SRV en vivo** (no me conecto a la BD por iniciativa
> propia). El "estado presunto" de cada paso viene de notas internas y puede estar
> desactualizado. Por eso cada paso trae una consulta **«¿ya aplicado?»**: corréla
> antes de aplicar y decidí. Casi todos los scripts son re-ejecutables sin daño; las
> **excepciones están marcadas con ⚠️**.

---

## 2. Antes de empezar (obligatorio)

- [ ] **Backup del SRV** antes de tocar nada. Referencia: `Database/backup_bd_simple.ps1`.
- [ ] **Confirmar el `company_id` del tenant en el SRV.** Los pasos **9 y 10** asumen
      `company_id = 2`. Si en prod el tenant es otro, **editá esos scripts** antes de correrlos.
- [ ] Tener **psql (cliente 17)** y la cadena de conexión del SRV.
- [ ] Elegir **ventana de bajo uso**: el **paso 6** cambia el saldo que ven cobranza y
      el estado de cuenta (no borra datos, pero cambia el número mostrado).

**Definí la conexión una vez** (ejemplo — poné tus credenciales; no las guardes en el repo):

```powershell
# PowerShell (Windows)
$env:SRV = "postgresql://USUARIO:CLAVE@172.16.0.9:5432/siad_v3"
```
```bash
# bash (si corrés psql desde el servidor)
export SRV="postgresql://USUARIO:CLAVE@172.16.0.9:5432/siad_v3"
```

**Correr cada script parando ante el primer error:**

```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/<script>.sql
```

> En PowerShell, `"$SRV"` es `"$env:SRV"`. Cada script trae su propio `BEGIN … COMMIT`:
> si algo falla, esa transacción se revierte sola y ningún cambio queda a medias.

---

## 3. Advertencias clave (leer antes de aplicar)

| | Advertencia |
|---|---|
| ⚠️ | **El seed de tipos (paso 2) se corre UNA sola vez.** Hace `DELETE FROM alm_tipo_articulo` y re-crea los 9 tipos con IDs nuevos. Re-ejecutarlo **después** de los pasos 3–5 dejaría todos los artículos y categorías **sin tipo** (FK `ON DELETE SET NULL`). Verificá con su query «¿ya aplicado?» antes de correrlo. |
| ⚠️ | **`2026-07-16_backup_sp_obtener_cliente_saldo.sql` NO se aplica.** Es el **rollback** del paso 6 (restaura el SP viejo con el bug de saldo). Guardalo por si hay que revertir. |
| ⚠️ | **El paso 6 cambia saldos visibles.** El SP pasa a sumar movimientos vigentes. Corré la **auditoría** (solo lectura, incluida al final del script) **antes y después** para dimensionar el impacto. En el mirror movió ~20 de 881 clientes, todas explicadas. |
| ⚠️ | **Los pasos 9 y 10 dependen de `company_id = 2`** y de que existan las cuentas contables `11102010301` / `11102010501` en `con_plan_cuentas`. Ajustá si el tenant difiere. |
| ⚠️ | **El paso 5 borra 1 artículo de prueba** ("MANTENIMIENTO PREVENTIVO DE BOMBAS - CONTRATADO") **solo si no tiene ningún movimiento** (guardas `NOT EXISTS`). Es intencional. |
| ⚠️ | **El paso 14 (presupuesto multitenant) va de la mano del binario del portal.** Cambia la firma de `fn_pst_next_id_presupuesto_dtl` (ahora recibe `company_id`): el portal viejo la llama con 1 argumento y **fallaría al agregar detalles de presupuesto** después de aplicar el SQL. Aplicar el script y desplegar el portal **en la misma ventana**. Además el backfill asume `company_id = 2`. |
| ⚠️ | **El paso 19 (soft-delete de artículos) va de la mano del binario del portal.** El código nuevo lee y escribe `alm_articulo.activo`: si desplegás el portal sin aplicar el script, el maestro de artículos falla al consultar. Aplicá el SQL y desplegá el portal en la misma ventana. (Al revés es inocuo: el SQL sin el binario deja la columna sin usar.) |
| ⚠️ | **NO apliques `Database/ddl_v3/20260227_presupuesto_valor_real_triggers.sql` tal cual** en una base que ya tiene el paso 14. Sus `CREATE OR REPLACE` de `fn_pst_aplicar_delta_valor_real` (mono-tenant) y `fn_pst_recalcular_valor_disponible(varchar)` **pisan las versiones company-aware del paso 14** y reintroducen fuga entre empresas. Lo que falta de ese ddl_v3 lo repone el **paso 18** sin tocar esas dos. |

---

## 4. Orden de aplicación (resumen)

| # | Script | Qué hace | Naturaleza | Re-ejecutable |
|---|--------|----------|------------|:---:|
| 1 | `2026-07-15_add_tipo_cuenta_prv_proveedor_cuenta_bancaria.sql` | `prv_proveedor_cuenta_bancaria.tipo_cuenta` (CHEQUES/AHORRO) | Aditivo (columna) | Sí |
| 2 | `2026-07-16_alm_tipo_articulo_seed_desde_lineas.sql` | Reemplaza catálogo de tipos por los 9 grupos | **Datos (DELETE+INSERT)** | ⚠️ **NO** |
| 3 | `2026-07-16_alm_grupo_tipo_articulo_y_limites.sql` | Amplía tipos + `alm_grupo.tipo_articulo_id` + backfill | Aditivo + widening + datos | Sí |
| 4 | `2026-07-16_alm_articulo_backfill_tipo_desde_linea.sql` | Asigna `alm_articulo.tipo_articulo_id` desde el grupo | Datos (UPDATE where NULL) | Sí |
| 5 | `2026-07-16_alm_articulo_saneo_sin_tipo.sql` | Sanea artículos sin tipo (1 update + 1 delete de prueba) | Datos | Sí |
| 6 | `2026-07-16_saldo_vigencia_y_desglose_abono.sql` | Vista `vw_transaccion_abonado_vigente` + SP saldo + tabla desglose | Objetos (vista/SP/tabla) — **cambia saldos** | Sí |
| 7 | `2026-07-17_prv_compromiso_abono.sql` | Tabla `prv_compromiso_abono` (abonos a compromisos) | Aditivo (tabla) | Sí |
| 8 | `2026-07-17_bitacora_maestros.sql` | Tablas `bitacora_maestros` + `bitacora_maestro_config` | Aditivo (2 tablas) | Sí |
| 8b | `2026-07-17_bitacora_maestro_catalogo.sql` | Tabla `bitacora_maestro_catalogo` (catálogo editable de entidades auditables) | Aditivo (1 tabla) | Sí |
| 9 | `2026-07-17_asignar_cuenta_contable_ban_cuenta.sql` | Asigna `ban_cuenta.cont_account_id` por nombre de banco | Datos (UPDATE) — **company 2** | Sí |
| 10 | `2026-07-17_ban_tipo_transaccion_transferencia.sql` | Crea el tipo `TRF` (Transferencia) | Datos (INSERT) — **company 2** | Sí |
| 11 | `2026-07-21_cheques_numeracion_bitacora.sql` | `ban_cuenta.cheque_maximo` + `ban_cheque` + `ban_cheque_bitacora` | Aditivo (columna + 2 tablas) | Sí |
| 12 | `2026-07-23_prv_compromiso_dtl_conceptodtl_1000.sql` | `prv_compromiso_dtl.conceptodtl` 100→1000 | Widening | Sí |
| 13 | `2026-07-23_prv_compromiso_dtl_descripcion_1000.sql` | `prv_compromiso_dtl.descripcion` 150→1000 | Widening | Sí |
| 14 | `2026-07-24_presupuesto_multitenant_company_id.sql` | Multitenant en presupuesto: `company_id` en `pst_config_presupuesto_hdr/dtl`, PKs/FKs compuestas, funciones y vistas por empresa | Estructural + backfill — **company 2** · **con binario** | Sí |
| 15 | `2026-07-24_fix_fn_pst_next_id_dtl_ids_no_numericos.sql` | `fn_pst_next_id_presupuesto_dtl` deja de exigir id numérico (semilla = MAX por empresa+presupuesto) | Objetos (función) — **depende del paso 14** | Sí |
| 16 | `2026-07-27_proveedor_contactos.sql` | Tablas `prv_tipo_contacto` + `prv_proveedor_contacto`, semilla del catálogo y migración del contacto legacy | Aditivo (2 tablas) + datos idempotentes | Sí |
| 17 | `2026-07-27_bitacora_config_contactos.sql` | Da de alta las dos tablas de contactos en el catálogo de auditoría y hereda su config de `prv_proveedor_cuenta_bancaria` | Datos idempotentes — **depende de los pasos 8 y 16** | Sí |
| 18 | `2026-07-28_presupuesto_completar_ddl_valor_real.sql` | Repone las piezas faltantes del mecanismo de valor_real de presupuesto: `fn_pst_resolver_cuenta_code`, `fn_pst_resolver_poliza_fecha`, `fn_pst_aplicar_delta_por_poliza` y el procedimiento `sp_pst_aplicar_partida_presupuesto` | Objetos (funciones + procedimiento) — **depende del paso 14** | Sí |
| 19 | `2026-07-29_alm_articulo_activo.sql` | `alm_articulo.activo` (soft-delete del maestro de artículos) + índice parcial | Aditivo (columna + índice) · **con binario** | Sí |
| — | `2026-07-16_backup_sp_obtener_cliente_saldo.sql` | **NO aplicar** — rollback del paso 6 | Respaldo | — |

> **Sin dependencias cruzadas duras entre bloques:** todas las FK apuntan a tablas que
> ya existen en prod. Los órdenes estrictos son: **interno del bloque almacén
> (pasos 2→3→4→5)**, **14→15**, **14→18**, **8 + 8b + 16 → 17** (el 17 inserta filas en las tablas
> que crean el 8/8b y describe las tablas que crea el 16), y **no aplicar el backup del
> paso 6**. El resto podría reordenarse, pero seguir el orden de arriba es lo más seguro.

---

## 5. Detalle por paso

> Convención: primero corré la query **«¿ya aplicado?»**. Si ya está, saltá el paso.
> Si no, aplicá el script y corré la **verificación**.

### Paso 1 — `prv_proveedor_cuenta_bancaria.tipo_cuenta`
Columna para distinguir cuenta del proveedor CHEQUES/AHORRO (la usa el pago por
transferencia/cheque). Aditivo, no toca datos.

```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-07-15_add_tipo_cuenta_prv_proveedor_cuenta_bancaria.sql
```
¿Ya aplicado?
```sql
SELECT column_name, character_maximum_length
  FROM information_schema.columns
 WHERE table_name='prv_proveedor_cuenta_bancaria' AND column_name='tipo_cuenta';
-- 1 fila (varchar/20) = ya aplicado.
```

### Paso 2 — Seed de tipos de artículo ⚠️ UNA SOLA VEZ
Vacía `alm_tipo_articulo` (4 genéricos) y siembra los 9 grupos desde `alm_linea`.

**Verificá PRIMERO que no esté ya sembrado** (si ya lo está, **NO** lo corras: borraría
las asignaciones de los pasos 3–5):
```sql
SELECT codigo, nombre FROM alm_tipo_articulo ORDER BY company_id, codigo;
-- Códigos 01..09 con nombres de grupos  -> YA sembrado, SALTAR este paso.
-- Filas genéricas (Operativo/Mantenimiento/Consumo/Servicios) -> aún NO, aplicar.
```
```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-07-16_alm_tipo_articulo_seed_desde_lineas.sql
```

### Paso 3 — Límites de tipo + `alm_grupo.tipo_articulo_id`
Amplía `nombre` (→100) y las 5 cuentas contables (→25), re-sincroniza nombres, agrega
`alm_grupo.tipo_articulo_id` (FK, `ON DELETE SET NULL`) + índice + backfill de categorías.
```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-07-16_alm_grupo_tipo_articulo_y_limites.sql
```
¿Ya aplicado?
```sql
SELECT
  (SELECT character_maximum_length FROM information_schema.columns
     WHERE table_name='alm_tipo_articulo' AND column_name='nombre')                AS nombre_len,   -- 100 = aplicado
  (SELECT count(*) FROM information_schema.columns
     WHERE table_name='alm_grupo' AND column_name='tipo_articulo_id')              AS tiene_col;    -- 1  = aplicado
```

### Paso 4 — Backfill de `alm_articulo.tipo_articulo_id`
Asigna a cada artículo el tipo cuyo código coincide con el de su grupo (`linea_id`).
Solo toca filas `tipo_articulo_id IS NULL` (no pisa asignaciones manuales).
```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-07-16_alm_articulo_backfill_tipo_desde_linea.sql
```
¿Ya aplicado? (cuántos artículos CON grupo siguen sin tipo — debería ser ~0)
```sql
SELECT count(*) AS con_grupo_sin_tipo
  FROM alm_articulo
 WHERE tipo_articulo_id IS NULL AND linea_id IS NOT NULL;
```

### Paso 5 — Saneo de artículos sin tipo
Clasifica la válvula de prueba en el tipo 01 y elimina 1 artículo de prueba **sin
movimientos** (guardas `NOT EXISTS`). Los legacy (DISPONIBLE, 0032, 0037) se dejan
sin tipo a propósito.
```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-07-16_alm_articulo_saneo_sin_tipo.sql
```
Verificación (deben quedar solo los legacy sin tipo):
```sql
SELECT id, company_id, codigo_articulo, descripcion
  FROM alm_articulo WHERE tipo_articulo_id IS NULL ORDER BY company_id, id;
```

### Paso 6 — Saldo por vigencia + desglose ⚠️ CAMBIA SALDOS
Crea `vw_transaccion_abonado_vigente`, reemplaza `sp_obtener_cliente_saldo(bigint,varchar)`
(pasa a `SUM(débitos − créditos)` de los vigentes) y crea `adm_desglose_abono_porcentaje`.
**No modifica datos**, pero cambia el saldo calculado.

**Auditoría (solo lectura) ANTES:** descomentá y corré el bloque `WITH … ` del final del
`.sql` para comparar saldo viejo vs nuevo por cliente.
```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-07-16_saldo_vigencia_y_desglose_abono.sql
```
¿Ya aplicado?
```sql
SELECT to_regclass('public.vw_transaccion_abonado_vigente') AS vista,
       to_regclass('public.adm_desglose_abono_porcentaje')  AS tabla_desglose;
-- ambas NOT NULL = aplicado.
```
> **Rollback** de solo el SP: `Database/2026-07-16_backup_sp_obtener_cliente_saldo.sql`
> (la vista y la tabla son aditivas, no hace falta revertirlas).

### Paso 7 — `prv_compromiso_abono` (abonos a compromisos)
Libro de abonos parciales. Saldo derivado (`hdr.monto − SUM(vigentes)`). FK a
`prv_compromiso_hdr`. Aditivo.
```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-07-17_prv_compromiso_abono.sql
```
¿Ya aplicado?
```sql
SELECT to_regclass('public.prv_compromiso_abono');   -- NOT NULL = aplicado
```

### Paso 8 — Bitácora de maestros
Crea `bitacora_maestros` (auditoría append-only) y `bitacora_maestro_config` (qué se
audita por empresa). Ambas con `IF NOT EXISTS` → seguro aunque una ya exista.
```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-07-17_bitacora_maestros.sql
```
¿Ya aplicado?
```sql
SELECT to_regclass('public.bitacora_maestros')       AS log,
       to_regclass('public.bitacora_maestro_config') AS config;   -- ambas NOT NULL = aplicado
```

### Paso 8b — Catálogo de entidades auditables
Crea `bitacora_maestro_catalogo`: la lista de maestros auditables por empresa, editable
desde Configuración > Auditoría. Estaba **sin registrar en este runbook** (se detectó al
preparar el paso 17, que inserta en esa tabla). Sin ella, la pantalla de configuración de
auditoría no tiene de dónde leer y el paso 17 falla. Solo estructura vacía: la semilla
inicial la escribe el backend por empresa la primera vez que se abre la pantalla.
```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-07-17_bitacora_maestro_catalogo.sql
```
¿Ya aplicado?
```sql
SELECT to_regclass('public.bitacora_maestro_catalogo');   -- NOT NULL = aplicado
```

### Paso 9 — Cuenta contable de cuentas bancarias ⚠️ company 2
Rellena `ban_cuenta.cont_account_id` (hoy NULL) emparejando por nombre de banco
(Occidente→`11102010301`, Trabajadores→`11102010501`). Solo donde está NULL.
```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-07-17_asignar_cuenta_contable_ban_cuenta.sql
```
¿Ya aplicado? / Verificación:
```sql
SELECT banco_nombre, count(*) AS cuentas, count(cont_account_id) AS con_contable
  FROM ban_cuenta WHERE company_id=2 AND activo=TRUE
 GROUP BY banco_nombre ORDER BY banco_nombre;
-- Occidente y Trabajadores con con_contable = cuentas = ya aplicado.
```

### Paso 10 — Tipo de transacción `TRF` (Transferencia) ⚠️ company 2
Inserta la fila `TRF · Transferencia` (salida, activa, no emite cheque) que resuelve el
pago a proveedores por transferencia. `WHERE NOT EXISTS`.
```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-07-17_ban_tipo_transaccion_transferencia.sql
```
¿Ya aplicado?
```sql
SELECT tipo_transaccion, nombre, entra_sale, estado
  FROM ban_tipos_transacciones
 WHERE company_id=2 AND (upper(btrim(tipo_transaccion))='TRF' OR nombre ILIKE '%transferencia%');
-- 1 fila = aplicado.
```

### Paso 11 — Numeración de cheques + bitácora
`ban_cuenta.cheque_maximo` + tablas `ban_cheque` (libro) y `ban_cheque_bitacora`
(eventos append-only). FK compuestas tenant-safe. Aditivo.
```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-07-21_cheques_numeracion_bitacora.sql
```
¿Ya aplicado?
```sql
SELECT to_regclass('public.ban_cheque')          AS libro,
       to_regclass('public.ban_cheque_bitacora') AS eventos,
       (SELECT count(*) FROM information_schema.columns
          WHERE table_name='ban_cuenta' AND column_name='cheque_maximo') AS col_max;
-- libro/eventos NOT NULL y col_max=1 = aplicado.
```

### Paso 12 — `prv_compromiso_dtl.conceptodtl` 100→1000
Widening; no reescribe la tabla ni altera datos. Idempotente (solo aplica si <1000).
```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-07-23_prv_compromiso_dtl_conceptodtl_1000.sql
```

### Paso 13 — `prv_compromiso_dtl.descripcion` 150→1000
Igual que el anterior, sobre `descripcion`.
```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-07-23_prv_compromiso_dtl_descripcion_1000.sql
```
¿Ya aplicados (12 y 13)?
```sql
SELECT column_name, character_maximum_length
  FROM information_schema.columns
 WHERE table_name='prv_compromiso_dtl' AND column_name IN ('conceptodtl','descripcion');
-- ambas con 1000 = aplicados.
```

### Paso 14 — Presupuesto multitenant ⚠️ company 2 · junto con el binario
Agrega `company_id` a `pst_config_presupuesto_hdr/dtl` (backfill a la empresa 2),
convierte las PKs en compuestas por empresa, recrea las 7 FKs de detalle/actividad/
solicitud como compuestas, hace las funciones `fn_pst_next_id_presupuesto_dtl`,
`fn_pst_recalcular_valor_disponible`, `fn_pst_aplicar_delta_valor_real` y
`fn_pst_afectar_saldo_real_credito` company-aware, y recrea las vistas
`view_lista_configuracion_presupuesto` y `vw_pst_gestion_actividad_presupuesto`
filtradas por empresa. No borra ni modifica filas de negocio. Todo en una sola
transacción (`BEGIN … COMMIT`); re-ejecutable.

**Prerequisitos:** las tablas `pst_*` del módulo ya deben existir (ddl_v3 de feb–mar 2026,
presuntamente ya en SRV) y el tenant debe ser `company_id = 2` (si no, editar el script).
**Desplegar el portal en la misma ventana** (la firma nueva de
`fn_pst_next_id_presupuesto_dtl` requiere el binario nuevo, y viceversa).
```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-07-24_presupuesto_multitenant_company_id.sql
```
¿Ya aplicado?
```sql
SELECT count(*) AS pk_con_company
  FROM information_schema.key_column_usage
 WHERE constraint_name='pk_pst_config_presupuesto_hdr' AND column_name='company_id';
-- 1 = ya aplicado.
```
Verificación posterior:
```sql
SELECT (SELECT count(*) FROM pst_config_presupuesto_hdr WHERE company_id IS NULL) AS hdr_sin_empresa,
       (SELECT count(*) FROM pst_config_presupuesto_dtl WHERE company_id IS NULL) AS dtl_sin_empresa,
       (SELECT count(*) FROM view_lista_configuracion_presupuesto)                AS vista_lista,
       pg_get_function_identity_arguments('public.fn_pst_next_id_presupuesto_dtl'::regproc) AS firma_correlativo;
-- 0 / 0 / (= filas de dtl) / 'p_company_id bigint, p_id_presupuesto character varying'
```

### Paso 15 — `fn_pst_next_id_presupuesto_dtl` acepta ids no numéricos · depende del paso 14
La versión del paso 14 exige `id_presupuesto` numérico para derivar la semilla del
correlativo ('60000' → 60001…), pero los datos reales usan 'PRE-2025' / 'PRE-2026'
(correlativos 1–224): agregar un detalle nuevo lanzaba excepción (bug pre-existente
al multitenant). Redefine solo la función (misma firma): semilla = `GREATEST(MAX
por empresa+presupuesto, valor numérico del id si lo es, 0)`. No toca tablas ni datos.
**Aplicar inmediatamente después del paso 14** (necesita la firma con `company_id`).
```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-07-24_fix_fn_pst_next_id_dtl_ids_no_numericos.sql
```
¿Ya aplicado? (si falla con «no es numerico», falta aplicar):
```sql
SELECT public.fn_pst_next_id_presupuesto_dtl(2::bigint, 'PRE-2026'::varchar);
-- devuelve MAX del presupuesto + 1 (225 con los 224 correlativos actuales) = aplicado.
```

### Paso 16 — Contactos de proveedor + catálogo de tipos de contacto
Crea `prv_tipo_contacto` (catálogo por empresa, nombre único sin distinguir
mayúsculas ni espacios) y `prv_proveedor_contacto` (N contactos por proveedor, con
`company_id` propio porque `cod_proveedor` se repite entre empresas). Además siembra
5 tipos por cada empresa que tenga proveedores (Ventas, Cobros, Gerencia, Soporte
técnico, Administración) y migra el contacto que hoy vive en las columnas sueltas
`prv_proveedores.nombre_contacto/telefono/email` como contacto de `orden = 1`.

**No toca ninguna tabla existente**: las columnas legacy se conservan y el servicio
las mantiene sincronizadas con el contacto #1. Los dos `INSERT` están guardados con
`NOT EXISTS`, así que el script es re-ejecutable de punta a punta.

**Independiente del binario:** el portal viejo no conoce estas tablas y el servicio
nuevo consulta con guardas `TableExistsAsync`, así que el SQL puede aplicarse antes
del despliegue del portal sin romper nada (aunque hasta que suba el binario los
contactos no se ven en la app).
```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-07-27_proveedor_contactos.sql
```
¿Ya aplicado?
```sql
SELECT to_regclass('public.prv_tipo_contacto')       AS catalogo,
       to_regclass('public.prv_proveedor_contacto')  AS contactos;
-- ambas NOT NULL = aplicado.
```
Verificación posterior (semilla y migración):
```sql
SELECT company_id, count(*) AS tipos FROM prv_tipo_contacto GROUP BY 1 ORDER BY 1;
-- 5 por cada empresa con proveedores.

SELECT (SELECT count(*) FROM prv_proveedor_contacto
         WHERE usuario_creo = 'migracion')                                        AS contactos_migrados,
       (SELECT count(*) FROM prv_proveedores
         WHERE btrim(COALESCE(nombre_contacto,'')) <> '')                         AS proveedores_con_contacto;
-- los dos números deben coincidir. El filtro usuario_creo='migracion' cuenta SOLO lo que
-- trajo el script: sin él, el primer contacto que capture un usuario desde la app rompería
-- la igualdad de forma legítima y el check dejaría de servir.
-- Si "contactos_migrados" sale mayor, hay (company_id, cod_proveedor) repetidos en
-- prv_proveedores (no tiene PK); detectarlos con GROUP BY 1,2 HAVING count(*)>1.
```
> El `.sql` trae al final el bloque completo de verificación comentado (columnas,
> constraints, índices, idempotencia y los dos INSERT que deben fallar).

### Paso 17 — Auditoría de las tablas de contactos · depende de los pasos 8, 8b y 16
Da de alta `prv_proveedor_contacto` y `prv_tipo_contacto` en `bitacora_maestro_catalogo`
(una fila por empresa que ya tenga catálogo) y les crea la fila de
`bitacora_maestro_config` **heredando los flags de `prv_proveedor_cuenta_bancaria`** en
esa misma empresa — la tabla hermana del proveedor. Si esa empresa no audita las cuentas
bancarias, no se enciende nada: la entrada de catálogo alcanza para prenderlo a mano
desde Configuración > Auditoría.

**Por qué hace falta:** las dos tablas ya están en la lista blanca de código
(`AuditableMaestros`), pero el auto-seed del catálogo solo corre cuando la tabla está
**vacía** para esa empresa. En una base que ya abrió la pantalla de auditoría, la entrada
nueva no aparece sola.

**Solo datos**, `INSERT … WHERE NOT EXISTS` en ambos casos → re-ejecutable.
```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-07-27_bitacora_config_contactos.sql
```
¿Ya aplicado?
```sql
SELECT count(*) AS filas_catalogo
  FROM public.bitacora_maestro_catalogo
 WHERE tabla IN ('prv_proveedor_contacto','prv_tipo_contacto');
-- 2 por cada empresa con catálogo = aplicado.
```
Verificación posterior (la config heredada debe coincidir con la de la hermana):
```sql
SELECT company_id, entidad, habilitado, audita_crear, audita_editar, audita_eliminar
  FROM public.bitacora_maestro_config
 WHERE entidad IN ('prv_proveedor_cuenta_bancaria','prv_proveedor_contacto','prv_tipo_contacto')
 ORDER BY company_id, entidad;
```

### Paso 18 — Completar el mecanismo de valor_real de presupuesto · depende del paso 14
Crea las 4 piezas que el ddl_v3 `20260227_presupuesto_valor_real_triggers.sql` define pero que
**nunca se aplicaron** (verificado 2026-07-28: no existen ni en el mirror ni en desarrollo):
`fn_pst_resolver_cuenta_code`, `fn_pst_resolver_poliza_fecha`, `fn_pst_aplicar_delta_por_poliza`
y el procedimiento `sp_pst_aplicar_partida_presupuesto` (lo invoca
`OrdenesPagoDirectoService.ApplyPresupuestoPartidaAsync`, que hoy lanzaría excepción si el SP
falta — aunque ese método aún no se cablea). El paso 14 se escribió asumiendo ese ddl_v3: dejó
`fn_pst_aplicar_delta_valor_real` llamando a `fn_pst_resolver_cuenta_code` (inexistente). Solo
objetos de código; no toca tablas ni datos. Re-ejecutable.

> ⚠️ **NO uses el ddl_v3 `20260227_presupuesto_valor_real_triggers.sql` para esto.** Su
> `CREATE OR REPLACE` de `fn_pst_aplicar_delta_valor_real` (mono-tenant) y
> `fn_pst_recalcular_valor_disponible(varchar)` **pisaría las company-aware del paso 14** y
> reintroduciría fuga entre empresas. Este paso 18 trae **solo** lo faltante.

**Prerrequisitos:** paso 14 aplicado (firma company-aware de `fn_pst_aplicar_delta_valor_real`)
y el tipo `public.tipo_linea_partida` debe existir (el script aborta con mensaje claro si falta).
```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-07-28_presupuesto_completar_ddl_valor_real.sql
```
¿Ya aplicado? (las 4 NOT NULL = aplicado):
```sql
SELECT to_regprocedure('public.fn_pst_resolver_cuenta_code(bigint,bigint)')                          AS resolver_cuenta,
       to_regprocedure('public.fn_pst_resolver_poliza_fecha(text,bigint,bigint)')                    AS resolver_fecha,
       to_regprocedure('public.fn_pst_aplicar_delta_por_poliza(text,bigint,bigint,bigint,numeric)')  AS delta_por_poliza,
       to_regprocedure('public.sp_pst_aplicar_partida_presupuesto(bigint,date,public.tipo_linea_partida[])') AS sp_partida;
```
Verificación posterior (el paso 14 NO debe haberse tocado — cada función, una sola fila con firma company-aware):
```sql
SELECT p.proname, pg_get_function_identity_arguments(p.oid) AS firma
  FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
 WHERE n.nspname='public'
   AND p.proname IN ('fn_pst_aplicar_delta_valor_real','fn_pst_recalcular_valor_disponible')
 ORDER BY p.proname;
-- aplicar_delta_valor_real:     p_company_id bigint, p_account_id bigint, p_poliza_date date, p_delta numeric
-- recalcular_valor_disponible:  p_company_id bigint, p_id_presupuesto character varying
```

### Paso 19 — `alm_articulo.activo` (soft-delete del maestro de artículos)
Agrega la columna de soft-delete al artículo, que era el único catálogo de Almacén que
todavía borraba **físicamente**. Sus hermanas (`alm_bodega`, `alm_tipo_articulo`,
`alm_grupo`, `alm_unidad_medida`, `alm_articulo_bodega`, `alm_articulo_proveedor`) ya usan
`activo`. Desde este cambio el artículo se **descontinúa**: deja de ofrecerse para
documentos nuevos, pero se conserva y su kardex sigue consultable. Aditivo e idempotente
(`ADD COLUMN IF NOT EXISTS` + `CREATE INDEX IF NOT EXISTS`), no toca datos: el
`DEFAULT true` deja todos los artículos existentes activos.

> ⚠️ **Va de la mano del binario del portal.** El código nuevo (`ArticulosService`,
> `SiadDbContext.Almacen.cs`) **lee y escribe `alm_articulo.activo`**: si se despliega el
> portal sin aplicar este script, el maestro de artículos falla al consultar. Aplicar el SQL
> y desplegar el portal **en la misma ventana**. Al revés es inocuo (el SQL sin el binario
> no rompe nada: la columna queda sin usar).

> **No requiere DDL** para las otras dos piezas de la misma tanda: la **concurrencia
> optimista** usa la columna de sistema `xmin` (ya existe en toda tabla; solo se mapea en EF)
> y la **auditoría** ya estaba lista (`alm_articulo` está en `AuditableMaestros` y el
> interceptor interpreta `activo` true→false como "Eliminación").

```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-07-29_alm_articulo_activo.sql
```
¿Ya aplicado?
```sql
SELECT count(*) AS tiene_col
  FROM information_schema.columns
 WHERE table_name='alm_articulo' AND column_name='activo';
-- 1 = ya aplicado.
```
Verificación posterior (nadie debió quedar descontinuado por la migración):
```sql
SELECT count(*) AS total,
       count(*) FILTER (WHERE activo)     AS activos,
       count(*) FILTER (WHERE NOT activo) AS descontinuados  -- debe ser 0
  FROM public.alm_articulo;

SELECT indexname FROM pg_indexes
 WHERE tablename='alm_articulo' AND indexname='ix_alm_articulo_company_activo';  -- 1 fila
```

---

## 6. Después de aplicar todo

- [ ] Correr las verificaciones «¿ya aplicado?» de cada paso una última vez.
- [ ] **Smoke logueado en la app** (contra el SRV): registrar un abono a un compromiso,
      emitir un cheque, ver `/bancos/cheques`, ver la bitácora de maestros, y confirmar
      un saldo de cliente en el estado de cuenta.
- [ ] **No** apuntar la suite de tests (`SIAD_TEST_DB`) al SRV: los tests corren en el
      **mirror**. Aunque envuelven todo en `BEGIN … ROLLBACK`, prod no es el lugar.

---

## 7. Estado presunto en el SRV (referencia — verificar en vivo)

Según notas internas al 2026-07-23 (point-in-time, **no verificado contra el SRV**):

- **Bloque almacén (pasos 2–5):** las fases previas (seed/límites/backfill) podrían **ya
  estar** en el SRV; el **saneo (paso 5)** figura como pendiente. **Verificar con las
  queries** — sobre todo antes del paso 2 (destructivo si se repite).
- **Pasos 1, 6, 7, 8, 9, 10, 11:** aplicados en el mirror, **pendientes en el SRV**.
  El paso 8 podría estar **parcial** (faltaría `bitacora_maestro_config`); re-aplicarlo
  es seguro (`IF NOT EXISTS`).
- **Pasos 12 y 13:** creados el 2026-07-23; probablemente **ni en el mirror**.
- **Paso 14:** creado y **aplicado en el mirror el 2026-07-24** (verificado: PKs/FKs compuestas,
  backfill 2 hdr + 224 dtl a empresa 2, 22/22 tests de Presupuesto en verde contra el mirror).
  **Pendiente en el SRV.** En el mirror no existían `view_lista_configuracion_presupuesto` ni
  `fn_pst_recalcular_valor_disponible(varchar)` (el script los crea igual, avisa con NOTICE).
- **Paso 15:** creado y **aplicado en el mirror el 2026-07-24** (verificado: PRE-2026 → 225,
  PRE-2025 → 115, '60000' → 60001; INSERT real de un detalle a PRE-2026 con correlativo 225
  y avance a 226, revertido con ROLLBACK — el mirror quedó con sus 224 correlativos).
  **Pendiente en el SRV.**
- **Paso 8b:** el `.sql` del catálogo existe desde el 2026-07-17 y está aplicado en el
  **mirror** (lo usa la pantalla de auditoría). En el SRV, **presuntamente pendiente** —
  igual que el paso 8, con el que va de la mano.
- **Paso 16:** creado el 2026-07-27 y **aplicado en el mirror `siad_v3_restore`** ese mismo
  día (verificado indirectamente: los tests de integración de contactos corren en verde
  contra el mirror, lo que exige que ambas tablas y la semilla de 5 tipos existan).
  **Pendiente en el SRV.** Va junto con el código de contactos de proveedor.
- **Paso 17:** creado el 2026-07-27. **Sin aplicar en ningún lado** — ni mirror ni SRV.
- **Paso 18:** creado el 2026-07-28 y **aplicado ese día en el mirror `siad_v3_restore` y en
  `siad_v3_desarrollo`** (verificado: las 4 piezas existen y el paso 14 quedó intacto, sin
  versiones mono-tenant duplicadas). **Pendiente en el SRV.** Nace de verificar que las 4 piezas
  del ddl_v3 de valor_real (`resolver_cuenta_code`, `resolver_poliza_fecha`, `aplicar_delta_por_poliza`,
  `sp_pst_aplicar_partida_presupuesto`) faltaban en mirror y desarrollo mientras el paso 14 ya las
  da por hechas. Repone solo lo faltante sin pisar el paso 14. Va después del paso 14.

- **Paso 19:** creado el 2026-07-29 y **aplicado ese día en el mirror `siad_v3_restore`**
  (verificado: columna `activo` NOT NULL DEFAULT true, 634 artículos todos activos y 0
  descontinuados, índice `ix_alm_articulo_company_activo` creado; los 35 tests de Almacén y
  13 de Auditoría corren en verde contra el mirror). **Pendiente en el SRV.** Va junto con el
  binario del portal que introduce el soft-delete del maestro de artículos. No tiene
  dependencias con otros pasos de esta tanda.

## 8. Nota de versionado (git)

A la fecha, estos 3 scripts + este runbook están **sin commitear** (untracked) en la rama:

- `Database/2026-07-21_cheques_numeracion_bitacora.sql`
- `Database/2026-07-23_prv_compromiso_dtl_conceptodtl_1000.sql`
- `Database/2026-07-23_prv_compromiso_dtl_descripcion_1000.sql`
- `Database/2026-07-24_presupuesto_multitenant_company_id.sql`
- `Database/2026-07-24_fix_fn_pst_next_id_dtl_ids_no_numericos.sql`
- `Database/2026-07-27_proveedor_contactos.sql`
- `Database/2026-07-27_bitacora_config_contactos.sql`
- `Database/2026-07-28_presupuesto_completar_ddl_valor_real.sql`
- `Database/2026-07-29_alm_articulo_activo.sql`
- `Database/2026-07-23_runbook_despliegue_srv.md` (este archivo)

Conviene versionarlos para que queden reflejados junto al resto de la tanda. **Vos
decidís cuándo** hacer commit/push.
