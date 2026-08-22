# Runbook de despliegue a SRV — Fases A, B y C (ago 2026)

**Base destino:** `siad_v4` @ `172.16.0.9`
**Fecha:** 2026-08-20
**Alcance:** Fase A (Talento Humano, 3 pasos) + Fase B (1 índice) + Fase C (configuración
contable de Almacén y Compras, 1 script). Tanda cerrada y acotada.

> ### ⚠️ Corrección de base destino
>
> El runbook anterior (`2026-07-23_runbook_despliegue_srv.md`) apunta a **`siad_v3`**.
> Medido el 2026-08-20 contra el servidor: la base **ACTIVA es `siad_v4`**
> (7.378 MB, 2 conexiones); `siad_v3` tiene 340 MB y **0 conexiones**.
>
> El `appsettings.json` compilado en `apc.MobileApi/bin/Release/` también apunta a `siad_v3`,
> pero está desactualizado — **no es fuente de verdad**. Los scripts
> `2026-08-05_rol_*_permisos.sql` sí lo dicen bien: *"la base ACTIVA (siad_v4)"*.
>
> Consecuencia: **la tanda de julio está medida contra la base equivocada** y hay que
> revalidarla antes de usarla. Contra `siad_v4`, a la base solo le faltan **66 objetos**
> (no 1.236): 55 son de Talento Humano y 11 son índices sueltos.

---

## 1. Qué cubre este runbook

Llevar el módulo **Talento Humano** de local a `siad_v4`: las 3 tablas
(`th_empleado`, `th_cargo`, `th_departamento`) y sus 61 filas de datos.

Es lo **único estructural** que le falta a `siad_v4` respecto de local, además de 11 índices
que van aparte (Fase B).

## 2. Antes de empezar (obligatorio)

**Backup de `siad_v4`** — no de `siad_v3`:

```bash
pg_dump -h 172.16.0.9 -U postgres -d siad_v4 -Fc -f siad_v4_antes_fase_a.backup
```

**Definir la conexión** (la clave no va en el repo):

```bash
export SRV="postgresql://USUARIO:CLAVE@172.16.0.9:5432/siad_v4"
```

**Confirmar que la base es la correcta antes de escribir nada:**

```bash
psql "$SRV" -c "SELECT current_database(), pg_size_pretty(pg_database_size(current_database()));"
```

Debe decir `siad_v4` y un tamaño del orden de 7 GB. Si dice `siad_v3`, **parar**.

## 3. Advertencias clave (leer antes de aplicar)

- ⚠️ **El orden NO es el alfabético.** El paso 3 tiene fecha `2026-08-19`, anterior a la del
  paso 2 (`2026-08-20`), pero va **de último**. Aplicarlos por nombre de archivo deja los
  catálogos vacíos y los empleados sin `cargo_id` / `departamento_id`.
- Los 3 pasos son **aditivos y re-ejecutables**. Ninguno hace `DROP`, `DELETE`, `TRUNCATE`
  ni `UPDATE` masivo sobre datos existentes.
- Los tres asumen **`company_id = 2`**.
- La semilla del paso 2 contiene **nombre e identidad de 34 personas reales**.
- Nada fuera del módulo referencia estas tablas (verificado con `pg_constraint`), así que
  la tanda es reversible con
  `DROP TABLE th_empleado, th_cargo, th_departamento CASCADE;`.

## 4. Orden de aplicación (resumen)

| Paso | Script | Naturaleza | ¿Re-ejecutable? | Depende de |
|---:|---|---|:--:|---|
| 1 | `2026-08-19_th_empleado.sql` | Aditivo (tabla nueva) | Sí | — |
| 2 | `2026-08-20_th_empleado_seed.sql` | Datos idempotente (34 filas) | Sí | Paso 1 |
| 3 | `2026-08-19_th_cargo_departamento.sql` | Aditivo + datos derivados | Sí | Pasos 1 y 2 |

## 5. Detalle por paso

### Paso 1 — `th_empleado` (tabla base del módulo)

Crea la tabla vacía, con su índice por empresa y el único UNIQUE `(company_id, codigo)`.

```bash
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-19_th_empleado.sql
```

**¿Ya aplicado?**

```sql
SELECT to_regclass('public.th_empleado');   -- NULL = falta
```

**Verificación:** la tabla existe y tiene 15 columnas.

---

### Paso 2 — Semilla de los 34 empleados

