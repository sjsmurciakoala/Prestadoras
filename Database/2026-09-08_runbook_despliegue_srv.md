# Runbook — Poner el mirror al día con lo que producción ya tiene

**Base destino:** ⚠️ **al revés de lo habitual.** El destino es el **mirror local
`siad_v3_restore`** y la réplica `siad_v3_desarrollo`. **`siad_v4` @ `172.16.0.9` YA LOS TIENE TODOS.**
**Fecha:** 2026-09-08
**Alcance:** 7 pasos, 12 aplicaciones de script más un ajuste de datos sin script. El rescate del
parche CAI, la apertura del periodo contable de septiembre, los cuatro scripts de informes, los
tres de julio y agosto que al mirror le faltaban, la corrección de la emisión de lectura y la
réplica de los últimos 7 objetos de estructura. **Ninguno crea ni borra tablas ni columnas.** Solo uno borra objetos
(`balance_clase_nombre`, que recrea en el acto lo que borra) y solo uno es irreversible
(el paso 6, que cierra el mes comercial de junio).
**Origen:** la comparación del 2026-09-08 entre `siad_v3_restore` y `siad_v4`, y el diagnóstico de
los 103 fallos de `SIAD.Tests` que salió de ella.

> ⚠️ **Casi nada de este runbook es un pendiente de SRV.** Producción es el ORIGEN, no el destino:
> el paso 1 existe para que el repositorio pueda volver a crear lo que hoy solo vive en el
> servidor, y los pasos 2, 3 y 4 ponen al mirror y a desarrollo al día con lo que arriba ya corre.
>
> ⚠️ **A producción no se le toca nada.** El flujo de este proyecto va en un solo sentido:
> `siad_v4` se replica hacia local, nunca al revés. Ni siquiera el paso 5, que corrige un defecto
> que existe igual arriba: queda anotado como hallazgo, no como pendiente de despliegue.
>
> ⚠️ **Riesgo que este script elimina:** hasta hoy, un restore del mirror sobre `siad_v4`
> borraba el parche y nada en el repositorio podía recrearlo.
>
> ⚠️ **Tandas anteriores aún pendientes en SRV** y que esta **no reemplaza ni cierra**:
> `2026-08-22` (CxP unificada), `2026-08-27` (control presupuestario, 5 scripts), `2026-08-31`
> (aprobación por niveles, 3 scripts), `2026-09-01` (disponible sin truncar), `2026-09-05`
> (ciudad de la empresa) y `2026-09-07` (roles y permisos).
>
> ⚠️ La base **ACTIVA es `siad_v4`**, no `siad_v3`.

---

## 1. Qué cubre este runbook

Cinco objetos del ciclo CAI se aplicaron directo a producción entre el 2026-05-14 y el
2026-08-23 y nunca bajaron al repositorio. El código de este script **no se escribió a mano**:
se extrajo de `siad_v4` con `pg_get_functiondef` y `pg_get_indexdef`, en una sesión abierta con
`default_transaction_read_only=on`.

| Pieza | Qué corrige |
|---|---|
| `sp_adm_avanzar_correlativo_actual_cai` | Función nueva. Helper único que adelanta `correlativo_actual` en el bloque y en el CAI, con `GREATEST` para que nunca retroceda |
| `sp_adm_obtener_o_reservar_bloque_cai_ruta` | BUGFIX #4. El folio disponible sale del mayor entre el contador y el máximo correlativo realmente emitido, no del contador solo |
| `sp_adm_prepare_correlativo_cai_sync` | BUGFIX #4. Una reserva anulada deja de bloquear su número, y reservar también consume el correlativo |
| `sp_adm_confirmar_correlativo_cai_sync` | BUGFIX #4. Mismo filtro `status_id = 1`, y el avance del contador se delega en el helper |
| `sp_adm_periodo_ciclo_cerrar` | Guard del 2026-08-23. Rechaza el cierre del ciclo si quedan folios CAI en `PENDING_SYNC` sin factura |
| 3 índices únicos de `adm_cai_correlativo_emitido` | Pasan a **parciales** (`WHERE status_id = 1`). Con los totales, una reserva anulada quemaba su folio para siempre |

**El síntoma que motivó el parche arriba:** el snapshot de la ruta repartía dos veces el mismo
folio, y los folios anulados quedaban inutilizables.

## 2. Antes de empezar (obligatorio)

**Respaldo de la base que se va a tocar.** Para el mirror:

```bash
pg_dump -h localhost -U postgres -d siad_v3_restore -Fc -f siad_v3_restore_antes_rescate_cai.backup
```

**Definir la conexión:**

