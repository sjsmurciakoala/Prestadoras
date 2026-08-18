# Registro de scripts pendientes de aplicar en el SRV

**Base destino:** `siad_v3` @ `172.16.0.9` (producción)
**Fecha del registro:** 2026-07-30
**Rama:** `Cambios_almacen2.0`

> ⚠️ **Nada de esto se verificó contra el SRV en vivo.** No me conecto a esa base por
> iniciativa propia. El estado que figura abajo viene de (a) lo que dice el runbook
> `2026-07-23_runbook_despliegue_srv.md`, (b) lo que se aplicó al **mirror** en esta sesión y
> (c) la cabecera de cada script. **Antes de aplicar cualquier cosa, corré su consulta
> «¿ya aplicado?»** — casi todos los scripts la traen, y el runbook la tiene por paso.

---

## 1. Cómo leer este registro

En `Database/` hay **94 scripts `.sql`**. La inmensa mayoría **ya está en producción**: son
los que construyeron el sistema (cobranza y caja de mayo/junio, las migraciones SIMAFI de
julio, la base del módulo de almacén). **"No figura en el runbook" NO significa "no aplicado"**
— el runbook solo cubre la tanda pendiente que arrancó el 2026-07-15.

Lo que este documento aporta es la separación en tres grupos:

| Grupo | Qué es | Cuántos |
|---|---|---|
| **A** | Tanda del runbook (pasos 1–23): **pendientes de SRV** | 24 |
| **B** | Sin registrar en el runbook, **estado en SRV sin confirmar** — el grupo de riesgo | 6 + 3 |
| **C** | Histórico ya aplicado (mayo/junio + SIMAFI + base de almacén) | el resto |

---

## 2. Grupo A — la tanda del runbook (pasos 1 a 23)

El detalle completo de cada paso, con su comando, su consulta «¿ya aplicado?» y su
verificación, está en **`Database/2026-07-23_runbook_despliegue_srv.md`**. Este registro no
lo duplica; solo resume el estado.

| Paso | Script | Mirror | SRV | ⚠️ |
|---|---|---|:--:|---|
| 1 | `2026-07-15_add_tipo_cuenta_prv_proveedor_cuenta_bancaria.sql` | sí | **pendiente** | |
| 2 | `2026-07-16_alm_tipo_articulo_seed_desde_lineas.sql` | presunto | **verificar** | ⚠️ **NO re-ejecutable**: hace `DELETE`+`INSERT` del catálogo de tipos. Repetirlo tras los pasos 3–5 deja artículos y categorías **sin tipo**. |
| 3 | `2026-07-16_alm_grupo_tipo_articulo_y_limites.sql` | presunto | **verificar** | |
| 4 | `2026-07-16_alm_articulo_backfill_tipo_desde_linea.sql` | presunto | **verificar** | |
| 5 | `2026-07-16_alm_articulo_saneo_sin_tipo.sql` | presunto | **pendiente** | Borra 1 artículo de prueba si no tiene movimientos (intencional). |
| 6 | `2026-07-16_saldo_vigencia_y_desglose_abono.sql` | sí | **pendiente** | ⚠️ **Cambia saldos visibles.** Correr la auditoría del propio script antes y después. |
| 7 | `2026-07-17_prv_compromiso_abono.sql` | sí | **pendiente** | |
| 8 | `2026-07-17_bitacora_maestros.sql` | sí (¿parcial?) | **pendiente** | |
| 8b | `2026-07-17_bitacora_maestro_catalogo.sql` | sí | **pendiente** | |
| 9 | `2026-07-17_asignar_cuenta_contable_ban_cuenta.sql` | sí | **pendiente** | ⚠️ Asume `company_id = 2` y cuentas `11102010301`/`11102010501`. |
| 10 | `2026-07-17_ban_tipo_transaccion_transferencia.sql` | sí | **pendiente** | ⚠️ Asume `company_id = 2`. |
| 11 | `2026-07-21_cheques_numeracion_bitacora.sql` | sí | **pendiente** | |
| 12 | `2026-07-23_prv_compromiso_dtl_conceptodtl_1000.sql` | ¿? | **pendiente** | |
| 13 | `2026-07-23_prv_compromiso_dtl_descripcion_1000.sql` | ¿? | **pendiente** | |
| 14 | `2026-07-24_presupuesto_multitenant_company_id.sql` | sí (24-jul) | **pendiente** | ⚠️ **Con binario.** Cambia la firma de `fn_pst_next_id_presupuesto_dtl`: el portal viejo falla al agregar detalles. Backfill asume `company_id = 2`. |
| 15 | `2026-07-24_fix_fn_pst_next_id_dtl_ids_no_numericos.sql` | sí (24-jul) | **pendiente** | Depende del 14. |
| 16 | `2026-07-27_proveedor_contactos.sql` | sí (27-jul) | **pendiente** | |
| 17 | `2026-07-27_bitacora_config_contactos.sql` | **no** | **pendiente** | Depende de 8, 8b y 16. |
| 18 | `2026-07-28_presupuesto_completar_ddl_valor_real.sql` | sí (28-jul) | **pendiente** | Depende del 14. ⚠️ **NO** aplicar el `ddl_v3` equivalente: revierte el multitenant. |
| 19 | `2026-07-29_alm_articulo_activo.sql` | **sí (hoy)** | **pendiente** | ⚠️ **Con binario**: el código lee/escribe `alm_articulo.activo`. |
| 20 | `2026-07-30_alm_tipo_articulo_impuesto_tasa.sql` | **sí (hoy)** | **pendiente** | ⚠️ **Con binario.** Depende de `cfg_impuesto_tasa` (ver grupo B). |
| 21 | `2026-07-30_alm_carga_inicial.sql` | **sí (hoy)** | **pendiente** | ⚠️ **Con binario** y **depende del grupo B**. |
| 21b | *(validación del 21)* | **sí (hoy)** | **pendiente** | Escanea `alm_kardex`. Correr fuera de la ventana crítica. |
| 22 | `2026-07-30_cfg_compra_isv.sql` | ¿? | **pendiente** | Otra línea de trabajo (ISV por empresa). |
| 23 | `2026-07-30_alm_orden_compra.sql` | ¿? | **pendiente** | Otra línea de trabajo (órdenes de compra). |
| 24 | `2026-07-31_alm_articulo_bodega_mover_a_bodega_01.sql` | **sí (31-jul)** | **pendiente** | ⚠️ **One-shot**, asume `company_id = 2` e ids `PRIN = 1` / `01 = 2`. **Prerrequisito del corte de inventario.** |
| 25 | `2026-07-31_alm_compra_recepcion.sql` | **sí (31-jul)** | **pendiente** | ⚠️ **Con binario** (captura de compras, aún sin implementar). Depende del 23. |
| 26 | `2026-08-01_alm_requisicion_descargo.sql` | **sí (01-ago)** | **pendiente** | ⚠️ **Con binario** + **backfill de 42.866 filas** + **DROP de `ix_alm_requisicion_pendiente`**. Depende del grupo B §3.1b. |
| — | `2026-07-16_backup_sp_obtener_cliente_saldo.sql` | — | **NO APLICAR** | Es el *rollback* del paso 6. Guardarlo por si hay que revertir. |

---

## 3. Grupo B — el grupo de riesgo: sin registrar y sin confirmar

Estos **no tienen paso en el runbook** y su estado en el SRV **no consta**. Son los que
pueden hacer fallar un despliegue.

### 3.1 Base del kardex — **prerrequisito duro del paso 21**

| Script | Qué aporta | Si falta en SRV |
|---|---|---|
| `2026-07-09_alm_kardex_bodega_id.sql` | `alm_kardex.bodega_id` | El kardex por bodega no funciona |
| `2026-07-13_alm_kardex_articulo_id.sql` | `alm_kardex.articulo_id` + backfill | Las guardas por artículo no encuentran movimientos |
| `2026-07-14_alm_kardex_trazabilidad.sql` | Las 8 columnas del libro nuevo (`uuid`, `documento_tipo`, `existencia_resultante`…), el CHECK, el índice único y el **trigger de inmutabilidad** | **El paso 21 se detiene** (tiene guarda) y **el motor de posteo no puede escribir** |
| `2026-07-14_alm_kardex_ampliar_precisiones.sql` | Precisión de importes | Truncamiento de decimales |
| `2026-07-14_alm_kardex_fk_articulo_restrict.sql` | FK `ON DELETE RESTRICT` | Asientos huérfanos |
| `2026-07-14_alm_fk_compuestas_tenant.sql` | FK compuestas por tenant + claves alternas `uq_alm_articulo_company_id` / `uq_alm_bodega_company_id` | **El paso 21 se detiene** (las necesita para las FK de `alm_ajuste_inventario`) |

> ⚠️ **Estos seis dejan de ser re-ejecutables una vez activo `trg_alm_kardex_inmutable`**:
> hacen `UPDATE` de backfill sobre `alm_kardex` y fallarían con `K0001`.

**En el mirror están aplicados** (verificado hoy: 8/8 columnas de trazabilidad, las 2 claves
alternas y el trigger activo). **Confirmar en el SRV es la Fase 0(a)** del diseño
`docs/plans/2026-07-29-carga-inicial-existencias-kardex-design.md`.

Consulta para confirmar los seis de un golpe:

```sql
SELECT
  (SELECT count(*) FROM information_schema.columns
    WHERE table_name='alm_kardex'
      AND column_name IN ('uuid','documento_tipo','documento_id','bodega_destino_id',
                          'existencia_resultante','costo_promedio_resultante',
                          'usuariocreacion','fechacreacion'))                    AS trazabilidad_8,
  (SELECT count(*) FROM pg_constraint
    WHERE conname IN ('uq_alm_articulo_company_id','uq_alm_bodega_company_id'))  AS claves_alternas_2,
  (SELECT count(*) FROM pg_trigger WHERE tgname='trg_alm_kardex_inmutable')      AS trigger_1;
-- Esperado: 8 | 2 | 1
```

### 3.1b Base de los DOCUMENTOS de almacén — **prerrequisito de los pasos 21 y 25**

Detectado el 2026-07-31 al diseñar requisiciones: este script **no figuraba en ninguna parte**
de este registro ni del runbook, y de él dependen la recepción de compras (paso 25, ya
implementada) y todo el flujo de requisiciones/descargos.

