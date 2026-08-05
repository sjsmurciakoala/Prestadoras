# Fase 8 — Ejecución del corte de inventario (guion operativo)

Fecha: 2026-07-31 · Rama: `Cambios_almacen2.0` · Diseño: [2026-07-29-carga-inicial-existencias-kardex-design.md](2026-07-29-carga-inicial-existencias-kardex-design.md)

La Fase 8 **no es código**: las Fases 1–7 ya están implementadas. Es la ejecución del corte, y
la hace una persona con la aplicación abierta. Este documento es el guion.

> **Regla que ordena todo lo demás:** el kardex es inmutable (no admite `UPDATE` ni `DELETE`).
> Un asiento mal costeado solo se corrige revirtiéndolo. Por eso el guion es: medir → sanear →
> simular → respaldar → ejecutar → verificar → cerrar, y **mirror antes que SRV**.

---

## 1. Dimensionamiento — YA MEDIDO (mirror, 2026-07-31)

Consultas de §11 corridas sobre `siad_v3_restore` @ localhost (solo lectura). Resultado:

| Métrica | Valor |
|---|---:|
| Empresas con existencia | 1 (`company_id = 2`) |
| Pares (artículo, bodega) con existencia ≠ 0 | **244** |
| — POSTEABLES | **241** |
| — NEGATIVAS | **3** |
| — SIN_COSTO / DESCONTINUADO / BODEGA INACTIVA | **0 / 0 / 0** |
| Valor que sembraría el corte | **L. 2,588,085.19** |
| Artículos distintos que tocaría el rollup | 244 |
| Bodegas involucradas | 1 (`PRIN` — Bodega principal) |
| Aperturas ya posteadas | **0** |
| Pre-chequeos del libro nuevo (§11, los tres) | **0 / 0 / 0** ✅ |
| Descuadres cabecera vs Σ bodegas activas | **0** ✅ |
| Histórico SIMAFI en `alm_kardex` | 47,215 asientos, todos `uuid IS NULL`, del **2014-11-30** al **2025-11-19** |
| `bitacora_maestro_config` | **sin filas** en el mirror → el interceptor no auditaría el rollup |

**Conclusión: el corte es *one-shot*, no un proyecto de captura de costos.** 241 de 244 pares se
postean sin intervención; quedan 3 filas que necesitan mano, todas en la misma bodega.

Las tres negativas:

| Código | Descripción | Bodega | Existencia | `valor_unitario` |
|---|---|---|---:|---:|
| `0147` | TAPON DE COPA 3" PVC POTABLE | PRIN | **-6.00** | **0.0000** |
| `5039` | CINTA EPSSON 2190 SO15335 | PRIN | **-2.00** | **-317.5650** |
| `0167` | UNION PVC POTABLE DE 6" | PRIN | **-2.00** | 360.0000 |

> ⚠️ **Estos números son del MIRROR.** El SRV puede haberse movido desde el respaldo del que
> salió la copia. Antes de ejecutar en producción hay que **volver a correr las mismas consultas
> contra `siad_v3` @ 172.16.0.9** — están en `§7` de este documento, listas para pegar.

---

## 2. Lo que falta decidir (contador)

| # | Decisión | Estado con los números de arriba |
|---|---|---|
| **D2** | ¿`valor_unitario` lleva ISV? | **RESUELTA (2026-07-30)**: no lo lleva; `costo_apertura_incluye_isv = false`. Ya está así en `alm_config_inventario`. |
| **D4** | **Fecha de corte** | **PENDIENTE.** Es el único parámetro obligatorio del lote y fecha *todos* los asientos. Dato duro: el histórico SIMAFI llega al **2025-11-19**, así que la fecha de corte debe ser **posterior o igual** a esa, o el punto cero quedaría antes de movimientos que ya existen. |
| **D5** | Costo de los pares sin costo | **VACÍA**: no hay ningún par con existencia positiva y costo 0. El único artículo sin costo (`0147`) también está en negativo, así que lo resuelve D6. |
| **D6** | **Qué hacer con las 3 negativas** | **PENDIENTE.** Son 10 unidades en total. Hay que decidir el costo con el que entran (ver §3). |