```bash
export MIRROR="postgresql://USUARIO:CLAVE@localhost:5432/siad_v3_restore"
```

**Confirmar la base antes de escribir nada:**

```bash
psql "$MIRROR" -c "SELECT current_database(), pg_size_pretty(pg_database_size(current_database()));"
```

Debe decir `siad_v3_restore`. Si dice `siad_v4`, **parar**: arriba ya está aplicado y no hay
nada que hacer.

**Ver el estado actual de los índices** (así se sabe si el parche ya está):

```sql
SELECT indexname, indexdef
  FROM pg_indexes
 WHERE schemaname = 'public'
   AND tablename  = 'adm_cai_correlativo_emitido'
   AND indexname LIKE 'uq_%'
 ORDER BY 1;
```

Si los tres traen `WHERE (status_id = 1)`, el parche ya está aplicado.

## 3. Advertencias clave (leer antes de aplicar)

- El script es **re-ejecutable**. Las funciones son `CREATE OR REPLACE`; los índices se tocan
  dentro de un bloque `DO` que primero comprueba si ya son parciales y, si lo son, no hace nada.
- ⚠️ **Los tres índices se borran y se vuelven a crear** cuando todavía son totales. En una
  tabla grande eso bloquea las escrituras mientras dura. En el mirror son 228 filas y es
  instantáneo; en `siad_v4` son 154 y además ya están parciales, así que el bloque los salta.
- **Impacto de datos: cero.** No inserta, no borra y no actualiza ninguna fila.
- Los índices parciales son **menos restrictivos** que los totales, así que su creación no puede
  fallar por colisión con datos existentes. En el mirror, además, no hay ninguna fila con
  `status_id` distinto de 1.
- ⚠️ **Cambia el comportamiento del cierre de ciclo.** Después de aplicar, cerrar un ciclo con
  folios CAI en `PENDING_SYNC` sin factura falla con `CICLO_FOLIOS_SIN_CONFIRMAR`. Es
  deliberado: son lecturas ya emitidas que siguen en un teléfono. El guard **no** corre cuando
  se llama con `p_forzar = true`.
- **Dependencias verificadas en el mirror el 2026-09-08:** existen
  `sp_adm_actualizar_estado_cai(bigint)`, `sp_adm_reservar_bloque_cai` de 8 argumentos,
  `fn_adm_periodo_ciclo_rutas_pendientes`, y todas las columnas que el parche usa. El script se
  puede aplicar tal cual.

## 4. Orden de aplicación (resumen)

| Paso | Script | Naturaleza | ¿Re-ejecutable? | Depende de |
|---:|---|---|:--:|---|
| 1 | `2026-09-08_cai_parche_bugfix4_rescate.sql` | Objetos (5 funciones) + 3 índices redefinidos | Sí | Nada. Solo que existan las funciones auxiliares del ciclo CAI y la tabla `adm_cai_correlativo_emitido` con `status_id` |
| 2 | `2026-09-08_con_periodo_septiembre_2026.sql` | Datos idempotente (1 fila de periodo por empresa) | Sí | Nada. Solo que `con_periodo_contable` ya tenga periodos de esa empresa |
| 3a | `2026-09-03_balance_clase_nombre.sql` | ⚠️ Objetos con `DROP FUNCTION` + recreación | Sí | `rep_balance_comprobacion` y `con_configuracion_balance` |
| 3b | `2026-09-03_patrimonio_matriz.sql` | Objetos (1 función nueva) + 1 `UPDATE` condicional de catálogo | Sí | El dataset `estado-cambios-patrimonio` debe existir |
| 3c | `2026-09-03_presupuesto_comparativo.sql` | Objetos (1 función nueva) + 3 `INSERT` con guarda | Sí | `pst_config_presupuesto*` y `con_partida*` |
| 3d | `2026-09-04_perf_rep_saldo_clientes_categoria_cobranza.sql` | Objetos (1 `CREATE OR REPLACE`) | Sí | `vw_rep_movimiento_vigente` |
| 4a | `2026-07-30_uc_f7_h5c_perf_reportes.sql` | Objetos (9 funciones + 1 vista) + `ANALYZE` | Sí | `vw_rep_movimiento_vigente`, `adm_pago*` |
| 4b | `2026-08-04_ncnd_factura_migrada_fallback_numrecibo.sql` | Objetos (2 funciones) | Sí | `factura.numrecibo` |
| 4c | `2026-08-05_estados_fase2_lectores_sql.sql` | Objetos (7 funciones + 1 vista) | Sí | `factura.estado_id` poblado |
| 4d | `2026-09-04_perf_rep_saldo_clientes_categoria_cobranza.sql` **otra vez** | Reposición | Sí | El paso 4a |
| 5 | `2026-09-08_sp_lectura_v3_ciclo_ruta_secuencia.sql` | Objetos (1 `CREATE OR REPLACE`) | Sí | Nada |
| 6 | Ajuste de datos del mirror, **sin script** | ⚠️ Cierre de mes comercial, irreversible | **NO** | El paso 5 |
| 7 | `2026-09-08_replica_estructura_faltante_desde_prod.sql` | Aditivo (2 funciones + 5 índices) | Sí | Nada |