| Script | Qué aporta | Si falta en SRV |
|---|---|---|
| `2026-07-14_alm_documentos_bodega_posteo.sql` | `bodega_id`, `posteado`, `fecha_posteo`, `uuid`, `origen` en **`alm_compra`, `alm_requisicion` y `alm_descargo`**; los CHECK de coherencia (`ck_*_bodega_si_siad`, `ck_*_uuid_si_siad`, `ck_*_posteo`), los índices únicos de idempotencia (`uq_*_company_uuid`), los índices parciales de pendientes y los **3 triggers de blindaje** (`trg_alm_compra_blindaje`, `trg_alm_requisicion_blindaje`, `trg_alm_descargo_blindaje`, SQLSTATE K0002) | **El motor no puede postear ningún documento**: sin `uuid` no hay idempotencia y sin `origen` el histórico SIMAFI no está blindado. El paso 25 falla al insertar. |

> ⚠️ Contiene `UPDATE` de backfill sobre las tres tablas (`origen = 'SIMAFI'`, `posteado = true`)
> y **suelta y recrea los tres triggers de blindaje** dentro de la misma transacción. Re-ejecutarlo
> con datos SIAD ya cargados es seguro para la estructura (todo es `IF NOT EXISTS` / `DROP … ADD`),
> pero **revisar el backfill antes**: marcaría como SIMAFI cualquier fila sin `origen`.

**En el mirror está aplicado** (verificado: las 5 columnas existen en las tres tablas y los tres
triggers están activos). En el SRV **no consta**.

```sql
SELECT count(*) FROM information_schema.columns
 WHERE table_name IN ('alm_compra','alm_requisicion','alm_descargo')
   AND column_name IN ('origen','posteado','fecha_posteo','uuid','bodega_id');   -- esperado: 15
SELECT count(*) FROM pg_trigger
 WHERE tgname IN ('trg_alm_compra_blindaje','trg_alm_requisicion_blindaje','trg_alm_descargo_blindaje');
-- esperado: 3
```

### 3.2 Catálogo del ISV — **prerrequisito del paso 20**

`2026-07-14_cfg_impuestos.sql` — crea `cfg_impuesto` / `cfg_impuesto_tasa`. El runbook lo
**menciona como advertencia** pero **no le da paso propio**. Requiere además la extensión
`btree_gist`. En el mirror existe con 4 tasas (EXENTO, EXONERADO, ISV15 15%, ISV18 18%).

```sql
SELECT to_regclass('public.cfg_impuesto_tasa') AS existe;   -- NULL = falta
```

### 3.3 Aplicados hoy al mirror, sin registrar en el runbook

| Script | Naturaleza | ⚠️ |
|---|---|---|
| `2026-07-16_libretas_globales.sql` | **Datos + objetos** | ⚠️ Cambia datos de negocio: normaliza 33 indicativos de cliente y 24 planillas, corrige 1 cliente puntual (**hardcodea `company_id = 2`**), **desactiva 5 credenciales de lector**, **anula el `codciclo` de las 10**, y reemplaza 3 funciones de ciclo (`fn_adm_ruta_de_indicativo`, `fn_adm_periodo_ciclo_info`, `fn_adm_periodo_ciclo_rutas_pendientes`) para derivar las rutas de los CLIENTES. **Sin bloque de ROLLBACK.** |
| `2026-07-16_codigo_cliente_automatico.sql` | Aditivo | Tabla `adm_codigo_cliente_config` + 2 funciones. Semilla asume `company_id = 2`. |
| `2026-07-30_saneo_libretas_fantasma.sql` | Datos (3 filas) | Borra la libreta `OOL1`, desactiva su ruta legacy y **desactiva al cliente `09013580`**. Idempotente y con ROLLBACK escrito. Depende de `libretas_globales`. |

> Las cifras de `libretas_globales` son las **del mirror**. En el SRV pueden diferir:
> **medilas antes de aplicar** con las consultas de conteo del propio script.

### 3.4 Retenciones a proveedores — Fase F1 (iniciativa nueva, 2026-08-06)

Primer script de la iniciativa de retenciones a proveedores (rama
`feat/almacen-integracion-contable`). **Aditivo idempotente, bajo riesgo:** crea tres tablas
nuevas y **no toca ninguna existente** (sin `DROP`/`ALTER`/`TRUNCATE`).

| Script | Qué aporta | Naturaleza | Mirror | SRV |
|---|---|---|:--:|:--:|
| `2026-08-06_cfg_retenciones.sql` | `cfg_retencion` + `cfg_retencion_tasa` (catálogo GLOBAL, con vigencia y EXCLUDE gist) + `prv_retencion_cuenta` (cuenta del pasivo por empresa) + semilla (ISR 12.5% y 1% con tasa; ISV retenido **solo concepto, sin tasa** — pendiente D2, confirmar % con SAR) | Aditivo idempotente (`CREATE … IF NOT EXISTS`, `INSERT … ON CONFLICT` / `WHERE NOT EXISTS`) — **re-ejecutable** | **sí (2026-08-06)** | **pendiente** |

- **Dependencias:** solo tablas base ya en producción — `cfg_company(company_id)` y
  `con_plan_cuentas(account_id)` — más la extensión `btree_gist` (ya presente por `cfg_impuestos`).
  **No** depende de la tanda de almacén ni asume ningún `company_id` (la tabla tenant no se siembra).
- **Con binario:** la pantalla `/mantenimientos/retenciones` y su API (`api/retenciones`) leen estas
  tablas; aplicar el SQL en la misma ventana que el despliegue del portal de la iniciativa.

Comando:

```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-06_cfg_retenciones.sql
```

¿Ya aplicado?

```sql
SELECT to_regclass('public.cfg_retencion')        AS concepto,       -- NULL = falta
       to_regclass('public.cfg_retencion_tasa')   AS tasa,
       to_regclass('public.prv_retencion_cuenta') AS cuenta_empresa;
```

Verificación (ISV-RET debe salir SIN tasa):

```sql
SELECT r.codigo, r.base_calculo, r.tipo_impuesto, t.porcentaje, t.vigencia_desde
FROM cfg_retencion r LEFT JOIN cfg_retencion_tasa t ON t.retencion_id = r.id
ORDER BY r.codigo;   -- ISR-PROV 1.00 | ISR-SERV 12.50 | ISV-RET (sin tasa)
```

### 3.5 Retenciones a proveedores — Fase F0 (posteo al mayor, 2026-08-07)

Segundo script de la iniciativa de retenciones (rama `feat/almacen-integracion-contable`).
**Aditivo idempotente, sin DDL ni borrado:** solo datos de configuración contable; **no toca**
ninguna tabla existente con `ALTER`/`DROP`/`TRUNCATE`.

| Script | Qué aporta | Naturaleza | Mirror | SRV |
|---|---|---|:--:|:--:|
| `2026-08-07_con_integracion_prov_activar.sql` | Para `company_id=2`: fila `con_integracion_asiento` module='PROV' (journal_id/type_id resueltos por el mismo fallback de OPD; en el mirror = 1/1) + `con_integracion_config.activo_proveedores=TRUE` | Datos idempotente (`INSERT … WHERE NOT EXISTS`, `UPDATE` idempotente) — **re-ejecutable**; aborta con `RAISE` si falta la config o no hay diario/tipo activo | **sí (2026-08-07)** | **pendiente** |

- **Dependencias:** la empresa debe tener fila en `con_integracion_config` (ya existe; la usan
  Ventas/Caja/Bancos) y ≥1 diario y ≥1 tipo de partida activos (ya existen). `'PROV'` ya está en el
  CHECK de `con_integracion_asiento` y `activo_proveedores` ya existe (fase2 de integración contable)
  → **no** requiere script de estructura.
- **Asume `company_id = 2`** (única con `con_integracion_config` en el mirror). Confirmar el tenant
  real antes de aplicar en SRV.
- **⚠️ Con binario y cambio de flujo de dinero:** enciende que el asiento de PAGO de compromisos
  (proveedor DEBE / retención HABER / banco HABER) se postee **POSTED al mayor** por el motor
  `sp_con_generar_comprobante_config`, en vez de quedar en borrador. El binario de F0
  (`OrdenesPagoDirectoService` + `PrvContabilidad`) lo lee al procesar/abonar/anular. **Aplicar en la
  misma ventana que el despliegue del portal.** Las 23 partidas PROV históricas en borrador se dejan
  como histórico (F0 aplica solo a nuevas).

Comando:

```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-07_con_integracion_prov_activar.sql
```

¿Ya aplicado?

```sql
SELECT c.activo_proveedores, a.journal_id, a.type_id
  FROM con_integracion_config c
  LEFT JOIN con_integracion_asiento a ON a.company_id = c.company_id AND a.module = 'PROV'
 WHERE c.company_id = 2;   -- Esperado: activo_proveedores = t | journal_id y type_id NO nulos
```

### 3.6 Retenciones a proveedores — Fase F4 (registro fiscal hdr/dtl, 2026-08-07)

Tercer script de la iniciativa de retenciones (rama `feat/almacen-integracion-contable`).
**Aditivo idempotente, bajo riesgo:** crea tres tablas nuevas y **no toca ninguna existente**
(sin `DROP`/`ALTER`/`TRUNCATE`).

| Script | Qué aporta | Naturaleza | Mirror | SRV |
|---|---|---|:--:|:--:|
| `2026-08-07_prv_retencion_hdr_dtl.sql` | `prv_retencion_hdr` (libro fiscal: una cabecera por pago, `estado_id` 1/9, folio por empresa, ligada a `partida_id` + `numero_abono`) + `prv_retencion_dtl` (una fila por retención, snapshot código/nombre/%/base) + `prv_retencion_correlativo` (contador de folio por empresa) | Aditivo idempotente (`CREATE … IF NOT EXISTS`) — **re-ejecutable** | **sí (2026-08-07)** | **pendiente** |

- **Dependencias:** `prv_compromiso_hdr(company_id, numero_orden)` (FK compuesta), `cfg_retencion(id)`
  (F1 §3.4 — aplicar antes) y `con_plan_cuentas(account_id)`. El script aborta con `RAISE` si falta
  alguna. **No** asume ningún `company_id` (las tablas no se siembran).
