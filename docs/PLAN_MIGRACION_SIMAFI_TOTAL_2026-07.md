# Migración total SIMAFI → SIAD, con códigos originales (2026-07-30)

**Estado**: M1 (staging) y M2 (validación del saldo) cerrados. NO se ha migrado nada.

> **M2 validó el mapeo**: 249,436 de 250,813 recibos (99.45%) cuadran al centavo
> contra las líneas de `facturacion`. Ver
> [M2_VALIDACION_SALDOS_SIMAFI_2026-07.md](M2_VALIDACION_SALDOS_SIMAFI_2026-07.md),
> que **corrige §3, §4 y §5 de este plan** — sobre todo: `clientesaldos` es un
> snapshot de 2019-2020 y no puede ser el criterio de aceptación de M6.

## 1. Criterio del usuario

Migrar la información **como SIMAFI la tiene**: números de recibo, fechas y
códigos originales. **Sin documentos sintéticos ni marcas de "migración"** —
la historia debe verse en el portal como si siempre hubiera vivido ahí. Esto
**anula el H3 del plan de corte F7** (que creaba facturas `SI-<clave>`).

Alcance decidido: **todo el histórico**; ejecutado en dos tiempos —
(a) lo **activo** de SIMAFI, que es lo que habilita el cutover, y
(b) las **tablas archivadas**, inventariadas abajo, como fase de consulta
posterior (no bloquea el cutover).

## 2. Modelo real de SIMAFI (verificado, corrige la doc previa)

| Tabla | Filas | Qué es realmente |
|---|---|---|
| `facturas` | 3.23M | **Maestro del recibo**: recibo, clave, emisión, vence, pago, banco, aplicado |
| `facturacion` | 1.22M (2025-07-23 → hoy) | **Líneas por concepto**: recibo + `codigo` + `valor` |
| `transaccion_abonado` | 12.17M | Movimientos (sin columna `saldo`; usa debitos/creditos + columnas por servicio) |
| `historicomedicion` | 1.02M | Lecturas |
| `pagos_bancos` | 482K | Pagos del canal bancario |

> La documentación previa decía "facturacion = maestro". **Es al revés.**

### Tablas archivadas (SIMAFI corta y renombra con la fecha)
`facturacion23072025` (7.78M), `transaccion_abonado2018` (5.0M),
`numerodei01032018` (571K), `numerodei03062019` (161K),
`numerodei26102023` (8.7K), `detalle copia junio 2016` (58K), `basepri*`,
`baseing*`. Fase (b): verificar esquema de cada una antes de incluirla.

## 3. Catálogo de conceptos y su destino en el SIAD

| Código | Descripción SIMAFI | Líneas | Destino |
|---|---|---|---|
| `02` | Agua Potable | 250,745 | Línea `AGUA_POTABLE` |
| `03` | Alcantarillado Sanitario | 80,995 | Línea `ALCANTARILLADO` |
| `04` | Fondo Fuentes de Agua | 235,272 | Línea `TASA_AMBIENTAL` |
| `05` | Tasa ERSAPS | 235,253 | Línea `TASA_SVA_ERSAPS` |
| `06` | Gestión Legal | 3 | Línea de servicio (mapear en catálogo) |
| `112-03` | Reconexión | 24 | Línea de servicio |
| `12` | Convenio de Pago | 717 | `cln_plan_pago_*` (cuotas F6) — **NO como cargo**, ver §4 |
| `16` | **Pagos** (−16.5M) | 31,216 | **`adm_pago` + aplicaciones**, NO línea |
| `15` | Créditos (−272K) | 139 | Nota de crédito |
| `17` | Descuentos o Rebajas (−2.1M) | 250,812 | Nota de crédito / ajuste de línea |
| `01` | **Saldo Anterior (L 431.9M)** | 138,747 | **NO se migra como cargo** — ver §4 |

## 4. ⚠️ La trampa del código `01`

`01 Saldo Anterior` acumula **L 431,939,083** en 138,747 líneas. NO es deuda
real: es el **arrastre** que SIMAFI reescribe en cada recibo (el saldo del
recibo anterior, re-facturado como concepto). Migrarlo como línea de
`factura_detalle` **multiplicaría la cartera por un orden de magnitud** —
cada mes volvería a cargar lo que ya está en las facturas anteriores.

Regla: los cargos reales son `02/03/04/05/06/112-03`; el `01` se descarta y
el saldo del cliente **emerge solo** de las facturas impagas + pagos
aplicados.

