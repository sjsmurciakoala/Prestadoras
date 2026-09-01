# Evaluación de proveedores — plan de implementación

**Fecha:** 2026-08-14
**Rama:** `feat/almacen-integracion-contable`
**Estado:** diseñado · F0 pendiente de aprobación
**Prototipo:** https://claude.ai/code/artifact/3cf86db2-c312-4ebd-9d9a-da3c2a560e39
**Prerrequisito ya resuelto:** fecha de entrega pactada en la O/C (§3.17 de pendientes SRV)

---

## 1. Qué se pide

Una **vista** y un **reporte** que califiquen a cada proveedor por período, combinando lo que ya
registra el sistema (órdenes, recepciones, facturas) con lo que califica el encargado de compras.
El resultado es un puntaje 0–100 y una clase (A confiable … D no aceptable) por proveedor y período.

---

## 2. Punto de partida (verificado en el código)

### 2.1 No existe nada de evaluación

No hay tabla, función ni pantalla de evaluación/calificación de proveedores. Lo más cercano es el
**estado de cuenta** (`fn_prv_estado_cuenta_*`, 2026-08-13), que es el molde de estilo a seguir:
funciones de lectura en Postgres + servicio delgado + pantalla + PDF.

### 2.2 Lo que sí se puede medir hoy

| Criterio | De dónde sale | ¿Listo? |
|---|---|:--:|
| Cumplimiento de entrega | `alm_orden_compra(_detalle).fecha_entrega_pactada` vs `alm_compra_hdr.fecha` | **sí, desde 2026-08-14** |
| Completitud del pedido | `alm_orden_compra_detalle.cantidad_aplicada` ÷ `cantidad_pedida` | sí |
| Exactitud de precio | `alm_compra.precio_unitario` vs `alm_orden_compra_detalle.costo_unitario` (vía `orden_compra_detalle_id`) | sí |
| Documentación fiscal | `alm_compra_hdr.cai`, `numero_factura_sar` + `prv_proveedores.rtn` | sí |
| Volumen de compra | Σ `alm_compra_hdr.total` con `estado = 1` | sí |
| Calidad de lo recibido | **nada**: la devolución al proveedor no está tipificada | **no — F4** |

### 2.3 Cuidados que cambian el SQL

- **`prv_proveedores` es keyless y multiempresa**: todo se referencia por
  `(company_id, cod_proveedor)`, sin FK. Igual que compras y compromisos.
- **`company_id` no es homogéneo**: `prv_proveedores.company_id` es `int4`; `alm_compra_hdr` y
  `alm_orden_compra` son `BIGINT`. Cast explícito en las funciones.
- **El histórico SIMAFI queda fuera del universo**: vive en `alm_compra` con `compra_hdr_id` NULL,
  sin cabecera, sin CAI y sin O/C. No es evaluable y no debe castigar a nadie.
- **Recepciones anuladas** (`alm_compra_hdr.estado = 9`) se excluyen de todos los denominadores.
- **Sólo las órdenes emitidas desde el 2026-08-14** tienen fecha pactada. El criterio de entrega
  arranca con denominador chico: la ficha debe decir cuántas órdenes fueron evaluables, y el peso
  del criterio se redistribuye cuando no hay ninguna (§4.3).

---

## 3. Modelo de datos (F0)

Seis tablas, todas `company_id` + tenant-safe:

| # | Tabla | Rol |
|---|---|---|
| 1 | `prv_evaluacion_periodo` | Período evaluado: código (`2026-T2`), rango de fechas, estado abierto/cerrado |
| 2 | `prv_evaluacion_criterio` | Catálogo por empresa: código, nombre, **peso**, origen (automático/manual), meta, parámetro |
| 3 | `prv_evaluacion_clase` | Escala: A/B/C/D con rango de puntaje |
| 4 | `prv_evaluacion_hdr` | Una fila por proveedor + período: puntaje, clase, compras, estado |
| 5 | `prv_evaluacion_dtl` | Una fila por criterio evaluado: **snapshot** de peso y nombre, numerador, denominador, % logro, puntos |
| 6 | `prv_recepcion_incidencia` | Incidencias por recepción (devolución, daño, especificación, faltante). Alimenta el criterio de calidad |

