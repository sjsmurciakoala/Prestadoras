# Migración Proveedores SIMAFI → Portal APC · Mapeo

**Fecha:** 2026-07-10
**Origen:** MySQL `bdsimafi` @ `172.16.0.3:3306` (SIMAFU 3, MySQL 5.0.96), user `migracion`.
Solo conecta el driver .NET `MySqlConnector`; el CLI moderno falla por protocolo.
**Destino:** PostgreSQL `siad_v3` (SRV `172.16.0.9`) + espejo `siad_v3_restore` (localhost). `company_id = 2`.

> Estado: **EJECUTADO Y VERIFICADO EN EL MIRROR** (`localhost/siad_v3_restore`). **Pendiente de aplicar en el SRV** — requiere autorización explícita del usuario.
>
> Artefactos: `Database/2026-07-10_proveedores_simafi_{00_staging_ddl,01_landing,02_transform,03_reconciliacion}.sql` + `tools/MigradorProveedores/extract.cs`.
>
> Resultado en el mirror: **605 proveedores** (+ `SINPROV`), **4,297 cabeceras** y **3,257 líneas** de compromiso, monto L37,629,292.91. Cero huérfanos, cero descuadres de correlativo, datos del portal intactos.

---

## 1. Inventario

### Origen

| Tabla | Filas | Rol |
|---|---:|---|
| `proveedor` | 606 | Catálogo de proveedores. Única tabla de proveedores en toda la BD (280 tablas). |
| `ordenesp` | 75,436 | Órdenes de pago, **grano línea presupuestaria**. 35,780 órdenes distintas. |
| `ordenescompra` | 17,106 | Órdenes de compra (otro flujo). |
| `compras` | 4,635 | Compras por producto. |

### Destino

| Tabla | Filas hoy | Observación |
|---|---:|---|
| `prv_proveedores` | 2 | La tabla viva. **Sin PK ni FK**; solo índice por `company_id`. |
| `prv_tipoproveedor` | 4 | Capturado en el portal: Limpieza, Mayoristas, Fabricantes, Servicios. |
| `prv_bancos` | 5 | Capturado en el portal. |
| `prv_proveedor_cuenta_bancaria` | 1 | Capturado en el portal. |
| `prv_compromiso_hdr` / `_dtl` | 4 / 4 | Pruebas. |
| `prv_kardex` | 0 | Vacía. |
| `prv_tipostransacc` | 0 | Vacía. |
| `stg_proveedor_nombres` | 420 | Staging de sesión previa: `legacy_cuenta` → `descrip` (nombre desde el plan de cuentas). |

**Ojo:** la entidad EF se llama `prv_proveedore` (singular) pero mapea a la tabla `prv_proveedores`. La unicidad lógica es `(cod_proveedor, company_id)` y hoy la impone **solo la aplicación** (`WHERE NOT EXISTS` en `ProveedoresService`), no la base.

---

## 2. Catálogo: `proveedor` → `prv_proveedores`

Longitudes medidas sobre las 606 filas: ninguna trunca.