- **⚠️ Con binario:** el registro hdr/dtl lo escribe el binario de F4 (`OrdenesPagoDirectoService`
  + `RetencionRegistroService`) al procesar/abonar/anular, y la pantalla `/proveedores/retenciones`
  lo consulta. **Aplicar en la misma ventana que el despliegue del portal.**

Comando:

```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-07_prv_retencion_hdr_dtl.sql
```

¿Ya aplicado?

```sql
SELECT to_regclass('public.prv_retencion_hdr')         AS hdr,   -- NULL = falta
       to_regclass('public.prv_retencion_dtl')         AS dtl,
       to_regclass('public.prv_retencion_correlativo') AS folio;
```

### 3.7 Términos de pago del proveedor — catálogo (2026-08-11)

Script de la iniciativa (rama `feat/almacen-integracion-contable`). **Aditivo idempotente, bajo
riesgo:** crea una tabla nueva y agrega una columna nullable con FK; **no borra ni reescribe datos**
(sin `DROP`/`TRUNCATE`; el `ALTER` solo agrega la columna).

| Script | Qué aporta | Naturaleza | Mirror | SRV |
|---|---|---|:--:|:--:|
| `2026-08-11_alm_termino_pago.sql` | `alm_termino_pago` (catálogo por empresa: `nombre` + `dias` de crédito + `es_default` + `activo`; UNIQUE(company_id, nombre), índice único parcial de `es_default`, CHECK `dias >= 0`) + `alm_compra_hdr.termino_pago_id` (FK NULL `ON DELETE SET NULL`) + índice | Aditivo idempotente (`CREATE/ADD … IF [NOT] EXISTS`) — **re-ejecutable** | **sí (2026-08-11)** | **pendiente** |

- **Dependencias:** `alm_compra_hdr` (paso 25, `2026-07-31_alm_compra_recepcion.sql`) debe existir para
  el `ADD COLUMN`. **No** asume ningún `company_id` (el catálogo arranca **vacío**, sin seed: lo llena
  el usuario en la nueva pantalla).
- **⚠️ Con binario:** el catálogo (pantalla `/almacen/terminos-pago` + API `api/almacen/terminos-pago`)
  y el cableo en la factura de recepción (combo del término + autocálculo del vencimiento) leen estas
  tablas. **Aplicar en la misma ventana que el despliegue del portal.** El SQL sin el binario es inocuo.

Comando:

```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-11_alm_termino_pago.sql
```

¿Ya aplicado?

```sql
SELECT to_regclass('public.alm_termino_pago') AS catalogo,   -- NULL = falta
       (SELECT count(*) FROM information_schema.columns
         WHERE table_name = 'alm_compra_hdr' AND column_name = 'termino_pago_id') AS fk_en_hdr;  -- esperado: 1
```

**Semilla (OPCIONAL):** `2026-08-11_alm_termino_pago_seed.sql` inserta 5 términos base para
`company_id = 2` (Contado 0 d **predeterminado**, Crédito 15/30/45/60 d). Idempotente
(`ON CONFLICT (company_id, nombre) DO NOTHING`). **⚠️ Asume `company_id = 2`**; en el SRV la empresa
puede preferir definir los suyos desde la pantalla — el seed es opcional. Va **después** del script de
estructura. Aplicado al mirror el 2026-08-11.

```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-11_alm_termino_pago_seed.sql
```

### 3.8 Término de pago por proveedor (2026-08-11)

Continuación de §3.7 (rama `feat/almacen-integracion-contable`). **Aditivo idempotente, bajo riesgo:**
agrega una columna nullable con FK; **no borra ni reescribe datos**.

| Script | Qué aporta | Naturaleza | Mirror | SRV |
|---|---|---|:--:|:--:|
| `2026-08-11_prv_proveedor_termino_pago.sql` | `prv_proveedores.termino_pago_id` (FK NULL → `alm_termino_pago` `ON DELETE SET NULL`) + índice | Aditivo idempotente (`ADD/CREATE … IF [NOT] EXISTS`) — **re-ejecutable** | **sí (2026-08-11)** | **pendiente** |

- **Dependencias:** `alm_termino_pago` (§3.7 — aplicar **antes**). No asume ningún `company_id`.
- **⚠️ Con binario:** el combo "Término de pago" en el maestro de proveedores (`ProveedoresService` /
  `ProveedorForm`) y la precarga al elegir el proveedor en la factura de recepción leen esta columna.
  **Aplicar en la misma ventana que el despliegue del portal.** El SQL sin el binario es inocuo.

Comando:

```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-11_prv_proveedor_termino_pago.sql
```

¿Ya aplicado?

```sql
SELECT count(*) AS fk_en_proveedor FROM information_schema.columns
 WHERE table_name = 'prv_proveedores' AND column_name = 'termino_pago_id';   -- esperado: 1
```

### 3.9 Condición de pago de la factura — Fase 0 (2026-08-12)

Primer paso de la iniciativa "facturas al crédito → CxP" (rama `feat/almacen-integracion-contable`).
**Aditivo, bajo riesgo:** agrega una columna con default + CHECK; no borra ni reescribe otros datos.

| Script | Qué aporta | Naturaleza | Mirror | SRV |
|---|---|---|:--:|:--:|
| `2026-08-12_alm_compra_condicion_pago.sql` | `alm_compra_hdr.condicion_pago` SMALLINT NOT NULL DEFAULT 1 (1=Contado/2=Crédito/3=Prepagado) + CHECK `ck_alm_compra_hdr_condicion_pago` | Aditivo idempotente (`ADD COLUMN IF NOT EXISTS` + guard del CHECK) — **re-ejecutable** | **sí (2026-08-12)** | **pendiente** |

- **Dependencias:** `alm_compra_hdr` (paso 25). No asume ningún `company_id`. Las facturas existentes quedan en 1 (Contado) por el default.
- **⚠️ Con binario:** el servidor deriva `condicion_pago` del término (0 días=Contado, >0=Crédito) al registrar la factura, y la pantalla muestra la condición. **Aplicar en la misma ventana que el despliegue del portal.**

Comando:

```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-12_alm_compra_condicion_pago.sql
```

¿Ya aplicado?

```sql
SELECT count(*) AS col FROM information_schema.columns
 WHERE table_name = 'alm_compra_hdr' AND column_name = 'condicion_pago';   -- esperado: 1
```

### 3.10 Cuenta por pagar de compra + abonos — Fase 1 (2026-08-12)

Fase 1 de "facturas al crédito → CxP" (rama `feat/almacen-integracion-contable`). **Aditivo:** dos
tablas nuevas; **no toca ninguna existente**. La CxP se genera **hacia adelante** (al registrar cada
factura); el histórico no se backfillea.

| Script | Qué aporta | Naturaleza | Mirror | SRV |
|---|---|---|:--:|:--:|
| `2026-08-12_alm_compra_cxp.sql` | `alm_compra_cxp` (cuenta por pagar 1:1 con la factura: proveedor, vencimiento, condición, monto, **saldo materializado**, `estado_id` 1/2/3/9; UNIQUE por factura + clave alterna tenant; FK compuesta → `alm_compra_hdr`) + `alm_compra_cxp_abono` (pagos: método, banco, cheque, `partida_id`, estado 'V'/'A'; UNIQUE por numero_abono; FK → `alm_compra_cxp`) | Aditivo idempotente (`CREATE … IF NOT EXISTS`) — **re-ejecutable** | **sí (2026-08-12)** | **pendiente** |

- **Dependencias:** `alm_compra_hdr` (paso 25) con su clave alterna `uq_alm_compra_hdr_tenant` (para la FK compuesta) y `condicion_pago` (§3.9). No asume ningún `company_id`.
- **⚠️ Con binario:** el servicio genera la CxP al registrar la factura y la anula al anular; el servicio/pantalla de pagos (F1b/F1c) leen estas tablas. **Aplicar en la misma ventana que el despliegue del portal.**
- **Contabilidad:** el asiento de nacimiento (proveedor al HABER) **NO** entra aquí — es Fase 2, D-1 del contador.

Comando:

```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-12_alm_compra_cxp.sql
```

¿Ya aplicado?

```sql
SELECT to_regclass('public.alm_compra_cxp') AS cxp, to_regclass('public.alm_compra_cxp_abono') AS abono;
```

**Backfill (datos, opcional pero recomendado):** `2026-08-12_alm_compra_cxp_backfill.sql` genera la CxP
de las facturas **ya registradas** (vigentes y no prepagadas) con estado Pendiente y saldo = total; las
**anuladas no llevan CxP** y las prepagadas tampoco. Idempotente (`INSERT … WHERE NOT EXISTS`). Va
**después** de la estructura (§3.10) y del binario. Aplicado al mirror el 2026-08-12 (**9 CxP, L 107,257.25**).
El histórico SIMAFI (`alm_compra` sin cabecera) no aplica.

```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-12_alm_compra_cxp_backfill.sql
```

### 3.11 Integración contable de compras — módulo propio COMPRAS, Fase 2 (2026-08-12)

Fase 2 de compras a proveedor: Compras/CxP se vuelve un **módulo contable independiente**, como
VENTAS, CAJA, BANCOS, PROV, ALMACEN. Un solo flag `activo_compras` gatea los **dos** asientos del
ciclo, ambos con `module='COMPRAS'`:

- **factura** de compra → DEBE inventario / HABER proveedor
- **pago** de la CxP (bancario) → DEBE proveedor / HABER banco

Separado de inventario (ALMACEN) **y** de los pagos OPD (PROV): toda la contabilidad de compras en
un diario/módulo propio.

| Script | Qué aporta | Naturaleza | Mirror | SRV |
|---|---|---|:--:|:--:|
| `2026-08-12_con_integracion_compras_modulo.sql` | (0) amplía el CHECK `ck_con_integracion_asiento_module` para admitir `'COMPRAS'` (7→8 módulos, no quita ninguno); (1) `con_integracion_config.activo_compras` BOOLEAN NOT NULL DEFAULT false; (2) siembra la fila `con_integracion_asiento (module='COMPRAS')` copiando diario/tipo de `ALMACEN` | Aditivo + ampliación de CHECK · idempotente (`ADD COLUMN IF NOT EXISTS`, `INSERT … WHERE NOT EXISTS`, re-CREATE del CHECK) — **re-ejecutable** | **sí (2026-08-12)** | **pendiente** |