**Confirmado en M2 con números**: cargos reales 106,201,670.03 vs arrastre `01`
431,939,083.46. Migrar el `01` llevaría la cartera a 538,071,330.15 — **5×**.

**Corrección de M2 — el `12` también se descarta como cargo.** `12 Convenio de
Pago` (717 líneas, 834,940.29) es deuda existente re-presentada en cuotas:
SIMAFI la compensa en el mismo recibo con un `01` **negativo** de igual importe.
El ledger no la registra como cargo. Se migra como plan de pago (M5), no como
línea de factura. Excluir `01` **y** `12` sube la coincidencia de 99.17% a 99.45%.

⚠️ **No confundir columnas**: ese `01` es `facturacion.codigo`. En
`transaccion_abonado`, `tipo_partida='01'` son los 9.38M de **cargos legítimos**.

**El oráculo cambió**: `clientesaldos` está congelado en 2019-2020 y
`maestrosep.totalmora` tiene 1,864 valores negativos imposibles. La validación
real es línea a línea contra `facturacion` — ver §5 de M2.

## 5. Orden de ejecución propuesto

1. ~~**M1 — Espejo de lectura**~~ ✅ **CERRADO**: `simafi_stg` con las 7 tablas
   (18.1M filas) cuadradas contra el origen.
2. ~~**M2 — Validación del saldo**~~ ✅ **CERRADO**: mapeo validado al 99.45%.
   Ver [M2_VALIDACION_SALDOS_SIMAFI_2026-07.md](M2_VALIDACION_SALDOS_SIMAFI_2026-07.md).
   (La reconciliación de clientes/catálogos por `clave` contra los 1,147 del
   piloto queda pendiente dentro de M3.)
3. **M3 — Documentos**. **La fuente primaria es el ledger, no `facturas`.**
   - ~~**M3a — Clientes**~~ ✅ **HECHO (2026-07-28, local)**. Script idempotente
     [`Database/2026-07-28_m3a_carga_clientes_simafi.sql`](../Database/2026-07-28_m3a_carga_clientes_simafi.sql).
     24,623 insertados, 25,770 en total, **0 faltantes**; de los cargados,
     **0 difieren** de SIMAFI en estado/medidor/nombre (las diferencias que se
     ven son ediciones del portal sobre los 1,147 del piloto, respetadas).
     Quedan en NULL con razón verificada: `maestro_cliente_rtn` (`rtm` no es un
     RTN: 'CONSTANCIA', 'SIN INFO.', '0'), `barrio_codigo` (`sector` **no** es el
     barrio — el catálogo usa 3 dígitos con nombre propio, SIMAFI trae 2 y vacío
     en 10,173) y `tipo_uso_codigo` (vacío en origen y en el piloto).
     568 sin ciclo (ciclo '0') y 3 sin categoría.
   - ~~**M3b — Documentos y libro**~~ ✅ **HECHO (2026-07-29, local)**.
     [`Database/2026-07-28_m3b_carga_documentos_simafi.sql`](../Database/2026-07-28_m3b_carga_documentos_simafi.sql)
     + [`Database/2026-07-29_m3c_cierre_migracion_documentos.sql`](../Database/2026-07-29_m3c_cierre_migracion_documentos.sql).
     **3,896,835 facturas · 9,378,291 líneas · 12,173,095 movimientos**, todas
     reconciliadas exactas contra el origen (los 4 que faltan son movimientos con
     clave de cliente vacía).
   - ~~**M3c — Volumen**~~ ✅ 4 h 40 min la carga completa (el detalle solo:
     3 h 31). Ver la nota de estrategia en la cabecera del script de M3b:
     un primer intento con `NOT EXISTS` por fila proyectaba **días**.

   **✅ CRITERIO DE M6 CUMPLIDO SOBRE LA CARTERA COMPLETA (2026-07-29, local):
   25,530 de 25,530 clientes con saldo idéntico al de SIMAFI, cero diferencias.
   L 48,858,786.58 = L 48,858,786.58.**

   Por qué el ledger es la fuente primaria:
   - Trae el recibo en **9,378,291 de 9,378,312 cargos** (solo 21 sin número).
   - Trae el **desglose por concepto en todo el histórico** (101/102 desde 2005,
     103/104 desde los 2010s) — `factura_detalle` es reconstruible completo.
   - ⚠️ **`facturas.total` NO es el importe** (3.23M recibos suman 19.9M, ~6 L por
     recibo).
   - ⚠️ `facturacion` solo cubre desde 2025-07-23 (250,813 de 3,228,765
     recibos = **7.8%**).

   ⚠️ **El maestro `facturas` también está cortado por fecha.** El ledger tiene
   3,827,204 recibos distintos y `facturas` 3,228,765; solo **2,875,588 están en
   ambos**. De los 951,616 recibos sin maestro, **938,312 (98.6%) son de
   2005-2012** (L 230,051,855.20) — SIMAFI archivó los viejos. Los ~13,300
   restantes, repartidos 2013-2027, son cargos sin recibo impreso (N/D,
   reconexiones: `transaccion` 105/11/111).
   Para esos, `vence` y estado de pago deben derivarse del ledger
   (columna `plazo` y la liquidación del propio movimiento), no de `facturas`.

   ⚠️ `facturas.emision` tiene fechas corruptas (mínimo `0026-12-20`). Sanear en M3.

   ⚠️ **El número de recibo NO es único por cliente.** 3,788,747 recibos (99.0%)
   pertenecen a un solo cliente, pero **38,457 (1.0%) están compartidos por
   varios** (108,088 pares cliente-recibo). La clave natural de `factura` debe ser
   **(cliente, recibo)**, no `recibo` solo.