Inserta los 34 empleados con su texto de `cargo` y `departamento` — el paso 3 los necesita
para derivar los catálogos. `ON CONFLICT (company_id, codigo) DO NOTHING`.

```bash
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-20_th_empleado_seed.sql
```

**¿Ya aplicado?**

```sql
SELECT count(*) FROM th_empleado WHERE company_id = 2;   -- esperado: 34
```

**Verificación:** 34 filas, 17 valores distintos de `cargo`, 10 de `departamento`.
El script avisa por `NOTICE` si detecta que el paso 3 corrió antes que él.

---

### Paso 3 — Catálogos de cargos y departamentos ⚠️ VA DE ÚLTIMO

Crea `th_cargo` y `th_departamento`, agrega `th_empleado.cargo_id` / `departamento_id` con
FK compuesta por empresa, siembra cada catálogo con los valores **distintos** que ya trae el
texto de los empleados y enlaza cada empleado a su id.

```bash
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-19_th_cargo_departamento.sql
```

**¿Ya aplicado?**

```sql
SELECT to_regclass('public.th_cargo');   -- NULL = falta
```

**Verificación final de toda la Fase A:**

```sql
SELECT 'th_empleado'        AS t, count(*) FROM th_empleado
UNION ALL SELECT 'th_cargo',            count(*) FROM th_cargo
UNION ALL SELECT 'th_departamento',     count(*) FROM th_departamento
UNION ALL SELECT 'sin cargo_id',        count(*) FROM th_empleado WHERE cargo_id IS NULL
UNION ALL SELECT 'sin departamento_id', count(*) FROM th_empleado WHERE departamento_id IS NULL;
```

Esperado — **exactamente** esto:

```
 th_empleado         | 34
 th_cargo            | 17
 th_departamento     | 10
 sin cargo_id        |  0
 sin departamento_id |  0
```

Si `sin cargo_id` > 0, el paso 3 corrió antes que el 2: volvé a correr el paso 3 (es
idempotente y enlaza por coincidencia de nombre).

## 5-bis. ✅ Fase A APLICADA en `siad_v4` — 2026-08-20

Los tres pasos se ejecutaron en orden contra `siad_v4`, con `ON_ERROR_STOP=1`, exit 0 cada uno.
Verificación posterior en la base:

```
 th_empleado         | 34
 th_cargo            | 17
 th_departamento     | 10
 sin cargo_id        |  0
 sin departamento_id |  0
```

Idéntico a local. Las 3 tablas no existían antes de aplicar. **Fases B y C siguen pendientes.**

## 6. Prueba hecha antes de publicar este runbook

La secuencia completa de los 3 pasos se ejecutó **sobre el mirror local `siad_v3_restore`**,
partiendo de cero (`DROP` de las 3 tablas) y **dentro de `BEGIN … ROLLBACK`**, sin dejar
rastro. Resultado: 34 / 17 / 10 con cero empleados sin enlazar — idéntico a local. El mirror
quedó intacto (verificado después del rollback).

## 7. Estado presunto

| Script | Local (`siad_v3_restore`) | `siad_v4` |
|---|:--:|:--:|
| `2026-08-19_th_empleado.sql` | sí | ✅ **APLICADO 2026-08-20** |
| `2026-08-20_th_empleado_seed.sql` | sí (los datos son de acá) | ✅ **APLICADO 2026-08-20** |
| `2026-08-19_th_cargo_departamento.sql` | sí | ✅ **APLICADO 2026-08-20** |
| `2026-08-20_ix_factura_company_cliente_estado.sql` (Fase B) | sí | ✅ **APLICADO 2026-08-20** |
| `2026-08-20_fase_c_integracion_almacen_compras.sql` (Fase C) | sí (equivalente) | ✅ **APLICADO 2026-08-20** |
| `2026-08-20_cfg_correo_notificaciones_seed.sql` (Fase C.3) | sí (los datos son de acá) | ✅ **APLICADO 2026-08-20** |

Nunca verificar el estado del SRV conectándose por iniciativa propia: correr la consulta
«¿ya aplicado?» de cada paso al momento de desplegar.

## 8. Versionado (git)

Archivos nuevos de esta tanda, pendientes de commit:

- `Database/2026-08-20_th_empleado_seed.sql`
- `Database/2026-08-20_ix_factura_company_cliente_estado.sql`
- `Database/2026-08-20_fase_c_integracion_almacen_compras.sql`
- `Database/2026-08-20_cfg_correo_notificaciones_seed.sql`
- `Database/2026-08-20_runbook_despliegue_srv.md`

