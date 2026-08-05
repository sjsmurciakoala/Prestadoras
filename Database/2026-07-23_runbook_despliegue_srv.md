# Runbook de despliegue a SRV — tanda proveedores / bancos / almacén (jul 2026)

**Base destino:** `siad_v3` @ `172.16.0.9` (producción, "el servidor de la VPN")
**Rama:** `Cambios_almacen1.0`
**Preparado:** 2026-07-23

---

## 1. Qué cubre este runbook

Orden y guía para aplicar en el **SRV de producción** los scripts SQL de la tanda de
features desarrollada en `Cambios_almacen1.0` que —hasta donde está registrado— ya se
aplicaron en el **mirror `siad_v3_restore` (localhost)** pero **faltan en el SRV**.

Son **24 scripts a aplicar** en orden (pasos 1 a 23, con un 8b y un 21b intercalados), más
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
| ⚠️ | **El paso 20 (ISV por tipo de artículo) depende de `cfg_impuesto_tasa`**, del catálogo del ISV (`2026-07-14_cfg_impuestos.sql`) que **no figura en este runbook** y cuyo estado en el SRV no consta. El script del paso 20 **se detiene con mensaje claro** si esa tabla falta: aplicá/verificá el catálogo del ISV primero. Va con el binario del portal (el código nuevo lee/escribe la columna). |
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
| 20 | `2026-07-30_alm_tipo_articulo_impuesto_tasa.sql` | `alm_tipo_articulo.impuesto_tasa_id` (FK a `cfg_impuesto_tasa`) + índice: ISV de compras por tipo | Aditivo (columna + FK + índice) · **con binario** · **depende de `cfg_impuesto_tasa`** | Sí |
| 21 | `2026-07-30_alm_carga_inicial.sql` | `alm_config_inventario` + `alm_ajuste_inventario` + `REVERSA` en el CHECK de `documento_tipo` + 2 CHECK `NOT VALID` + 2 índices parciales del kardex | Aditivo (2 tablas + constraints) · **con binario** · **⚠️ exige la infra de kardex de jul** | Sí |
| 21b | *(mismo script, paso aparte)* | `VALIDATE CONSTRAINT` de `ck_alm_kardex_libro_nuevo` y `ck_alm_kardex_fecha_si_uuid` | Validación (escanea `alm_kardex`) | Sí |
| 22 | `2026-07-30_cfg_compra_isv.sql` | Tabla `cfg_compra_isv` (tratamiento del ISV en compras por empresa: COSTO/FISCAL) + semilla | Aditivo (tabla) · **con binario** · **independiente** | Sí |
| 23 | `2026-07-30_alm_orden_compra.sql` | `alm_orden_compra` + `alm_orden_compra_detalle` + `alm_orden_compra_correlativo` + `alm_compra.orden_compra_detalle_id` (módulo de órdenes de compra) | Aditivo (3 tablas + columna + FK) · **con binario** · **independiente** | Sí |
| 24 | `2026-07-31_alm_articulo_bodega_mover_a_bodega_01.sql` | Muda las 634 filas de `alm_articulo_bodega` de la bodega `PRIN` a la `01`, donde vive el kardex histórico, y resuelve 1 colisión del índice único | **Datos destructivo/one-shot** (UPDATE masivo + 1 DELETE) · **⚠️ NO re-ejecutable a ciegas** · **antes del corte de inventario** | Sí |
| 25 | `2026-07-31_alm_compra_recepcion.sql` | `alm_compra_hdr` (cabecera de la factura de proveedor) + `alm_compra_correlativo` + `alm_compra.compra_hdr_id` (captura de recepción de compras) | Aditivo (2 tablas + columna + 2 FK) · **con binario** · **depende del paso 23** | Sí |
| 26 | `2026-08-01_alm_requisicion_descargo.sql` | `alm_requisicion_hdr` + `alm_descargo_hdr` + 2 correlativos sembrados en 17124 + columnas de parcialidad y enlace en las dos tablas planas + `DROP` de `ix_alm_requisicion_pendiente` | Aditivo (4 tablas + 5 columnas + 6 FK) **con backfill** y **1 DROP INDEX deliberado** · **con binario** · **⚠️ exige el script de documentos (§3.1b de pendientes)** | Sí |
| 27 | `2026-08-01_alm_tipo_movimiento.sql` | `alm_tipo_movimiento`: catálogo de tipos de movimiento de almacén + semilla de **los 12 tipos reales importados de `INV_TIPOSTRANSACC` de MERENDON** (2026-08-03) | Aditivo (1 tabla + índice) + ⚠️ **DELETE acotado** de la semilla genérica anterior + semilla idempotente · **con binario** · **independiente** | Sí |
| 28 | `2026-08-03_alm_movimiento.sql` | Documento de movimiento de almacén: `alm_movimiento_hdr` + `alm_movimiento_dtl` + `alm_movimiento_correlativo` (entradas y salidas manuales, equivalente de `dlgTransaccionesGenericasINV`) | Aditivo (3 tablas + índices) + siembra del correlativo · **con binario** · **depende del paso 27** | Sí |
| 29 | `2026-08-04_alm_traslado.sql` | Traslado entre bodegas (Fase 5): columnas de traslado en `alm_movimiento_hdr`/`_dtl`, 2 tablas nuevas `alm_traslado_recepcion`/`_dtl`, widening de 2 CHECK (estado→1/2/3/9; clase→+TRASLADO), CHECK `existencia_transito>=0` y semilla del tipo `TRF` | Aditivo (2 tablas + 6 columnas + 4 FK) + **widening de 2 CHECK** + semilla idempotente · **con binario** · **depende de los pasos 27 y 28** | Sí |
| — | `2026-07-16_backup_sp_obtener_cliente_saldo.sql` | **NO aplicar** — rollback del paso 6 | Respaldo | — |

> **Casi sin dependencias cruzadas duras entre bloques:** salvo el paso 20, todas las FK
> apuntan a tablas que ya existen en prod. **El paso 20 es la excepción:** su FK apunta a
> `cfg_impuesto_tasa` (catálogo del ISV, `2026-07-14_cfg_impuestos.sql`), que **no está en
> esta tanda** — aplicá ese catálogo antes. Los órdenes estrictos son: **interno del bloque
> almacén (pasos 2→3→4→5)**, **14→15**, **14→18**, **8 + 8b + 16 → 17** (el 17 inserta filas en las
> tablas que crean el 8/8b y describe las tablas que crea el 16), **23 → 25** (la cabecera de
> recepción lleva FK a `alm_orden_compra`), y **no aplicar el backup del paso 6**. El resto podría
> reordenarse, pero seguir el orden de arriba es lo más seguro.

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

