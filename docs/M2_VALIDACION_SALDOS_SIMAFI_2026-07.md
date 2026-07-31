# M2 — Validación del saldo por cliente (SIMAFI → SIAD)

**Fecha**: 2026-07-28 · **Base**: `siad_v3_copia09`, esquema `simafi_stg` (M1)

**Veredicto: el mapeo es correcto. `clientesaldos` no sirve como oráculo — está
congelado en 2019-2020.** La validación se hizo por una vía mejor: contra las
líneas de `facturacion`, donde **249,436 de 250,813 recibos (99.45%) cuadran al
centavo**.

---

## 1. El modelo real de `transaccion_abonado`

Es un libro mayor por cliente, no un maestro de recibos. Dos columnas de código,
que **no** hay que confundir:

| Columna | Qué es | Valores |
|---|---|---|
| `tipo_partida` | naturaleza del movimiento | `01` = cargo (9.38M filas) · `02` = abono (2.79M) |
| `transaccion` | **el concepto** | ver abajo |

| `transaccion` | Concepto | Filas | Importe |
|---|---|---|---|
| `101` | Agua Potable | 3,858,743 | 1,079,260,192.42 D |
| `102` | Alcantarillado Sanitario | 1,211,811 | 240,522,539.75 D |
| `103` | Fondo Fuentes / Ambiental | 2,388,461 | 39,083,618.42 D |
| `104` | Tasa ERSAPS | 1,822,465 | 15,033,315.23 D |
| `105` | Notas de débito y cargos varios (reconexión, corte, N/D) | 35,353 | 29,628,422.64 D |
| `111` / `11` | Reconexión y similares | 18,650 | 11,039,366.83 D |
| `201` | **Pagos** | 2,474,875 | 1,280,257,448.90 C |
| `202` / `203` / `205` | Notas de crédito y ajustes | 362,696 | 85,392,639.09 C |

> ⚠️ **Trampa de nomenclatura.** La regla acordada dice "descartar el código
> `01`". Ese `01` es `facturacion.codigo` (Saldo Anterior). En
> `transaccion_abonado`, `tipo_partida='01'` son los **9.38M de cargos
> legítimos**. Aplicar la regla a la columna equivocada borraría toda la cartera.

**En el ledger no hay arrastre que descartar**: el saldo anterior no se
re-registra como movimiento. El saldo emerge solo de `sum(debitos) - sum(creditos)`.

## 2. Reconstrucción de un cliente

`090134380` — 40 movimientos, 2016-12 a 2018-06, cliente cortado que nunca pagó.
Reconstruido movimiento a movimiento: **3,679.29**, idéntico a SIMAFI.

`090206979` — 466 días con movimiento, veinte años de facturas y pagos, activo
hasta 2026-07-14: **9,603.43**, idéntico a `maestrosep.totalmora`.

Criterio, sin exclusión alguna:

```sql
select trim(cliente), sum(debitos) - sum(creditos)
from simafi_stg.transaccion_abonado group by 1;
```

## 3. Por qué `clientesaldos` no puede ser el oráculo

Contra los 22,393 registros de `clientesaldos`, solo **6,616 cuadran**. La causa
no es el mapeo: **la tabla es un snapshot viejo**.

- Sus valores se repiten en progresión aritmética — 141.49, 282.98, 424.47,
  565.96, 707.45 — múltiplos de tarifas fijas mensuales.
- Para **15,429 clientes existe una fecha pasada** en la que el saldo
  reconstruido valía exactamente lo que dice `clientesaldos`. Esas fechas se
  concentran en **2019 (7,464 clientes) y 2020 (4,725)**.
- Ejemplo: `090704922` figura con 141.49; su ledger neteaba 141.49 entre
  2018-08 y 2020-12, y hoy está al día en 0.00 (última factura pagada 2026-07-24).

Solo 347 clientes **nunca** coincidieron en ninguna fecha. Es decir: el ledger
reproduce el 98.4% de `clientesaldos` — en el momento en que esa foto fue tomada.

## 4. `maestrosep.totalmora` es mejor, pero también está desfasado — y roto

| Resultado | Clientes |
|---|---|
| Cuadra **hoy** | 9,264 |
| Cuadró en una fecha pasada | 14,077 |
| Nunca cuadró | 2,425 |

90.6% reproducido. Las fechas de coincidencia se dispersan por mes reciente
(2026-06: 3,488 · 2025-10: 2,874 · 2026-07: 2,207), lo que delata un **campo
denormalizado que SIMAFI refresca por cliente**, no un corte global.

De los 2,425 que nunca cuadraron, **1,864 tienen `totalmora` negativo**
(−3,096,599.79 en total), valor imposible para un saldo. Verificado:
`090601158` factura ~21,000/mes, paga completo cada mes, ledger en 0.00 — y
SIMAFI le asigna `totalmora = −155,593.74`. **El campo está corrupto, no el
ledger.**

## 5. La validación que sí cierra: línea por línea contra `facturacion`

`facturacion` cubre 2025-07-23 → 2026-07-27 (250,813 recibos). Comparando, por
recibo, los cargos de `facturacion` contra los débitos del ledger:

| Regla | Recibos exactos | % |
|---|---|---|
| Excluyendo `01`, `15`, `16`, `17` | 248,720 | 99.17% |
| **Excluyendo además `12`** | **249,436** | **99.45%** |

**L 104,744,104.55 idénticos al centavo** entre ambas fuentes.

