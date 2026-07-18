# Desglose por servicio: vigencia de abonos + porcentajes de distribución

**Fecha:** 2026-07-16
**Estado:** Diseño aprobado (con respaldo del SP antes de reemplazarlo)

## Problema

Los abonos registrados por Caja/Posteos/WS bancario no se reflejan ni en el saldo
actual del cliente ni en el desglose por servicio del estado de cuenta.

### Causa raíz: dos convenciones opuestas de `transaccion_abonado.estado`

| Módulo | Vigente | Anulado/Reversado |
|---|---|---|
| Facturación V3, `sp_obtener_cliente_saldo`, desglose, Cobranza, Corte | `'A'` | `'N'` / `'R'` |
| Caja (`AbonoService`, `CaptacionPagosService`), WS bancario (`sp_ban_ws_*`) | `'C'` (pendiente: `'P'`) | `'A'` |

Consecuencias (verificadas en el mirror, cliente `090040001`: facturado 662.22,
abonado 331.11, el sistema muestra 662.22):

1. Los abonos vigentes (`'C'`) son invisibles para el saldo y el desglose.
2. Los abonos reversados (`'A'`) **sí** restan del saldo.
3. La columna `saldo` (corrido) de los abonos queda corrupta porque se calcula
   con el mismo SP roto. No es confiable como fuente.

## Decisiones aprobadas

- **Respaldar** la definición actual de `sp_obtener_cliente_saldo` (ambas firmas)
  en un script en `Database/` y luego **actualizar el SP original** (misma firma).
- Corrección **global** del saldo: el SP corregido arregla de una vez la ficha del
  cliente, estado de cuenta, facturación (saldo_previo), y las consultas inline de
  Cobranza/Corte se actualizan a la misma regla.
- Distribución de abonos **al vuelo** (en la lectura), no partiendo el abono en la BD.
- Las **NC/ND quedan fuera** del reparto porcentual; siguen en su fila propia.
- El **"Saldo anterior"** (migrado de SIMAFI) es un ítem configurable más.
- Normalizar la convención de estados en datos y escritores queda como **fase 2**
  (backlog), no se toca ningún flujo de pago ahora.

## Parte 1 — Regla de vigencia única

Un movimiento de `transaccion_abonado` está **vigente** si no es de los muertos
(formulación final, corregida el mismo 2026-07-16 al descubrir los planes de pago):

```sql
COALESCE(estado, '') NOT IN ('N', 'R', 'P')                                -- anulada V3, reversado legacy, recibo pendiente
AND NOT (estado = 'A' AND COALESCE(tipotransaccion, '') IN ('201','202')) -- pagos de caja/WS anulados ('A' = anulado para la caja)
```

Casos cubiertos: factura activa `'A'` ✔, factura anulada `'N'` ✘, pago vigente
`'C'+201/202` ✔, pago reversado `'A'+202` ✘, recibo pendiente `'P'` ✘, pendiente
anulado `'A'+202` ✘, reverso legacy `'R'` ✘, pagos migrados `'A'+PAGO...` ✔,
NC/ND `'A'+205/206` ✔, y **planes de pago**: traslado `PLAN` con `'C'` ✔ (crédito
que compensa las cuotas `PLAN-CUOTA` `'A'` ✔ para no duplicar la deuda trasladada;
verificado con el cliente 090807355: 722.78). La primera versión de la regla
(lista blanca de `'C'` solo para 201/202) dejaba fuera el traslado `PLAN` e
inflaba el saldo de los clientes con plan.

Implementación:

- **Vista** `public.vw_transaccion_abonado_vigente` con la regla (una sola
  definición en BD).
- `sp_obtener_cliente_saldo` (firmas `(bigint, varchar)` y `(varchar)`) pasa de
  "saldo corrido del último movimiento activo" a
  `SUM(COALESCE(debitos,0) - COALESCE(creditos,0))` sobre la vista. Devuelve una
  fila con 0 cuando el cliente no tiene movimientos (antes devolvía 0 filas; los
  callers hacen `?? 0`).
- Scripts en `Database/`:
  1. `2026-07-16_backup_sp_obtener_cliente_saldo.sql` — definición actual íntegra
     (restaurable con solo ejecutarlo).
  2. `2026-07-16_fix_saldo_vigencia_y_desglose_abono.sql` — vista + SPs nuevos +
     tabla de porcentajes (idempotente).