**Sin D4 no se puede ejecutar. Sin D6 el corte se ejecuta igual** (las negativas se omiten con
su motivo), **pero no se puede CERRAR**: el gate exige cero negativas.

---

## 3. Saneo de las 3 negativas (D6)

Una existencia negativa dice que el kardex registró más salidas de las que había. La vía
legítima es un **ajuste de ENTRADA** que la lleve a 0, con motivo — no un `UPDATE`.

Desde el maestro: **Artículos → (el artículo) → pestaña Existencias → botón "Registrar ajuste"**.

| Código | Clase | Cantidad | Costo unitario | Motivo sugerido |
|---|---|---:|---|---|
| `0147` | ENTRADA | 6 | **lo decide el contador** (el artículo tiene `valor_unitario = 0`) | Saneo de existencia negativa previo al corte |
| `5039` | ENTRADA | 2 | **lo decide el contador** (`valor_unitario` está en **-317.5650**, que es un dato malo por sí solo) | Saneo de existencia negativa previo al corte |
| `0167` | ENTRADA | 2 | 360.0000 (el `valor_unitario` del artículo sirve) | Saneo de existencia negativa previo al corte |

Tras el ajuste la fila queda en **0**, sale del universo del corte y deja de bloquear el cierre.
El costo tecleado queda en el asiento del ajuste, que es el registro.

> El motor exige costo **mayor que cero** en toda ENTRADA. Para `0147` y `5039` hay que teclear
> un costo real: no hay forma de postear un ajuste a costo 0.
>
> Aparte del saneo, conviene **corregir `alm_articulo.valor_unitario` de `5039`** (hoy negativo)
> desde el maestro de artículos: un costo negativo no es válido en ningún flujo posterior.

---

## 4. Prerrequisitos duros antes de tocar el SRV

- [ ] **⚠️ Paso 24 aplicado: la mudanza del stock a la bodega `01`**
      (`Database/2026-07-31_alm_articulo_bodega_mover_a_bodega_01.sql`). **Es el prerrequisito
      que más duele saltarse.** El kardex histórico vive en la bodega `01` (`bodega_id = 2`:
      47,213 de 47,215 asientos) y el backfill del 2026-07-07 dejó el stock en `PRIN`
      (`bodega_id = 1`: 634 filas). Son la misma bodega física — el saldo del histórico coincide
      exacto con la existencia en 585 de 587 artículos (99.7%).

      Sin la mudanza, el punto de corte **empareja por par (artículo, bodega) y no encuentra
      nada**: filtrando por bodega el descuadre queda mudo (falso negativo, no aprobado) y **sin
      filtrar el saldo DUPLICA la existencia** — medido: artículo `0001` con saldo 572.00 contra
      existencia 286.00, 8 de los 12 artículos con más histórico descuadrados.

      Tras aplicar el paso 24 y repetir el ensayo, los mismos 12 artículos dan **0 descuadres**
      con y sin filtro de bodega, con el histórico atenuado y la línea `CARGA_INICIAL` arrancando
      el saldo. Verificado en el mirror el 2026-07-31.

      Ojo: el script asume `PRIN = 1` y `01 = 2`. **Esos ids no tienen por qué coincidir en
      producción** (se asignan por secuencia); su `DO` block aborta si no coinciden.
- [ ] **Todo el SQL pendiente aplicado en SRV.** Está registrado en
      [Database/2026-07-30_pendientes_srv.md](../../Database/2026-07-30_pendientes_srv.md):
      grupo A (pasos 1–23 del runbook) **y** grupo B (los 6 scripts de kardex + `cfg_impuestos`,
      que son **prerrequisito duro del paso 21** y **no son re-ejecutables** después del trigger
      de inmutabilidad).
- [ ] **Paso 21b corrido** (los dos `VALIDATE CONSTRAINT`): sin `ck_alm_kardex_libro_nuevo`
      validado, `uuid IS NOT NULL` y `documento_tipo IS NOT NULL` pueden divergir y el invariante
      de §5 deja de ser confiable.
- [ ] **Binario desplegado** con las Fases 1–7 (la pantalla `/almacen/carga-inicial` y el cierre
      de la captura manual salen en el mismo binario, por §13 del diseño).
- [ ] **Permisos sembrados**: `module.inventario.carga_inicial.*` y `module.inventario.ajustes.*`
      asignados al rol que va a operar; **cerrar/reabrir exigen permiso de Configuración**.

