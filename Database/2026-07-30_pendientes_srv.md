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