### Paso 20 — `alm_tipo_articulo.impuesto_tasa_id` (ISV de compras por tipo) · con binario
Agrega la columna `impuesto_tasa_id` (FK a `cfg_impuesto_tasa`, `ON DELETE RESTRICT`) + índice
parcial. Permite configurar **por tipo de artículo** si sus compras registran ISV (una tasa
gravada se suma al costo) o no (tasa exenta o NULL). Aditivo e idempotente; nace **NULL** en
todos los tipos, así que ninguno cambia de comportamiento hasta que se le asigne una tasa desde
Mantenimientos → Tipos de artículo. **Sin parte contable** (crédito fiscal fuera de alcance).

> ⚠️ **Prerrequisito externo a esta tanda:** la FK apunta a `cfg_impuesto_tasa`
> (`2026-07-14_cfg_impuestos.sql`), que **no figura en este runbook** y cuyo estado en el SRV
> no consta. El script del paso 20 trae una guarda que lo **detiene con mensaje claro** si esa
> tabla falta, sin dejar nada a medias. Verificá/aplicá el catálogo del ISV primero (requiere
> además la extensión `btree_gist`).

> ⚠️ **Va con el binario del portal.** El código nuevo (`TipoArticuloService`,
> `SiadDbContext.Almacen.cs`, `TipoArticuloForm`) lee y escribe la columna. El SQL sin el binario
> es inocuo (columna sin usar); el binario sin el SQL rompe el mantenimiento de tipos.

```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-07-30_alm_tipo_articulo_impuesto_tasa.sql
```
¿Ya aplicado?
```sql
SELECT count(*) AS tiene_col
  FROM information_schema.columns
 WHERE table_name='alm_tipo_articulo' AND column_name='impuesto_tasa_id';
-- 1 = ya aplicado.
```
Verificación posterior (todos los tipos nacen sin tasa; FK e índice existen):
```sql
SELECT count(*) AS total, count(impuesto_tasa_id) AS con_tasa FROM public.alm_tipo_articulo;
-- con_tasa = 0 recién aplicado.
SELECT conname FROM pg_constraint WHERE conname='fk_alm_tipo_articulo_impuesto_tasa';   -- 1 fila
SELECT indexname FROM pg_indexes WHERE indexname='ix_alm_tipo_articulo_impuesto_tasa';  -- 1 fila
```

### Paso 21 — Infraestructura de la carga inicial de existencias · con binario
Crea `alm_config_inventario` (política del corte, una fila por empresa, con semilla) y
`alm_ajuste_inventario` (documento de ajuste: la vía legítima de mover stock cuando se cierre
la captura manual). Sobre `alm_kardex`: amplía el CHECK de `documento_tipo` con **`REVERSA`**
(superconjunto del vigente), agrega dos CHECK **`NOT VALID`** y dos índices parciales
(`ix_alm_kardex_carga_inicial`, `ix_alm_kardex_reversa`). Aditivo e idempotente; **no toca
ningún dato de negocio**.

> ⚠️ **Prerrequisito duro — verificar ANTES.** Este paso asume aplicados en el SRV
> `2026-07-09_alm_kardex_bodega_id.sql`, `2026-07-13_alm_kardex_articulo_id.sql` y los cuatro
> de `2026-07-14` (trazabilidad, ampliar precisiones, FK RESTRICT, FK compuestas).
> **Ninguno figura en este runbook** y su estado en el SRV no consta. El script trae guardas
> que lo **detienen con mensaje claro** si falta algo, sin dejar nada a medias. Ojo además:
> esos scripts **dejan de ser re-ejecutables** una vez activo `trg_alm_kardex_inmutable`
> (hacen UPDATE de backfill y fallan con `K0001`).

> ⚠️ **Va con el binario del portal**: el motor de posteo (`InventarioPostingService`) escribe
> `documento_tipo = 'REVERSA'`, que el CHECK viejo rechaza. El SQL sin el binario es inocuo.

```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-07-30_alm_carga_inicial.sql
```
¿Ya aplicado?
```sql
SELECT count(*) AS tiene_tablas FROM information_schema.tables
 WHERE table_name IN ('alm_config_inventario','alm_ajuste_inventario');
-- 2 = ya aplicado.
```
Verificación posterior:
```sql
SELECT company_id, base_costo_apertura, costo_apertura_incluye_isv, apertura_cerrada
  FROM public.alm_config_inventario ORDER BY company_id;   -- una fila por empresa con artículos
SELECT pg_get_constraintdef(oid) FROM pg_constraint
 WHERE conname='ck_alm_kardex_documento_tipo';             -- debe incluir 'REVERSA'
SELECT conname, convalidated FROM pg_constraint
 WHERE conname IN ('ck_alm_kardex_libro_nuevo','ck_alm_kardex_fecha_si_uuid');
-- convalidated = false: se validan en el paso 21b.
```

### Paso 21b — Validar los dos CHECK del kardex ⚠️ fuera de la ventana crítica
Los CHECK del paso 21 nacen `NOT VALID` **a propósito**: la afirmación "todo el histórico tiene
`documento_tipo` NULL" es plausible pero **no está comprobada contra el SRV**, y una sola fila
incompatible abortaría el `ALTER` —y con él toda la transacción— en plena ventana de despliegue.
Con `NOT VALID` el `ALTER` es instantáneo. La validación escanea `alm_kardex` (~47 mil filas):
corrédla después, con calma.

**Pre-chequeo (los tres deben dar 0). Si alguno no da 0, NO valides: investigá primero.**
```sql
SELECT count(*) FROM alm_kardex WHERE documento_tipo IS NOT NULL AND uuid IS NULL;
SELECT count(*) FROM alm_kardex WHERE uuid IS NOT NULL AND documento_tipo IS NULL;
SELECT count(*) FROM alm_kardex WHERE uuid IS NOT NULL AND fecha IS NULL;
```
```sql
ALTER TABLE public.alm_kardex VALIDATE CONSTRAINT ck_alm_kardex_libro_nuevo;
ALTER TABLE public.alm_kardex VALIDATE CONSTRAINT ck_alm_kardex_fecha_si_uuid;
```
¿Ya aplicado?
```sql
SELECT conname, convalidated FROM pg_constraint
 WHERE conname IN ('ck_alm_kardex_libro_nuevo','ck_alm_kardex_fecha_si_uuid');
-- convalidated = true en ambos = ya validado.
```