Los pasos 1, 2, 3 y 4 son independientes entre sí: se pueden aplicar en cualquier orden o por
separado. Dentro del paso 3, los cuatro scripts tampoco dependen unos de otros. **Dentro del
paso 4 sí importa el orden**, ver la advertencia de más abajo.

**El paso 3 no es un pendiente de SRV**: los cuatro ya están en `siad_v4` desde el 3 y el 4 de
septiembre. Entran a este runbook porque tres de ellos no tenían dónde registrarse (el runbook
del 2026-09-03 cubre otra tanda de 14 scripts) y porque al mirror le faltaban los cuatro. El
cuarto tiene su propio runbook, `2026-09-04_runbook_despliegue_srv.md`, cuyo estado se actualizó.

⚠️ **El paso 4 sí tiene un orden obligatorio, y no es el que parece.** Los tres scripts se aplican
por fecha, porque se pisan entre ellos: 4a y 4c reescriben los mismos cuatro objetos
(`rep_saldo_clientes_categoria` en sus dos sobrecargas, los dos `rep_saldos_*_ciclo` y
`vw_rep_movimiento_vigente`), y la versión buena es la de 4c. Y 4a reescribe además
`rep_saldo_clientes_categoria_cobranza`, que el paso 3d ya había dejado en su versión de
septiembre: **por eso 4d vuelve a aplicar ese mismo script al final**. Sin 4d, el mirror termina
con la función de julio y pierde el arreglo de rendimiento.

## 5. Detalle por paso

### Paso 1 — Aplicar el parche CAI (`2026-09-08_cai_parche_bugfix4_rescate.sql`)

Reemplaza cuatro funciones, crea el helper nuevo y vuelve parciales los tres índices únicos.

```bash
psql "$MIRROR" -v ON_ERROR_STOP=1 -f Database/2026-09-08_cai_parche_bugfix4_rescate.sql
```

El bloque de índices emite un `NOTICE` por cada uno diciendo si lo creó, lo redefinió o lo dejó
como estaba.

**¿Ya aplicado?**

```sql
SELECT count(*) AS funciones
  FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
 WHERE n.nspname = 'public'
   AND p.proname IN ('sp_adm_avanzar_correlativo_actual_cai',
                     'sp_adm_obtener_o_reservar_bloque_cai_ruta',
                     'sp_adm_prepare_correlativo_cai_sync',
                     'sp_adm_confirmar_correlativo_cai_sync',
                     'sp_adm_periodo_ciclo_cerrar');
```

Esperado tras aplicar: `5`. Antes de aplicar, en el mirror da `4`, porque falta el helper.

**Verificación posterior — los índices:**

```sql
SELECT indexname, indexdef
  FROM pg_indexes
 WHERE schemaname = 'public'
   AND tablename  = 'adm_cai_correlativo_emitido'
   AND indexname LIKE 'uq_%'
 ORDER BY 1;
```

Los tres deben traer `WHERE (status_id = 1)`.

**Verificación posterior — contraste contra producción.** Las huellas de las cinco funciones
deben coincidir con las de `siad_v4`:

```sql
SELECT p.proname, md5(regexp_replace(p.prosrc, '\s+', '', 'g')) AS huella
  FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
 WHERE n.nspname = 'public' AND p.proname LIKE 'sp_adm_%cai%'
 ORDER BY 1;
```

**Verificación funcional:** pedir un snapshot de ruta desde la app del lector y confirmar que el
correlativo que devuelve no repite uno ya emitido. Después, intentar cerrar un ciclo con folios
pendientes y comprobar que responde `CICLO_FOLIOS_SIN_CONFIRMAR`.

### Paso 2 — Abrir el periodo contable de septiembre (`2026-09-08_con_periodo_septiembre_2026.sql`)

Inserta una fila por empresa en `con_periodo_contable` con `code = '202609'`, del 2026-09-01
06:00 al 2026-10-01 05:59:59, en estado ABIERTO. Los límites se copian literalmente del periodo
85 de producción.

```bash
psql "$MIRROR" -v ON_ERROR_STOP=1 -f Database/2026-09-08_con_periodo_septiembre_2026.sql
```

**¿Ya aplicado?**

