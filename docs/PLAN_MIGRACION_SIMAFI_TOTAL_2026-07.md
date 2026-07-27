# Migración total SIMAFI → SIAD, con códigos originales (2026-07-30)

**Estado**: descubrimiento terminado, mapeo propuesto. NO se ha migrado nada.

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
| `12` | Convenio de Pago | 717 | `cln_plan_pago_*` (cuotas F6) |
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
aplicados. La validación de la migración es precisamente que el saldo
calculado coincida con el saldo que SIMAFI muestra hoy por cliente.

## 5. Orden de ejecución propuesto

1. **M1 — Espejo de lectura**: tablas *staging* en Postgres con volcado
   crudo de SIMAFI (sin transformar). Permite auditar sin la VPN.
2. **M2 — Clientes y catálogos**: reconciliar contra lo ya migrado (1,147
   clientes en local) por `clave`.
3. **M3 — Documentos**: `facturas` → `factura` (numeración original) +
   `facturacion` (cargos reales) → `factura_detalle`.
4. **M4 — Pagos**: `16` y `pagos_bancos` → `adm_pago` + `adm_pago_aplicacion`
   con fecha y número originales; el saldo de cada línea queda como resultado
   de aplicar los pagos, no como dato importado.
5. **M5 — Ajustes**: `15`/`17` → notas de crédito; `12` → planes de pago.
6. **M6 — Validación**: saldo por cliente SIAD vs SIMAFI, cliente a cliente;
   la migración solo se da por buena con diferencia 0.
7. **M7 — Contabilidad desde cero** (decisión del usuario): respaldo previo
   de las 12,095 partidas actuales, luego re-migración con los números de
   comprobante originales. **Después** de que M6 cierre en 0.
8. **M8 — Archivadas** (fase b, opcional para consulta histórica).

## 6. Riesgos

| Riesgo | Mitigación |
|---|---|
| Duplicar cartera por el código `01` | Regla §4 + validación M6 obligatoria |
| Volumen (12M movimientos) | Staging + lotes; medir con un ciclo antes de correr todo |
| Esquemas distintos entre tablas archivadas | Verificar una por una en M8 |
| Perder la contabilidad validada | Respaldo antes de M7; M7 va después de M6 |
| Numeración original vs. secuencias del SIAD | Las series `adm_documento_secuencia` deben arrancar por encima del máximo migrado |