- **Dependencias:** `con_integracion_config`, `con_integracion_asiento` (con la fila `ALMACEN` de la empresa, para copiar el default). No asume un `company_id` fijo (multiempresa).
- **⚠️ Con binario:** la recepción postea la factura y `CompraCxpService` postea el pago, ambos **gated** por `activo_compras`; el reverso (anular factura / anular abono) revierte el asiento. **Aplicar en la misma ventana que el despliegue del portal.** Con el flag **apagado** (default) el binario no genera ninguna póliza (verificado por test) → despliegue seguro: NO se auto-activa.
- **Para encenderlo** (decisión del contador): `UPDATE con_integracion_config SET activo_compras = TRUE WHERE company_id = <id>;` + que las cuentas resuelvan: la fila `con_integracion_asiento` de `COMPRAS` (la siembra este script; el contador ajusta el diario/tipo), `prv_proveedores.cuenta_contable` posteable (los 237 vacíos, ver §3.12) y `cuenta_inventario` posteable en cada `alm_tipo_articulo` (§3.12).

Comando:

```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-12_con_integracion_compras_modulo.sql
```

¿Ya aplicado?

```sql
SELECT (SELECT count(*) FROM information_schema.columns
         WHERE table_name='con_integracion_config' AND column_name='activo_compras') AS flag,        -- esperado 1
       (SELECT count(*) FROM con_integracion_asiento WHERE module='COMPRAS') AS asientos_compras;    -- >= 1 (por empresa con ALMACEN)
```

> **Historia:** un primer intento (2026-08-12) usó el flag propio `activo_compras` reusando el módulo
> ALMACEN; se cambió a gatear por `activo_almacen`/`activo_proveedores` (unificado); y finalmente, por
> decisión del usuario, se hizo el **módulo COMPRAS propio** (esta versión). El script del primer
> intento (`2026-08-12_con_integracion_config_activo_compras.sql`) se eliminó; en SRV nunca se aplicó.

### 3.12 Saneo — formato de la cuenta de inventario del tipo (2026-08-12)

Prerrequisito de datos para **encender** la contabilidad de compras (§3.11) — es decir, con
`activo_compras` = TRUE. El asiento de la factura resuelve la cuenta de inventario con igualdad
**exacta** contra `con_plan_cuentas.code`. Los
tipos migrados guardan el código **con guiones** (`114-01-01-02-01`) y el plan lo tiene **sin
guiones** (`11401010201`): mismo código, distinto formato, y por eso el binario no lo resuelve. Este
script quita los guiones **sólo** cuando el código normalizado ya existe y es posteable en el plan de
la misma empresa.

| Script | Qué aporta | Naturaleza | Mirror | SRV |
|---|---|---|:--:|:--:|
| `2026-08-12_alm_tipo_articulo_normalizar_cuenta_inventario.sql` | `UPDATE` correctivo de `alm_tipo_articulo.cuenta_inventario` (quita guiones) con guardia `EXISTS` (sólo si el código normalizado existe y `allows_posting`). Sin `company_id` fijo → multiempresa | **Correctivo de datos, no destructivo, reversible, idempotente** (`WHERE cuenta_inventario ~ '\D'`) — **re-ejecutable** | **sí (2026-08-12)** | **pendiente** |

- **Dependencias:** `alm_tipo_articulo` y `con_plan_cuentas`. No toca estructura. Sólo relevante si se va a encender la contabilidad de compras (`activo_compras`, §3.11).
- **⚠️ Datos:** modifica filas existentes. Reversible: el script trae el mapeo y el `ROLLBACK` manual comentado. Aplicado al **mirror** el 2026-08-12 (**8 filas, empresa 2, tipos 02–09**; verificado: 0 tipos sin cuenta posteable). SRV pendiente. Respaldo del estado previo en `scratchpad/respaldo_cuenta_inventario_antes.csv`.
- Sólo cubre `cuenta_inventario` — es la única con el desajuste de guiones. **Verificado (2026-08-12):** las otras 4 cuentas del tipo (`cuenta_costo_ventas`, `cuenta_ventas`, `cuenta_ajustes`, `cuenta_devoluciones`) **no** tienen problema de formato, están **vacías**; sólo habrá que asignarlas si esos flujos (ventas/ajustes/devoluciones) se integran al mayor, lo cual no afecta al flag de compras.

Comando:

```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-12_alm_tipo_articulo_normalizar_cuenta_inventario.sql
```

¿Ya aplicado? (debe devolver 0)

```sql
SELECT count(*) AS tipos_sin_cuenta_posteable
  FROM alm_tipo_articulo t
 WHERE t.cuenta_inventario IS NOT NULL AND btrim(t.cuenta_inventario) <> ''
   AND NOT EXISTS (SELECT 1 FROM con_plan_cuentas c
                     WHERE c.company_id = t.company_id AND btrim(c.code) = btrim(t.cuenta_inventario) AND c.allows_posting);
```

> Los **proveedores** sin cuenta (237 en el mirror, empresa 2) **no** son de formato: están vacíos y
> requieren que el contador asigne la cuenta por pagar. No hay script — es carga de datos en el maestro.

### 3.13 Correo y notificaciones por empresa — mantenimiento SendGrid, F1 (2026-08-13)

Primer script de la iniciativa de correo/notificaciones (rama `feat/almacen-integracion-contable`).
**Aditivo idempotente, bajo riesgo:** crea tres tablas nuevas y **no toca ninguna existente** (sin
`DROP`/`ALTER`/`TRUNCATE`).

| Script | Qué aporta | Naturaleza | Mirror | SRV |
|---|---|---|:--:|:--:|
| `2026-08-13_cfg_correo_notificaciones.sql` | `cfg_correo` (la **conexión**, 1 por empresa: proveedor + **API key cifrada** con DataProtection + remitente por defecto + activo global; UNIQUE(company_id, proveedor), CHECK proveedor ∈ SENDGRID/SMTP) + `cfg_notificacion` (**área/tipo**, N por empresa: remitente propio opcional; UNIQUE(company_id, tipo), CHECK tipo ∈ ADMINISTRACION/ALMACEN/COBRANZA/SISTEMA) + `cfg_notificacion_destinatario` (**destinatarios** TO/CC, N por área; FK → `cfg_notificacion` ON DELETE CASCADE, UNIQUE(notificacion_id, lower(correo), clase)) | Aditivo idempotente (`CREATE … IF NOT EXISTS`) — **re-ejecutable** | **sí (2026-08-13)** | **pendiente** |

- **Dependencias:** ninguna externa. La única FK es interna (destinatario → notificación, ambas en
  este script). Tenant-scoped por `company_id` (sin FK a `cfg_company`, igual que el resto). **No**
  asume ningún `company_id`: las tablas arrancan **vacías** (sin seed) — las filas las crea la
  pantalla y el catálogo de tipos lo define el código.
- **⚠️ Con binario (pendiente):** el mantenimiento `/configuracion/correo` + su API y el servicio
  `ICorreoConfigService` (Fases F2/F3, **aún sin implementar**) leen/escriben estas tablas. El SQL
  **sin** el binario es inocuo. Aparte, la Fase **F0** ya cableó el key-ring de DataProtection en
  producción (`Program.cs`), prerrequisito para que el binario pueda **descifrar** la API key que se
  guarde aquí; el SQL no depende de F0.

Comando:

```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-13_cfg_correo_notificaciones.sql
```

¿Ya aplicado?

```sql
SELECT to_regclass('public.cfg_correo')                    AS conexion,   -- NULL = falta
       to_regclass('public.cfg_notificacion')              AS area,
       to_regclass('public.cfg_notificacion_destinatario') AS destinatario;
```

### 3.14 Saneo — numero_cuenta de las cuentas de banco (2026-08-13)

El combo "Cuenta del banco" del pago a proveedores (`/almacen/compras/pagos`) muestra ahora **sólo**
`ban_cuenta.numero_cuenta`. Algunos valores migrados traen ruido en el propio dato: sufijo
desambiguador `"… (SIMS06)"` o prefijo `"Cta. "` / `"Cta. No."`. Este script deja el número limpio.

| Script | Qué aporta | Naturaleza | Mirror | SRV |
|---|---|---|:--:|:--:|
| `2026-08-13_ban_cuenta_sanear_numero_cuenta.sql` | `UPDATE` correctivo de `ban_cuenta.numero_cuenta`: quita el sufijo `"(…)"` y el prefijo `"Cta."/"Cta. No."`. **Sólo cuentas `activo = true`** (las que aparecen en el combo) + **guardia anti-colisión** `NOT EXISTS` contra el `UNIQUE (company_id, numero_cuenta)`. Sin `company_id` fijo → multiempresa | **Correctivo de datos, no destructivo, reversible, idempotente** — **re-ejecutable** | **pendiente** | **pendiente** |

- **Dependencias:** ninguna (sólo `ban_cuenta`). No toca estructura, no lleva binario. Independiente del resto de la tanda.
- **⚠️ Datos + UNIQUE:** el sufijo `"(SIMSxx)/(SIMCxxxx)"` **desambigua** cuentas que comparten el número base (p.ej. SIMS01/SIMS06 = `11-701-000572-3`). Por eso el saneo toca **sólo las activas** y lleva guardia `NOT EXISTS`; las inactivas con el mismo número base conservan su sufijo (histórico invisible en el combo). En el mirror (empresa 2) afecta **5 filas activas** (SIMS06, SIMS10, SIMS11, SIMS12, SIMS13); las inactivas con ruido (SIMS01, SIMC1140, SIMC2586, SIMC7535, SIMC7753) **no** se tocan. Reversible: el script trae el `ROLLBACK` manual comentado con los valores originales.

Comando:

```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-13_ban_cuenta_sanear_numero_cuenta.sql
```

¿Ya aplicado? (debe devolver 0)

```sql
SELECT count(*) AS cuentas_activas_con_ruido
  FROM public.ban_cuenta
 WHERE activo = true
   AND (numero_cuenta ~ '\([^)]*\)\s*$' OR numero_cuenta ~* '^\s*Cta\.');
```