Los scripts de los pasos 1 y 3 ya están versionados (commit `6368afa` y siguientes).

---

# Fase B — Índices

## B.1 Qué cubre (y por qué es 1 y no 11)

El diff decía que a `siad_v4` le faltaban **11 índices**. Medidos contra la base real,
**10 no aportan nada**. `factura` en `siad_v4` tiene **3.896.635 filas / 906 MB** — no es
una tabla donde se agreguen índices a ciegas.

| Índice | Veredicto | Motivo medido |
|---|---|---|
| `ix_factura_company_cliente_estado` | **CREAR** | Soporta `sp_obtener_cliente_saldo`, que existe y se usa en `siad_v4` y filtra por `clientecodigo` + `estado`. Arriba solo hay `(company_id, clientecodigo)`. |
| `ix_factura_cartera_vencida` | omitir | Duplicado exacto de `ix_factura_company_fechaemision`, ya presente (27 MB, 297 escaneos). |
| `ix_factura_company` | omitir | `company_id` tiene **1 solo valor** en la tabla. |
| `ix_factura_tipo_doc` | omitir | `tipo_documento_fiscal_id` tiene **1 solo valor**. |
| `ix_factura_estado_id` | omitir | 3 valores distintos en 3,9 M filas. |
| `idx_factura_estado_fecha` | omitir | Columna líder `estado`, 3 valores. |
| `idx_factura_numfactura_cliente` | omitir | `numfactura` es **100 % NULL**. |
| `ix_factura_factura_origen` | omitir | Parcial sobre `factura_origen_id`, **100 % NULL**: indexaría 0 filas. |
| `ix_bitacora_maestros_company_entidad` | omitir | No viene de ningún script: creado a mano en el mirror. |
| `ix_bitacora_maestros_company_registro` | omitir | Ídem. |
| `ix_bitacora_maestros_company_usuario` | omitir | Ídem. `siad_v4` ya tiene los 2 que sí crea `2026-07-17_bitacora_maestros.sql`, y la tabla tiene 3 filas. |

## B.2 Paso único — `ix_factura_company_cliente_estado` ⚠️ SIN transacción

```bash
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-20_ix_factura_company_cliente_estado.sql
```

⚠️ **No envolver en `BEGIN/COMMIT` ni pasar `-1`.** `CREATE INDEX CONCURRENTLY` no puede
correr dentro de una transacción. Se usa `CONCURRENTLY` porque un `CREATE INDEX` normal
bloquea **todas las escrituras** de `factura` mientras construye — sobre 3,9 M filas, eso es
parar la facturación.

Conviene correrlo sin transacciones largas abiertas sobre `factura`: `CONCURRENTLY` espera
a que terminen.

**¿Ya aplicado?**

```sql
SELECT indexname FROM pg_indexes
 WHERE schemaname='public' AND indexname='ix_factura_company_cliente_estado';
```

**Verificación — comprobar que quedó VÁLIDO:**

```sql
SELECT c.relname, i.indisvalid AS valido, pg_size_pretty(pg_relation_size(c.oid)) AS tamano
  FROM pg_class c JOIN pg_index i ON i.indexrelid = c.oid
 WHERE c.relname = 'ix_factura_company_cliente_estado';
```

Esperado: `valido = true`, ~35 MB. ⚠️ Si `valido = false`, el `CONCURRENTLY` falló a mitad y
dejó un índice **inválido** que Postgres no usa pero que igual pesa y frena las escrituras:

```sql
DROP INDEX CONCURRENTLY IF EXISTS public.ix_factura_company_cliente_estado;
```

...y volver a correr el script.

## B.2-bis ✅ APLICADO en `siad_v4` — 2026-08-20

`CREATE INDEX CONCURRENTLY` corrió sin bloquear: **11 segundos**, índice **válido**
(`indisvalid = true`), **27 MB**. `factura` pasó de 6 a 7 índices.

## B.3 Anotado para revisar (fuera de alcance)

`ix_factura_company_recibo_cliente` ocupa **151 MB** y registra **0 escaneos** en
`pg_stat_user_indexes`. Es candidato a eliminar: cuesta espacio y frena cada escritura de
`factura`. **No se toca en esta tanda** — eliminarlo es destructivo y los contadores de
`pg_stat` se reinician con Postgres, así que 0 escaneos podría ser solo una ventana corta.
Antes de decidir: confirmar desde cuándo acumulan las estadísticas
(`pg_stat_get_db_stat_reset_time`) y si algún SP usa el patrón
`(company_id, numrecibo, clientecodigo)`.