```sql
SELECT period_id, company_id, code, name, start_date, end_date, status
  FROM public.con_periodo_contable
 WHERE start_date <= now() AND end_date >= now()
 ORDER BY company_id;
```

Esperado tras aplicar: una fila por empresa, `code` 202609, `status` ABIERTO. Antes de aplicar,
en el mirror la consulta no devuelve nada.

**Verificación posterior:** `SELECT public.fn_con_periodo_abierto(2, current_date);` debe
devolver el `period_id` nuevo. Pese al nombre, esa función no devuelve un booleano.

⚠️ **No arregla el fondo.** En octubre vuelve a faltar el periodo y las mismas 13 pruebas se caen
otra vez. El arreglo duradero es que esas pruebas creen el periodo dentro de su transacción, como
ya hacen con la integración contable, el control presupuestario y la aprobación por niveles.

⚠️ **El periodo 70 sigue distinto al de producción.** En el mirror se llama "Julio 2026" pero
abarca julio y agosto, y sigue abierto; arriba está recortado a julio, con un periodo 84 aparte
para agosto, ambos cerrados. Este script no lo toca.

### Paso 3 — Los cuatro scripts de informes que producción ya tenía

Se aplican en este orden, aunque son independientes:

```bash
psql "$MIRROR" -v ON_ERROR_STOP=1 -f Database/2026-09-03_balance_clase_nombre.sql
psql "$MIRROR" -v ON_ERROR_STOP=1 -f Database/2026-09-03_patrimonio_matriz.sql
psql "$MIRROR" -v ON_ERROR_STOP=1 -f Database/2026-09-03_presupuesto_comparativo.sql
psql "$MIRROR" -v ON_ERROR_STOP=1 -f Database/2026-09-04_perf_rep_saldo_clientes_categoria_cobranza.sql
```

⚠️ **El primero borra y recrea** las dos sobrecargas de `rep_estado_situacion_financiera`, porque
cambia la firma de retorno. Ningún otro objeto las referencia y la recreación va en la misma
transacción. Ese script **incluye** el contenido de `2026-09-03_balance_monto_anterior.sql`, así
que ese no hace falta aplicarlo aparte.

**¿Ya aplicado?** Las cuatro funciones deben existir y sus huellas coincidir con `siad_v4`:

```sql
SELECT p.proname, md5(regexp_replace(p.prosrc, '\s+', '', 'g')) AS huella
  FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
 WHERE n.nspname = 'public'
   AND p.proname IN ('rep_estado_situacion_financiera',
                     'rep_estado_cambios_patrimonio_matriz',
                     'rep_presupuesto_comparativo',
                     'rep_saldo_clientes_categoria_cobranza')
 ORDER BY p.proname, pg_get_function_identity_arguments(p.oid);
```

Esperado: 5 filas, porque `rep_estado_situacion_financiera` tiene dos sobrecargas.

**Verificación del catálogo:**

```sql
SELECT codigo, origen_clave FROM public.rep_catalogo_dataset
 WHERE codigo IN ('presupuesto-comparativo', 'estado-cambios-patrimonio');
```

Esperado: `presupuesto-comparativo` apuntando a `public.rep_presupuesto_comparativo`, y
`estado-cambios-patrimonio` ya apuntando a `public.rep_estado_cambios_patrimonio_matriz`.

**Por qué el catálogo importa para las pruebas:** `ReportTemplateFactory.ResolveDataset` busca el
dataset en `rep_catalogo_dataset`; si no lo encuentra recurre a una lista de datasets por defecto
escritos en C#, y el comparativo de presupuesto **no está** en esa lista. Sin la fila del
catálogo, la fábrica devuelve una plantilla genérica sin las etiquetas del informe, y
`EstadoFinancieroLayoutTests.El_comparativo_de_presupuesto_enfrenta_los_dos_ejercicios` falla.

### Paso 4 — Los tres scripts de julio y agosto, más la reposición

**El orden es obligatorio.** Ver la advertencia de §4.

```bash
psql "$MIRROR" -v ON_ERROR_STOP=1 -f Database/2026-07-30_uc_f7_h5c_perf_reportes.sql
psql "$MIRROR" -v ON_ERROR_STOP=1 -f Database/2026-08-04_ncnd_factura_migrada_fallback_numrecibo.sql
psql "$MIRROR" -v ON_ERROR_STOP=1 -f Database/2026-08-05_estados_fase2_lectores_sql.sql
psql "$MIRROR" -v ON_ERROR_STOP=1 -f Database/2026-09-04_perf_rep_saldo_clientes_categoria_cobranza.sql
```

**Qué cambia de verdad:**