4. **M4 — Pagos**: `pagos_bancos` y **`transaccion_abonado.transaccion='201'`**
   → `adm_pago` + `adm_pago_aplicacion` con fecha y número originales; el saldo
   de cada línea queda como resultado de aplicar los pagos, no como dato
   importado.
   ⚠️ **`facturacion.codigo='16'` no es fuente completa de pagos**: para los
   mismos recibos reporta 16,551,197.56 contra 69,043,965.16 del ledger.

   ⚠️ **SIMAFI no guarda a qué factura se aplicó cada pago — verificado.**
   - `docuaplicar`: **1,368,579 de 2,474,875 pagos (55%) no lo traen**
     (L 829,032,069.55); 1,106,188 apuntan a su propio recibo (redundante);
     56,171 a un recibo **inexistente** (L 69,283,477.89); solo **110** aportan
     un vínculo útil. 77,043 apuntan a un recibo de otro cliente.
   - El `recibo` propio del pago tampoco alcanza: cuadran 1,315,709 recibos, pero
     **633,431 quedan "pagados de más"** (L 402,619,886.45 contra
     L 163,707,583.82 de cargos) porque al pagar atrasos SIMAFI carga todo el
     pago contra el recibo corriente.

   **Consecuencia**: las aplicaciones se **reconstruyen con FIFO** (la factura más
   vieja primero). Esto **no afecta el criterio de M6**: el saldo por cliente
   depende de los totales, no del reparto.

   ✅ **M4 HECHO (2026-07-29, local)** —
   [`Database/2026-07-29_m4_aplicacion_pagos_fifo.sql`](../Database/2026-07-29_m4_aplicacion_pagos_fifo.sql)
   y la corrección previa de cargos
   [`Database/2026-07-29_m3d_correccion_cargos_y_pagos.sql`](../Database/2026-07-29_m3d_correccion_cargos_y_pagos.sql).
   **2,837,660 pagos · 9,393,969 aplicaciones**; estados de factura
   3,713,367 cobradas / 179,383 pendientes / 4,159 parciales.
   **Control: 25,525 de 25,530 clientes con el saldo pendiente por línea idéntico
   al del libro.** Los 5 restantes están explicados al centavo: son 14 cargos que
   SIMAFI registró **sin número de recibo** (L 2,532.25 entre 2013 y 2026), que
   por definición no pueden tener línea de factura. El dinero sí está en el libro,
   así que el saldo del cliente es correcto.

   **Algoritmo** (destino: `adm_pago` + `adm_pago_aplicacion`, que aplica a nivel
   de línea vía `factura_detalle_id`): por cliente, ordenar las líneas de cargo y
   los pagos por fecha y calcular sus acumulados. Cada línea *i* ocupa el intervalo
   `(A[i-1], A[i]]` y cada pago *j* el intervalo `(P[j-1], P[j]]`; el monto aplicado
   de *j* a *i* es el solapamiento de ambos intervalos. Resoluble con funciones de
   ventana, sin cursores. `adm_pago.transaccion_abonado_ide` conserva el enlace al
   movimiento original.
   Al aplicar se actualiza `factura_detalle.montovalor_saldo` y, en consecuencia,
   `factura.estado` / `estado_id`: `C`/2 si queda en cero, `B`/4 si queda parcial,
   `A`/1 si sigue intacta.