---

---

# Fase C — Configuración contable de Almacén y Compras

## C.1 Qué cubre

Un solo script: `2026-08-20_fase_c_integracion_almacen_compras.sql`.

```bash
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-20_fase_c_integracion_almacen_compras.sql
```

| Pieza | Qué inserta / cambia |
|---|---|
| C.1a | `con_diario` **18 INV «INVENTARIO»**, **28 COM «COMPRAS»** |
| C.1b | `con_tipo_transaccion` **54 INV «INVENTARIOS»**, **55 COM «COMPRAS»** |
| C.1c | `con_integracion_asiento` **ALMACEN** (18/54) y **COMPRAS** (28/55) |
| C.2 | `alm_tipo_articulo.cuenta_inventario` de los 9 tipos → cuentas que sí existen arriba |

Todo en una transacción. Los ids 18/28/54/55/801/851 se verificaron **libres** en `siad_v4`.
El orden interno importa: hay FK compuestas `(company_id, journal_id)` y `(company_id, type_id)`
desde `con_integracion_asiento`.

⚠️ **No enciende nada.** `con_integracion_config.activo_almacen` y `.activo_compras` quedan en
`f`. Encenderlos va con el despliegue del binario que los soporta.

## C.2 El problema de las cuentas (no era el formato)

Lo que parecía «arriba las cuentas están con guiones» resultó más profundo:

- El plan de cuentas de `siad_v4` **no usa guiones** (0 de 1.674 códigos los tienen).
- Los 9 tipos de artículo apuntan a `114-01-01-01-01` y similares → **ninguna existe**.
- Y **tampoco existirían quitándoles los guiones**: la rama `114` de producción llega solo a
  **nivel 5** (8 cuentas), mientras que local tiene 19 incluidas las de **nivel 6** que usa el
  almacén (`11401010101`, `11401010201`, `11409020101`…).

**Decisión tomada (2026-08-20): no abrir cuentas nuevas en el plan de producción.** Se remapea
cada tipo al ancestro más específico que ya existe arriba y permite posteo:

| Tipos | Cuenta destino | Nivel |
|---|---|:--:|
| 01, 02, 03, 05, 09 | `11401010000` Materiales | 5 |
| 07 | `11401020000` Insumos | 5 |
| 04, 06, 08 | `11409000000` Otros Inventarios | **4** |

Se pierde detalle contable — cinco tipos comparten cuenta — a cambio de no tocar el plan de una
empresa real. Los tipos 04, 06 y 08 caen en **nivel 4** porque la rama `11409` de producción no
tiene nivel 5.

El script **verifica antes de actualizar** que las tres cuentas destino existan y permitan
posteo; si no, aborta con excepción.

## C.3 Correo / SendGrid — ✅ APLICADO 2026-08-20 (falta la API key)

Script: `2026-08-20_cfg_correo_notificaciones_seed.sql`. Aplicado en `siad_v4`. Estado:

| Tabla | Contenido |
|---|---|
| `cfg_correo` | id 1 · SENDGRID · remitente `egaray@koalaoutsourcing.com` · **`activo = false`, sin API key** |
| `cfg_notificacion` | id 1 · tipo `ALMACEN` · activa |
| `cfg_notificacion_destinatario` | ids 1 y 2 → `egaray@` y `srivera@`, clase TO |

⚠️ **La API key no se migró a propósito.** `apc/Program.cs` usa en producción
`SetApplicationName("HODSOFT.Prestadoras")` + `ProtectKeysWithDpapi(protectToLocalMachine: true)`,
contra `"HODSOFT.Prestadoras.Development"` sin DPAPI en local. Son dos motivos acumulados por los
que el ciphertext de local **no descifra** arriba: distinto discriminador de propósito, y key-ring
**atado a la máquina**. Copiarlo dejaría el envío fallando en silencio.

**PENDIENTE — paso manual:** portal **en el servidor** → mantenimiento de Correo → pegar la API key
de SendGrid → guardar → marcar `activo`. Hasta entonces no se envía ningún correo.

> Nota técnica: el `id` de las 3 tablas es `GENERATED ALWAYS AS IDENTITY`. No se pueden copiar los
> ids de local; el script deja que la identity los genere y enlaza los destinatarios por búsqueda
> (`tipo = 'ALMACEN'`). Ojo: `column_default` sale vacío en columnas identity — hay que mirar
> `is_identity`.