| Destino | Tipo | Origen | Notas |
|---|---|---|---|
| `cod_proveedor` | varchar(20) NOT NULL | `TRIM(codigo)` | máx. 4 chars. **2 colisiones al hacer TRIM** (ver D5). |
| `cod_tipoproveedor` | smallint NOT NULL | `tipoprov` | **100% NULL en origen** (ver D3). |
| `nombre` | varchar(150) NOT NULL | `TRIM(proveedor)` | máx. 60. Sin vacíos. |
| `cuenta_contable` | varchar(20) NOT NULL | `regexp_replace(contable,'\D','','g')` | 219 vacíos, 17 inexistentes en el plan (ver D4). |
| `direccion` | varchar(1000) NOT NULL | `direccion` | máx. 99. 132 vacías → `''`. |
| `fecha_creacion` | timestamp NOT NULL | `fecha` | 2014-08-08 → 2026-07-06. Sin nulos. |
| `fecha_modificacion` | timestamp | — | `NULL`. |
| `status` | boolean | — | `TRUE`. El origen no tiene bandera de activo. |
| `cuenta_bancaria` | varchar(50) | — | `NULL`. El origen no guarda cuentas bancarias. |
| `compras_acum`, `compras_dolares` | double | — | `NULL`. No existen en `proveedor`. |
| `saldo_actual`, `saldo_act_dolares` | double | — | `NULL`. Idem. |
| `saldo_anterior`, `saldo_ant_doleres` | double | — | `NULL`. Idem. (`doleres` es typo del esquema, se respeta.) |
| `razon_social` | varchar(150) | `razonsocial` | máx. 99. 441 vacías. |
| `rtn` | varchar(20) | `rtn` | máx. 20. 81 vacíos. Formato heterogéneo (`05061988001731`, `1002-1969-00114`). |
| `telefono` | varchar(20) | `telefono` | máx. 10. |
| `pagina_web` | varchar(150) | `paginaweb` | máx. 26. |
| `fax` | varchar(50) | `fax` | máx. 17. |
| `email` | varchar(150) | `email` | máx. 40. 401 vacíos. |
| `nombrebanco1`, `nombrebanco2` | varchar(80) | — | `NULL`. Sin origen. |
| `nombre_contacto` | varchar(150) | `percontacto` | máx. 30. |
| `company_id` | integer NOT NULL | — | `2`. |
| `usuario_creo` | varchar(100) NOT NULL | — | `'migracion'`. |
| `usuario_modifica` | varchar(100) | — | `NULL`. |
| `ultimo_correlativo_compromiso` | integer NOT NULL | — | `0`. |
| `rowid` | uuid | — | default `gen_random_uuid()`. |

### Campos del origen que no tienen destino

Con datos — se preservan en staging, no se pierden:

| Campo | Filas con dato | Qué es |
|---|---:|---|
| `codigo2` | 606 | Código numérico alterno. |
| `contableant` | 187 | Cuenta contable anterior. |
| `representantel` | 54 | Representante legal. |
| `observa` | 29 | Observaciones. |

Vacíos en el 100% de las filas — se descartan sin pérdida: `solvencia`, `prodsumi`, `dirfoto1`, `dirfoto2`, `pais`, `ciudad`, `descritipoprov`.

### Cross-walk de la cuenta contable

Normalizando `contable` a solo dígitos y cruzando contra `con_plan_cuentas` (company 2, 1,674 cuentas):

| Estado | Proveedores |
|---|---:|
| Mapea a `con_plan_cuentas` | 370 |
| Sin `contable` en el origen | 219 |
| `contable` no existe en el plan | 17 |

De los 17 huérfanos, solo 4 se rescatan vía `stg_simafi_cuenta_map`. Los códigos vienen en dos granularidades: 255 de 11 dígitos y 132 de 12 dígitos (`211-01-01-09-166`, último segmento de 3 dígitos); ambas existen en el plan.

**La cuenta contable no identifica al proveedor.** Es 1:1 en 367 cuentas, pero `21101010948` la comparten **8 proveedores** y otras 6 cuentas la comparten 2. Cualquier join por cuenta produce ambigüedad.

---

## 3. Compromisos: `ordenesp` → `prv_compromiso_hdr` / `_dtl`

`ordenesp` es grano línea: 1 a 10+ líneas por `ordenp`. Encaja con la partición cabecera/detalle del destino.

**Ventana limpia 2025-01-01 → hoy: 8,179 líneas en 4,297 órdenes.** El resto de la tabla trae fechas basura (años `0201`, `2104`, `2201`, `2202`) que hay que filtrar por rango explícito, no por `YEAR() >= 2025`.

Corrección importante sobre los nombres de columna: **`cuentac` es la cuenta por pagar del proveedor** (`211-…`) y **`contable` es la cuenta de gasto** (`721-…`). Es al revés de lo que sugiere el nombre.

### Cabecera (agrupando por `ordenp`)