### 3.15 Bancos — 'COMPRA_CXP' en el CHECK ck_ban_cheque_origen (2026-08-13)

**Bug de despliegue** hallado probando el pago con **cheque** desde compras: el cheque se emite con
origen `ChequeOrigen.CompraCxp = 'COMPRA_CXP'` (`SIAD.Core/DTOs/Bancos/ChequesDtos.cs`), pero el CHECK
`ck_ban_cheque_origen` sólo permitía PROCESAR/ABONO/TRANSACCION/MANUAL → el INSERT en `ban_cheque`
fallaba y el pago con cheque se revertía entero. Efectivo/transferencia no tocan `ban_cheque`, por eso
no se veía; los tests de CompraCxp usan transferencia. **Prerrequisito para pagar compras con cheque.**

| Script | Qué aporta | Naturaleza | Mirror | SRV |
|---|---|---|:--:|:--:|
| `2026-08-13_ban_cheque_origen_compra_cxp.sql` | `ALTER` del CHECK `ck_ban_cheque_origen` para incluir `'COMPRA_CXP'` (DROP IF EXISTS + ADD con la lista completa). No asume `company_id` | **Aditivo** (sólo agrega un valor permitido), no destructivo, **idempotente / re-ejecutable** | **sí (2026-08-13)** | **pendiente** |

- **Dependencias:** `ban_cheque` (estructura antigua, ya en SRV). No lleva binario nuevo (el código que usa 'COMPRA_CXP' ya está en la rama de compras). Independiente del resto de la tanda.

Comando:

```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-13_ban_cheque_origen_compra_cxp.sql
```

¿Ya aplicado? (debe listar 'COMPRA_CXP' en la definición)

```sql
SELECT pg_get_constraintdef(oid) AS def FROM pg_constraint WHERE conname = 'ck_ban_cheque_origen';
```

---

### 3.16 Proveedores — estado de cuenta: 3 funciones de lectura (2026-08-13)

**F0** de la iniciativa "estado de cuenta del proveedor"
([docs/plans/2026-08-13-proveedor-estado-cuenta-plan.md](../docs/plans/2026-08-13-proveedor-estado-cuenta-plan.md)).
No existía nada equivalente para proveedores: todo lo de saldo/antigüedad en esta base es de
clientes. Las funciones unifican las **dos** ramas donde vive la deuda —`alm_compra_cxp`(+abono)
y `prv_compromiso_hdr`(+`prv_compromiso_abono`)— y concentran ahí las reglas de vigencia, incluida
la **compat legacy** del compromiso procesado sin abonos (si no se respeta, el estado de cuenta
inventa los ~L 6.8M de las 228 órdenes migradas de SIMAFI).

| Script | Qué aporta | Naturaleza | Mirror | SRV |
|---|---|---|:--:|:--:|
| `2026-08-13_prv_estado_cuenta.sql` | `CREATE OR REPLACE FUNCTION` × 3: `fn_prv_estado_cuenta_documentos` (base, dueña de las reglas de vigencia), `fn_prv_estado_cuenta_resumen` (saldo, vencido, antigüedad, último pago) y `fn_prv_estado_cuenta_movimientos` (libro con saldo corrido). Solo lectura. No asume `company_id` | **Objetos** — aditivo, no destructivo, **idempotente / re-ejecutable**. No crea ni altera tablas, columnas, índices ni datos | **sí (2026-08-13)** | **pendiente** |

- **Dependencias:** `alm_compra_cxp` + `alm_compra_cxp_abono` (§3.10), `prv_compromiso_abono`
  (2026-07-17) y `prv_compromiso_hdr` con `company_id` (paso 10 / 2026-07-10). Va **después** de
  §3.10; es independiente del resto de la tanda de compras.
- **Lleva binario:** sí. El backend (`ProveedorEstadoCuentaService` + `api/proveedores/{codigo}/estado-cuenta`)
  llama a estas funciones; sin el script, el endpoint responde error `42883` (función inexistente).
  El script sin el binario es inocuo.

Comando:

```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-13_prv_estado_cuenta.sql
```

¿Ya aplicado? (deben aparecer las 3 funciones)

```sql
SELECT proname, pg_get_function_identity_arguments(oid) AS args
  FROM pg_proc WHERE proname LIKE 'fn_prv_estado_cuenta%' ORDER BY proname;
```

Verificación posterior (sustituir el código de proveedor; el cuadre debe dar `true`):

```sql
SELECT (SELECT SUM(cargo) - SUM(abono) FROM fn_prv_estado_cuenta_movimientos(2, '0088', NULL, NULL))
     = (SELECT saldo_total FROM fn_prv_estado_cuenta_resumen(2, '0088', NULL)) AS cuadra;
```

> No comparar contra «la última fila» del libro con un `ORDER BY fecha DESC, tipo DESC`: el saldo
> corrido se ordena por `(fecha, tipo, origen, documento_id, desempate)`, así que con empates de
> fecha ese `LIMIT 1` cae en una fila intermedia y el cuadre da `false` sin que nada esté mal.

**Verificado en el mirror el 2026-08-13** (proveedor 0088, empresa 2): saldo total **L 73,327.50**
en 7 documentos, vencido L 69,586.00, último pago L 150.00 del 2026-08-13; los tres netos
(movimientos, documentos-todos, documentos-pendientes) coinciden exactamente.

---

### 3.17 Órdenes de compra — fecha de entrega pactada (2026-08-14)

La O/C guardaba `fecha`, `fecha_emision` y `fecha_aprobacion`, pero **no** cuándo se comprometió el
proveedor a entregar, así que la puntualidad no era medible: lo único comparable contra la recepción
era la fecha de aprobación, que mide el ciclo interno y no el cumplimiento del proveedor. Es el
insumo del criterio de mayor peso del scorecard de proveedores
([docs/prototipos/2026-08-14-evaluacion-proveedores.html](../docs/prototipos/2026-08-14-evaluacion-proveedores.html)).

| Script | Qué aporta | Naturaleza | Mirror | SRV |
|---|---|---|:--:|:--:|
| `2026-08-14_alm_orden_compra_fecha_entrega.sql` | `alm_orden_compra.fecha_entrega_pactada` (fecha comprometida de la orden) + `alm_orden_compra_detalle.fecha_entrega_pactada` (fecha propia del renglón para entregas escalonadas; NULL = rige la de la cabecera). Ambas DATE NULL, sin DEFAULT y sin CHECK | **Aditivo idempotente** (`ADD COLUMN IF NOT EXISTS` × 2 + 2 `COMMENT`) — **re-ejecutable**. No reescribe filas ni crea índices | **sí (2026-08-14)** | **pendiente** |

- **Dependencias:** paso 23 del runbook (`2026-07-30_alm_orden_compra.sql`, que crea las dos tablas).
  Independiente del resto de la tanda.
- **Lleva binario:** sí, y en los dos sentidos. El servicio nuevo **exige** la fecha al crear o editar
  una orden (decisión usuario 2026-08-14: obligatoria desde el borrador) y la lee en el listado y el
  detalle; sin la columna, EF falla con `42703` al abrir la pantalla de órdenes. El script **sin** el
  binario es inocuo: dos columnas que nadie llena.
- **Obligatoriedad:** vive en `OrdenCompraService`, no en la BD. Las órdenes creadas antes de este
  cambio quedan con la columna en NULL y siguen abriéndose y recibiéndose; al **editarlas** habrá que
  llenar la fecha (el borrador ya no se guarda sin ella). Por eso la columna admite NULL.

Comando:

```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-14_alm_orden_compra_fecha_entrega.sql
```

¿Ya aplicado? (deben salir las 2 filas, `date` / `YES` / sin default)

```sql
SELECT table_name, column_name, data_type, is_nullable, column_default
  FROM information_schema.columns
 WHERE column_name = 'fecha_entrega_pactada'
   AND table_name IN ('alm_orden_compra','alm_orden_compra_detalle')
 ORDER BY table_name;
```

Verificación posterior (ninguna orden existente debe haber cambiado):

```sql
SELECT count(*) AS ordenes, count(fecha_entrega_pactada) AS con_fecha FROM alm_orden_compra;
-- con_fecha = 0 justo después de aplicar; sube conforme se capturen órdenes nuevas.
```

**Verificado en el mirror el 2026-08-14:** las 2 columnas quedaron `date` / nullable / sin default y
las 9 órdenes existentes siguen con `con_fecha = 0`. Suite completa de `SIAD.Tests` en verde
(696 pasan, 0 fallan, 47 omitidas), con 7 pruebas nuevas de la fecha pactada.

---

### 3.18 Evaluación de proveedores — F0: estructura, semilla y métricas (2026-08-14)

**F0** de la iniciativa "evaluación de proveedores"
([docs/plans/2026-08-14-evaluacion-proveedores-plan.md](../docs/plans/2026-08-14-evaluacion-proveedores-plan.md)).
No existía nada de evaluación/calificación: lo único parecido es el estado de cuenta (§3.16), que
mide deuda, no desempeño. Crea el modelo para calificar por período y la función que calcula las
métricas automáticas desde órdenes y recepciones.

| Script | Qué aporta | Naturaleza | Mirror | SRV |
|---|---|---|:--:|:--:|
| `2026-08-14_prv_evaluacion.sql` | 6 tablas (`prv_evaluacion_periodo`, `_criterio`, `_clase`, `_hdr`, `_dtl`, `prv_recepcion_incidencia`) + `fn_prv_evaluacion_metricas` (solo lectura) + semilla de 6 criterios y 4 clases por empresa | **Aditivo idempotente** (`CREATE … IF NOT EXISTS`, `CREATE OR REPLACE FUNCTION`, `INSERT … WHERE NOT EXISTS`) — **re-ejecutable** (probado). No altera ninguna tabla existente | **sí (2026-08-14)** | **pendiente** |

- **Dependencias:** `alm_compra_hdr` (§paso 25 del runbook, por la FK compuesta de incidencias),
  `alm_orden_compra(_detalle)` (paso 23) y **§3.17** (`fecha_entrega_pactada`, sin la cual la
  función no compila). `cfg_company` para la semilla. Va **después** de §3.17.