### Paso 22 — `cfg_compra_isv` (tratamiento del ISV en compras, por empresa) · con binario
Crea la tabla `cfg_compra_isv` (una fila por empresa, PK = `company_id`) con `tratamiento` ∈
('COSTO','FISCAL') y una semilla `COSTO` por empresa con artículos. Es la **segunda capa** de la
configuración del ISV: el paso 20 dice —por tipo— cuánto ISV lleva cada compra; este dice qué se
hace con ese ISV (al costo vs. impuesto fiscal). **Sin parte contable** (solo guarda la decisión).
Aditivo e idempotente, tabla nueva sin FK, no toca ningún dato existente. **Independiente de los
demás pasos.**

> ⚠️ **Va con el binario del portal**: el código nuevo (`IsvCompraConfigService` y la pantalla de
> configuración) lee y escribe la tabla. El SQL sin el binario es inocuo (tabla sin usar).

```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-07-30_cfg_compra_isv.sql
```
¿Ya aplicado?
```sql
SELECT to_regclass('public.cfg_compra_isv');   -- NOT NULL = ya aplicado.
```
Verificación posterior:
```sql
SELECT company_id, tratamiento FROM public.cfg_compra_isv ORDER BY company_id;
-- una fila por empresa con artículos, todas en COSTO recién aplicado.
SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname='ck_cfg_compra_isv_tratamiento';
```

### Paso 23 — Órdenes de compra (módulo nuevo) · con binario · independiente
Crea el modelo de órdenes de compra que el portal no tenía: `alm_orden_compra` (cabecera),
`alm_orden_compra_detalle` (renglones; FK compuesta tenant-safe a la cabecera y a `alm_articulo`)
y `alm_orden_compra_correlativo` (numeración por empresa). Además agrega
`alm_compra.orden_compra_detalle_id` (+ FK compuesta tenant-safe e índice) para enlazar la
recepción con su O/C (modo Con orden de compra). Estados numéricos (1 Borrador · 2 Aprobada ·
3 Recibida parcial · 4 Cerrada · 9 Anulada). Aditivo e idempotente (`CREATE TABLE/INDEX IF NOT
EXISTS`, `ADD COLUMN IF NOT EXISTS`, la FK dentro de un `DO` block guardado por `pg_constraint`);
**no toca ningún dato existente** (la columna nueva de `alm_compra` es NULL).

> ⚠️ **Va con el binario del portal**: el código nuevo del módulo de O/C (entidades, servicio,
> pantalla) lee y escribe estas tablas. El SQL sin el binario es inocuo (tablas/columna sin usar).

> **Dependencias:** solo `alm_articulo` y `alm_compra`, ambas preexistentes en prod. Independiente
> de los demás pasos de esta tanda. No asume ningún `company_id` (el correlativo se crea on-demand
> por empresa desde el servicio).

```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-07-30_alm_orden_compra.sql
```
¿Ya aplicado?
```sql
SELECT to_regclass('public.alm_orden_compra')             AS cabecera,
       to_regclass('public.alm_orden_compra_detalle')     AS detalle,
       to_regclass('public.alm_orden_compra_correlativo') AS correlativo;
-- las tres NOT NULL = aplicado.
```
Verificación posterior:
```sql
SELECT count(*) AS col_enlace FROM information_schema.columns
 WHERE table_name='alm_compra' AND column_name='orden_compra_detalle_id';    -- 1 = aplicado
SELECT conname FROM pg_constraint WHERE conname='fk_alm_compra_oc_detalle';   -- 1 fila
SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname='ck_alm_orden_compra_estado';
-- CHECK (estado IN (1, 2, 3, 4, 9))
```

---

### Paso 24 — El stock se muda a la bodega de su kardex (`PRIN` → `01`) ⚠️ one-shot
El kardex histórico y el stock quedaron en bodegas distintas: **47,213 de 47,215 asientos** de
`alm_kardex` están en `bodega_id = 2` (código `01`), mientras `alm_articulo_bodega` tiene **634
filas en `bodega_id = 1`** (`PRIN`) y una sola en la 2. Son la misma bodega física con dos
identidades — el saldo del histórico de la bodega 2 coincide **exacto** con la existencia de la
bodega 1 en **585 de 587 artículos (99.7%)**. El causante probable es
`2026-07-07_alm_articulo_bodega_backfill_existencia.sql`, que sembró las filas por bodega desde
la cabecera sin mirar de qué bodega hablaba el kardex.

El script mueve las filas (conservan id, existencia, costos, mínimos, máximos, punto de reorden,
ubicación y la marca de principal: **solo cambia `bodega_id`**) y resuelve la única colisión con
`uq (company, artículo, bodega)`: el artículo `0030` ya tenía fila en la bodega 01 (existencia 0,
creada el 2026-07-29 por `admin@siad-demo.com`); se conserva el mayor de los dos mínimos y se
elimina la duplicada. Al final **rehace el rollup de cabecera** de los artículos que quedaron
desalineados: eliminar una fila de bodega cambia la Σ de mínimos y sin ese paso
`alm_articulo.existencia_minima` queda stale (medido: el `0030` quedaba con 50 en la cabecera
contra 30 en sus bodegas).

> ⚠️ **PRERREQUISITO DEL CORTE DE INVENTARIO.** Sin esta mudanza, el punto de corte del kardex
> no empareja: filtrando por bodega el descuadre queda mudo (falso negativo) y **sin filtrar, el
> saldo DUPLICA la existencia**. Medido en el mirror tras un ensayo del corte: el artículo `0001`
> mostraba saldo 572.00 contra existencia 286.00, y 8 de los 12 artículos con más histórico
> quedaron descuadrados. Va **antes** de ejecutar el corte
> (`docs/plans/2026-07-31-fase8-ejecucion-corte-inventario.md`).

> ⚠️ **NO re-ejecutable a ciegas** y **asume `company_id = 2`**. El script trae un `DO` block que
> aborta si los ids de bodega no son `PRIN = 1` y `01 = 2`: **los ids se asignan por secuencia y
> no tienen por qué coincidir entre mirror y producción.** Si en el SRV son otros, hay que
> ajustar el script antes de correrlo.

> **No toca `alm_kardex`** (no se deshabilita ningún trigger; mover el histórico habría exigido
> apagar `trg_alm_kardex_inmutable`), **no usa disparadores**, **no es DDL**, y **no toca**
> compras, descargos ni requisiciones: sus 90,107 filas tienen `bodega_id` en NULL.