- Consultas inline que replican el patrón viejo y se actualizan a la vista/SP:
  `ClientesServices.GetEstadoCuentaAsync` (desglose), `CobranzaService` (lista de
  saldos), `CorteMasivoService`.
- Auditoría en prod antes de aplicar (query comparativa saldo viejo vs nuevo por
  cliente, incluida como comentario en el script). **El script en prod lo aplica
  el usuario.**

## Parte 2 — Mantenimiento de porcentajes del desglose

### Tabla

```sql
CREATE TABLE IF NOT EXISTS public.adm_desglose_abono_porcentaje (
    desglose_abono_id  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint NOT NULL,
    item_codigo        varchar(50) NOT NULL,   -- adm_servicio.codigo o 'SALDO_ANTERIOR'
    porcentaje         numeric(5,2) NOT NULL CHECK (porcentaje >= 0 AND porcentaje <= 100),
    usuario            varchar(100),
    fecha_modificacion timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_desglose_abono UNIQUE (company_id, item_codigo)
);
```

Tenant-scoped por `company_id`. Sin entidad EF: acceso Dapper (patrón del módulo
Tarifario).

### Slice

- DTOs `SIAD.Core/DTOs/Clientes/DesgloseAbonoConfigDtos.cs`: item (código, nombre,
  orden, porcentaje) y payload de guardado.
- Servicio `SIAD.Services/Clientes/DesgloseAbonoConfigService.cs` + interfaz:
  - `GetAsync`: servicios activos del catálogo + fila `SALDO_ANTERIOR`, con su %
    actual (0 si no configurado).
  - `SaveAsync`: valida suma = 100.00 (o todos 0 = desactivar distribución) y
    hace upsert. Resuelve `company_id` vía `ICurrentCompanyService`.
- Controller `apc/Controllers/Clientes/DesgloseAbonoConfigController.cs` con
  `[ModuleAuthorize]`, permiso registrado en `PermissionNames` +
  `PermissionEndpointCatalog`.
- Cliente HTTP `apc.Client/Services/Clientes/DesgloseAbonoConfigClient.cs`,
  registrado en `CommonServices`.
- Página `apc.Client/Pages/Clientes/DesgloseAbonoConfig.razor`: DxGrid (estándar
  de grids) con columna de % editable (DxSpinEdit en template), fila de total,
  botón Guardar deshabilitado si la suma ≠ 100 y ≠ 0. Entrada de menú en
  `SidebarNavigationDefinition`.

### Distribución en el estado de cuenta

En `GetEstadoCuentaAsync` (C#, no en SQL, para controlar el redondeo):

1. El desglose agrupa con la vista de vigencia y separa los "otros" en tres
   cubetas: `SALDO_ANTERIOR`, **pagos** (tipotransaccion `201`/`202` o
   `ILIKE '%PAGO%'` o `tipo_servicio='E'`) y **ajustes** (NC/ND y resto).
2. Si la configuración de la empresa suma 100: el neto de la cubeta de pagos se
   reparte entre los ítems configurados — `round(monto * pct / 100, 2)` por ítem
   y el residuo de redondeo al ítem de mayor % (desempate: menor orden visual) —
   sumándose (con signo) al saldo de cada ítem. La fila "Pagos y ajustes" queda
   solo con los ajustes (si hay).
3. Si no hay configuración o no suma 100: comportamiento actual (fila
   "Pagos y ajustes" con todo). **El TOTAL siempre cuadra con el saldo actual.**

La lógica de reparto se extrae a un método puro (testeable sin BD).

## Errores y bordes

- Config incompleta / suma ≠ 100 → fallback al comportamiento actual, nunca
  descuadra el total.
- Cliente sin movimientos → saldo 0, desglose solo con servicios recurrentes en 0.
- Servicio desactivado con % configurado → su % deja de aplicar (la suma ya no da
  100 → fallback); el mantenimiento lo muestra para corregirlo.
- Pagos que exceden lo facturado de un ítem → ese ítem puede quedar negativo (es
  inherente a porcentajes fijos, aceptado).

## Pruebas

- `SIAD.Tests` (integración, rollback): saldo con abono `'C'` resta; abono
  reversado `'A'+202` no resta; recibo pendiente `'P'` no resta; SP devuelve 0
  para cliente sin movimientos.
- Unit test del método puro de distribución: reparto exacto, residuo de redondeo,
  fallback con suma ≠ 100.
- Build de la solución + verificación manual del estado de cuenta contra el mirror.