- **Lleva binario:** todavía no. F0 es sólo la base; el servicio y las pantallas son F1–F2. El
  script sin binario es inocuo (6 tablas vacías que nadie lee).
- **La semilla es una propuesta** (D4 del plan): 6 criterios sumando 100 y 4 clases A–D. Se edita
  desde la pantalla del catálogo (F3), no con SQL.

Comando:

```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-14_prv_evaluacion.sql
```

¿Ya aplicado? (deben salir las 6 tablas y la función)

```sql
SELECT count(*) AS tablas FROM information_schema.tables
 WHERE table_name IN ('prv_evaluacion_periodo','prv_evaluacion_criterio','prv_evaluacion_clase',
                      'prv_evaluacion_hdr','prv_evaluacion_dtl','prv_recepcion_incidencia');
SELECT proname FROM pg_proc WHERE proname = 'fn_prv_evaluacion_metricas';
```

Verificación posterior (la semilla debe sumar 100 por empresa y la función correr sin escribir):

```sql
SELECT company_id, count(*) AS criterios, sum(peso) AS peso_total
  FROM prv_evaluacion_criterio GROUP BY company_id;   -- 6 criterios · 100.00
SELECT * FROM fn_prv_evaluacion_metricas(2, '2026-01-01', '2026-12-31');
```

**Verificado en el mirror el 2026-08-14:** 6 tablas, 4 FK compuestas tenant-safe, semilla 6/100.00 y
4 clases cubriendo 0–100. La función devolvió 3 proveedores con datos reales (0088 con 8 recepciones
y L 76,344.75). Dos lecturas que confirman el diseño: `ENTREGA` sale **0/0** porque ninguna recepción
existente viene de una O/C con fecha pactada, y `CALIDAD` sale **0/0** porque todavía no hay
incidencias — ambos casos los redistribuye el servicio en vez de puntuar cero.

### 3.19 Almacén — flag "notificar por correo" en los conceptos de movimiento (2026-08-13)

Parte de la iniciativa de notificaciones de stock bajo (rama `feat/almacen-integracion-contable`).
**Aditivo, bajo riesgo:** una columna nueva con DEFAULT; no borra ni reescribe datos.

| Script | Qué aporta | Naturaleza | Mirror | SRV |
|---|---|---|:--:|:--:|
| `2026-08-13_alm_tipo_movimiento_notifica_correo.sql` | `alm_tipo_movimiento.notifica_correo` BOOLEAN NOT NULL DEFAULT false. Solo tiene efecto en conceptos de clase SALIDA: si true, un movimiento genérico con ese concepto envía aviso por correo al área ALMACEN al cruzar bajo mínimo | Aditivo idempotente (`ADD COLUMN IF NOT EXISTS`) — **re-ejecutable** | **sí (2026-08-13)** | **pendiente** |

- **Dependencias:** `alm_tipo_movimiento` (ya en prod). No asume ningún `company_id`.
- **⚠️ Con binario:** la entidad `alm_tipo_movimiento` y `TipoMovimientoService` leen/escriben la columna,
  el mantenimiento de conceptos trae el checkbox, y `MovimientoAlmacenService` la usa para gatear el aviso.
  El binario **sin** la columna rompe toda consulta de conceptos (columna inexistente). **Aplicar en la
  misma ventana que el despliegue del portal.**

Comando:

```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-13_alm_tipo_movimiento_notifica_correo.sql
```

¿Ya aplicado?

```sql
SELECT count(*) AS col FROM information_schema.columns
 WHERE table_name = 'alm_tipo_movimiento' AND column_name = 'notifica_correo';   -- esperado: 1
```

### 3.20 Proveedores — antigüedad de saldos: F0 (2026-08-14)

**F0** de la iniciativa "antigüedad de saldos del proveedor" (aging de cuentas por pagar,
[docs/plans/2026-08-14-antiguedad-saldos-proveedor-plan.md](../docs/plans/2026-08-14-antiguedad-saldos-proveedor-plan.md)).
Consolida el saldo por pagar de **todos** los proveedores repartido por antigüedad a una fecha de
corte, en **6 tramos** (por vencer / 1-30 / 31-60 / 61-90 / **91-120** / **+120**). El cálculo por
proveedor ya existía en el estado de cuenta (§3.16), pero corría de a uno y cortaba en «>90»: esta
función lo corre sobre todos reutilizando la misma función base, y abre el último tramo en dos.

| Script | Qué aporta | Naturaleza | Mirror | SRV |
|---|---|---|:--:|:--:|
| `2026-08-14_prv_antiguedad_saldos.sql` | `CREATE OR REPLACE FUNCTION fn_prv_antiguedad_saldos(company, corte, incluir_por_vencer, origen, tipoproveedor)` (solo lectura). Reutiliza `fn_prv_estado_cuenta_documentos` vía `CROSS JOIN LATERAL` — **no duplica** las reglas de vigencia (anuladas, compat legacy, abonos 'V'). No asume `company_id` | **Objetos** — aditivo, no destructivo, **idempotente / re-ejecutable** (lleva `DROP FUNCTION IF EXISTS` de su firma). No crea ni altera tablas, columnas, índices ni datos | **sí (2026-08-14)** | **pendiente** |

> **Firma:** `(bigint, date, boolean, integer, integer)`. `p_origen` y `p_cod_tipoproveedor` son **`integer`, no `smallint`** — con `smallint` los literales enteros de la llamada (`...(2, NULL, TRUE, 0, NULL)`) no resuelven la sobrecarga (`integer` no se reduce a `smallint`). Corregido antes de dejarlo fijo.

- **Dependencias:** `fn_prv_estado_cuenta_documentos` (§3.16 — aplicar **antes**), y por transitividad
  `alm_compra_cxp` (§3.10), `prv_compromiso_hdr`/`prv_compromiso_abono` y `prv_proveedores` /
  `prv_tipoproveedor` (ya en prod). Va **después** de §3.16.
- **Lleva binario:** todavía no. F0 es solo la función; el servicio, la pantalla matriz y el PDF son
  F1–F3. El script sin binario es inocuo (una función que nadie llama). Cuando llegue el binario, el
  endpoint sin el script respondería `42883`.

Comando:

```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-14_prv_antiguedad_saldos.sql
```

¿Ya aplicado? (debe aparecer la función)

```sql
SELECT proname, pg_get_function_identity_arguments(oid) AS args
  FROM pg_proc WHERE proname = 'fn_prv_antiguedad_saldos';
```

Verificación posterior (el cuadre contra el estado de cuenta debe dar `true` en las 4 columnas):

```sql
WITH ag AS (SELECT * FROM fn_prv_antiguedad_saldos(2, NULL, TRUE, 0, NULL) WHERE cod_proveedor = '0088'),
     rs AS (SELECT * FROM fn_prv_estado_cuenta_resumen(2, '0088', NULL))
SELECT ag.saldo_total = rs.saldo_total                            AS cuadra_total,
       ag.por_vencer  = rs.saldo_por_vencer                       AS cuadra_por_vencer,
       ag.vencido     = rs.saldo_vencido                          AS cuadra_vencido,
       (ag.tramo_91_120 + ag.tramo_mas_120) = rs.antiguedad_mas90 AS cuadra_tramo_abierto
  FROM ag, rs;
```

**Verificado en el mirror el 2026-08-14** (empresa 2): **7 proveedores** con saldo, total **L 322,960.99**.
El cuadre exhaustivo (cada proveedor del aging vs su `fn_prv_estado_cuenta_resumen`) dio **7/7** en las
cuatro columnas (total, por vencer, vencido y tramo abierto 91-120 + >120 == antigüedad >90), y el
cuadre global igualó exacto la suma de la función base sobre el universo. El proveedor 0088 dio
L 73,327.50 en 7 documentos, idéntico a §3.16. Los tramos de 61 días en adelante salen en 0 porque en
el mirror aún no hay deuda tan añeja — las columnas existen y funcionan.

### 3.21 Almacén — interruptor de existencia negativa en salidas: F0 (2026-08-15)

**F0** de la iniciativa "permitir existencia negativa en salidas" (rama `feat/almacen-integracion-contable`,
[docs/plans/2026-08-15-existencia-negativa-salidas-plan.md](../docs/plans/2026-08-15-existencia-negativa-salidas-plan.md)).
Hoy el motor de inventario **bloquea** toda salida que cruzaría a existencia negativa; se quiere
permitirla (desfase físico vs. sistema), pero **NO abierto para todos**: detrás de un interruptor por
empresa con override opcional por bodega. Este F0 crea **solo el interruptor**; el motor lo lee en F1.

| Script | Qué aporta | Naturaleza | Mirror | SRV |
|---|---|---|:--:|:--:|
| `2026-08-15_alm_existencia_negativa.sql` | (1) `cfg_inventario_negativo` (interruptor por empresa: PK `company_id`, `permitir` BOOLEAN NOT NULL DEFAULT false + auditoría; semilla idempotente de una fila `false` por empresa con artículos) + (2) `alm_bodega.permite_existencia_negativa` BOOLEAN **NULL** (override tri-estado: NULL=hereda · true=permite · false=bloquea) | **Aditivo idempotente** (`CREATE TABLE IF NOT EXISTS`, `ADD COLUMN IF NOT EXISTS`, `INSERT … ON CONFLICT DO NOTHING`) — **re-ejecutable**. No borra ni reescribe datos. Reversible (`DROP TABLE` + `DROP COLUMN`, en el script) | **sí (2026-08-15)** | **pendiente** |

- **Dependencias:** `alm_articulo` (para la semilla) y `alm_bodega` (ambas ya en prod). No asume ningún
  `company_id`. Independiente del resto de la tanda.
- **Verificado en el mirror el 2026-08-15** (con orden explícita del usuario, para correr F1 con TDD):
  interruptor creado, semilla `company_id = 2 · permitir = false`, las **3 bodegas heredando** (NULL) y
  la columna `boolean` / nullable / sin default.
- **⚠️ Con binario (F1+):** el motor (`InventarioPostingService`) leerá el interruptor para aflojar la
  guarda de negativo, y la config/UI (F5) escribirán `cfg_inventario_negativo` y la columna de bodega.
  **Nada nace activado** (`permitir=false`, override NULL) → el SQL **sin** el binario es inocuo y el
  bloqueo se comporta igual que hoy. Aplicar en la misma ventana que el binario de la iniciativa.