---

## 5. Guion de ejecución

Se corre **dos veces**: primero completo en el mirror, y solo si sale limpio, en el SRV.

### 5.1 Respaldo (obligatorio, no negociable)

```bash
pg_dump -t alm_kardex -t alm_articulo_bodega -t alm_articulo -Fc -f pre_corte_2026-07-31.dump "$SRV"
```

Si el lote sale mal, el kardex no se puede editar: el único camino de vuelta es restaurar.

### 5.2 Ventana

- Maestro de artículos **cerrado a edición** durante la ventana (evita el ruido de concurrencia:
  un usuario guardando un artículo mientras corre el lote deja filas omitidas).
- Horario de bajo uso.

### 5.3 Sanear las negativas

§3 de este documento. Al terminar, la simulación debe reportar **0 negativas**.

### 5.4 Simular (dry-run — no escribe nada)

`/almacen/carga-inicial` → **Simular**. Verificar contra los números de §1:

- Pares pendientes, posteables y valor a sembrar coinciden con lo medido.
- Sin costo = 0, Negativas = 0, Descontinuados = 0, Bodega inactiva = 0.

Si algo no cuadra, **parar**: la diferencia es información, no ruido.

### 5.5 Ejecutar

Fecha de corte = **D4**. Tamaño de lote = 200 (el default; 244 pares entran en dos corridas).

> El lote es **reanudable e idempotente**: repetirlo no duplica, lo impide el `uuid`. Si la
> primera corrida deja omitidas, se resuelven y se vuelve a ejecutar.

La pantalla reporta posteadas / omitidas y el motivo de cada omisión.

### 5.6 Verificar (§7 de este documento)

- Invariante del motor: **0 filas**.
- Gate de cierre: **0 filas**.
- Asientos `CARGA_INICIAL` = pares posteados.

### 5.7 Smoke logueado

Abrir el kardex de un artículo con histórico:

- Aparece el movimiento de apertura con **Documento = CARGA INICIAL**.
- Las filas anteriores al corte se ven atenuadas y con saldo **—** (pre-corte).
- El costo promedio de la bodega dejó de ser 0.
- La tarjeta de saldo **no** queda en amarillo (`SaldoDescuadrado = false`).

### 5.8 Cerrar la apertura

`/almacen/carga-inicial` → **Cerrar apertura** (permiso de Configuración). El servidor revalida
el gate. Después del cierre:

- Ningún par **preexistente** puede abrirse por la vía normal; la corrección es revertir y
  reabrir, con permiso de Configuración.
- Los artículos que se den de alta **después** siguen abriendo su existencia inicial con
  normalidad (verificado por prueba automatizada).

---

## 6. Si sale mal

| Síntoma | Qué hacer |
|---|---|
| El lote deja omitidas | Leer el motivo de cada una. Resolver (ajuste, costo) y **volver a ejecutar**: es idempotente. |
| Un par quedó con el costo equivocado y **no tiene movimientos posteriores** | **Reabrir** (reversa + apertura nueva, atómico, con motivo). Permiso de Configuración. |
| Un par quedó con el costo equivocado y **ya tiene movimientos posteriores** | **Ajuste de clase VALOR**: no mueve unidades, reescribe el costo promedio. Reabrir ahí está bloqueado a propósito. |
| El invariante da distinto de 0 | **Parar y no cerrar.** Investigar los pares que salgan en la consulta antes de seguir. |
| Desastre | Restaurar el dump de §5.1. Es el único camino: el kardex no admite `DELETE`. |

---

## 7. Consultas de verificación (pegar tal cual)

Reemplazar `:company` por la empresa (en el mirror es `2`).