- **4a** quita el `COALESCE(ta.fecha_docu, ta.fecha_registro)` de nueve reportes y deja
  `ta.fecha_docu` a secas, para que el filtro se pueda empujar al índice. Crea además los índices
  de fecha de `adm_pago`, `adm_nota_credito`, `adm_nota_debito` y `factura`, y termina con
  `ANALYZE`.
- **4b** da a las notas de crédito y débito un fallback a `numrecibo` cuando la factura origen no
  tiene número fiscal. Sin él, emitir una nota contra una de las 3,9 M de facturas migradas de
  SIMAFI revienta con `23502`.
- **4c** es la fase 2 de estados numéricos: los lectores de saldo dejan de filtrar por la letra de
  `factura.estado` y pasan a `estado_id`.

**Prerrequisito de 4c, comprobado antes de aplicar:** en el mirror no hay ningún `estado_id` nulo.
El único descuadre entre letra e identificador son **3 facturas con letra `B` mapeadas al 1** en
vez del 4, que es la brecha ya documentada en `docs/ESTADOS_DOCUMENTOS_COMERCIALES.md`. Como el
filtro nuevo es `estado_id IN (1, 4)`, esas 3 siguen contando igual que antes: el cambio es
neutro en esta base.

**¿Ya aplicado?** El criterio es que las huellas coincidan con `siad_v4`. Son 17 objetos:

```sql
SELECT p.proname, pg_get_function_identity_arguments(p.oid) AS args,
       md5(regexp_replace(p.prosrc, '\s+', '', 'g')) AS huella
  FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
 WHERE n.nspname = 'public'
   AND p.proname IN ('rep_desglose_facturacion', 'rep_movimiento_periodo',
                     'rep_saldo_clientes_categoria', 'rep_saldo_clientes_categoria_cobranza',
                     'rep_saldo_clientes_categoria_detalle', 'rep_saldo_clientes_ciclo',
                     'rep_saldos_agua_potable_ciclo', 'rep_saldos_alcantarillado_sanitario_ciclo',
                     'rep_transacciones_periodo', 'sp_adm_emitir_nota_credito',
                     'sp_adm_emitir_nota_debito', 'fn_ban_ws_pendientes',
                     'sp_obtener_cliente_saldo', 'sp_obtener_cliente_saldo_servicio_detalle')
 ORDER BY 1, 2;

SELECT md5(regexp_replace(
         pg_get_viewdef('public.vw_rep_movimiento_vigente'::regclass, true), '\s+', '', 'g'));
```

Esperado: 16 funciones (dos de ellas con dos sobrecargas) más la vista, todas con la misma huella
que arriba. **Si `rep_saldo_clientes_categoria_cobranza` no coincide, faltó el paso 4d.**

### Paso 5 — La emisión dejaba el histórico sin ciclo, ruta ni secuencia

⚠️ **Este paso es una DIVERGENCIA deliberada respecto de producción.** En todos los demás el
mirror iba atrás y se le puso al día; aquí el mirror queda por delante: el defecto vive igual en
las dos bases (antes del cambio, `sp_lectura_v3` tenía la misma huella `md5` en `siad_v3_restore`
y en `siad_v4`), pero **a producción no se le toca**. Queda como hallazgo anotado.

**Consecuencia práctica:** al replicar `siad_v4` sobre el mirror, esta corrección se pierde. Si se
quiere conservar, hay que volver a aplicarla después del restore.

```bash
psql "$MIRROR" -v ON_ERROR_STOP=1 -f Database/2026-09-08_sp_lectura_v3_ciclo_ruta_secuencia.sql
```

El script lleva el diagnóstico completo en su cabecera. En resumen: el orden de `NULLIF` y
`COALESCE` estaba invertido en tres asignaciones, la cadena vacía que devuelve
`sp_adm_calcular_factura_lectura` le ganaba al valor de reserva, y el histórico nacía sin ciclo,
sin ruta y sin secuencia. A partir de ahí ese cliente quedaba bloqueado en ese periodo con
`No hay periodo abierto ... ciclo=(sin ciclo)`, un mensaje engañoso.

**¿Ya aplicado?**

```sql
WITH l AS (SELECT unnest(regexp_split_to_array(
             (SELECT prosrc FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
               WHERE n.nspname = 'public' AND p.proname = 'sp_lectura_v3'), E'
')) AS linea)
SELECT count(*) FILTER (WHERE linea LIKE '%NULLIF(btrim(v_calc.%') AS forma_nueva,
       count(*) FILTER (WHERE linea LIKE '%NULLIF(COALESCE(v_calc.%') AS forma_vieja
  FROM l;
```

Esperado: `forma_nueva` = 3, `forma_vieja` = 0.