Comando:

```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-15_alm_existencia_negativa.sql
```

¿Ya aplicado?

```sql
SELECT to_regclass('public.cfg_inventario_negativo') AS interruptor,   -- NULL = falta
       (SELECT count(*) FROM information_schema.columns
         WHERE table_name = 'alm_bodega' AND column_name = 'permite_existencia_negativa') AS override_bodega;  -- esperado: 1
```

Verificación posterior (semilla en false, todas las bodegas heredando):

```sql
SELECT company_id, permitir FROM public.cfg_inventario_negativo ORDER BY company_id;   -- todas false
SELECT count(*) FILTER (WHERE permite_existencia_negativa IS NULL) AS heredan, count(*) AS total FROM public.alm_bodega;
```

### 3.22 Almacén — backfill del costo promedio / existencia resultante en el kardex histórico (2026-08-18)

Backfill de datos de la iniciativa "que el costo promedio quede almacenado por cada registro"
(rama `feat/almacen-integracion-contable`). El motor de posteo ya persiste `existencia_resultante`
y `costo_promedio_resultante` en cada asiento **nuevo** de `alm_kardex`; el **histórico migrado de
SIMAFI** (asientos con `uuid` NULL) las trae en **NULL**, y por eso el libro por bodega imprime "—"
y la vista por artículo tiene que derivar el corrido al vuelo. Este script rellena esas columnas
recorriendo cada par (artículo, bodega) en orden `(fecha, id)` y calculando el saldo y el costo
promedio **corrido** con window functions — la MISMA fórmula que `KardexService.AplicarPuntoDeCorte`.

| Script | Qué aporta | Naturaleza | Mirror | SRV |
|---|---|---|:--:|:--:|
| `2026-08-18_alm_kardex_backfill_resultantes_historico.sql` | `UPDATE` de `existencia_resultante` (15,2) y `costo_promedio_resultante` (12,4) en los ~47.203 asientos históricos (`uuid IS NULL AND articulo_id IS NOT NULL`, 588 pares, company_id=2) con el saldo/costo corrido. Excluye 12 asientos basura (sin artículo, todo en 0 → quedan NULL) | **Datos, one-shot idempotente** (`WHERE … existencia_resultante IS NULL`) — **re-ejecutable** (no re-toca lo ya rellenado ni los snapshots del motor). **Requiere la escotilla** `DISABLE/ENABLE TRIGGER trg_alm_kardex_inmutable` dentro de la transacción | **pendiente** | **pendiente** |

- **Dependencias:** los 6 scripts del kardex (**§3.1**), en particular `2026-07-14_alm_kardex_trazabilidad.sql`,
  que crea las columnas resultantes **y** el trigger de inmutabilidad. Sin ellos el `UPDATE` falla
  (columnas inexistentes) o el `DISABLE TRIGGER` no encuentra el trigger. **No** depende del corte de
  inventario (Fase 7) ni lo reemplaza: sólo rellena las resultantes del histórico.
- **⚠️ Requiere OWNER de `alm_kardex`** (para `DISABLE/ENABLE TRIGGER`) y **ventana de bajo uso** (el
  script hace `LOCK TABLE … SHARE ROW EXCLUSIVE`). El trigger de inmutabilidad queda desactivado **sólo
  durante el UPDATE**, dentro de la transacción, y se reactiva antes del `COMMIT`; si algo falla, el
  `ROLLBACK` revierte también el `DISABLE` (es transaccional en Postgres).
- **No lleva binario:** las columnas ya existen y el código no cambió. La presentación se conserva igual
  (KardexService sigue derivando el corrido como verificador); el beneficio observable es que el **libro
  por bodega** deja de mostrar "—" en el histórico. Aplicable en cualquier momento, independiente del
  despliegue del portal.
- **Bordes medidos en el mirror (2026-08-18, company_id=2):** interleaving 0, revaluación real 0,
  huérfanos con código 0 → la window pura es exacta (sin PL/pgSQL). **Verificado con test de integración**
  `KardexBackfillHistoricoTests` (3/3: el backfill SQL coincide con la derivación de KardexService y no
  toca los snapshots del motor) y **suite Almacén 327/327**. **Aún NO aplicado en firme a ninguna base**
  (el test corre en transacción con ROLLBACK).

Comando:

```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-18_alm_kardex_backfill_resultantes_historico.sql
```

¿Ya aplicado? (si devuelve ~47.203 falta; si devuelve 12 ya está — sólo quedan los basura)

```sql
SELECT count(*) AS historico_sin_resultante
  FROM alm_kardex WHERE uuid IS NULL AND existencia_resultante IS NULL;
```

Verificación posterior (los snapshots del motor no cambian; el histórico del 634 queda con costo):

```sql
SELECT count(*) AS motor FROM alm_kardex WHERE uuid IS NOT NULL;   -- igual que antes
SELECT id, fecha, existencia_resultante, costo_promedio_resultante
  FROM alm_kardex WHERE company_id=2 AND articulo_id=634 AND uuid IS NULL
 ORDER BY fecha, id;   -- ya sin NULL en las resultantes
```

---

### 3.23 Retenciones en "Pagos a proveedores" — unificación en el libro fiscal (2026-08-18)

Nueva iniciativa (rama `feat/almacen-integracion-contable`): permitir **retener al pagar una factura
de compra** desde `/almacen/compras/pagos` (CxP de compras), reusando el MISMO libro fiscal de
retenciones (`prv_retencion_hdr/dtl`, §3.6) del flujo de compromisos, con un discriminador de
**origen**. Así la constancia y la declaración mensual (SAR) toman ambos orígenes sin duplicar
reportes ni folios (decisión del usuario: "completo, unificado").

| Script | Qué aporta | Naturaleza | Mirror | SRV |
|---|---|---|:--:|:--:|
| `2026-08-18_alm_retencion_compras_unificada.sql` | (1) `alm_compra_cxp_abono.retenido` (14,2, default 0) — el bruto sigue en `monto`, el neto al banco = `monto - retenido`; (2) `prv_retencion_hdr`: `numero_orden` **pasa a anulable** + `origen` SMALLINT (1 compromiso / 2 compra, default 1) + `cxp_id` INT + FK `(company_id,cxp_id)→alm_compra_cxp` (MATCH SIMPLE, exime a OPD) + CHECK de coherencia origen↔referencia + índice único parcial `uq_prv_retencion_hdr_cxp_pago` para compras | **Aditivo idempotente** salvo aflojar el NOT NULL de `numero_orden` (no borra ni cambia datos; las filas OPD reciben `origen=1` por default y cumplen el CHECK) — **re-ejecutable** (`ADD COLUMN IF NOT EXISTS`, `CREATE INDEX IF NOT EXISTS`, guardas por `pg_constraint`) | **sí (2026-08-18)** | **pendiente** |

- **Dependencias:** `alm_compra_cxp` + `alm_compra_cxp_abono` (**§3.10** — aplicar **antes**) y
  `prv_retencion_hdr` + `prv_retencion_dtl` (**§3.6** — aplicar **antes**). Va **después** de ambas.
  No asume ningún `company_id` (el default `origen=1` aplica a todas las filas existentes).