### 5.1 La regla del `01`, confirmada con números

| Concepto | Importe |
|---|---|
| Cargos reales | 106,201,670.03 |
| Arrastre `01` Saldo Anterior | 431,939,083.46 |
| **Si se migrara el `01` como cargo** | **538,071,330.15** |

Migrarlo multiplicaría la cartera **por 5**. La regla es correcta.

### 5.2 Hallazgo nuevo: el código `12` también debe excluirse

`12 Convenio de Pago` (717 líneas, 834,940.29) **no es un cargo nuevo**: es deuda
existente re-presentada en cuotas. SIMAFI lo compensa en el mismo recibo con un
`01` **negativo**. Ejemplo, recibo `4101544`:

| Código | Descripción | Valor |
|---|---|---|
| `02` | Agua Potable | 632.99 |
| `03` | Alcantarillado | 379.79 |
| `04` | Fondo Fuentes | 18.75 |
| `05` | Tasa ERSAPS | 20.26 |
| `12` | **Convenio de Pago** | **+4,379.77** |
| `01` | **Saldo Anterior** | **−4,379.77** |

El ledger, correctamente, solo registra 1,051.79. Excluir el `12` movió 716
recibos más a coincidencia exacta.

> Esto **no** significa perder los convenios: significa que el plan de pago se
> migra como `cln_plan_pago_*` (M5), no como cargo de factura. La deuda ya está
> en las facturas originales.

### 5.3 Lo que queda difiriendo (1,313 recibos, 0.5%)

- **3 recibos de `06 Gestión Legal`** (70,209.77): el ledger sí los tiene, pero
  como N/D independiente (`transaccion=105`, "N/D Gestion Legal", 353 filas /
  1,197,106.48), atribuido a otro recibo. Diferencia de imputación, no de monto.
- **~1,310 recibos con el ledger por encima** (+300,602.41 neto): recargos y
  notas de débito registrados en el movimiento pero no desglosados como línea de
  `facturacion`.

Ninguno de los dos casos invalida el criterio; sí deben revisarse en M3.

## 6. Cartera resultante

| Fuente | Cartera |
|---|---|
| **Ledger reconstruido (25,766 clientes)** | **48,649,742.92** |
| `maestrosep.totalmora` (desfasado + 1,864 negativos) | 35,706,171.92 |
| `clientesaldos` (foto 2019-2020, 22,393 clientes) | 33,907,881.94 |

335 clientes quedan con saldo a favor (−88,347.07).

## 7. Advertencias para M3/M4

1. **`facturas.total` NO es el importe de la factura.** 3.23M recibos suman
   19,876,863.68 — unos 6 lempiras por recibo. El importe debe salir del ledger
   (`transaccion` 101-105 por recibo) o de `facturacion`. No usar `facturas.total`
   como monto en M3.
2. **`facturacion.codigo='16'` no es fuente completa de pagos.** Para los mismos
   recibos: `facturacion` reporta 16,551,197.56 y el ledger 69,043,965.16. Los
   pagos deben salir de `transaccion_abonado` (`transaccion='201'`) más
   `pagos_bancos`.
3. **`facturacion` solo cubre desde 2025-07-23.** El resto está en la archivada
   `facturacion23072025` (7.78M filas). Para el histórico completo, el ledger es
   la única fuente continua desde 2005.
4. Hay **226 movimientos con fecha futura** (hasta 2027-11-15, 300,887.19) y
   **1 con fecha nula**. Decidir tratamiento antes de M3.
5. **3,372 clientes con movimientos no tienen fila en `clientesaldos`**
   (4,887,256.33) y 234 la tienen sin movimientos.
6. `estado` del movimiento: 12.15M en `C`, 19,430 en `A`. No se usó como filtro
   —— la coincidencia del 99.45% se logró sin excluir nada por `estado`.

## 8. Criterio de aceptación de M6 — DECIDIDO (2026-07-28)

El plan decía que la migración *"solo se da por buena con diferencia 0"* contra el
saldo de SIMAFI por cliente. **Ese criterio es inalcanzable tal como estaba escrito**,
porque las dos tablas de saldo de SIMAFI están desfasadas y una de ellas tiene
1,864 valores negativos imposibles.

Criterio aprobado en su lugar:

1. **Diferencia 0 contra el ledger reconstruido** — el SIAD debe reproducir
   `sum(debitos) - sum(creditos)` por cliente, exacto, para los 25,766. Esto sí
   es verificable y es lo que el portal mostrará.
2. **Diferencia 0 línea a línea contra `facturacion`** en la ventana
   2025-07-23 → hoy, excluyendo `01`, `12`, `15`, `16`, `17`, salvo los 1,377
   recibos ya inventariados en §5.3.
3. `maestrosep.totalmora` y `clientesaldos` quedan como **referencia
   informativa**, no como condición de aceptación.

## Reproducibilidad

Scripts en el scratchpad de la sesión: `m2_fecha.sql` (acumulado por
cliente/día y fechas de coincidencia), `m2_analisis.sql` (distribuciones),
`m2_lineas.sql` (cruce por recibo), `m2_regla.sql` (regla refinada).

Tablas de trabajo dejadas en `simafi_stg`: `_m2_dia` (6.29M), `_m2_acum` (6.29M),
`_m2_oraculo`, `_m2_match` (25,766), `_m2_recon`, `_m2_lin` (250,813), `_m2_fact`.
Son desechables: se reconstruyen con los scripts.