```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-07-31_alm_articulo_bodega_mover_a_bodega_01.sql
```
¿Ya aplicado?
```sql
SELECT b.id, b.codigo, count(ab.id) AS filas
FROM alm_bodega b
LEFT JOIN alm_articulo_bodega ab ON ab.company_id = b.company_id AND ab.bodega_id = b.id
WHERE b.company_id = 2 GROUP BY b.id, b.codigo ORDER BY b.id;
-- PRIN con 0 filas y '01' con las 634 = ya aplicado.
```
Verificación posterior:
```sql
-- 1. La cabecera sigue cuadrada contra la suma de bodegas activas. Esperado: 0 filas.
SELECT a.id FROM alm_articulo a
LEFT JOIN alm_articulo_bodega ab ON ab.company_id=a.company_id AND ab.articulo_id=a.id AND ab.activo
WHERE a.company_id = 2 GROUP BY a.id, a.existencia
HAVING a.existencia <> COALESCE(SUM(ab.existencia), 0);

-- 2. El objetivo: histórico y stock en el mismo par. Esperado: ~585 de 587 coinciden.
WITH hist AS (
  SELECT articulo_id, SUM(COALESCE(ingresos,0)-COALESCE(salidas,0)) AS saldo_hist
  FROM alm_kardex WHERE company_id=2 AND uuid IS NULL AND bodega_id=2 AND articulo_id IS NOT NULL
  GROUP BY articulo_id
), stock AS (SELECT articulo_id, existencia FROM alm_articulo_bodega WHERE company_id=2 AND bodega_id=2)
SELECT count(*) AS comparados, count(*) FILTER (WHERE h.saldo_hist = s.existencia) AS coinciden
FROM hist h JOIN stock s USING (articulo_id);

-- 3. Exactamente una bodega principal por artículo. Esperado: 0 filas.
SELECT articulo_id FROM alm_articulo_bodega WHERE company_id=2 AND activo AND principal
GROUP BY articulo_id HAVING count(*) <> 1;

-- 4. Rollup de MÍNIMOS al día (es lo que arregla el paso 4 del script). Esperado: 0 filas.
SELECT a.id, a.codigo_articulo FROM alm_articulo a
LEFT JOIN alm_articulo_bodega ab ON ab.company_id=a.company_id AND ab.articulo_id=a.id AND ab.activo
WHERE a.company_id = 2 GROUP BY a.id, a.codigo_articulo, a.existencia_minima
HAVING a.existencia_minima <> COALESCE(SUM(ab.existencia_minima), 0);
```

---

### Paso 25 — Recepción de compra: cabecera de la factura de proveedor · con binario · depende del 23
Crea la pieza que consume las órdenes de compra: `alm_compra_hdr`, la **cabecera** de la recepción
(un documento = una factura de proveedor), y `alm_compra_correlativo` (numeración interna por
empresa, mismo patrón de `UPDATE ... RETURNING` que la O/C). Además agrega
`alm_compra.compra_hdr_id` (+ FK compuesta tenant-safe e índice) para colgar los N renglones de su
factura. La cabecera lleva lo que es del documento y no del renglón: proveedor, factura SAR
(`numero_factura_sar`, texto — `alm_compra.numero_factura` es `NUMERIC` y no admite guiones), CAI,
bodega que recibe, O/C recibida, términos, moneda/tasa, consumo interno, override de ISV,
descuento global, otros gastos, flete y totales.

Cierra tres decisiones abiertas del diseño (`docs/centura-flujos/README_compras_recepcion_proveedor.md`):
**D-3** agrupador de documento = cabecera real (no un campo dentro de la tabla plana), **D-4**
numeración = contador por `company_id`, **D-5** factura SAR = columna nueva de texto.

> ⚠️ **Va con el binario del portal**: la pantalla y el servicio de recepción escriben estas tablas.
> El SQL sin el binario es inocuo (tablas vacías y una columna NULL sin usar).

> **Dependencias:** el **paso 23** (`alm_orden_compra` y su clave alterna `uq_alm_orden_compra_tenant`)
> y la clave alterna `uq_alm_bodega_company_id` de `2026-07-14_alm_fk_compuestas_tenant.sql`. El
> script verifica ambas al inicio y **aborta con mensaje claro** si faltan. No asume ningún
> `company_id` (el correlativo se crea on-demand por empresa desde el servicio).

> **No toca datos existentes:** `alm_compra` solo recibe una columna NULL, así que el histórico
> SIMAFI queda intacto y sigue blindado por `trg_alm_compra_blindaje`. Ningún tipo cambia y nada
> pasa a `NOT NULL`. El motor de posteo **no cambia**: la unidad de posteo sigue siendo la línea
> de `alm_compra` con su `uuid`.

```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-07-31_alm_compra_recepcion.sql
```
¿Ya aplicado?
```sql
SELECT to_regclass('public.alm_compra_hdr')         AS cabecera,
       to_regclass('public.alm_compra_correlativo') AS correlativo;
-- las dos NOT NULL = aplicado.
```
Verificación posterior:
```sql
-- 1. Columna de enlace y FK compuesta en alm_compra.
SELECT count(*) AS col_enlace FROM information_schema.columns
 WHERE table_name='alm_compra' AND column_name='compra_hdr_id';           -- 1 = aplicado
SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname='fk_alm_compra_hdr';
-- FOREIGN KEY (company_id, compra_hdr_id) REFERENCES alm_compra_hdr(company_id, id) ...

-- 2. Las dos FK de la cabecera deben ser COMPUESTAS (company_id, ...).
SELECT conname, pg_get_constraintdef(oid) FROM pg_constraint
 WHERE conrelid='alm_compra_hdr'::regclass AND contype='f';

-- 3. El histórico NO se tocó: ninguna línea SIMAFI quedó con cabecera.
SELECT origen, count(*) AS lineas, count(compra_hdr_id) AS con_cabecera
  FROM alm_compra GROUP BY origen ORDER BY origen;   -- con_cabecera = 0 en SIMAFI

-- 4. Los CHECK vigentes.
SELECT conname, pg_get_constraintdef(oid) FROM pg_constraint
 WHERE conrelid='alm_compra_hdr'::regclass AND contype='c' ORDER BY conname;
-- ck_alm_compra_hdr_estado (1,9) · _moneda (HNL,USD) · _posteo · _tasa (>0)
```

---