### Paso 6 — Ajuste de datos del mirror: cerrar junio y abrir el ciclo 19 en julio

⚠️ **Solo para el mirror. NO hay script y NO se re-ejecuta.** Es una corrección de datos
incoherentes, no un cambio de esquema. En `siad_v4` no aplica: allí los periodos ya abren ciclos
que tienen clientes.

**El problema:** el periodo comercial de julio del mirror tenía abierto el ciclo `01`, que en esa
base no tiene **ni un solo cliente** (los 1.146 activos están en los ciclos 19 y 20). Ninguna
emisión de julio podía pasar la validación, eligiera el cliente que eligiera. En `siad_v4` julio
abrió el ciclo 19, que sí tiene clientes.

```sql
BEGIN;
SELECT public.sp_adm_periodo_ciclo_cerrar(2, 7, 'ajuste_mirror_2026-09-08', true);
SELECT public.sp_adm_periodo_comercial_cerrar(2, 5, 'ajuste_mirror_2026-09-08');
SELECT public.sp_adm_periodo_ciclo_abrir(2, 2026, 7, '19', 'ajuste_mirror_2026-09-08');
COMMIT;
```

**Por qué la cadena es tan larga.** `sp_adm_periodo_ciclo_abrir` exige que el mes anterior esté
cerrado, y mide eso sobre `adm_periodo_comercial.status_id`, **no** sobre el estado de sus ciclos:
cerrar solo el ciclo de junio no basta. Y `sp_adm_periodo_comercial_cerrar` no tiene parámetro de
forzado, así que su checklist tiene que pasar limpio.

**El forzado del primer paso salta dos guards:** 2 rutas de junio sin facturas emitidas y 3 folios
CAI reservados sin confirmar (el guard rescatado en el paso 1 de este mismo runbook). Al cerrar el
ciclo, las rutas pendientes desaparecen del checklist del periodo, así que el cierre del mes pasa
sin forzarlo.

⚠️ **Irreversible.** El procedimiento avisa que un periodo o un ciclo cerrado no se reabre.
Todo se simuló antes en una transacción con `ROLLBACK`.

**Estado previo, por si hace falta reconstruirlo a mano:**

| periodo | status | ciclo | status |
|---|---|---|---|
| 2026-06 (id 5) | 1 | 19 (id 6) | 2 |
| 2026-06 (id 5) | 1 | 20 (id 7) | 1 |
| 2026-07 (id 6) | 1 | 01 (id 8) | 1 |

**Resultado:** junio y sus dos ciclos cerrados; julio con los ciclos 01 y 19 abiertos; **473
históricos de julio creados por arrastre, todos con ciclo**; 5 rutas con lector; fecha límite el
27 de julio. El ciclo `01` de julio queda abierto y sin clientes: la apertura lo reporta como
aviso `OTRO_CICLO_ABIERTO` y no se tocó.

### Paso 7 — Los últimos 7 objetos de estructura que solo existían arriba

Cierra el diff de estructura: tras este paso **no falta ningún objeto real en el mirror**.

```bash
psql "$MIRROR" -v ON_ERROR_STOP=1 -f Database/2026-09-08_replica_estructura_faltante_desde_prod.sql
```

Trae dos funciones de reporte, `rep_banco_diario` y `rep_factura_ticket`, y cinco índices sobre
`factura`, `adm_pago` y `transaccion_abonado`. La segunda función **no tiene ningún otro script en
`Database/`**: este archivo es su único respaldo en el repositorio.

⚠️ Los cinco índices son de rendimiento para el volumen de producción, donde esas tablas tienen
3,9 M, 2,8 M y 12,1 M de filas. En el mirror tienen 225, 0 y 1.494. **No aportan nada aquí**: se
traen para que el diff contra producción quede limpio.

**Queda fuera a propósito:** los datos (las filas de catálogo de esos dos informes, que van en la
fase siguiente) y el andamiaje de la migración (`_m4_*` y `_repcmp`, 38 objetos vacíos que no usa
ningún código).

**¿Ya aplicado?**

```sql
SELECT count(*) FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
 WHERE n.nspname = 'public' AND p.proname IN ('rep_banco_diario', 'rep_factura_ticket');
SELECT count(*) FROM pg_indexes WHERE schemaname = 'public'
   AND indexname IN ('ix_adm_pago_ta_ide', 'ix_factura_company_clientecodigo',
                     'ix_factura_company_numfactura', 'ix_factura_company_recibo_cliente',
                     'ix_ta_company_cliente');
```

Esperado: 2 y 5.

## 6. Estado presunto