- **Con binario:** el popup de retención en `/almacen/compras/pagos`, el endpoint de retenciones
  aplicables bajo el módulo Compras, el posteo de la retención al mayor (HABER "retenciones por
  pagar", banco por el neto) y la ramificación por origen en `RetencionRegistroService`
  (constancia + declaración resuelven el nombre del proveedor desde la CxP cuando `origen=2`)
  dependen de este SQL. Apagar/encender el posteo sigue gobernado por `activo_compras` (§3.11).
- **No es destructivo para el flujo de compromisos:** las filas OPD siguen con `origen=1`,
  `numero_orden` no nulo, `cxp_id` NULL, y la FK/constancia/declaración de compromisos no cambian.

Comando:

```
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-18_alm_retencion_compras_unificada.sql
```

¿Ya aplicado? (si alguna devuelve NULL/`f`, falta)

```sql
SELECT to_regclass('public.uq_prv_retencion_hdr_cxp_pago')                              AS idx_compras,   -- NULL = falta
       EXISTS (SELECT 1 FROM information_schema.columns
                WHERE table_name='prv_retencion_hdr' AND column_name='origen')          AS col_origen,
       EXISTS (SELECT 1 FROM information_schema.columns
                WHERE table_name='alm_compra_cxp_abono' AND column_name='retenido')     AS col_retenido;
```

Verificación posterior (numero_orden quedó anulable; los 3 constraints existen):

```sql
SELECT is_nullable FROM information_schema.columns
 WHERE table_name='prv_retencion_hdr' AND column_name='numero_orden';   -- YES
SELECT conname FROM pg_constraint
 WHERE conname IN ('ck_alm_compra_cxp_abono_retenido','fk_prv_retencion_hdr_cxp','ck_prv_retencion_hdr_origen')
 ORDER BY conname;   -- 3 filas
```

---

## 4. Orden de aplicación recomendado

```
[Fase 0]  Confirmar grupo B: los 6 del kardex (§3.1) y cfg_impuestos (§3.2)
             └─ si faltan, aplicarlos ANTES que nada del almacén nuevo
[Fase 1]  Pasos 1 a 18 del runbook, en su orden (respetando ⚠️ del paso 2)
[Fase 2]  Libretas y código de cliente (§3.3), en ese orden
[Fase 3]  Saneo de libretas fantasma (§3.3) — después de libretas_globales
[Fase 4]  Pasos 19, 20, 21 y 21b  ──┐
[Fase 5]  Pasos 22, 23 y 27        ──┴─ TODOS con el binario del portal
             └─ 27 = alm_tipo_movimiento: independiente, aditivo; sólo necesita alm_bodega
[Fase 6]  Paso 24 — mudanza del stock a la bodega '01' (prerrequisito del corte)
[Fase 7]  Corte de inventario (NO es SQL: se opera desde el portal)
             └─ guion en docs/plans/2026-07-31-fase8-ejecucion-corte-inventario.md
```

> ⚠️ **El paso 24 va SÍ o SÍ antes del corte.** El kardex histórico vive en la bodega `01` y el
> stock quedó en `PRIN`; sin mudarlo, el punto de corte no empareja y el saldo del kardex
> **duplica** la existencia. Detectado y corregido en el mirror el 2026-07-31 con un ensayo del
> corte. Ojo: el script asume `PRIN = 1` y `01 = 2`, ids que **no tienen por qué coincidir en
> producción** — su `DO` block aborta si no coinciden.

> **Después de todo el SQL viene el corte de inventario**, que no es un script: se ejecuta desde
> `/almacen/carga-inicial` con el binario ya desplegado. Su guion (respaldo, saneo de negativas,
> dry-run, ejecución, verificación y cierre) está en
> [docs/plans/2026-07-31-fase8-ejecucion-corte-inventario.md](../docs/plans/2026-07-31-fase8-ejecucion-corte-inventario.md).
> Medido en el mirror el 2026-07-31: **244 pares, 241 posteables, 3 negativas, L. 2,588,085.19**
> — es un corte de una sola vez, no un proyecto de captura de costos.

**Regla general:** todo lo del 19 en adelante va **en la misma ventana que el despliegue del
portal**. El SQL sin el binario suele ser inocuo; el binario sin el SQL rompe la pantalla
correspondiente.

---

## 5. Antes de tocar producción

- [ ] **Backup del SRV** (`Database/backup_bd_simple.ps1`).
- [ ] Confirmar el `company_id` real del tenant: **los pasos 9, 10, 14 y los de §3.3 asumen `2`**.
- [ ] Correr la consulta «¿ya aplicado?» de cada paso antes de aplicarlo.
- [ ] Ventana de bajo uso: el paso 6 cambia saldos visibles y `libretas_globales` toca lectores.
- [ ] `psql "$SRV" -v ON_ERROR_STOP=1 -f Database/<script>.sql` — cada script trae su `BEGIN … COMMIT`.

---

## 6. Versionado

Los scripts de esta sesión (`2026-07-29_alm_articulo_activo.sql`, los cuatro de `2026-07-30`,
`2026-07-30_saneo_libretas_fantasma.sql`, este registro y el runbook) están **sin commitear**.
Conviene versionarlos antes de llevarlos al servidor. **Vos decidís cuándo.**

**Iniciativa de retenciones (2026-08-06):** `2026-08-06_cfg_retenciones.sql` (§3.4) también está
**sin commitear**. Versionarlo junto con el binario de la pantalla `/mantenimientos/retenciones`.

**Iniciativa de retenciones — F0 (2026-08-07):** `2026-08-07_con_integracion_prov_activar.sql` (§3.5)
está **sin commitear**. Versionarlo junto con el binario de F0 (`OrdenesPagoDirectoService` +
`PrvContabilidad`) — el SQL sin el binario no hace daño, pero el binario sin el SQL no postea al mayor
(el motor exige la fila `con_integracion_asiento` PROV + `activo_proveedores=TRUE`).

**Iniciativa de retenciones — F4 (2026-08-07):** `2026-08-07_prv_retencion_hdr_dtl.sql` (§3.6) está
**sin commitear**. Versionarlo junto con el binario de F4 (`OrdenesPagoDirectoService` +
`RetencionRegistroService` + la pantalla `/proveedores/retenciones`). El SQL sin el binario es inocuo;
el binario sin el SQL rompe el registro de la retención (INSERT a tablas inexistentes).

**Términos de pago del proveedor (2026-08-11):** `2026-08-11_alm_termino_pago.sql` (§3.7),
`2026-08-11_alm_termino_pago_seed.sql` (semilla opcional, §3.7) y
`2026-08-11_prv_proveedor_termino_pago.sql` (§3.8) están **sin commitear**. Versionarlos junto con el
binario del catálogo (`/almacen/terminos-pago`), el cableo en la factura de recepción y el campo
"Término de pago" en el maestro de proveedores. El SQL sin el binario es inocuo; el binario sin el SQL
rompe la pantalla del catálogo, el combo en la factura y el alta/edición de proveedores (leen columnas
inexistentes). Orden: estructura del catálogo → (semilla) → columna en proveedores.

**Facturas al crédito → CxP (2026-08-12):** `2026-08-12_alm_compra_condicion_pago.sql` (§3.9),
`2026-08-12_alm_compra_cxp.sql` (§3.10), `2026-08-12_alm_compra_cxp_backfill.sql` (backfill, §3.10) y
`2026-08-12_con_integracion_compras_modulo.sql` (módulo COMPRAS, §3.11) están **sin commitear**.
Versionarlos junto con el binario de la CxP de compra (`RecepcionCompraService` genera/anula la CxP,
`CompraCxpService` + la pantalla `/almacen/compras/pagos`) y de la integración contable Fase 2
(`CompraContabilidad` + los enganches gated por `activo_compras`, module `COMPRAS`). Orden: condición
de pago → CxP → (backfill) → módulo COMPRAS. El módulo es inocuo apagado; los otros sin el binario son
inocuos, el binario sin ellos rompe el registro de la factura y la pantalla de pagos.

**Saneo cuenta de inventario del tipo (2026-08-12):** `2026-08-12_alm_tipo_articulo_normalizar_cuenta_inventario.sql`
(§3.12) está **sin commitear**. Es sólo datos (no lleva binario), aplicado al **mirror** el 2026-08-12,
**SRV pendiente**; es prerrequisito para *encender* la contabilidad de compras (`activo_almacen`, §3.11),
no para desplegar el binario.

**Correo y notificaciones — F1 (2026-08-13):** `2026-08-13_cfg_correo_notificaciones.sql` (§3.13) está
**sin commitear**; **aplicado al mirror `siad_v3_restore` el 2026-08-13** (con orden explícita del
usuario, para correr los tests de F4 — 14/14 verdes), **SRV pendiente**. Versionarlo junto con el
binario del mantenimiento `/configuracion/correo` (F2 hecho, F3 pendiente) y con el cambio de F0 en
`Program.cs` (key-ring de DataProtection estable en producción). El SQL sin el binario es inocuo.

**Saneo numero_cuenta de bancos (2026-08-13):** `2026-08-13_ban_cuenta_sanear_numero_cuenta.sql`
(§3.14) está **sin commitear** y **sin aplicar** (ni mirror ni SRV). Es sólo datos, no lleva binario:
el cambio de UI (el combo de "Cuenta del banco" muestra sólo `numero_cuenta`) ya funciona sin él; esto
es la limpieza cosmética del dato. Independiente del resto de la tanda; aplicable en cualquier momento.

**Fix cheque de compras (2026-08-13):** `2026-08-13_ban_cheque_origen_compra_cxp.sql` (§3.15) está
**sin commitear**; **aplicado al mirror** el 2026-08-13, **SRV pendiente**. Es prerrequisito para
pagar compras con **cheque** (sin él, el pago con cheque revienta el INSERT en `ban_cheque`). Aditivo
al CHECK, no lleva binario nuevo (el código que usa 'COMPRA_CXP' ya está en la rama de compras).

**Estado de cuenta del proveedor (2026-08-13):** `2026-08-13_prv_estado_cuenta.sql` (§3.16) está
**sin commitear**; **aplicado al mirror** el 2026-08-13, **SRV pendiente**. Versionarlo junto con el
binario del estado de cuenta (`ProveedorEstadoCuentaService` + la pantalla y el PDF). El SQL sin el
binario es inocuo; el binario sin el SQL responde `42883`.

**Evaluación de proveedores — F0 (2026-08-14):** `2026-08-14_prv_evaluacion.sql` (§3.18) está
**sin commitear**; **aplicado al mirror** el 2026-08-14, **SRV pendiente**. No lleva binario todavía
(F1–F2 son el servicio y las pantallas), así que puede aplicarse solo. Va **después** de §3.17.

**Fecha de entrega pactada en la O/C (2026-08-14):** `2026-08-14_alm_orden_compra_fecha_entrega.sql`
(§3.17) está **sin commitear**; **aplicado al mirror** el 2026-08-14 (con orden explícita del usuario)
y **SRV pendiente**. Versionarlo junto con el binario de órdenes de compra (`OrdenCompraService` exige
la fecha desde el borrador; la captura y el listado la muestran). **Va en la misma ventana que el
binario**: sin la columna, la pantalla de órdenes falla con `42703` al leerla. El SQL solo es inocuo.

**Antigüedad de saldos del proveedor — F0 (2026-08-14):** `2026-08-14_prv_antiguedad_saldos.sql`
(§3.20) está **sin commitear**; **aplicado al mirror** el 2026-08-14 (con orden explícita del usuario) y
verificado (cuadre 7/7), **SRV pendiente**. Solo lectura, no lleva binario todavía (F1–F3 son el
servicio, la pantalla matriz y el PDF), así que puede aplicarse solo. Va **después** de §3.16
(`fn_prv_estado_cuenta_documentos`), de la que depende vía `LATERAL`. El SQL sin el binario es inocuo.

**Existencia negativa en salidas — F0 (2026-08-15):** `2026-08-15_alm_existencia_negativa.sql`
(§3.21) está **sin commitear**; **aplicado al mirror** el 2026-08-15 (con orden explícita del usuario,
para correr F1 con TDD), **SRV pendiente**. Aditivo/reversible; nada nace activado. Versionarlo junto con el binario de la iniciativa
(motor F1 + config/UI F5). El SQL sin el binario es inocuo (interruptor apagado); el binario lee el
interruptor pero, con `permitir=false` y override NULL, se comporta igual que hoy. Independiente del
resto de la tanda.

**Backfill resultantes del kardex histórico (2026-08-18):** `2026-08-18_alm_kardex_backfill_resultantes_historico.sql`
(§3.22) está **sin commitear** y **sin aplicar en firme** (ni mirror ni SRV; sólo probado en transacción
con ROLLBACK vía `KardexBackfillHistoricoTests`, 3/3). Es sólo datos, no lleva binario. Depende de los 6
del kardex (§3.1). Requiere **OWNER** de `alm_kardex` y **ventana de bajo uso** (usa la escotilla del
trigger de inmutabilidad). Aplicable en cualquier momento, independiente del resto de la tanda.