5. **M5 — Ajustes** ⏸️ **PENDIENTE, no bloquea — no afecta ningún saldo**.
   Las 25,900 notas de crédito (`205`), los 740 convenios (`203`) y los 15,851
   descuentos de adulto mayor ya están migrados **como créditos y aplicados**;
   falta representarlos como `adm_nota_credito` y `cln_plan_pago_*`.
   ⚠️ **VERIFICADO (2026-07-29): el detalle de cuotas NO EXISTE en el origen.**
   Las columnas candidatas de `maestrosep` están vacías en las 25,766 filas:
   `cuotas1` = 0, `saldoextra1` = 0, `extrafin1` = 0, `cuotas2` = 0, `fecha2` en
   1 sola fila. `fecha1` sí está poblada (18,992) pero **no es la fecha del plan**:
   el cliente `090703034` tiene `codigoplan 0000012-2024` y `fecha1 = 2010-01-15`.
   Lo único real es el encabezado: `codigoplan` (674 clientes, formato
   `NNNNNNN-AAAA`), `convenio` (monto, 154) y `planpago` (marca, 558).

   **Recomendación: no reconstruir los planes.** Sin cronograma, cualquier
   `cln_plan_pago_dtl` sería inventado. La deuda ya está bien representada en las
   facturas y el saldo cuadra. Conservar `codigoplan` como referencia y nada más.

   🔴 **Pregunta abierta para el usuario del sistema, antes del cutover:** los 740
   movimientos `203 Convenio de Pago` son **créditos por L 7,336,806.47** — deuda
   que salió de la cuenta corriente al pasar a convenio. Nuestro saldo cuadra
   porque reproduce fielmente a SIMAFI, pero **esa cartera vive en el módulo de
   convenios de SIMAFI, que no está en el staging ni migrado**. Si se sigue
   cobrando, hay que migrarla; si no, hay que confirmarlo por escrito.
6. ~~**M6 — Validación**~~ ✅ **APROBADA (2026-07-29, local)** —
   [M6_VALIDACION_MIGRACION_SIMAFI_2026-07.md](M6_VALIDACION_MIGRACION_SIMAFI_2026-07.md).
   **25,530 de 25,530 clientes con saldo idéntico (L 48,858,786.58)**; volumen y
   dinero exactos; cero huérfanos. Criterio original: diferencia 0 contra el **ledger reconstruido**
   (`sum(debitos)-sum(creditos)` por cliente, 25,766 clientes, 48,649,742.92) y
   diferencia 0 línea a línea contra `facturacion` en su ventana.
   `clientesaldos` y `totalmora` quedan como referencia informativa, **no** como
   condición de aceptación — están desfasados. Ver §8 de M2.
7. **M7 — Contabilidad desde cero** (decisión del usuario): respaldo previo
   de las 12,095 partidas actuales, luego re-migración con los números de
   comprobante originales. **Después** de que M6 cierre en 0.
8. **M8 — Archivadas** (fase b, opcional para consulta histórica).

## 5.1 Regla de ejecución

**Toda la migración se construye y se valida en la base local `siad_v3_copia09`.
No se sube nada al servidor 0.9 hasta que el ciclo completo M3→M6 cierre en
local.** El despliegue se decide después, de una sola vez, con los scripts ya
probados.

## 6. Riesgos

| Riesgo | Mitigación |
|---|---|
| Duplicar cartera por el código `01` | Regla §4 + validación M6 obligatoria — **cuantificado en M2: 5× (106M → 538M)** |
| Duplicar cartera por el código `12` | Excluirlo como cargo (M2 §5.2); migrarlo como plan de pago en M5 |
| Aplicar la regla del `01` a `tipo_partida` en vez de a `facturacion.codigo` | Borraría los 9.38M de cargos. Ver aviso en §4 |
| Tomar `facturas.total` como importe de la factura | No lo es (~6 L por recibo). Usar ledger o `facturacion` |
| Volumen (12M movimientos) | Staging + lotes; medir con un ciclo antes de correr todo |
| Esquemas distintos entre tablas archivadas | Verificar una por una en M8 |
| Perder la contabilidad validada | Respaldo antes de M7; M7 va después de M6 |
| Numeración original vs. secuencias del SIAD | Las series `adm_documento_secuencia` deben arrancar por encima del máximo migrado |