## C.3-bis ✅ Fase C APLICADA en `siad_v4` — 2026-08-20

Todo en una transacción, exit 0. Verificado en la base:

```
 diarios 18/28                        | 2 de 2
 tipos 54/55                          | 2 de 2
 asientos ALMACEN/COMPRAS             | 2 de 2
 tipos de articulo con cuenta INVALIDA| 0 de 9   (antes: 9 de 9)
 activo_almacen = false · activo_compras = false
```

Los interruptores **siguen apagados**, como corresponde: se encienden con el binario.

## C.4 Verificación

```sql
SELECT module, journal_id, type_id
  FROM con_integracion_asiento
 WHERE company_id = 2 AND module IN ('ALMACEN','COMPRAS');

SELECT t.codigo, t.cuenta_inventario,
       EXISTS (SELECT 1 FROM con_plan_cuentas c
                WHERE c.company_id = 2 AND c.code = t.cuenta_inventario
                  AND c.allows_posting = true) AS cuenta_valida
  FROM alm_tipo_articulo t
 WHERE t.company_id = 2 ORDER BY t.codigo;
```

Esperado: 2 asientos, y `cuenta_valida = true` en los 9 tipos.

## C.5 Prueba hecha antes de publicar

Ejecutada sobre el mirror dentro de `BEGIN … ROLLBACK`, simulando el estado de `siad_v4`
(sin asientos ALMACEN/COMPRAS y con las 9 cuentas con guiones). Resultado: 2 diarios, 2 tipos,
2 asientos y 0 cuentas con guiones. **Segunda corrida seguida: idempotente**, sin duplicar.
El mirror quedó intacto tras el rollback.

---

## 9. Después de estas fases

- **No llevar hacia arriba**: `cfg_recargo_mora` (arriba está **activo**, en local no),
  el correlativo `adm_codigo_cliente_config.siguiente`, ni los motivos de anulación/aumento
  — en todos ellos `siad_v4` va adelante.
- **Encender la integración** (`activo_almacen`, `activo_compras`) solo junto con el binario
  que soporta esos módulos.
- **Traer a local** lo que arriba va adelante, para que el mirror refleje producción.

---

# Fix 2026-08-21 — `departamento` VARCHAR(3) → VARCHAR(80)  ✅ APLICADO

**Síntoma:** error **500 Internal Server Error** al crear una requisición desde el portal.

**Causa:** `alm_requisicion_hdr.departamento` era `varchar(3)` en `siad_v4` y `varchar(80)` en
local. Desde que el departamento se elige del catálogo, el portal manda el **nombre**
(«Recursos Humanos», 16 caracteres). Postgres rechaza el INSERT → `DbUpdateException`; el
controller solo traduce `InvalidOperationException` a 400, así que esa excepción se escapa
como 500.

**Aplicado en `siad_v4`** (exit 0 los dos):

```bash
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-19_alm_requisicion_departamento_catalogo.sql
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-08-19_alm_descargo_departamento_catalogo.sql
```

| Columna | Antes | Ahora |
|---|---|---|
| `alm_requisicion_hdr.departamento` | varchar(3) | **varchar(80)** |
| `alm_requisicion.departamento` | varchar(3) | **varchar(80)** |
| `alm_descargo_hdr.departamento` | varchar(3) | **varchar(80)** |

Datos históricos intactos: 42.694 de 42.866 en `alm_requisicion`, 42.739 de 42.757 en
`alm_descargo`. Verificado con un INSERT real revertido: «Recursos Humanos» entra.

> El **descargo tenía el mismo bug latente** — iba a fallar igual en cuanto se usara.

## ⚠️ Punto ciego del método de comparación

El diff que dio «66 objetos faltantes» **no detectaba anchos de columna**: la huella comparaba
`data_type` (`character varying`) sin `character_maximum_length`, así que `varchar(3)` y
`varchar(80)` figuraban como idénticos. Y los scripts que solo hacen `ALTER COLUMN ... TYPE`
no crean objetos, así que el parseo de `CREATE`/`ADD COLUMN` tampoco los veía.

**Barrido completo posterior** (8.272 columnas, con longitud y precisión): solo **4 diferencias**
— las 3 de arriba, ya corregidas, más `factura_detalle.montovalor_saldo` (arriba `numeric` sin
precisión contra `numeric(18,2)` local: más permisivo, inofensivo). No quedan más.