| Destino | Origen | Notas |
|---|---|---|
| `numero_orden` int NOT NULL | `CAST(ordenp AS int)` | 120 líneas con `ordenp` vacío en la ventana. Máx. 333,420, cabe en int. |
| `fecha` NOT NULL | `MIN(fecha)` | |
| `monto` numeric NOT NULL | `SUM(debe)` o `SUM(valorp)` | **Ambiguo (ver D6).** |
| `concepto` varchar(150) NOT NULL | `concepto` | máx. 435 → **1,426 líneas truncarían.** |
| `cod_proveedor` varchar(7) | derivado | Solo 44% enlazable (ver D7). Nótese varchar(**7**) vs varchar(20) en `prv_proveedores`. |
| `cuenta_contable` varchar(20) | `cuentac` | CxP del proveedor. |
| `cod_proyecto` varchar(20) | `codproy` | |
| `pagar_a` varchar(100) | `beneficiar` | máx. 60. |
| `nombre_proveedor` varchar(150) | `beneficiar` | |
| `rtn` varchar(20) | vía enlace a `proveedor.rtn` | |
| `anulado` boolean NOT NULL | — | `ordenesp` no tiene columna de anulación. `FALSE`. |
| `flag_proveedor`, `status_transacc`, `correlativo_proveedor` | — | Sin origen. Semántica por definir. |

### Detalle (una fila por línea de `ordenesp`)

| Destino | Origen |
|---|---|
| `numero_orden` | `ordenp` |
| `cod_presupuestario` varchar(20) | `codigo` char(12) |
| `programa` varchar(2) | `codpro` |
| `actividad` varchar(2) | `codact` |
| `objeto_gasto` varchar(100) | `renglon` (+ descripción vía `renglon.desrenglon`) |
| `cuenta_gasto` varchar(20) | `contable` |
| `monto` numeric | `debe` |
| `descripcion`, `conceptodtl` | `concepto` |

`codpro` / `codact` / `renglon` ya tienen catálogos migrados en staging por el trabajo de Presupuesto (`stg_simafi_programa`, `_actividad`, `_renglon`).

### `ordenesp` no es un detalle de orden de compra: es una partida de doble entrada

Cada línea trae `debe` / `haber`. El `concepto` es constante dentro de la orden en 4,231 de 4,297 casos (es de cabecera). `contable` (gasto) nunca aparece sin `cuentac`: 3,257 líneas traen ambas, 2,690 solo la CxP, 2,232 ninguna.

**La partida no cuadra:** de 4,297 órdenes, solo **56** tienen `SUM(debe) = SUM(haber)`.

### El problema del enlace compromiso → proveedor

`ordenesp` **no tiene `cod_proveedor`**. Los dos caminos posibles, medidos sobre las 8,179 líneas de la ventana:

| Camino | Enlace único | Ambiguo | Sin enlace |
|---|---:|---:|---:|
| `cuentac` → `proveedor.contable` | 2,611 | 629 | 4,939 |
| `beneficiar` → `proveedor.proveedor` (nombre exacto) | 2,454 | 69 | 5,656 |
| **Combinado (cuenta única O nombre único)** | **3,587** | — | **4,592** |

A nivel de orden: **1,961 de 4,297 enlazan; 2,336 no.**

**Pero el 56% sin enlace no es un problema de calidad de datos.** Los beneficiarios que no enlazan son, en su mayoría, **terceros que no son proveedores**: BANCO ATLANTIDA (260 líneas), BANCO DAVIVIENDA (132), BANCO LAFISE (81+68), BANCO CUSCATLAN (69), el SAR (70), el IHSS (51), el RAP (80), y personas naturales (SANDRA ELIZABETH MALDONADO CUELLAR, 209). `ordenesp` es el libro de **órdenes de pago en general**, no de compromisos con proveedores.

### El monto del compromiso: RESUELTO — `SUM(valorp)`

`valorp` es el **valor de la línea presupuestaria**, no un total de cabecera. La regla es exacta:

- `valorp <> 0` ⟺ la línea tiene renglón presupuestario ⟺ la línea tiene cuenta de gasto (3,257 líneas; 8 excepciones, ver abajo).
- En esas 3,257 líneas, **`valorp = debe` en 3,257 de 3,257 (100%)**.
- Agregado por orden: `SUM(valorp) = SUM(debe)` sobre líneas presupuestarias en **2,146 de 2,146 órdenes, cero discrepancias**. Total **L37,317,562.85**.

