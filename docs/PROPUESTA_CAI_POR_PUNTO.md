# Propuesta de diseño — CAI con correlativo por punto de emisión (no por bloques)

**Fecha:** 2026-07-08
**Estado:** propuesta para discutir con APC/Cristian **después del demo** (no bloquea el viernes).
**Origen:** observación de negocio durante la migración SIMAFI → SIAD.

---

## 1. Contexto

El **CAI** (Código de Autorización de Impresión) es la autorización del **SAR** para emitir facturas
dentro de un **rango de números** y hasta una **fecha límite**. Cada factura debe llevar un número
**único, consecutivo, sin huecos, dentro del rango y vigente**. Emitir duplicado o fuera de rango es
falta fiscal.

SIAD hoy tiene **dos capas** para la numeración:

- `adm_cai_facturacion` — la autorización del SAR (rango, vigencia, `establecimiento_codigo`,
  `punto_emision`, `correlativo_actual`).
- `adm_cai_bloque_reservado` + `adm_cai_correlativo_emitido` — capa de **bloques reservados**, agregada
  para el **modo offline** de la app de lectores V3 (cada dispositivo/ruta reserva un tramo del rango
  para no colisionar sin conexión, y sincroniza al reconectar).

La observación de negocio: **el esquema de bloques es más complejo de lo necesario** para la operación
real. La propuesta es que **cada punto (ruta/ciclo/caja) lleve su propio correlativo independiente**.

---

## 2. Cómo lo hace SIMAFI hoy (evidencia)

Tabla `cai` de SIMAFI (`172.16.0.3`). Para **una** autorización CAI del SAR (código + rango 50.000,
renovado cada año 2019→2024), hay **varias filas con distinto `CodigoBase`** (punto de emisión), y
**cada una con su propio `ContadorActual`**:

| CAI (código SAR) | Año | Rango | CodigoBase (punto) | ContadorActual |
|---|---|---|---|---|
| F3F8…67E5 | 2019 | 1–50000 | 000-**003**-01 | 41004 |
| F3F8…67E5 | 2019 | 1–50000 | 000-**004**-01 | 42532 |
| F3F8…67E5 | 2019 | 1–50000 | 000-**005**-01 | 39170 |
| F3F8…67E5 | 2019 | 1–50000 | 000-**006**-01 | 42813 |
| F3F8…67E5 | 2019 | 1–50000 | 000-**007**-01 | 42326 |

**Conclusión:** SIMAFI **ya opera con correlativo independiente por punto de emisión** (003–007), no con
un bloque repartido de un contador compartido. Es decir, la propuesta **coincide con la operación real
de APC** — no es un cambio arbitrario.

**Matiz de granularidad:** SIMAFI separa por **~5 puntos de emisión**, no literalmente uno por cada una
de las 20 rutas. Hay que confirmar con APC a qué nivel quieren el contador: **por ruta, por ciclo, o por
caja/punto de emisión**.

---

## 3. Análisis / trade-off

| | CAI/correlativo por punto (propuesta) | Bloques reservados (actual V3) |
|---|---|---|
| Simplicidad | Alta — "cada punto usa su próximo número" | Baja — reservar→consumir→confirmar→sincronizar→conflictos→expiración |
| Fidelidad a SIMAFI | Alta (es su modelo) | Media (capa extra) |
| Colisiones entre puntos | Imposibles (contadores separados) | Se previenen por bloques |
| Colisiones **dentro** de un punto | Solo un problema si **varios dispositivos** facturan el **mismo** punto a la vez | Resueltas por bloques |
| Admin con el SAR | Manejar N puntos/rangos/vigencias | Un rango grande compartido |

**El único caso donde los bloques ganan algo** es si **un mismo punto de emisión es facturado por varios
dispositivos offline simultáneamente**. Si cada ruta/punto = **un solo lector/dispositivo**, los bloques
sobran y el modelo por-punto es más simple y fiel.

**Pregunta clave para decidir:** ¿un mismo punto/ruta puede ser facturado por más de un dispositivo a la
vez? Si **no** → adoptar contador por punto. Si **sí** → mantener bloques (o un híbrido).

---

## 4. Lo que SIAD ya soporta

`adm_cai_facturacion` **ya tiene** `establecimiento_codigo`, `punto_emision` y `correlativo_actual`
propios por CAI. O sea, el modelo "un contador por punto" **ya cabe en el esquema actual** — no requiere
rediseño de tablas, sino **decidir usar ese modo** y dejar la capa de bloques únicamente para el caso
multi-dispositivo (o retirarla si no aplica).

---

## 5. Recomendación

1. **Confirmar con APC la granularidad** deseada del correlativo: ruta / ciclo / punto de emisión.
2. **Confirmar el modelo de concurrencia** de la app: ¿un dispositivo por ruta/punto, o varios?
3. Si es un dispositivo por punto (lo más probable): **adoptar correlativo por punto** usando
   `punto_emision`/`correlativo_actual` de `adm_cai_facturacion`; los bloques quedan como opcional para
   escenarios multi-dispositivo.
4. **Alinear con SIMAFI:** replicar sus puntos de emisión (003–007) o el esquema que APC use en 2026.

---

## 6. Alcance y relación con el demo

- Es una **decisión de arquitectura del producto** (app de lectores + facturación), **separable del demo
  del viernes**. Para la migración/demo se factura server-side, en orden, con un CAI → no requiere
  resolver esto ahora.
- **Relevante para el flujo completo:** cuando se pruebe el flujo end-to-end **vía la app** (lector toma
  lectura offline → sube → factura → bancos), ese camino **sí** ejercita la capa de bloques. Por eso
  conviene cerrar esta decisión antes de operar la app a escala.

> **Pendiente:** llevar esta propuesta + la evidencia de SIMAFI a la reunión con APC para decidir
> granularidad y modelo de concurrencia.