### Paso 26 — Requisiciones y descargos: cabeceras, correlativos y parcialidad · con binario · depende del script de documentos
Crea la estructura que le faltaba al módulo para **capturar** requisiciones y descargos, que hoy
son sólo consulta sobre el histórico migrado de SIMAFI: `alm_requisicion_hdr` (la solicitud) y
`alm_descargo_hdr` (la entrega), sus dos correlativos por empresa **sembrados desde el máximo
histórico (17124)**, las columnas de parcialidad y enlace sobre las dos tablas planas
(`requisicion_hdr_id`, `cantidad_despachada`, `aplicado_en_oc`, `descargo_hdr_id`,
`requisicion_id`) y las claves alternas por tenant que no existían. Diseño completo en
[docs/centura-flujos/README_requisiciones_descargos.md](../docs/centura-flujos/README_requisiciones_descargos.md).

> **La regla que fija este paso:** requisición, descargo y kardex tipo 202 son **el mismo hecho**
> (42.866 / 42.757 / 42.698 líneas, **42.653 pares comunes**, medido en el mirror). Por eso **sólo el
> descargo postea** y el histórico **no se migra ni se re-postea**. El `CHECK ck_alm_requisicion_no_postea`
> y el `DROP` de `ix_alm_requisicion_pendiente` existen para que ningún barrido futuro convierta
> solicitudes en salidas duplicadas.

> ⚠️ **No es puramente aditivo.** Hace `UPDATE` de backfill sobre las **42.866** líneas de
> `alm_requisicion` (`cantidad_despachada`, `aplicado_en_oc`) y **elimina** el índice
> `ix_alm_requisicion_pendiente`. Ambas cosas son deliberadas y están en el bloque de rollback.

> **Dependencias:** el script de documentos de almacén (`2026-07-14_alm_documentos_bodega_posteo.sql`,
> §3.1b del registro de pendientes), las dos claves alternas de `2026-07-14_alm_fk_compuestas_tenant.sql`
> y `2026-07-14_alm_kardex_trazabilidad.sql`. El script **verifica las cuatro y aborta con mensaje
> claro** si falta alguna. También aborta si encuentra documentos con `origen = 'SIAD'` en las planas.

```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-01_alm_requisicion_descargo.sql
```
¿Ya aplicado?
```sql
SELECT to_regclass('public.alm_requisicion_hdr') AS req_hdr,
       to_regclass('public.alm_descargo_hdr')    AS des_hdr,
       to_regclass('public.alm_requisicion_correlativo') AS req_corr,
       to_regclass('public.alm_descargo_correlativo')    AS des_corr;
-- las cuatro NOT NULL = aplicado.
```
Verificación posterior: V1–V8 del propio script (final del archivo). Las dos críticas:
```sql
-- El histórico NO cambió: 42.866 y 42.757, todo SIMAFI/posteado.
SELECT 'req' AS t, origen, posteado, count(*) FROM alm_requisicion GROUP BY 1,2,3
UNION ALL SELECT 'des', origen, posteado, count(*) FROM alm_descargo GROUP BY 1,2,3;

-- Los correlativos continúan la numeración vieja (empresa 2 → 17124 en ambos).
SELECT 'req' AS t, company_id, ultimo_numero FROM alm_requisicion_correlativo
UNION ALL SELECT 'des', company_id, ultimo_numero FROM alm_descargo_correlativo ORDER BY 1,2;
```

### Paso 27 — Catálogo de tipos de movimiento de almacén · con binario · independiente
Crea `alm_tipo_movimiento`, el catálogo **configurable por el usuario** de motivos de entrada y
salida de almacén, y lo siembra con **los 12 tipos reales del legacy**, importados de
`dbo.INV_TIPOSTRANSACC` de MERENDON (SQL Server) el 2026-08-03, para **cada empresa que tenga
bodegas**. Diseño en
[docs/plans/2026-08-01-movimientos-almacen-catalogo-diseno.md](../docs/plans/2026-08-01-movimientos-almacen-catalogo-diseno.md).

> ⚠️ **Contiene un `DELETE` acotado.** La primera versión del script (2026-08-01) sembraba 3 tipos
> **inventados** a partir de `ClaseAjusteInventario` (`SOBRANTE_CONTEO`, `MERMA`,
> `CORRECCION_COSTO`) que no salían de ningún dato real. El script los retira **sólo si ningún
> documento los referencia** (aborta con mensaje si los hay). Aprobado por el usuario el
> 2026-08-03, incluida la pérdida de la única clase `VALOR` — Centura no tiene equivalente.

> **Qué se importó y qué no:** código y nombre **verbatim**; `ENTRA_SALE` → `clase`
> (`E`→ENTRADA, `S`→SALIDA; resultado real **7 ENTRADA / 5 SALIDA**). **No** se importaron:
> `CUENTA_CONTABLE` (las 7 cuentas son de Merendón y **ninguna existe** en `con_plan_cuentas` de
> SIAD — quedan NULL, que es heredar del tipo de artículo), `CAMBIA_COSTO` (no vive por tipo sino
> en `INV_TRANSACC_AXL` por `(AREA_AFECTADA, ENTRA_SALE)`, y su regla real —toda entrada cambia
> costo, ninguna salida— ya es lo que hace el motor) ni `CORRELATIVO`. `AREA_AFECTADA` no se
> importó pero determinó el `activo`.

> **Sólo 4 quedan activos:** `AIE`, `AIS`, `NPG`, `APL` (área `D`, captura manual). Los 8 restantes
> entran **inactivos**: `FAC`/`CAN`/`DPI`/`DEP` y `COM`/`TTR` los postea automáticamente su propio
> flujo en SIAD, y `TFE`/`TFS` necesitan la clase `TRASLADO` de la Fase 5 (activarlos hoy
> permitiría registrar media transferencia).

> **Qué resuelve:** hoy agregar un motivo de movimiento exige recompilar y migrar un `CHECK`. Con
> este catálogo es un `INSERT` desde la pantalla. La lógica de inventario **no** se mueve a la
> tabla: la gobierna la `clase` (`ENTRADA` / `SALIDA` / `VALOR`), que el motor de posteo ya sabe
> ejecutar y **no cambia una sola línea**. La tabla sólo aporta el nombre de negocio, la cuenta
> contable de override y la bandera de autorización.

> **100% aditivo, idempotente y reversible.** `CREATE TABLE IF NOT EXISTS` + `INSERT … ON CONFLICT
> DO NOTHING`: re-ejecutarlo no duplica la semilla ni pisa lo que el usuario ya haya editado. El
> bloque de rollback está al final del propio script.

> **Sin dependencias duras.** Sólo lee `alm_bodega` para saber a qué empresas sembrar. Trae la clave
> alterna `uq_alm_tipo_movimiento_tenant (company_id, id)` que la **Fase 2** necesitará para su FK
> compuesta; no hace falta nada más hoy.

```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-01_alm_tipo_movimiento.sql
```
¿Ya aplicado?
```sql
SELECT to_regclass('public.alm_tipo_movimiento') AS existe;   -- NULL = falta
```