```sql
-- (a) Universo del corte, con la MISMA clasificación que el servicio.
WITH u AS (
  SELECT ab.existencia, a.valor_unitario,
         CASE WHEN ab.existencia < 0     THEN 'NEGATIVA'
              WHEN a.valor_unitario <= 0 THEN 'SIN_COSTO'
              WHEN a.activo = false      THEN 'ARTICULO_DESCONTINUADO'
              WHEN ab.activo = false     THEN 'BODEGA_INACTIVA'
              ELSE 'POSTEABLE' END AS clase
  FROM alm_articulo_bodega ab
  JOIN alm_articulo a ON a.company_id = ab.company_id AND a.id = ab.articulo_id
  WHERE ab.company_id = :company AND ab.existencia <> 0
)
SELECT clase, count(*) AS pares,
       round(sum(CASE WHEN clase='POSTEABLE' THEN existencia*valor_unitario ELSE 0 END), 2) AS valor
FROM u GROUP BY clase ORDER BY pares DESC;

-- (b) Pre-chequeos del libro nuevo. Los tres deben dar 0.
SELECT
  (SELECT count(*) FROM alm_kardex WHERE documento_tipo IS NOT NULL AND uuid IS NULL) AS doc_sin_uuid,
  (SELECT count(*) FROM alm_kardex WHERE uuid IS NOT NULL AND documento_tipo IS NULL) AS uuid_sin_doc,
  (SELECT count(*) FROM alm_kardex WHERE uuid IS NOT NULL AND fecha IS NULL)          AS uuid_sin_fecha;

-- (c) INVARIANTE del motor. Cero filas = cuadrado. Correr DESPUÉS del lote.
--     Sin filtrar activo: las bodegas inactivas con existencia también llevan apertura.
SELECT ab.articulo_id, ab.bodega_id, ab.existencia,
       COALESCE(SUM(k.ingresos - k.salidas), 0) AS libro_nuevo
FROM   alm_articulo_bodega ab
LEFT JOIN alm_kardex k
       ON k.company_id  = ab.company_id
      AND k.articulo_id = ab.articulo_id
      AND k.bodega_id   = ab.bodega_id
      AND k.uuid IS NOT NULL
WHERE ab.company_id = :company
GROUP BY ab.articulo_id, ab.bodega_id, ab.existencia
HAVING ab.existencia <> COALESCE(SUM(k.ingresos - k.salidas), 0);

-- (d) GATE de cierre. Debe dar 0.
SELECT count(*) FROM alm_articulo_bodega
WHERE company_id = :company AND existencia <> 0 AND costo_promedio = 0;

-- (e) Qué se posteó.
SELECT documento_tipo, count(*) AS asientos, min(fecha) AS fecha_corte,
       round(sum(ingresos * valor_unitario), 2) AS valor
FROM alm_kardex
WHERE company_id = :company AND uuid IS NOT NULL
GROUP BY documento_tipo ORDER BY documento_tipo;

-- (f) Estado del corte.
SELECT * FROM alm_config_inventario WHERE company_id = :company;
```

---

## 8. Pruebas manuales pendientes (§11 del diseño)

Dos comportamientos que el fixture de tests anula (cada prueba corre dentro de
`BEGIN … ROLLBACK`, así que no hay lotes independientes ni commits reales). Se verifican a mano
**en el mirror**, antes del SRV:

1. **Reanudabilidad.** Ejecutar el lote con `tamañoLote = 200`; matar el proceso tras el segundo
   lote; verificar que hay 400 asientos commiteados; relanzar; verificar que el total es el
   esperado y **no** la suma duplicada, y que el invariante de §7(c) da cero.
2. **Concurrencia del `FOR UPDATE`.** Dos sesiones posteando el mismo par a la vez: la segunda
   espera y después encuentra la apertura vigente (no duplica).

Con 244 pares y un solo depósito, la prueba 1 se puede hacer con `tamañoLote = 100`.

---

## 9. Después de la Fase 8

Recién con el corte **cerrado** se puede abrir la Fase 9 (compras: recepción, ISV, costeo real,
integración contable). El orden no se puede invertir: con `costo_promedio = 0` y una compra
entrando primero, el promedio ponderado se corrompe sin arreglo — 100 unidades a 0 más 10 a
L. 50 dan L. 4.55.

**D1 ya no bloquea.** La pregunta "¿el ISV de compras va al costo o a crédito fiscal?" se resolvió
convirtiéndola en mantenimiento configurable desde la UI (tasa por tipo de artículo en *Almacén →
Mantenimientos → Tipos de artículos*, y tratamiento COSTO/FISCAL por empresa en *Almacén →
Mantenimientos → ISV en compras*). No hace falta reunión con el contador para avanzar; lo único
fuera de alcance es el asiento contable del crédito fiscal.