Las 4,794 líneas restantes (solo `cuentac`, sin gasto ni renglón) son los asientos de pago y retención contra la CxP: ahí viven los `debe`/`haber` grandes que inflaban `SUM(debe)` a L194.1M.

**Las 8 excepciones** (`32838`, `32797`, `32843`, `33287`, `34174`, `33531`, `35340`, `36119`) traen `valorp <> 0` con `debe = haber = 0` y sin cuentas. Solo `SUM(valorp)` las recupera; `SUM(debe)` las dejaría en cero.

> Corrección: un análisis previo comparó `MAX(valorp)` contra `SUM(debe)` y concluyó que el monto era indeterminable. El error era la agregación, no los datos: `valorp` es por línea y hay que sumarlo. Los supuestos contraejemplos (`31458`, `32370`) cuadran exactos con `SUM`.

**Regla adoptada:** `prv_compromiso_hdr.monto = SUM(valorp)` sobre todas las líneas de la orden.

### La mitad de las órdenes no tiene contenido presupuestario

| | Enlaza a proveedor | No enlaza | Monto |
|---|---:|---:|---|
| **Con línea presupuestaria** | 912 | 1,234 | L37,317,562.85 |
| **Sin línea presupuestaria** | 1,049 | 1,102 | L0.00 |

2,151 de las 4,297 órdenes son puros asientos de pago/retención contra la CxP: no tienen renglón, ni cuenta de gasto, ni `valorp`. Cargarlas como compromisos las dejaría con `monto = 0`.

---

## 4. Sin origen viable

- **`prv_bancos` y `prv_proveedor_cuenta_bancaria`**: `proveedor` no guarda banco ni cuenta bancaria. Los 5 bancos y 1 cuenta actuales son captura del portal. **No hay nada que migrar.**
- **`prv_kardex`** (31 columnas: saldos, `num_cheque`, `cai`, `correlativo_dei`) y **`prv_tipostransacc`**: no hay tabla equivalente en el origen. Candidatos parciales (`pagoscon` 112 filas, `pagos`, `maestroche`) no cubren la forma. Igual que en Bancos, los saldos por proveedor no son reconstruibles con lo que hay. **Recomendación: dejar fuera del alcance.**

---

## 5. Decisiones abiertas

### Cerradas (usuario, 2026-07-10)

| # | Decisión | Resolución |
|---|---|---|
| **D1** | Alcance | **Catálogo + compromisos.** |
| **D2** | Ventana temporal | Catálogo **completo (606)**; ventana `2025-01-01 → hoy` solo para compromisos. |
| **D3** | `cod_tipoproveedor` (origen 100% NULL) | **Crear tipo nuevo `Sin clasificar`** en `prv_tipoproveedor` y asignarlo a los 606. |
| **D4** | 236 proveedores sin cuenta contable válida | **Cargarlos con `cuenta_contable = ''`.** Los 606 entran; quedan listados en la vista de pendientes. |
| **D5** | `cod_proveedor` duplicado tras TRIM | `0519` se deduplica (fila idéntica). En `0322`, **COMERCIAL INDIRA conserva `0322`** y **GENERAL DE REPUESTOS (GERESA) se renombra a `0322B`**; el cambio se reporta en la reconciliación. |

### Abiertas

| # | Decisión | Resolución |
|---|---|---|
| **D6** | Monto de la cabecera | **`SUM(valorp)`** por orden. Verificado: idéntico a `SUM(debe)` en las 3,257 líneas presupuestarias y en las 2,146 órdenes, y además recupera las 8 órdenes donde solo `valorp` trae el importe. |
| **D7** | Órdenes que no enlazan a proveedor | Cargar **todas** las 4,297, colgadas de un proveedor genérico nuevo `SINPROV` ("Sin proveedor"). En la descripción se anota la categoría del beneficiario (banco, SAR, IHSS, RAP, persona natural) según corresponda. |
| **D8** | `concepto` trunca (704 de 4,297 órdenes, máx. 435) | **Ampliar `prv_compromiso_hdr.concepto` a varchar(500)** vía DDL, en SRV y mirror. Sin pérdida de texto. |