Dos decisiones de forma que evitan problemas después:

- **El detalle guarda el peso y el nombre del criterio como snapshot.** Así el catálogo se puede
  reordenar o repesar sin reescribir la historia, y una evaluación cerrada sigue explicando su
  propio puntaje.
- **La periodicidad es un dato, no una estructura.** El período es un rango de fechas con nombre:
  trimestral, mensual o anual sin tocar el esquema.

---

## 4. Cómo se calcula

### 4.1 Universo del período

Recepciones de `alm_compra_hdr` con `fecha` dentro del rango, `estado = 1`, agrupadas por
`cod_proveedor`. De ahí salen compras del período, número de facturas y número de órdenes.

### 4.2 Fórmula por criterio

Todos devuelven **numerador / denominador** — así la ficha puede mostrar la evidencia
("12 de 16 órdenes a tiempo") y no sólo el porcentaje.

| Código | Criterio | Numerador | Denominador |
|---|---|---|---|
| `ENTREGA` | Cumplimiento de entrega | Líneas recibidas con `fecha recepción ≤ fecha pactada` | Líneas recibidas contra O/C **con** fecha pactada |
| `COMPLETO` | Completitud del pedido | Σ `LEAST(cantidad_aplicada, cantidad_pedida)` | Σ `cantidad_pedida` de las O/C recibidas en el período |
| `PRECIO` | Exactitud de precio | Líneas con desvío ≤ tolerancia | Líneas recibidas contra O/C |
| `CALIDAD` | Calidad de lo recibido | Recepciones **sin** incidencia | Recepciones del período |
| `DOCUMENTO` | Documentación fiscal | Recepciones con CAI y número SAR (y RTN del proveedor) | Recepciones del período |
| `SERVICIO`… | Criterios manuales | — | lo captura el comprador (0–100) |

Precisiones que evitan discusiones después:

- **La fecha pactada se resuelve por renglón**: la del renglón si tiene, si no la de la cabecera.
  Así una entrega escalonada no penaliza al proveedor por la fecha de la última línea.
- **Completitud se acota con `LEAST`**: recibir de más en un renglón no compensa un faltante en otro.
- **Tolerancia de precio configurable** en `prv_evaluacion_criterio.parametro` (defecto 2%).
- Las **compras prepagadas y de contado** entran igual: el criterio mide la entrega, no el pago.
- **`CALIDAD` se autodesactiva mientras nadie registre incidencias.** Medido en el mirror: sin
  incidencias, "recepciones sin incidencia ÷ recepciones" daba **8/8 = 100%** y le regalaba los 20
  puntos a todos. La función distingue "no hubo incidencias" de "nadie las captura": hasta la
  primera fila en `prv_recepcion_incidencia`, el criterio devuelve denominador 0 y su peso se
  redistribuye. Se activa solo con la primera incidencia registrada.

### 4.3 Puntaje y redistribución

`puntos = peso × logro`, `puntaje = Σ puntos` sobre 100. Cuando un criterio no tiene denominador en
el período (por ejemplo, ningún renglón contra O/C con fecha pactada), **no cuenta como cero**: se
excluye y su peso se reparte proporcionalmente entre los criterios que sí tienen datos. El detalle
guarda el criterio con `logro = NULL` para que la ficha lo muestre como "sin datos en el período".

Sin esta regla, todos los proveedores arrancarían reprobados por el hueco histórico de §2.3.

### 4.4 Dónde vive el cálculo

Una función `fn_prv_evaluacion_metricas(company_id, desde, hasta, tolerancia)` devuelve, por
proveedor, los numeradores y denominadores automáticos en una sola pasada. El servicio los cruza
con el catálogo de criterios, aplica pesos y clase, y persiste `hdr` + `dtl`. Recalcular un período
abierto rehace las filas automáticas y **respeta lo capturado a mano**; un período cerrado no se
recalcula.

---