### Paso 28 — Documento de movimiento de almacén · con binario · depende del paso 27
Crea las tres tablas que le faltaban al portal para **capturar** entradas y salidas manuales
multi-renglón: `alm_movimiento_hdr` (cabecera), `alm_movimiento_dtl` (renglón, unidad de posteo)
y `alm_movimiento_correlativo` (consecutivo por empresa, sembrado en 0). Es el equivalente
funcional de `dlgTransaccionesGenericasINV` de Centura. Diseño en
[docs/plans/2026-08-01-movimientos-almacen-catalogo-diseno.md](../docs/plans/2026-08-01-movimientos-almacen-catalogo-diseno.md).

> **100% aditivo, idempotente y reversible.** `CREATE TABLE IF NOT EXISTS` + siembra del
> correlativo con `ON CONFLICT DO NOTHING`. Rollback propio al final del script (3 `DROP TABLE`).

> **Depende del paso 27** (`alm_tipo_movimiento` y su clave alterna) y de las claves alternas de
> `alm_bodega` y `alm_articulo` (`2026-07-14_alm_fk_compuestas_tenant.sql`). El script **verifica
> las tres y aborta con mensaje claro** si falta alguna: sus 4 FK son compuestas `(company_id, …)`.

```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-03_alm_movimiento.sql
```
¿Ya aplicado?
```sql
SELECT to_regclass('public.alm_movimiento_hdr') AS hdr,
       to_regclass('public.alm_movimiento_dtl') AS dtl;   -- ambas NOT NULL = aplicado
```
Verificación posterior: V1–V8 del propio script (incluye tres pruebas negativas — anular sin
motivo, posteado sin uuid, fuga de tenant — que deben fallar dentro de `ROLLBACK`).
Verificación posterior: V1–V6 del propio script (incluye dos pruebas negativas). Las esenciales:
```sql
-- Una fila por (empresa, código); esperado hoy: company_id 2 → 12 filas.
SELECT company_id, count(*), string_agg(codigo, ', ' ORDER BY orden)
  FROM alm_tipo_movimiento GROUP BY company_id ORDER BY company_id;

-- La semilla genérica vieja ya no existe (esperado: 0 filas).
SELECT codigo FROM alm_tipo_movimiento
 WHERE codigo IN ('SOBRANTE_CONTEO','MERMA','CORRECCION_COSTO');

-- Sólo 4 activos (esperado: AIE, AIS, NPG, APL).
SELECT codigo, clase FROM alm_tipo_movimiento WHERE activo ORDER BY orden;
```

### Paso 29 — Traslado entre bodegas (tránsito + directo) · con binario · depende de los pasos 27 y 28
Fase 5 del módulo de movimientos. Sobre el documento del paso 28 agrega el **traslado entre bodegas**:
columnas de traslado en `alm_movimiento_hdr` (`bodega_destino_id`, `requiere_recepcion`, `recibido_por`,
`fecha_recepcion`) y `alm_movimiento_dtl` (`cantidad_recibida`), dos tablas nuevas de recepción parcial
(`alm_traslado_recepcion` / `alm_traslado_recepcion_dtl`, con FK compuestas tenant-safe), y la semilla
del tipo `TRF` «Traslado entre bodegas» (clase `TRASLADO`). Diseño en
[docs/plans/2026-08-04-traslado-bodegas-transito-diseno.md](../docs/plans/2026-08-04-traslado-bodegas-transito-diseno.md).

> **Aditivo + *widening* de dos CHECK, idempotente y reversible.** `ADD COLUMN IF NOT EXISTS` /
> `CREATE TABLE IF NOT EXISTS` / `DO` blocks / `ON CONFLICT DO NOTHING`. Los dos CHECK se **amplían**
> (más permisivos, sin pérdida de datos): `ck_alm_movimiento_hdr_estado` de `(1,9)` a `(1,2,3,9)` y
> `ck_alm_tipo_movimiento_clase` a `+TRASLADO`, cada uno con `DROP … IF EXISTS` + `ADD` (seguro al
> re-correr). Rollback propio al final del script. **No** hay `DROP` de columnas ni `DELETE`/`TRUNCATE`.

> **Depende de los pasos 27 y 28** (`alm_tipo_movimiento`, `alm_movimiento_hdr`/`_dtl` y sus claves
> alternas) y de `alm_articulo_bodega.existencia_transito` (`2026-07-13`) y `alm_kardex.bodega_destino_id`
> (ya en el scaffold). El script **verifica los prerrequisitos y aborta con mensaje claro** si falta alguno.

```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-04_alm_traslado.sql
```
¿Ya aplicado?
```sql
SELECT to_regclass('public.alm_traslado_recepcion')     AS rec,
       to_regclass('public.alm_traslado_recepcion_dtl') AS rec_dtl,
       (SELECT count(*) FROM alm_tipo_movimiento WHERE codigo='TRF') AS trf;  -- rec/rec_dtl NOT NULL y trf>0 = aplicado
```
Verificación posterior: V1–V8 del propio script (incluye dos pruebas negativas — destino = origen y
tránsito negativo — que deben fallar dentro de `ROLLBACK`).

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

- **Paso 20:** creado el 2026-07-30 (rama `Cambios_almacen2.0`) y **APLICADO AL MIRROR
  `siad_v3_restore` ese mismo día** (verificado: columna nullable creada, 9 tipos con 0 tasas
  asignadas, FK con `confdeltype='r'`, índice parcial; el prerrequisito `cfg_impuesto_tasa`
  existía con 4 tasas — EXENTO, EXONERADO, ISV15 15%, ISV18 18%). Tras aplicarlo, la suite de
  Almacén pasó de 18 fallos (`42703`) a **79/79 en verde**. **Pendiente en el SRV**, donde el
  estado de `cfg_impuesto_tasa` sigue sin constar; el script se detiene con mensaje claro si
  falta. Va junto con el binario del portal.