### Abierta

| # | Decisión | Contexto |
|---|---|---|
| **D9** | ¿Se cargan las 2,151 órdenes sin línea presupuestaria, y cómo se marcan? | Quedarían con `monto = 0` y sin detalle. Ver §7: `status_transacc` no significa lo que parecía. |

---

## 7. Hallazgos de integración con la aplicación

Verificados leyendo `OrdenesPagoDirectoService` y el esquema, antes de escribir el loader.

**`status_transacc = true` significa "procesada", y la pantalla oculta las procesadas por defecto.**

```csharp
if (!filtro.IncludeProcessed)
    headersQuery = headersQuery.Where(x => x.status_transacc != true);
if (!filtro.IncludeAnuladas)
    headersQuery = headersQuery.Where(x => x.anulado != true);
```

Consecuencia: marcar las órdenes vacías con `status_transacc = false` las deja **visibles** en la vista por defecto, no ocultas. Y como las 4,297 órdenes son históricas (2025→hoy, ya ejecutadas), cargarlas con `NULL`/`false` haría que la pantalla de Órdenes de Pago Directo muestre las 4,297 como **pendientes**.

**`prv_compromiso_hdr` no tiene `company_id`.** No implementa `ICompanyScopedEntity` y el servicio no filtra por compañía. Contradice la regla no negociable de multi-tenancy del `CLAUDE.md`. Cargar 4,297 órdenes las hace visibles desde cualquier compañía. Es un defecto preexistente del esquema, no de la migración, pero esta carga lo vuelve visible.

**`correlativo_proveedor` es una secuencia por proveedor.** El servicio la avanza con:

```sql
UPDATE prv_proveedores
SET ultimo_correlativo_compromiso = COALESCE(ultimo_correlativo_compromiso,0) + 1
WHERE btrim(cod_proveedor)=btrim(@cod_proveedor) AND company_id=@company_id
RETURNING ultimo_correlativo_compromiso;
```

El loader debe hacer backfill de `prv_proveedores.ultimo_correlativo_compromiso = MAX(correlativo_proveedor)` por proveedor; si no, la primera orden creada desde la UI reusaría un correlativo existente.

**Sin colisión en `numero_orden`.** Los `ordenp` de la ventana van de **29,169 a 333,420**; las 4 filas de prueba usan 1–4. Ninguna tabla del módulo (`prv_proveedores`, `prv_tipoproveedor`, `prv_compromiso_hdr`, `prv_compromiso_dtl`) tiene PK ni FK.

**`prv_compromiso_hdr.cod_proveedor` es varchar(7):** `SINPROV` cabe justo, y `0322B` también.

---

## 6. Plan propuesto (tras cerrar decisiones)

Mismo patrón que Bancos y Presupuesto, artefactos en `Prestadoras/Database/`:

1. `00_staging_ddl.sql` — `stg_simafi_proveedor` (todas las columnas del origen, texto) y, si D1 incluye compromisos, `stg_simafi_ordenesp`.
2. `tools/MigradorProveedores/extract.cs` — extractor .NET → genera `01_landing.sql` (`TRUNCATE` + `INSERT`, idempotente). Host por argumento, default `172.16.0.3`.
3. `02_transform.sql` — normaliza, deduplica, resuelve el cross-walk contable, hace upsert idempotente por `(cod_proveedor, company_id)`; crea `vw_stg_proveedores_pendientes`.
4. `03_reconciliacion.sql` — cuadres de solo lectura: origen = cargado + pendiente, cero huérfanos.
5. Aplicar en **mirror y SRV** (regla espejo), verificar que quedan idénticos.

**Recomendación adicional, fuera del alcance de la migración:** `prv_proveedores` no tiene llave primaria. Antes de cargar 606 filas conviene crear `UNIQUE (cod_proveedor, company_id)`, que es la unicidad que la aplicación ya asume. Sin ella, una segunda corrida del loader duplicaría todo en silencio.