| Base | Paso 1 (parche CAI) | Paso 2 (periodo) | Paso 3 (informes) | Paso 4 (julio y agosto) | Paso 5 (emisión) | Paso 6 (datos) | Paso 7 (estructura) |
|---|---|---|---|---|---|---|---|
| `siad_v4` @ 172.16.0.9 (SRV) | ✅ **YA LO TIENE.** Origen del código, aplicado a mano entre el 2026-05-14 y el 2026-08-23 | ✅ **YA LO TIENE.** `period_id` 85, creado el 2026-09-04 desde el portal | ✅ **YA LOS TIENE** desde el 3 y el 4 de septiembre | ✅ **YA LOS TIENE** desde julio y agosto | ❌ **NO se aplica.** A producción no se le toca. El defecto está arriba igual, anotado como hallazgo | — no aplica | ✅ **YA LOS TIENE.** Es el origen |
| `siad_v3_restore` (mirror, localhost) | ✅ **APLICADO** el 2026-09-08. Antes: 4 funciones y 0 índices parciales. Después: 5 y los 3 parciales. Las 8 huellas **coinciden** con `siad_v4` | ✅ **APLICADO** el 2026-09-08. `INSERT 0 1`; quedó `period_id` 71, code 202609, ABIERTO | ✅ **APLICADO** el 2026-09-08, los cuatro con exit 0. Las 5 huellas y las 2 filas de catálogo **coinciden** con `siad_v4` | ✅ **APLICADO** el 2026-09-08 en el orden 4a→4b→4c→4d. **Los 17 objetos coinciden exactamente** con `siad_v4` | ✅ **APLICADO** el 2026-09-08. 3 líneas en la forma nueva, 0 en la vieja | ✅ **APLICADO** el 2026-09-08. Junio cerrado, julio con el ciclo 19 abierto y 473 históricos | ✅ **APLICADO** el 2026-09-08. **Cero objetos reales faltantes** tras aplicarlo |
| `siad_v3_desarrollo` @ 3.208.232.209 | ⏳ **Pendiente** | ⏳ **Pendiente** | ⏳ **Pendiente** | ⏳ **Pendiente** | ⏳ **Pendiente** | ⏳ Sin verificar | ⏳ **Pendiente** |

Ninguno de los dos se verificó conectándose a `siad_v3_desarrollo`: cada paso trae su consulta
«¿ya aplicado?».

Nunca se verifica conectándose a la BD desde aquí: el paso trae su consulta «¿ya aplicado?».

### Suite de pruebas tras aplicar al mirror (2026-09-08)

`dotnet test SIAD.Tests/SIAD.Tests.csproj` con `SIAD_TEST_DB` apuntando al mirror:
**873 superadas, 103 con error, 62 omitidas** de 1.038.

**Los 103 fallos no vienen de este parche.** Las clases que sí ejercitan el ciclo CAI
(`CaiCorrelativoTests`, `AperturaCicloIntegralTests`, `PeriodoCierreF7Tests`, `LecturaV3Tests`)
**pasan todas**. Los fallos se reparten así:

| Clase | Fallos | Causa observada |
|---|---:|---|
| `Aprobaciones.*` (3 clases) | 47 | `23505` llave duplicada en `uq_cfg_aprobacion_nivel`, y "La orden debe enviarse a aprobación antes de poder firmarse" |
| `Presupuesto.*` (5 clases) | 40 | Arrastran el mismo modelo de aprobación |
| `Almacen.RecepcionCompraTests` / `OrdenCompraTests` | 14 | Idem |
| `Informes.EstadoFinancieroLayoutTests` | 1 | Al mirror le falta `2026-09-03_presupuesto_comparativo.sql` |
| `EmisionLecturaPortalTests` | 1 | Ver abajo |

El único fallo con apariencia de relación es
`No_deja_facturar_dos_veces_el_mismo_periodo_pero_si_tras_anular`, que muere con
`P0001: No hay periodo abierto para anio=2026 mes=7`. Es dato, no parche: el test llama a
`fn_adm_periodo_comercial_ciclo_abierto(2, 2026, 7, NULL)` y en el mirror esa función devuelve
`false` con el ciclo en NULL. **Este script no toca esa función** — solo la nombra en un
comentario.

⚠️ **No hay línea base.** La suite no se corrió antes de aplicar el paso 1, así que estos 103
fallos están atribuidos por dependencia (qué objeto toca cada prueba), no por comparación
antes/después.

### Cómo quedó la suite el mismo día

Los 103 se diagnosticaron y se atacaron en dos tandas. Ninguna de las dos introdujo regresiones:
se comparó la lista de fallos antes y después y salieron cero nuevos.