- **Paso 21 / 21b:** creados y **APLICADOS AL MIRROR `siad_v3_restore` el 2026-07-30**.
  Verificado en el mirror antes de aplicar: 8/8 columnas de trazabilidad, las 2 claves alternas
  por tenant, el trigger de inmutabilidad activo y el CHECK vigente con los 6 valores esperados.
  Tras aplicar: las 2 tablas creadas, 1 fila de config sembrada (company 2, `VALOR_UNITARIO`,
  `costo_apertura_incluye_isv=false`), `REVERSA` aceptado por el CHECK, 5/5 índices, y la suite
  de Almacén en **79/79**. El **21b se corrió también en el mirror**: los tres pre-chequeos
  dieron **0** y ambos CHECK quedaron `convalidated = true` — es decir, el escaneo pasa limpio
  aquí; en el SRV hay que repetir el pre-chequeo porque su histórico puede diferir.
  **Pendiente en el SRV.** Va con el binario que introduce el motor de posteo
  (`InventarioPostingService`), que escribe `documento_tipo = 'REVERSA'` y por tanto **exige el
  CHECK ampliado del paso 21**. Su prerrequisito duro son los seis scripts de kardex de julio
  (2026-07-09, 2026-07-13 y los cuatro de 2026-07-14), que **no figuran en este runbook** y cuyo
  estado en el SRV no consta: confirmarlo es la Fase 0(a) del diseño
  `docs/plans/2026-07-29-carga-inicial-existencias-kardex-design.md`.

- **Paso 22:** creado y **APLICADO AL MIRROR `siad_v3_restore` el 2026-07-30** (verificado:
  tabla creada, CHECK `COSTO`/`FISCAL`, 1 fila sembrada company 2 = `COSTO`, re-aplicar es
  idempotente sin duplicar; la suite de Almacén corre **91/91 en verde**, incluidos los 5 tests
  de la capa 2). Tabla nueva independiente (sin FK), segunda capa de la config del ISV en compras
  (tratamiento al costo / fiscal, una fila por empresa). **Pendiente en el SRV.** Va con el binario
  del portal. Sin dependencias con otros pasos de esta tanda.

- **Paso 23:** creado y **APLICADO AL MIRROR `siad_v3_restore` el 2026-07-30** (verificado: 3 tablas
  creadas, columna de enlace en `alm_compra`, `CHECK (estado IN (1,2,3,4,9))`, y las **dos** FK del
  detalle compuestas tenant-safe). Módulo nuevo de órdenes de compra: 3 tablas (`alm_orden_compra`,
  `alm_orden_compra_detalle`, `alm_orden_compra_correlativo`) + `alm_compra.orden_compra_detalle_id`.
  **Pendiente en el SRV.** Va con el binario del portal que introduce el módulo de O/C.
  Dependencias: `alm_articulo` y `alm_compra` (preexistentes) **y la clave alterna
  `(company_id, id)` de `alm_articulo`** que crea `2026-07-14_alm_fk_compuestas_tenant.sql` — el
  script se detiene con mensaje claro si falta. Independiente de los demás pasos de esta tanda.
  > Nota: una revisión posterior detectó que la primera versión creaba la FK al artículo **simple**
  > en vez de compuesta (5 de las 7 FK hacia `alm_articulo` ya eran compuestas). El script se corrigió
  > y su bloque `DO` es **reparador**: sustituye la FK simple si una corrida anterior la dejó, así que
  > re-ejecutarlo es seguro y deja la base igual en instalaciones nuevas y ya aplicadas.

- **Paso 24:** creado y **APLICADO AL MIRROR `siad_v3_restore` el 2026-07-31** (verificado: `PRIN`
  quedó en 0 filas y `01` con 634 / 244 con existencia; cabecera cuadrada contra la suma de bodegas
  activas; 585 de 587 artículos con el saldo del histórico igual a la existencia; exactamente una
  bodega principal por artículo; `alm_kardex` intacto en 47,215 asientos). Mudanza del stock de la
  bodega `PRIN` a la `01`, que es donde vive el kardex. **Pendiente en el SRV.**
  ⚠️ **One-shot, asume `company_id = 2` y los ids `PRIN = 1` / `01 = 2`** — el `DO` block del script
  aborta si no coinciden. **Es prerrequisito del corte de inventario**: sin él, el punto de corte no
  empareja y el saldo del kardex duplica la existencia (verificado con un ensayo del corte en el
  mirror el mismo día). Va **antes** del guion de
  `docs/plans/2026-07-31-fase8-ejecucion-corte-inventario.md`.
  > Nota: una verificación posterior detectó que la primera versión **no rehacía el rollup de
  > cabecera**, y al eliminar la fila duplicada dejaba `alm_articulo.existencia_minima` stale (el
  > artículo `0030` quedaba con 50 contra 30 en sus bodegas). Se agregó el paso 4 y se re-aplicó al
  > mirror: los pasos 1–3 afectaron **0 filas** (idempotencia confirmada en vivo) y el rollup
  > corrigió la única fila pendiente.

- **Paso 25:** creado y **APLICADO AL MIRROR `siad_v3_restore` el 2026-07-31**. Cabecera de la
  recepción de compra (`alm_compra_hdr`) + correlativo por empresa + `alm_compra.compra_hdr_id`.
  Cierra D-3 (cabecera real en vez de agrupador plano), D-4 (contador por `company_id`, igual que la
  O/C) y D-5 (`numero_factura_sar` de texto) del diseño
  `docs/centura-flujos/README_compras_recepcion_proveedor.md`. **Pendiente en el SRV.**
  Verificado en el mirror: 2 tablas creadas, 9 constraints en la cabecera, las **dos** FK compuestas
  tenant-safe, 11 índices, `alm_compra.compra_hdr_id` nullable y las **4,484 líneas del histórico
  SIMAFI intactas** (`con_cabecera = 0`). Guardas probadas dentro de `BEGIN … ROLLBACK` (el mirror
  quedó sin rastro): factura duplicada por proveedor rechazada y recapturable tras anular, número
  duplicado por empresa rechazado, estado fuera de (1,9) rechazado, `posteado=true` sin
  `uuid`+`fecha_posteo` rechazado, moneda fuera de (HNL,USD) y `tasa_cambio<=0` rechazadas, O/C
  inexistente rechazada, enlace de línea a cabecera inexistente rechazado, FK compuesta rechazando
  la bodega de otra empresa, y el correlativo avanzando. **Re-ejecución confirmada idempotente**
  (segunda corrida: todo «ya existe, omitiendo» y `COMMIT`).
  **Depende del paso 23** y de la clave alterna de `alm_bodega`; el script aborta con mensaje claro
  si falta alguna. Va con el binario del portal que introduce la captura de compras y el tipo
  `COMPRA` en el motor de posteo — todavía **sin implementar** en el código.
  > Nota para el servicio: el índice único de factura por proveedor **excluye las anuladas**, así que
  > una factura anulada se puede recapturar; pero entonces **des-anular la original falla** con
  > `unique_violation` (verificado). Si se implementa "reactivar", debe validarlo antes y devolver un
  > mensaje claro en vez de un 500.

