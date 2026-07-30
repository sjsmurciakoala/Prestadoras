# M6 — Validación de la migración SIMAFI → SIAD

**Fecha**: 2026-07-29 · **Base**: `siad_v3_copia09` (local) · **Empresa**: `company_id = 2`

## Veredicto

**APROBADA.** El criterio de aceptación se cumple: **25,530 de 25,530 clientes
tienen en el portal exactamente el mismo saldo que en SIMAFI** —
**L 48,858,786.58**, diferencia cero.

Las cuatro desviaciones que aparecen abajo están explicadas al centavo contra la
fuente y ninguna afecta el saldo.

---

## V1 — Volumen

| Concepto | Origen | Portal | |
|---|---|---|---|
| Movimientos del libro | 12,173,095 | 12,173,095 | ✅ |
| Líneas de cargo | 9,331,049 | 9,331,049 | ✅ |
| Movimientos de crédito | 2,837,660 | 2,837,660 | ✅ |
| Clientes | 25,766 | 25,934 | ⚠️ §D1 |

## V2 — Dinero

| Concepto | Origen | Portal | |
|---|---|---|---|
| Cargos | 1,414,578,353.51 | 1,414,578,353.51 | ✅ |
| Créditos | 1,365,722,543.23 | 1,365,722,543.23 | ✅ |
| Aplicado a documentos | — | 1,365,632,530.35 | ⚠️ §D2 |

## V3 — Saldo por cliente (criterio de aceptación)

| Medida | Resultado |
|---|---|
| Clientes evaluados | 25,530 |
| **Cuadran contra el libro** | **25,530** ✅ |
| Cuadran contra los documentos | 25,525 ⚠️ §D3 |
| Saldo origen | 48,858,786.58 |
| Saldo libro en el portal | 48,858,786.58 |

## V4 — Invariante de aplicación

Cada pago debe aplicar exactamente su monto:

| | |
|---|---|
| Pagos con aplicación | 2,837,596 |
| Invariante cumplido | 2,837,273 ✅ |
| Invariante incompleto | 323 ⚠️ §D2 |

## V5 — Integridad referencial

| Comprobación | Huérfanos |
|---|---|
| Detalle sin factura | **0** ✅ |
| Factura sin cliente | **0** ✅ |
| Aplicación sin pago | **0** ✅ |

---

## Desviaciones, todas explicadas

### D1 — 168 clientes de más (25,934 vs 25,766)

Clientes que tienen historia en el libro pero **no aparecen en el volcado de
`maestrosep`** — bajas cuyo maestro SIMAFI ya borró. Se les creó ficha
(`usuariocreacion = 'migracion_simafi_sin_ficha'`, estado inactivo, nombre
recuperado de `facturacion` cuando existe) para no perder su historia.
Los otros 4 son altas hechas en el portal después del volcado.

### D2 — L 90,012.88 de créditos sin aplicar (387 pagos)

| Causa | Pagos | Importe |
|---|---|---|
| Clientes con **saldo a favor** | 382 | 88,717.97 |
| Residuo de los clientes de §D3 | 5 | 1,294.91 |

Comprobación cruzada: el origen tiene **341 clientes con saldo a favor sumando
exactamente −L 88,717.97**. Un crédito mayor que la deuda no tiene documento
donde aplicarse; que quede sin aplicar es el comportamiento correcto.

### D3 — 5 clientes cuyo saldo por documentos difiere

Son **14 cargos que SIMAFI registró sin número de recibo** (conceptos 101-104,
entre 2013 y 2026). Sin recibo no hay documento donde colgarlos, así que existen
en el libro pero no como línea de factura. El saldo del cliente es correcto.

| Cliente | Movs | Importe |
|---|---|---|
| 090304430 | 4 | 1,520.78 |
| 090302100 | 2 | 544.13 |
| 090133197 | 3 | 171.94 |
| 090806315 | 3 | 157.16 |
| 090803258 | 2 | 138.24 |

### D4 — Pendiente de M5 (no afecta saldos)

Las **25,900 notas de crédito** (`205`), los **740 convenios** (`203`) y los
**15,851 descuentos de adulto mayor** están migrados como créditos y aplicados,
pero **no** como documentos `adm_nota_credito` ni como planes en
`cln_plan_pago_*`. Es un tema de representación: el dinero ya está bien.

⚠️ **SIMAFI no guarda el detalle de las cuotas**: `codigoplan` está vacío en los
12.2M de movimientos. Lo único disponible son 558 clientes marcados con
`planpago = 1` en `maestrosep` y sus columnas `fecha1-4` / `cuotas1-4` /
`saldoextra1-4`. Reconstruir los planes exige interpretarlas, con el riesgo de
inventar una estructura que el origen no tiene — decidir antes de ejecutar M5.

---

## Cómo reproducir

`docs/simafi_m2/m6.sql` (las cinco verificaciones) y `m6b.sql` (el desglose de
§D2). Scripts de carga, en orden:

1. `Database/2026-07-28_m3a_carga_clientes_simafi.sql`
2. `docs/simafi_m2/m3b_prep.sql` (cabeceras por cliente+recibo)
3. `Database/2026-07-28_m3b_carga_documentos_simafi.sql`
4. `Database/2026-07-29_m3c_cierre_migracion_documentos.sql`
5. `Database/2026-07-29_m3d_correccion_cargos_y_pagos.sql`
6. `Database/2026-07-29_m4_aplicacion_pagos_fifo.sql`

## Dos advertencias para quien repita esto

**El criterio de cargo facturable es `debitos > 0`, nunca `tipo_partida = '01'`.**
Hay 17,150 filas con `tipo_partida='02'` y débito (L 6,684,942.90) y 64,392 con
`'01'` y débito cero. Un primer intento con el filtro equivocado pasó una
verificación de conceptos que usaba **el mismo filtro en ambos lados** — un
control que comparte el supuesto con lo que controla no controla nada. El control
que sí sirvió fue contra el saldo del libro, que es una fuente independiente.

**En disco mecánico hay que reconstruir, no actualizar.** Los `UPDATE` masivos
corren a 200-400 filas/s (9.4M filas ≈ 8-10 h) y las `CREATE TABLE AS` van 30-60
veces más rápido. Hacerlo por lotes de rangos **no** ayuda: se probó y el primer
lote de 500 mil no cerró en 25 minutos. Mover la base a SSD antes de M7, que
tiene volumen comparable.