## 5. Permisos

Siguiendo el patrón de retenciones y estado de cuenta:

- `module.proveedores.evaluacion.view` — consultar panel, ficha y reporte.
- `module.proveedores.evaluacion.edit` — calcular el período, capturar criterios manuales, cerrar.
- El catálogo de criterios/clases va bajo configuración, como el de retenciones.

---

## 6. Pantallas y reporte

| Ruta | Qué es |
|---|---|
| `/proveedores/evaluacion` | Panel del período: filtros, KPIs y ranking con una columna por criterio |
| `/proveedores/evaluacion/{periodoId}/{codigo}` | Ficha: desglose con evidencia, historial, captura manual, incidencias |
| `/mantenimientos/evaluacion-proveedores` | Catálogo de criterios, pesos y escala de clases |

Reportes DevExpress por código, como `Rpt_Dev_Constancia_Retencion`:
**ficha de evaluación** (una hoja por proveedor, con firmas) y **cuadro comparativo** del período.

---

## 7. Fases

| Fase | Qué entrega | Depende de |
|---|---|---|
| **F0** | SQL: 6 tablas + semilla (6 criterios, 4 clases) + `fn_prv_evaluacion_metricas` | — |
| **F1** | Backend: DTOs, `EvaluacionProveedorService` (calcular/leer/capturar/cerrar), controlador, permisos | F0 |
| **F2** | Panel del período + ficha del proveedor | F1 |
| **F3** | Catálogo de criterios, pesos y clases | F1 |
| **F4** | Incidencias de recepción (registro desde la recepción y desde la ficha) → habilita `CALIDAD` | F0 |
| **F5** | Reportes: ficha PDF y cuadro comparativo (PDF/Excel) | F2 |

Orden sugerido: **F0 → F1 → F2 → F4 → F3 → F5**. F4 antes que F3 porque sin incidencias el criterio
de calidad queda sin datos y arrastra la nota de todos por igual.

---

## 8. Decisiones

### Cerradas

| # | Decisión | Resuelta |
|---|---|---|
| D1 | Fecha de entrega pactada: cabecera + renglón, obligatoria desde el borrador | 2026-08-14 |
| D2 | Periodicidad: es un dato (rango con nombre), no estructura — no bloquea | 2026-08-14 |
| D3 | Pesos y criterios: configurables por empresa, con snapshot en el detalle | 2026-08-14 |

### Abiertas (no bloquean F0/F1, sí el arranque en producción)

| # | Pregunta | Impacto |
|---|---|---|
| D4 | ¿Los 6 criterios y sus pesos propuestos se confirman, o cambian? | Semilla de F0 |
| D5 | ¿Las incidencias de calidad se registran en el sistema (F4) o se califican a mano? | Alcance de F4 |
| D6 | ¿La clase D sólo informa, o bloquea la emisión de órdenes a ese proveedor? | Fase posterior |
| D7 | ¿Quién evalúa y quién aprueba? ¿Un permiso o dos (compras evalúa, gerencia cierra)? | F1 |
| D8 | Órdenes anteriores al 2026-08-14 sin fecha pactada: ¿se dejan fuera (propuesto) o se capturan a mano? | Denominador de `ENTREGA` |

---

## 9. Riesgos

- **Arranque con pocos datos.** Hasta que roten uno o dos períodos completos con órdenes nuevas, el
  criterio de entrega tendrá denominadores de un dígito. La ficha debe mostrar siempre el
  denominador, no sólo el porcentaje, para que nadie tome decisiones sobre 2 órdenes.
- **Calidad sin F4 no puntúa** (regla de §4.2): no infla la nota, pero tampoco mide. Hasta que F4
  esté en uso, el puntaje real se reparte entre 4 criterios automáticos y 1 manual.
- **Proveedores de servicios.** Los que no pasan por O/C ni almacén (fletes, servicios) sólo tendrán
  documentación fiscal y criterios manuales: quedarán evaluados sobre 2 criterios. Si eso no se
  quiere, hay que excluirlos por tipo de proveedor en el filtro del período.