- **Paso 26:** creado y **APLICADO AL MIRROR `siad_v3_restore` el 2026-08-01**. Estructura de captura
  de requisiciones y descargos. **Pendiente en el SRV.** Verificado tras aplicar: las 4 tablas nuevas,
  las **6 FK todas compuestas** tenant-safe, los correlativos sembrados en **17124** (empresa 2), el
  histórico **idéntico** (42.866 requisiciones y 42.757 descargos, 100 % SIMAFI/posteado), 0 filas con
  `cantidad_despachada > cantidad`, 0 requisiciones SIAD pendientes y el índice
  `ix_alm_requisicion_pendiente` ausente. Backfill medido: 42.704 líneas descargadas + 162 en 0 = 42.866.
  Ocho guardas probadas dentro de `BEGIN … ROLLBACK` (el mirror quedó sin rastro): sobre-despacho,
  despacho negativo, requisición posteada (por CHECK y por el blindaje K0002), reserva negativa,
  descargo directo sin motivo, aprobada sin evidencia, reabastecimiento despachado y un alta válida
  como control positivo.
  > **Decisión del usuario (2026-08-01): SIMAFI ya no ingresa datos** y se traerá un respaldo de esa
  > base. Esto cierra el riesgo de colisión de numeración: el correlativo sembrado en 17124 puede
  > continuar sin que el legacy emita números en paralelo.
  > ⚠️ **Su arquitectura quedó supersedida** por el diseño del paso 27
  > (`docs/plans/2026-08-01-movimientos-almacen-catalogo-diseno.md` §1): el script trae su propio
  > bloque de rollback (líneas 440-463) y **está pendiente de decisión del usuario** si se revierte
  > en el mirror o se deja aplicado sin uso. No aplicarlo al SRV hasta resolver eso.

- **Paso 27:** creado el 2026-08-01 y **APLICADO AL MIRROR `siad_v3_restore` el 2026-08-03**, con la
  semilla reemplazada ese mismo día por los 12 tipos reales de `INV_TIPOSTRANSACC`. Verificado tras
  aplicar: 3 filas de la semilla vieja retiradas, 12 insertadas, 0 rastros de
  `SOBRANTE_CONTEO`/`MERMA`/`CORRECCION_COSTO`, 4 activos (`AIE`, `AIS`, `NPG`, `APL`) y el reparto
  de clases **7 ENTRADA / 5 SALIDA** — que es el conteo literal de `ENTRA_SALE` en el origen.
  **Re-ejecución confirmada idempotente en vivo** (segunda corrida: `DELETE` 0 filas, `INSERT 0 0`,
  total sigue en 12). **Pendiente en el SRV.**

- **Paso 28:** creado y **APLICADO AL MIRROR `siad_v3_restore` el 2026-08-03**. Las 3 tablas del
  documento de movimiento. Verificado tras aplicar: las 3 tablas creadas, las **4 FK todas
  compuestas** tenant-safe, el correlativo sembrado en 0 (empresa 2), y las tres guardas probadas
  dentro de `BEGIN … ROLLBACK` (anular sin motivo → 23514, posteado sin uuid → 23514, bodega de otra
  empresa → 23503, alta válida como control positivo → OK). El módulo se ejercitó con las **17
  pruebas de integración** `MovimientoAlmacenTests` + `MovimientoAlmacenAnulacionTests` en verde y la
  suite de Almacén completa en **220/220**; el mirror quedó **sin residuos** (los tests envuelven en
  `ROLLBACK`). **Pendiente en el SRV.** Va con el binario del portal que introduce la captura
  (`IMovimientoAlmacenService`, controlador y —pendientes— las pantallas).
  > **Limitación conocida arrastrada, NO bloqueante:** anular un movimiento de clase `VALOR` devuelve
  > la existencia pero **no restituye el costo promedio anterior** (el motor no lo guarda). Es el
  > defecto de `docs/plans/2026-08-01-costeo-articulo-diseno.md` §3, corrección 1, cuya solución es la
  > Fase B de ese diseño. La prueba 18 fija el comportamiento actual y avisa.
  Va junto con el binario del portal que introduce la pantalla `/almacen/tipos-movimiento`
  (Fase 1 del catálogo: entidad, servicio, controlador, cliente HTTP, pantallas y permisos
  `module.inventario.tipos_movimiento.*`, implementados el 2026-08-03).
  > **Sin la tabla, la pantalla nueva falla y las pruebas `TipoMovimientoServiceTests` fallan en vez
  > de saltarse.** Es el único acoplamiento del paso: no toca ninguna tabla existente.

- **Paso 29:** creado el 2026-08-04 (Fase 5, traslado entre bodegas). **PENDIENTE en el mirror y en el
  SRV** — aún **no aplicado** (no me conecto a la BD por iniciativa propia; lo aplicás vos, primero al
  mirror y luego al SRV). Aditivo + *widening* de dos CHECK; 2 tablas nuevas de recepción con FK
  compuestas tenant-safe; semilla del tipo `TRF`. Va con el binario del portal de la Fase 5:
  **toda la Fase 5 (5.1 motor → 5.2 DDL/entidades → 5.3 `TrasladoAlmacenService` → 5.4 API/permisos →
  5.5 UI) está en código y compila (0 errores)**. Lo único pendiente aparte de este script: **sus tests
  de integración no se han corrido** (`InventarioPostingTrasladoTests` + `TrasladoAlmacenTests`,
  dependen de este paso aplicado al mirror y de `SIAD_TEST_DB`) y la **prueba de humo logueada**.
  > **Acoplamiento:** sin este script, el servicio de traslado y sus pruebas fallarían (faltarían las
  > columnas y las tablas de recepción). No rompe nada existente: es aditivo + widening.

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
- `Database/2026-07-30_alm_tipo_articulo_impuesto_tasa.sql`
- `Database/2026-07-30_alm_carga_inicial.sql`
- `Database/2026-07-30_cfg_compra_isv.sql`
- `Database/2026-07-30_alm_orden_compra.sql`
- `Database/2026-07-31_alm_articulo_bodega_mover_a_bodega_01.sql`
- `Database/2026-07-31_alm_compra_recepcion.sql`
- `Database/2026-08-01_alm_requisicion_descargo.sql`
- `Database/2026-08-01_alm_tipo_movimiento.sql`
- `Database/2026-08-03_alm_movimiento.sql`
- `Database/2026-08-04_alm_traslado.sql`
- `Database/2026-07-23_runbook_despliegue_srv.md` (este archivo)

Conviene versionarlos para que queden reflejados junto al resto de la tanda. **Vos
decidís cuándo** hacer commit/push.