| Momento | Superadas | Con error | Omitidas |
|---|---:|---:|---:|
| Tras el paso 1 | 873 | 103 | 62 |
| Tras aislar las pruebas de aprobación | 961 | 15 | 62 |
| Tras el paso 2 | 987 | 2 | 49 |
| Tras el paso 3 | 988 | 1 | 49 |
| Tras el paso 4 | 988 | 1 | 49 |
| Tras los pasos 5 y 6 | **989** | **0** | 49 |

**La suite quedó en verde**, de 103 fallos por la mañana a ninguno.

El paso 4 no cambió el marcador, y era lo esperado: alinea reportes y lectores de saldo con
producción, no arregla pruebas. Lo importante es que **tampoco rompió ninguna**.

El paso 2 no solo arregló 13 fallos: **13 pruebas que se venían omitiendo volvieron a correr y
pasan** (`CompraCxpTests` de fase 2, `PrvContabilidadTests` y `RetencionRegistroTests`). Se
saltaban por no haber periodo contable abierto, así que la falta de periodo estaba escondiendo
cobertura, no solo rompiendo pruebas.

El aislamiento de las pruebas de aprobación fue cambio de código, no de base: un tercer ayudante
`LimpiarAprobacionPorNivelesAsync()` en `SIAD.Tests/Infrastructure/IntegrationTestBase.cs`,
llamado desde 9 clases. El mirror conserva intacta su configuración de escalera, porque la
limpieza ocurre dentro del `BEGIN … ROLLBACK` de cada prueba.

**El que faltaba** era
`No_deja_facturar_dos_veces_el_mismo_periodo_pero_si_tras_anular`, resuelto por los pasos 5 y 6.
Tenía dos causas encadenadas, y la segunda solo apareció al arreglar la primera: `sp_adm_calcular_factura_lectura` valida el periodo comercial con el ciclo del
histórico del cliente, no con el parámetro que recibe. Ese histórico salía vacío por el defecto
del paso 5. Corregido eso, el mensaje pasó de `ciclo=(sin ciclo)` a `ciclo=19`, y afloró la
segunda causa: el ciclo abierto en julio no tenía clientes, que es lo que resuelve el paso 6.

## 7. Reversa

El script trae el bloque de reversa comentado al final. Recrea los tres índices sin el `WHERE`.

⚠️ **La reversa de los índices puede fallar** si para entonces ya existen filas anuladas
(`status_id = 0`) que colisionen con una vigente: es justamente la situación que el parche
permite y el índice total prohíbe.

Las funciones **no** se restauran desde el script: hay que traerlas del respaldo previo.

## 8. Deuda que este runbook deja abierta

- **No hay pruebas en `SIAD.Tests` que cubran el BUGFIX #4.** El comportamiento nuevo (folio
  anulado que se puede reusar, reserva que consume el correlativo, cierre bloqueado por folios
  pendientes) no está cubierto por ningún test. En el mirror no hay ni una fila con
  `status_id = 0`, así que el caso que el parche arregla no se puede reproducir localmente con
  los datos actuales; en `siad_v4` hay 7 correlativos anulados.
- **Queda otra función sin rescatar.** Este script cubre el ciclo CAI. La misma comparación
  encontró que `rep_factura_ticket(p_company_id, p_factura_id)` también existe solo en
  `siad_v4` y **no tiene ningún script en `Database/`**. Está registrada en el catálogo de
  informes de producción como "Factura (ticket)". Merece su propio rescate.
  Las otras funciones exclusivas de `siad_v4` sí tienen script: `rep_banco_diario`
  (`2026-07-31_rep_banco_diario.sql`), `rep_estado_cambios_patrimonio_matriz`
  (`2026-09-03_patrimonio_matriz.sql`) y `rep_presupuesto_comparativo`
  (`2026-09-03_presupuesto_comparativo.sql`); a esas solo les falta bajar al mirror.
  `_m4_saldos` es andamiaje de la migración y no se rescata.

## 9. Versionado (git)

Archivos nuevos, **untracked** al 2026-09-08:

- `Database/2026-09-08_cai_parche_bugfix4_rescate.sql`
- `Database/2026-09-08_con_periodo_septiembre_2026.sql`
- `Database/2026-09-08_runbook_despliegue_srv.md`

Archivos modificados, sin commit, que acompañan al diagnóstico (código de pruebas, no de
producción):

- `SIAD.Tests/Infrastructure/IntegrationTestBase.cs` (+32 líneas, el ayudante nuevo)
- 9 clases de `SIAD.Tests/Aprobaciones/`, `SIAD.Tests/Presupuesto/` y `SIAD.Tests/Almacen/`
  (+1 línea cada una, la llamada al ayudante)
