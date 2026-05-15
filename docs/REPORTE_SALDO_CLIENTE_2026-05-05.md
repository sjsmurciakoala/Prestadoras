
# Reporte: fuente del saldo del cliente — 2026-05-05

**Contexto:** Sprint 2 Día 1. El usuario indicó que "no tiene claro de dónde nace el saldo". Antes de tocar el snapshot V3 con saldo previo es necesario fijar la fuente única.

---

## Hallazgos

### 1. SPs/funciones que tocan saldo (4 vivos en V3)

| SP/Fn | Path | Lee | Calcula |
|-------|------|-----|---------|
| `sp_obtener_cliente_saldo` | `Database/ddl_v3/captacion_pagos_stored_procedures.sql:57` | `cliente_detalle, transaccion_abonado, factura` | Suma de saldos por cliente. Retorna `saldo_actual, saldo_anterior, ultimo_recibo` |
| `sp_adm_calcular_factura_lectura` | `Database/ddl_v3/20260417_add_sp_adm_calcular_factura_lectura.sql:181-186` | Llama `sp_obtener_cliente_saldo` | Saldo total anterior + nuevos servicios |
| `sp_lectura_v3` | `Database/ddl_v3/20260417_add_sp_lectura_v3.sql:404` | Idem (`COALESCE(v_calc.saldos_anteriores, 0)`) | Saldo total de factura incluyendo períodos previos |
| `fn_getclientesaldos_posteomanual` | `Database/ddl_v3/fix_fn_getclientesaldos_posteomanual.sql` | `factura, factura_detalle` | Distribución del saldo por servicio (agua/alcant/otros) |

### 2. Tabla fuente operacional: `transaccion_abonado`

Es el **ledger** de movimientos por cliente.

Columnas clave: `ide` (PK), `cliente_clave`, `recibo`, `fecha_docu`, **`saldo`** (acumulado histórico), **`saldo_detalle`** (saldo pendiente por movimiento), `debitos`, `creditos`, `tipo_servicio`, `periodo`.

Consumo backend: `CobranzaService.ObtenerSaldosClienteAsync()` ([SIAD.Services/Cobranza/CobranzaService.cs:33](../SIAD.Services/Cobranza/CobranzaService.cs)) filtra `WHERE saldo_detalle != 0` y devuelve los últimos 50 movimientos.

Consumo UI: `Cobranza.razor:91` ([apc.Client/Pages/Facturacion/Cobranza/Cobranza.razor](../apc.Client/Pages/Facturacion/Cobranza/Cobranza.razor)) bind a `totalSaldo`.

### 3. Tabla contable `con_saldo_cuenta`

**No es saldo de cliente.** Es saldo contable de las cuentas del plan (mes 1..13, debitos/creditos por cuenta del balance). Actualizada por `sp_con_actualizar_saldos_por_poliza`. Sirve para reconciliación contable interna, NO para mostrar al cliente lo que debe.

### 4. Inconsistencias detectadas

1. **Doble fuente de saldo operacional**:
   - `transaccion_abonado.saldo_detalle` (ledger)
   - `factura.saldototal + factura_detalle.montovalor_saldo` (por documento)
   - `fn_getclientesaldos_posteomanual` mezcla ambas (líneas 20-26).

2. **Saldos anteriores se calculan dos veces** en runtime:
   - Una vez en `sp_adm_calcular_factura_lectura` (line 181-186)
   - Otra en `sp_lectura_v3` (line 404 `COALESCE(v_calc.saldos_anteriores, 0)`)
   - Si las definiciones divergen, dan números distintos.

---

## Decisión

**Fuente única de saldo operacional del cliente: `transaccion_abonado.saldo_detalle`.**

Justificación:
- Es el ledger primario, contiene movimientos individuales con su saldo pendiente.
- Ya es consumida por el flujo de cobranza vivo (no agrega capa nueva).
- `factura.saldototal` es derivado (se actualiza al pagar via `transaccion_abonado`), no es fuente.
- `con_saldo_cuenta` queda explícitamente reservada para contabilidad interna.

## Acciones derivadas (Sprint 2)

1. **Bug `sp_obtener_cliente_saldo`** — agregar `WHERE ta.estado='A'`. Documentar en cabecera del SP que la fuente es `transaccion_abonado.saldo_detalle`.

2. **Eliminar el cálculo duplicado**: `sp_lectura_v3` debe **reusar** el `saldos_anteriores` que ya devuelve `sp_adm_calcular_factura_lectura`, no recalcularlo. Una sola pasada por SP de cálculo, no dos.

3. **`fn_getclientesaldos_posteomanual`** — revisar si la "distribución por servicio" sigue siendo necesaria post-V3. Si sí, debe leer **solo** de `transaccion_abonado` agrupando por `tipo_servicio`, sin tocar `factura.saldototal`.

4. **Snapshot V3 con saldo previo** (Sprint 2 día 7-8) — alimentar el snapshot llamando a la fuente única, no recalculando desde `factura`.

5. **Documentar en cada DTO** (`CobranzaSaldoDetalleDto` y similares) que el campo proviene de `transaccion_abonado.saldo_detalle`.

## Riesgos

- Si hay datos donde `transaccion_abonado.saldo_detalle` y `factura.saldototal` divergen (por bugs históricos), aplicar la fuente única expone esa divergencia. **Mitigación**: antes de migrar el snapshot, correr query de validación que compare ambos para los 25 clientes de prueba; si hay diferencias, decidir cuál es la verdad y reconciliar antes de cortar.

- `con_saldo_cuenta` no incluye saldos por cliente, sólo por cuenta contable. Si en algún momento se quiere "saldo del cliente desde contabilidad", hay que crear `con_saldo_cliente` (no existe). **Por ahora no se necesita.**
