# Control presupuestario con compromiso en la aprobación de la O/C

**Fecha:** 2026-08-27 · **Estado:** DISEÑO — sin código ni SQL derivado
**Alcance:** Compras (O/C → recepción → factura → CxP → pago), Presupuesto, Contabilidad.

> Este documento **absorbe y supersede** a `docs/plans/2026-08-14-presupuesto-compras-ejecucion-design.md`
> (afectación solo por ejecución en la recepción). Aquel diseño no contemplaba compromiso; el enganche que
> proponía en la recepción sobrevive aquí como el movimiento de **devengo**.
>
> También **revierte explícitamente D-OC-5 / D-OC-b** (`docs/centura-flujos/README_orden_compra.md:30,176`,
> usuario 2026-07-30: *centro de costo solo informativo, sin validar presupuesto*). La instrucción vigente es
> controlar presupuesto **desde la aprobación de la O/C**.

---

## 1. Diagnóstico de la arquitectura actual

### 1.1 Lo que existe y sirve

| Pieza | Ubicación | Estado real (verificado 2026-08-27) |
|---|---|---|
| Presupuesto cabecera/detalle | `pst_config_presupuesto_hdr` / `_dtl` | En uso. PK compuesta `(company_id, id_presupuesto[, con_cuenta_code])`. Presupuestos **anuales** (`PRE-2025`, `PRE-2026`), `rango_periodo` fijo en 12, vigencia por `fecha_inicia`/`fecha_finaliza`, `estado_aprobado BOOLEAN` |
| Marca de cuenta presupuestable | `con_plan_cuenta.allows_budget` | Existe y **está configurada**: 329 de 2265 cuentas (medición en mirror, empresa 2). Se marca a mano en `PlanCuentaForm.razor:138` |
| Catálogo de centros de costo | `con_centro_costo` (`cost_center_id`, `code`, `name`, `allows_movement`, `status`) | **Existe y es real.** Multitenant, con `con_plan_cuenta.allows_cost_center` como marca por cuenta |
| Órdenes de compra | `alm_orden_compra` / `_detalle` / `_correlativo` | En uso. Estados 1 Borrador · 2 Aprobada · 3 Recibida parcial · 4 Cerrada · 9 Anulada. `cantidad_aplicada` por renglón la mueve la recepción |
| Recepción / factura de compra | `alm_compra_hdr` / `alm_compra` | Transaccional completa: kardex + CxP + asiento en un solo `BEGIN…COMMIT` |
| CxP y pagos de compra | `alm_compra_cxp` / `_abono` | 1:1 con la factura; saldo materializado bajo lock; estados 1/2/3/9 |
| Asiento de la compra | `CompraContabilidad.cs` | DEBE `alm_tipo_articulo.cuenta_inventario` por renglón agrupado; HABER cuenta del proveedor; remanente (flete/otros/descuento/ISV) capitalizado en la cuenta de mayor valor |
| Precedente de lock correcto | `fn_pst_afectar_saldo_real_credito` (`Database/ddl_v3/20260306_presupuesto_credito_allows_budget.sql:77`) | Usa `SELECT … FOR UPDATE OF d` sobre el detalle. **Es el patrón a replicar** |

### 1.2 Los siete hallazgos que condicionan el diseño

**H1 — El modelo presupuestario NO tiene el concepto de "comprometido".**
`pst_config_presupuesto_dtl` solo lleva tres montos: `valor_proyeccion` (lo presupuestado), `valor_real` (lo
ejecutado) y `valor_disponible` (derivado, `MAX(valor_proyeccion − valor_real, 0)`). No existe reserva, ni
pre-compromiso, ni ciclo de liberación. **Esta es la brecha central**: lo que se pide exige un cuarto eje de
montos y un ciclo de vida (comprometer → devengar → liberar) que hoy no existe en ninguna forma.

**H2 — El único eje del presupuesto es la cuenta contable.**
Una "partida presupuestaria" en este sistema es una fila de `pst_config_presupuesto_dtl`, es decir el par
`(id_presupuesto, con_cuenta_code)`. **No hay eje de centro de costo ni de período mensual.** El catálogo
`con_centro_costo` existe, pero el presupuesto no lo referencia. Controlar por centro de costo exige agregar
el eje al modelo (ver **D3**).

**H3 — La O/C no sabe contra qué cuenta presupuestaria compra.**
`alm_orden_compra_detalle.centro_costo` es `VARCHAR(40)` de texto libre **sin FK** al catálogo, declarado como
informativo. La cuenta contable del renglón hoy se deriva implícitamente (`articulo → alm_tipo_articulo.cuenta_inventario`)
y solo se resuelve al contabilizar la factura. La O/C nunca la materializa.

**H4 — `AprobarAsync` no tiene transacción, ni lock, ni validación de importes.**
`OrdenCompraService.cs:271` es un cambio de estado plano: lee la entidad, verifica que esté en Borrador y que
tenga renglones, sella `aprobado_por`/`fecha_aprobacion` y hace `SaveChangesAsync`. Es el punto de enganche
natural, pero hoy no ofrece ninguna garantía transaccional aprovechable tal cual.

**H5 — La compra se puede hacer sin O/C.**
`alm_compra_hdr.orden_compra_id` es *nullable* y la recepción soporta modo directo. **Un control que viva solo
en la aprobación de la O/C es evitable**: se registra la factura directa y el presupuesto no se entera.
Cualquier diseño serio necesita los dos puntos.

**H6 — Cero trazabilidad presupuestaria.**
`valor_real` es un número acumulado sin historia, sin usuario, sin fecha y sin referencia al documento que lo
movió. Hoy es imposible responder *por qué esta cuenta está al 90%*. El kardex presupuestario que pide §9 del
requerimiento **no existe en ninguna forma**.

**H7 — Ya hay dos escritores de `valor_real`, ninguno completo.**

| Escritor | Mecanismo | Problema |
|---|---|---|
| `OrdenesPagoDirectoService.ApplyCompromisoPresupuestoAsync:4037` (compromiso a proveedor / OPD) | LINQ + mutación de entidades EF, `IsolationLevel.Serializable` | **Sin ningún lock** sobre `pst_config_presupuesto_*`. El `40001` resultante no se maneja → 500 crudo bajo concurrencia. Sin bitácora |
| `BanTransaccionesService` → `fn_pst_afectar_saldo_real_credito` (créditos bancarios) | SP con `FOR UPDATE OF d` | Correcto en concurrencia, pero sin bitácora |

Además `sp_pst_aplicar_partida_presupuesto` existe en SQL y su único llamador C# **no está cableado** — es
código muerto. Y los triggers `trg_pst_*` sobre `con_partida`/`con_poliza` están explícitamente eliminados
(DROP en los scripts): **la contabilidad no afecta presupuesto**, y no debe empezar a hacerlo (sería doble
conteo contra este diseño).

### 1.3 El hallazgo que decide si esto sirve o no en producción

Medición en el mirror (empresa 2, 2026-08-14):

- Presupuesto **PRE-2026** (L 239,994,973, aprobado): 18 cuentas de INGRESO + 63 de COSTO + 28 de GASTO + 1 de
  CAPITAL. **Cero cuentas de inventario (114\*).** Ejecutado apenas L 122,400.
- **Las 9 cuentas `cuenta_inventario` de los tipos de artículo tienen `allows_budget = false` y no están
  presupuestadas.**

Consecuencia dura: si el compromiso de la O/C muerde contra la cuenta de inventario del artículo —la que
debita el asiento— **el control encendido hoy no bloquearía absolutamente nada**. No es un detalle de
configuración: define si la solución es útil o es un no-op silencioso. Ver **D1**.

### 1.4 Colisión de vocabulario que hay que fijar antes de escribir SQL

En este repositorio **"partida" ya significa asiento contable** (`con_partida`, `con_partida_dtl`; y por
convención se dice *partida contable*, nunca *póliza*). El requerimiento usa "partida presupuestaria" para la
línea de presupuesto.

**Convención adoptada:** en el texto funcional se dice **partida presupuestaria**; en la base de datos **todo
objeto nuevo lleva prefijo `pst_`** y la línea se llama *línea presupuestaria* (`pst_config_presupuesto_dtl`).
Ningún objeto nuevo usa la palabra `partida` sin el prefijo `pst_`.

---

## 2. Flujo funcional propuesto

```
Presupuesto aprobado (pst_config_presupuesto_hdr.estado_aprobado = true)
   |
   +-- Requisicion / Solicitud (alm_requisicion_hdr)   -- Borrador > En revision > Aprobada
   |      movimiento presupuestario: NINGUNO (solo consulta informativa de disponible)
   |
   +-- Orden de compra (alm_orden_compra)              -- Borrador
   |      movimiento presupuestario: NINGUNO
   |
   +-- APROBACION DE LA O/C   <<< punto de control duro
   |      1. resolver la partida presupuestaria de cada renglon (cuenta + centro de costo + fecha)
   |      2. lock FOR UPDATE de cada partida, en orden deterministico
   |      3. validar disponible >= monto POR PARTIDA (no por total de la O/C)
   |      4. si falta en cualquier partida -> rollback total, la O/C sigue en Borrador
   |      5. COMPROMISO (+) por partida  -> O/C = Aprobada
   |
   +-- Recepcion + Factura del proveedor (alm_compra_hdr)
   |      DEVENGO: comprometido (-) y ejecutado (+) por el monto recibido
   |        con O/C  -> consume el compromiso existente (el disponible NO cambia)
   |        sin O/C  -> consume disponible directamente (valida como compromiso + devengo en un paso)
   |
   +-- Cuenta por pagar (alm_compra_cxp)               -- movimiento presupuestario: NINGUNO
   |
   +-- Pago (alm_compra_cxp_abono)                     -- PAGADO (+), informativo, no altera disponible
```

**Principio de dueño único del movimiento** (requerimiento §7): cada movimiento presupuestario tiene
**exactamente un módulo emisor**. Ningún otro punto del sistema puede generarlo.

| Movimiento | Único emisor | Momento exacto |
|---|---|---|
| Compromiso (+) | Compras — `OrdenCompraService.AprobarAsync` | Al aprobar la O/C, dentro de su transacción |
| Liberación (−) | Compras — `AnularAsync` / `CancelarAsync` / `CerrarAsync` | Al anular, cancelar o cerrar con saldo |
| Ajuste de compromiso (±) | Compras — `ModificarAprobadaAsync` (**nueva**) | Al modificar una O/C ya aprobada |
| Devengo (+ ejecutado / − comprometido) | Compras — `RecepcionCompraService.CrearAsync` | Al registrar la factura, dentro de su transacción |
| Reversa de devengo | Compras — `RecepcionCompraService.AnularAsync` | Al anular la factura |
| Pagado (+) | Compras — `CompraCxpService` (abono) | Al aplicar el abono |
| Reversa de pagado | Compras — anulación del abono | |
| Ampliación / reducción | Presupuesto — `ConfiguracionPresupuestoService` | Al editar `valor_proyeccion` |

**Prohibido explícitamente:** que la contabilización de la factura (`CompraContabilidad`) genere movimiento
presupuestario. Debitar inventario y devengar presupuesto son el mismo hecho económico registrado una vez: lo
registra Compras, no Contabilidad. Los triggers `trg_pst_*` sobre `con_partida` deben seguir eliminados.

---

## 3. Estados presupuestarios y fórmulas

### 3.1 Los cuatro montos por partida

| Concepto | Columna | Origen |
|---|---|---|
| **Aprobado** (incluye modificaciones) | `valor_proyeccion` *(ya existe)* | Módulo Presupuesto |
| **Comprometido** | `valor_comprometido` *(**nueva**)* | O/C aprobadas con saldo sin devengar |
| **Devengado / Ejecutado** | `valor_real` *(ya existe, no cambia de significado)* | Facturas de proveedor registradas |
| **Pagado** | `valor_pagado` *(**nueva**)* | Abonos aplicados a la CxP |
| **Disponible** | `valor_disponible` *(ya existe, **cambia la fórmula**)* | Derivado |

### 3.2 Fórmulas

```
Disponible(partida)   = valor_proyeccion - valor_comprometido - valor_real
Comprometido(partida) = SUM(monto_comprometido - monto_devengado - monto_liberado)   [pst_compromiso vigentes]
Ejecutado(partida)    = SUM(devengos) - SUM(reversas de devengo)                     [pst_movimiento]
Pagado(partida)       = SUM(pagos) - SUM(reversas de pago)                           [pst_movimiento]

Cabecera:
Disponible(presupuesto) = valor_global - SUM(dtl.valor_comprometido) - SUM(dtl.valor_real)
```

**Cambio de semántica declarado:** hoy `valor_disponible = MAX(valor_proyeccion − valor_real, 0)`. Al restar
también el comprometido, las pantallas y reportes existentes siguen funcionando sin tocarse **porque
`valor_comprometido` nace en 0** y solo lo mueve el módulo nuevo. No hay migración de datos.

> **Inconsistencia preexistente que este diseño NO corrige:** `hdr.valor_disponible` se calcula contra
> `valor_global` mientras `dtl.valor_disponible` se calcula contra `valor_proyeccion`. Son bases distintas y
> pueden no cuadrar si `valor_global ≠ Σ valor_proyeccion`. Se documenta; corregirlo es otro trabajo.

### 3.3 Invariantes (verificables con una consulta de conciliación)

```
I1  valor_comprometido = SUM(saldo vigente de pst_compromiso de esa partida)
I2  valor_real         = SUM(devengos netos de pst_movimiento de esa partida)
I3  valor_comprometido >= 0  AND  valor_real >= 0  AND  valor_pagado >= 0
I4  valor_disponible >= 0                            (garantizado solo en modo Bloqueo)
I5  monto_devengado + monto_liberado <= monto_comprometido   (por fila de pst_compromiso)
I6  valor_pagado <= valor_real + tolerancia          (advertencia, no constraint)
```

### 3.4 Evolución del presupuesto etapa por etapa

Partida 11101 con L 1,000,000 aprobados. O/C 000123 por L 100,000; se recibe L 60,000 y se paga.

| Evento | Aprobado | Comprometido | Ejecutado | Pagado | **Disponible** |
|---|---:|---:|---:|---:|---:|
| Estado inicial | 1,000,000 | 0 | 0 | 0 | **1,000,000** |
| O/C en Borrador | 1,000,000 | 0 | 0 | 0 | **1,000,000** |
| **O/C aprobada (100,000)** | 1,000,000 | 100,000 | 0 | 0 | **900,000** |
| Factura recibida por 60,000 | 1,000,000 | 40,000 | 60,000 | 0 | **900,000** |
| Pago de la CxP (60,000) | 1,000,000 | 40,000 | 60,000 | 60,000 | **900,000** |
| **O/C cancelada (libera 40,000)** | 1,000,000 | 0 | 60,000 | 60,000 | **940,000** |

**La regla de oro que hace consistente el modelo:** *devengar no cambia el disponible*. Mueve dinero de
comprometido a ejecutado. El disponible solo lo mueven el **compromiso**, la **liberación** y las
**modificaciones del presupuesto**. Es lo que evita el doble conteo entre O/C y factura.

### 3.5 El caso del requerimiento §4

```
Aprobado 1,000,000 · Comprometido existente 700,000 · Disponible 300,000
  O/C nueva por 250,000 -> disponible (300,000) >= 250,000 -> APRUEBA, compromete, disponible 50,000
  O/C nueva por 350,000 -> disponible (300,000) <  350,000 -> RECHAZA, la O/C sigue en Borrador, nada se escribe
```

---

## 4. Modelo de datos propuesto

### 4.1 Mapa

```
pst_config_presupuesto_hdr        (existe, +2 columnas)      Presupuesto
        |
        +-- pst_config_presupuesto_dtl  (existe, +3 columnas) PARTIDA PRESUPUESTARIA  (cuenta [+ centro de costo])
                  |
                  +-- pst_compromiso     (NUEVA)              Saldo vivo del compromiso por documento y partida
                  |        |
                  |        +-- pst_compromiso_aplicacion (NUEVA)  Devengos/liberaciones que consumen ese compromiso
                  |
                  +-- pst_movimiento     (NUEVA)              KARDEX presupuestario (historia completa, inmutable)

cfg_presupuesto_control          (NUEVA)                      Modo del control por empresa y módulo

alm_orden_compra_detalle         (existe, +2 columnas)        cuenta_presupuestaria, centro_costo_id
```

### 4.2 Tablas existentes modificadas

#### `pst_config_presupuesto_dtl` — 3 columnas nuevas

| Columna | Tipo | Nulo | Default | Nota |
|---|---|---|---|---|
| `valor_comprometido` | `NUMERIC(18,4)` | NOT NULL | `0` | Σ saldos vivos de `pst_compromiso` |
| `valor_pagado` | `NUMERIC(18,4)` | NOT NULL | `0` | Informativo; no resta disponible |
| `centro_costo_id` | `BIGINT` | NULL | — | **Solo si se adopta D3-B.** Cambiaría la PK — ver D3 |

```sql
ALTER TABLE public.pst_config_presupuesto_dtl
    ADD COLUMN IF NOT EXISTS valor_comprometido NUMERIC(18,4) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS valor_pagado       NUMERIC(18,4) NOT NULL DEFAULT 0;

ALTER TABLE public.pst_config_presupuesto_dtl
    ADD CONSTRAINT ck_pst_dtl_montos_no_negativos
        CHECK (valor_comprometido >= 0 AND valor_real >= 0 AND valor_pagado >= 0);
```

#### `pst_config_presupuesto_hdr` — 1 columna nueva

| Columna | Tipo | Nulo | Default | Nota |
|---|---|---|---|---|
| `valor_comprometido` | `NUMERIC(18,4)` | NOT NULL | `0` | Σ de los detalles; se recalcula al final de cada SP |

#### `alm_orden_compra_detalle` — 2 columnas nuevas

| Columna | Tipo | Nulo | Nota |
|---|---|---|---|
| `cuenta_presupuestaria` | `VARCHAR(20)` | NULL | **Snapshot** de la cuenta contra la que se comprometió. Se propone al capturar (desde `alm_tipo_articulo.cuenta_inventario`), es editable si el usuario tiene permiso, y se congela al aprobar |
| `centro_costo_id` | `BIGINT` | NULL | FK compuesta tenant-safe a `con_centro_costo (company_id, cost_center_id)` |

`centro_costo VARCHAR(40)` queda **deprecada** (se conserva por el histórico; no se escribe más). El snapshot
explícito es indispensable: sin él, cambiar la cuenta del tipo de artículo reescribiría retroactivamente
contra qué partida se comprometió una O/C ya aprobada.

#### `alm_orden_compra` — estados nuevos

Hoy: `CHECK (estado IN (1,2,3,4,9))`. Se agregan dos estados que el requerimiento §12 exige y que hoy no
existen:

| Código | Estado | Origen |
|---|---|---|
| 5 | **Rechazada** | Desde Borrador. No genera movimiento |
| 6 | **Cancelada** | Desde Aprobada o Recibida parcial. **Libera el saldo comprometido pendiente** |

`AnularAsync` hoy **prohíbe** anular una O/C con recepciones. Eso es correcto y se mantiene: el caso "O/C con
recepción parcial que ya no se va a completar" es precisamente **Cancelada**, la operación que falta.

### 4.3 Tablas nuevas

#### `pst_compromiso` — saldo vivo del compromiso

Una fila por (documento origen, renglón, partida). Es lo que permite liberar **exactamente el saldo pendiente**
y no el total (requerimiento §6).

| Columna | Tipo | Nulo | Nota |
|---|---|---|---|
| `id` | `BIGSERIAL` | PK | |
| `company_id` | `BIGINT` | NOT NULL | Tenant. FK → `cfg_company` |
| `id_presupuesto` | `VARCHAR(10)` | NOT NULL | |
| `con_cuenta_code` | `VARCHAR(20)` | NOT NULL | FK compuesta → `pst_config_presupuesto_dtl (company_id, id_presupuesto, con_cuenta_code)` |
| `centro_costo_id` | `BIGINT` | NULL | Informativo mientras rija D3-A |
| `modulo` | `VARCHAR(20)` | NOT NULL | `COMPRAS`, `PROVEEDORES`, `BANCOS`… |
| `documento_tipo` | `VARCHAR(20)` | NOT NULL | `ORDEN_COMPRA` |
| `documento_id` | `BIGINT` | NOT NULL | `alm_orden_compra.id` |
| `documento_numero` | `VARCHAR(40)` | NULL | Número visible |
| `documento_detalle_id` | `BIGINT` | NULL | `alm_orden_compra_detalle.id` — granularidad de liberación |
| `fecha` | `DATE` | NOT NULL | Fecha con la que se resolvió el presupuesto. **La liberación usa esta, no la del día** |
| `monto_comprometido` | `NUMERIC(18,4)` | NOT NULL | Monto original (se ajusta en modificaciones de O/C) |
| `monto_devengado` | `NUMERIC(18,4)` | NOT NULL DEFAULT 0 | Consumido por facturas |
| `monto_liberado` | `NUMERIC(18,4)` | NOT NULL DEFAULT 0 | Devuelto por anulación/cancelación/cierre |
| `estado` | `SMALLINT` | NOT NULL DEFAULT 1 | 1 Vigente · 2 Cerrado (saldo 0) · 9 Liberado |
| `usuariocreacion` / `fechacreacion` | | | Auditoría |
| `usuariomodificacion` / `fechamodificacion` | | | Auditoría |

```sql
CONSTRAINT uq_pst_compromiso_documento
    UNIQUE (company_id, modulo, documento_tipo, documento_id, documento_detalle_id, con_cuenta_code),
CONSTRAINT ck_pst_compromiso_saldo
    CHECK (monto_devengado + monto_liberado <= monto_comprometido),
CONSTRAINT ck_pst_compromiso_montos
    CHECK (monto_comprometido >= 0 AND monto_devengado >= 0 AND monto_liberado >= 0)
```

Índices: `(company_id, documento_tipo, documento_id)`, `(company_id, id_presupuesto, con_cuenta_code)`,
y parcial `(company_id, estado) WHERE estado = 1` para el reporte de compromisos pendientes.

`saldo_comprometido` **no es columna**: se deriva (`monto_comprometido − monto_devengado − monto_liberado`) en
la vista `vw_pst_compromiso_saldo`. Evita una columna generada (compatibilidad con la versión de PostgreSQL del
servidor de producción, que no está confirmada) y elimina el riesgo de desincronización.

#### `pst_compromiso_aplicacion` — qué documento consumió qué compromiso

| Columna | Tipo | Nota |
|---|---|---|
| `id` | `BIGSERIAL` PK | |
| `company_id` | `BIGINT` NOT NULL | |
| `compromiso_id` | `BIGINT` NOT NULL | FK → `pst_compromiso` |
| `movimiento_id` | `BIGINT` NOT NULL | FK → `pst_movimiento` |
| `tipo` | `SMALLINT` NOT NULL | 1 Devengo · 2 Liberación · 3 Reversa de devengo |
| `documento_tipo` / `documento_id` / `documento_numero` | | La factura o el evento que aplicó |
| `monto` | `NUMERIC(18,4)` NOT NULL | Con signo |

Es la tabla que responde *"esta O/C de L 100,000 se consumió con estas 3 facturas y se le liberaron L 40,000
al cancelarla"*. Sin ella, el trazo O/C → factura es reconstruible pero no auditable.

#### `pst_movimiento` — kardex presupuestario (requerimiento §9)

**Inmutable, append-only.** Es la tabla que permite reconstruir la historia completa de una partida.

| Columna | Tipo | Nota |
|---|---|---|
| `id` | `BIGSERIAL` PK | |
| `company_id` | `BIGINT` NOT NULL | |
| `id_presupuesto` | `VARCHAR(10)` NOT NULL | |
| `con_cuenta_code` | `VARCHAR(20)` NOT NULL | |
| `centro_costo_id` | `BIGINT` NULL | |
| `tipo_movimiento` | `SMALLINT` NOT NULL | ver tabla abajo |
| `modulo` | `VARCHAR(20)` NOT NULL | `COMPRAS` |
| `documento_tipo` | `VARCHAR(20)` NOT NULL | `ORDEN_COMPRA`, `FACTURA_COMPRA`, `ABONO_CXP`, `PRESUPUESTO` |
| `documento_id` | `BIGINT` NOT NULL | Id del documento **que causa** el movimiento |
| `documento_numero` | `VARCHAR(40)` NULL | Número visible (No. de O/C, No. de factura) |
| `documento_detalle_id` | `BIGINT` NULL | |
| `orden_compra_id` | `BIGINT` NULL | **Siempre** la O/C relacionada, aunque el documento sea la factura |
| `compromiso_id` | `BIGINT` NULL | FK → `pst_compromiso` |
| `fecha` | `DATE` NOT NULL | Fecha de efecto presupuestario |
| `monto` | `NUMERIC(18,4)` NOT NULL | Siempre **positivo**; el signo lo da `tipo_movimiento` |
| `proyeccion_anterior` · `comprometido_anterior` · `ejecutado_anterior` · `disponible_anterior` | `NUMERIC(18,4)` NOT NULL | Saldos **antes** |
| `proyeccion_posterior` · `comprometido_posterior` · `ejecutado_posterior` · `disponible_posterior` | `NUMERIC(18,4)` NOT NULL | Saldos **después** |
| `excedio` | `BOOLEAN` NOT NULL DEFAULT false | `true` si pasó excediendo en modo Advertencia |
| `estado` | `SMALLINT` NOT NULL DEFAULT 1 | 1 Vigente · 9 Reversado |
| `movimiento_reversa_id` | `BIGINT` NULL | Apunta al movimiento que lo reversó |
| `observacion` | `VARCHAR(500)` NULL | Motivo (obligatorio en cancelaciones y anulaciones) |
| `usuario` | `VARCHAR(100)` NOT NULL | Quien ejecutó |
| `usuario_aprobo` | `VARCHAR(100)` NULL | Quien aprobó la O/C |
| `ip` | `VARCHAR(45)` NULL | Ver D7 |
| `fecha_registro` | `TIMESTAMP` NOT NULL DEFAULT `now()` | |

**Catálogo `tipo_movimiento`** (constante nueva en `SIAD.Core/Constants/EstadosNumericos.cs`):

| Código | Tipo | Efecto |
|---|---|---|
| 1 | Compromiso | comprometido **+** · disponible **−** |
| 2 | Liberación de compromiso | comprometido **−** · disponible **+** |
| 3 | Devengo | comprometido **−** · ejecutado **+** · disponible **=** |
| 4 | Reversa de devengo | comprometido **+** · ejecutado **−** · disponible **=** |
| 5 | Devengo directo (sin O/C) | ejecutado **+** · disponible **−** |
| 6 | Reversa de devengo directo | ejecutado **−** · disponible **+** |
| 7 | Pago | pagado **+** · disponible **=** |
| 8 | Reversa de pago | pagado **−** · disponible **=** |
| 10 | Ampliación de presupuesto | proyección **+** · disponible **+** |
| 11 | Reducción de presupuesto | proyección **−** · disponible **−** |
| 12 | Ajuste de compromiso (aumento) | comprometido **+** · disponible **−** |
| 13 | Ajuste de compromiso (disminución) | comprometido **−** · disponible **+** |

> Los tipos 12 y 13 se separaron del 1 y el 2 al implementar F1: el ajuste de una O/C aprobada es
> un evento **repetible** (una orden se puede modificar tres veces) y no puede compartir clave de
> idempotencia con el compromiso inicial, que es único.

**Idempotencia** — el índice que impide contar dos veces:

```sql
CREATE UNIQUE INDEX uq_pst_movimiento_idempotencia
    ON pst_movimiento (company_id, tipo_movimiento, documento_tipo, documento_id,
                       con_cuenta_code, COALESCE(documento_detalle_id, 0))
    WHERE estado = 1 AND tipo_movimiento NOT IN (10, 11, 12, 13);
```

Los tipos 10–13 quedan **fuera del índice a propósito**: ampliar un presupuesto y ajustar una O/C
aprobada son eventos repetibles. Incluirlos haría que el segundo ajuste chocara y se perdiera en
silencio.

Por lo demás funciona porque `documento_id` es siempre **el documento que causa** el movimiento: la O/C para el compromiso,
la factura para el devengo, el abono para el pago. Dos recepciones parciales de la misma O/C son dos facturas
distintas y por tanto dos filas legítimas; un reintento de la misma factura choca contra el índice.

**Inmutabilidad:** trigger `trg_pst_movimiento_solo_insert` que rechaza `UPDATE`/`DELETE` salvo el campo
`movimiento_reversa_id`. Precedente en el repo: `trg_transaccion_abonado_congelada`.

#### `cfg_presupuesto_control` — modo del control

| Columna | Tipo | Default | Nota |
|---|---|---|---|
| `company_id` | `BIGINT` | — | PK con `modulo` |
| `modulo` | `VARCHAR(30)` | — | `COMPRAS_OC`, `COMPRAS_FACTURA`, `PROVEEDORES`, `BANCOS` |
| `modo` | `SMALLINT` | `0` | **0 Apagado · 1 Advertencia · 2 Bloqueo** |
| `exige_presupuesto_aprobado` | `BOOLEAN` | `true` | Si el presupuesto debe estar aprobado para comprometer |
| `tolerancia_pct` | `NUMERIC(5,2)` | `0` | Variación permitida entre el compromiso de la O/C y el devengo de la factura |
| `permite_devengo_sin_oc` | `SMALLINT` | `1` | 0 Prohíbe · 1 Consume disponible · 2 Solo advierte — **cierra el hueco H5** |
| Auditoría | | | `usuariomodificacion`, `fechamodificacion` |

El modo **Advertencia** no es decorativo: permite encender el control en producción, observar un mes de datos
reales y detectar cuentas mal presupuestadas **sin bloquear la operación**. Sin él, el primer día de bloqueo se
convierte en una fila de órdenes que no se pueden aprobar.

### 4.4 Vistas

| Vista | Para qué |
|---|---|
| `vw_pst_compromiso_saldo` | `pst_compromiso` + saldo derivado + datos de la O/C y del proveedor |
| `vw_pst_ejecucion_presupuestaria` | Una fila por partida: aprobado, comprometido, ejecutado, pagado, disponible, % ejecución |
| `vw_pst_movimiento_detalle` | `pst_movimiento` + nombre de cuenta, centro de costo, proveedor y número de O/C (alimenta el kardex en pantalla) |
| `vw_pst_ejecucion_centro_costo` | Agregado por `centro_costo_id` (informativo mientras rija D3-A) |

---

## 5. Procedimientos almacenados y funciones

Toda la lógica vive en PostgreSQL, no en C#. Tres razones, en orden de peso:

1. **Concurrencia.** El `FOR UPDATE` sobre la partida es la única defensa real contra la doble aprobación
   (§7). En C# con EF no existe forma equivalente sin bajar a SQL de todos modos.
2. **La regla del repositorio.** `.github/skills/hodsoft-sin-linq`: todo acceso a datos va por SP, función o
   vista. Código nuevo con cero LINQ.
3. **Auditoría.** El SQL queda versionado en `Database/` y desplegable al SRV; la validación presupuestaria es
   exactamente el tipo de regla que el contador va a querer leer.

### 5.1 Tipo compuesto

```sql
CREATE TYPE public.pst_linea_afectacion AS (
    con_cuenta_code       VARCHAR(20),
    centro_costo_id       BIGINT,
    documento_detalle_id  BIGINT,
    monto                 NUMERIC(18,4)
);
```

### 5.2 Funciones auxiliares

| Función | Firma | Responsabilidad |
|---|---|---|
| `fn_pst_resolver_partida` | `(p_company_id, p_cuenta, p_fecha, p_requiere_aprobado) → (id_presupuesto, con_cuenta_code)` | Resuelve **una** partida vigente a esa fecha. Criterio idéntico al que ya usa `fn_pst_afectar_saldo_real_credito`: `ORDER BY h.fecha_inicia DESC, h.id_presupuesto DESC LIMIT 1` |
| `fn_pst_disponible` | `(p_company_id, p_cuenta, p_fecha) → NUMERIC` | Disponible real de la partida. **Solo lectura, sin lock** — la usa la UI para el panel previo |
| `fn_alm_oc_distribucion_partidas` | `(p_company_id, p_orden_compra_id) → SETOF pst_linea_afectacion` | Distribuye la O/C por partida. Replica la regla del asiento: `costo_unitario × cantidad_pedida` agrupado por cuenta, más el remanente (ISV, otros gastos, descuento global) capitalizado en la cuenta de mayor valor, de modo que **Σ líneas = `alm_orden_compra.total`** |
| `fn_alm_compra_distribucion_partidas` | `(p_company_id, p_compra_hdr_id) → SETOF pst_linea_afectacion` | Lo mismo para la **factura**. Debe devolver exactamente el DEBE del asiento de `CompraContabilidad` (test de equivalencia obligatorio, §9.10) |
| `fn_pst_recalcular_cabecera` | `(p_company_id, p_id_presupuesto)` | Recalcula `hdr.valor_comprometido` y `hdr.valor_disponible` |

### 5.3 Procedimientos de negocio

#### `sp_pst_comprometer_documento`

```sql
sp_pst_comprometer_documento(
    p_company_id       BIGINT,
    p_modulo           VARCHAR,
    p_documento_tipo   VARCHAR,
    p_documento_id     BIGINT,
    p_documento_numero VARCHAR,
    p_fecha            DATE,
    p_usuario          VARCHAR,
    p_usuario_aprobo   VARCHAR,
    p_ip               VARCHAR,
    p_lineas           pst_linea_afectacion[]
) RETURNS TABLE (con_cuenta_code VARCHAR, disponible NUMERIC, requerido NUMERIC, exceso NUMERIC, excedio BOOLEAN)
```

Secuencia interna:

1. Leer `modo` de `cfg_presupuesto_control` para `(company_id, 'COMPRAS_OC')`. **Si `modo = 0` → RETURN sin
   hacer nada** (comportamiento idéntico al de hoy).
2. **Consolidar** `p_lineas` por `(cuenta, centro_costo)` sumando montos — una O/C puede traer varios renglones
   contra la misma partida y deben validarse juntos, no uno por uno.
3. **Ordenar el resultado por `con_cuenta_code`** — determinístico, para evitar deadlocks entre dos
   aprobaciones simultáneas que toquen las mismas partidas en distinto orden (§7).
4. Por cada línea, en ese orden:
   a. Descartar si la cuenta no tiene `allows_budget = true` en `con_plan_cuenta`.
   b. `fn_pst_resolver_partida(...)` con `p_requiere_aprobado = exige_presupuesto_aprobado`.
      Si no hay partida → excepción en modo 2, aviso en modo 1.
   c. `SELECT … FROM pst_config_presupuesto_dtl … FOR UPDATE` sobre esa fila. **Aquí se serializa.**
   d. `disponible := valor_proyeccion − valor_comprometido − valor_real`.
   e. Si `monto > disponible`: excepción `P0001` en modo 2; en modo 1 registrar y marcar `excedio = true`.
   f. `UPDATE … SET valor_comprometido = valor_comprometido + monto, valor_disponible = GREATEST(…, 0)`.
   g. `INSERT INTO pst_compromiso (…) ON CONFLICT (uq…) DO UPDATE SET monto_comprometido = …` — idempotente.
   h. `INSERT INTO pst_movimiento (…)` con tipo 1 y los ocho saldos antes/después.
5. `fn_pst_recalcular_cabecera` por cada presupuesto tocado.
6. Devolver los avisos (vacío si todo entró holgado).

**Mensaje de error normalizado** (lo consume el servicio C# y lo muestra la UI):
`El renglón excede el presupuesto disponible para la cuenta {cuenta}. Disponible: {x}. Requerido: {y}. Faltan: {z}.`

#### `sp_pst_liberar_compromiso`

```sql
sp_pst_liberar_compromiso(
    p_company_id, p_modulo, p_documento_tipo, p_documento_id,
    p_motivo VARCHAR, p_usuario VARCHAR, p_ip VARCHAR,
    p_solo_saldo BOOLEAN DEFAULT true      -- true = libera solo lo pendiente (cancelación)
)
```

Recorre los `pst_compromiso` vigentes del documento **bajo `FOR UPDATE`**, calcula el saldo
(`monto_comprometido − monto_devengado − monto_liberado`), lo suma a `monto_liberado`, resta el mismo importe a
`dtl.valor_comprometido`, marca el compromiso como estado 9 y escribe un `pst_movimiento` tipo 2 por partida.
**Resuelve directamente el caso §6 del requerimiento**: O/C de 100,000 con 60,000 recibidos libera 40,000, no
100,000, porque el saldo ya descuenta lo devengado.

La liberación **no exige** que el presupuesto esté aprobado ni vigente: se puede cancelar una O/C contra un
presupuesto ya cerrado. Devolver dinero nunca debe estar bloqueado.

#### `sp_pst_ajustar_compromiso`

Para la modificación de una O/C ya aprobada (§6 del requerimiento). Recibe la **nueva** distribución completa,
la compara contra los `pst_compromiso` vigentes del documento y por cada partida:

- `nuevo > actual` → valida disponible por la **diferencia** y compromete solo el delta (movimiento tipo 1).
- `nuevo < actual` → libera el delta (tipo 2), **nunca por debajo de lo ya devengado** (`I5`).
- partida que desaparece → libera su saldo completo.
- partida nueva → valida y compromete el total.

Ejemplo del requerimiento: 100,000 → 130,000 compromete 30,000; 100,000 → 80,000 libera 20,000. Si ya se
recibieron 90,000, bajar a 80,000 se rechaza con *"no se puede reducir por debajo de lo ya recibido"*.

#### `sp_pst_devengar_documento`

```sql
sp_pst_devengar_documento(
    p_company_id, p_documento_tipo, p_documento_id, p_documento_numero,
    p_orden_compra_id BIGINT,      -- NULL en compra directa
    p_fecha DATE, p_usuario, p_ip,
    p_lineas pst_linea_afectacion[]
)
```

Dos caminos:

- **Con O/C** — por cada partida, busca los `pst_compromiso` vigentes de esa O/C (`FOR UPDATE`, orden por id) y
  consume hasta el saldo: `monto_devengado += x`, `dtl.valor_comprometido −= x`, `dtl.valor_real += x`.
  Disponible sin cambio. Registra `pst_movimiento` tipo 3 y `pst_compromiso_aplicacion`.
  - **Excedente sobre el compromiso** (la factura vino por más que la O/C: variación de precio, flete no
    previsto): el sobrante se trata como devengo directo (tipo 5) y **sí valida disponible**, con la
    `tolerancia_pct` de `cfg_presupuesto_control` como margen exento. Es el punto donde un proveedor no puede
    facturar de más sin que alguien lo autorice.
- **Sin O/C** (compra directa, `p_orden_compra_id IS NULL`) — según `permite_devengo_sin_oc`: rechaza (0),
  valida disponible y consume (1), o solo advierte (2). Movimiento tipo 5.

#### `sp_pst_revertir_devengo`

Anulación de la factura. Devuelve `valor_real` y, **si la O/C sigue abierta**, restituye el compromiso
(`monto_devengado −= x`, `dtl.valor_comprometido += x`) para que se pueda volver a recibir. Si la O/C ya está
Cerrada, Cancelada o Anulada, el importe se libera al disponible en vez de restituirse. Movimiento tipo 4 o 6.

#### `sp_pst_registrar_pago` / `sp_pst_revertir_pago`

Mueven solo `valor_pagado` y escriben el movimiento tipo 7/8. **No alteran el disponible** — el pago no es un
hecho presupuestario, es un hecho de tesorería; se registra para el reporte de ejecución y la conciliación
contra bancos.

---

## 6. Reglas de negocio

Formato: **Evento → Validación → Movimiento presupuestario → Resultado**.

| # | Evento | Validación | Movimiento | Resultado |
|---|---|---|---|---|
| **R1** | Crear O/C | Renglones válidos, proveedor activo, fecha de entrega ≥ fecha de orden | **Ninguno** | O/C = Borrador. Se muestra el disponible de cada partida como referencia, sin bloquear |
| **R2** | Editar O/C en Borrador | Estado = Borrador | **Ninguno** | O/C actualizada |
| **R3** | **Aprobar O/C** | Estado = Borrador · tiene renglones · permiso `compras.ordenes_compra.aprobar` · **toda partida con disponible ≥ monto** | **Compromiso (+)** por partida | O/C = Aprobada; `aprobado_por`, `fecha_aprobacion`; N filas en `pst_compromiso` y `pst_movimiento` |
| **R3-e** | Aprobar sin disponible | Falla en al menos una partida | **Ninguno** (rollback) | Excepción de negocio con cuenta, disponible y faltante. **La O/C sigue en Borrador** |
| **R4** | Rechazar O/C | Estado = Borrador · motivo obligatorio | **Ninguno** | O/C = Rechazada (5) |
| **R5** | Anular O/C aprobada **sin** recepciones | Estado = Aprobada · `cantidad_aplicada = 0` en todos los renglones | **Liberación (−)** del total | O/C = Anulada (9); disponible restituido íntegro |
| **R6** | **Cancelar O/C con recepción parcial** | Estado = Recibida parcial · motivo obligatorio | **Liberación (−)** del **saldo pendiente** | O/C = Cancelada (6). Ejemplo: 100,000 pedidos, 60,000 recibidos → libera 40,000 |
| **R7** | Modificar O/C aprobada | Permiso específico · el nuevo monto por partida ≥ lo ya devengado en ella | **Ajuste (±)** solo por el delta | O/C actualizada; delta validado si es aumento |
| **R8** | **Recepción/factura parcial** | O/C Aprobada o Recibida parcial · cantidad ≤ pendiente · variación ≤ `tolerancia_pct` | **Devengo**: comprometido − / ejecutado + | O/C = Recibida parcial; `cantidad_aplicada` sube; **disponible sin cambio** |
| **R9** | Recepción/factura total | Igual que R8 | **Devengo** por el resto | O/C = Cerrada (4); compromisos en estado 2 |
| **R10** | Anular factura | Factura vigente · CxP sin abonos vigentes | **Reversa de devengo** | Kardex, CxP, asiento y presupuesto revertidos en una sola transacción; el compromiso vuelve si la O/C sigue abierta |
| **R11** | **Compra directa sin O/C** | Según `permite_devengo_sin_oc` (0 prohíbe · 1 valida disponible y consume · 2 advierte) | **Devengo directo (+ ejecutado, − disponible)** | Cierra el hueco H5: no se puede evadir el control comprando sin O/C |
| **R12** | Facturar por más que la O/C | Exceso > `tolerancia_pct` sobre el compromiso | **Devengo directo** por el exceso, validado contra disponible | Se registra si hay disponible; si no, se rechaza la factura completa |
| **R13** | Pagar CxP | CxP vigente con saldo | **Pagado (+)** | Informativo; disponible sin cambio |
| **R14** | Anular pago | Abono vigente | **Reversa de pagado** | |
| **R15** | Cerrar O/C manualmente | Estado = Recibida parcial · permiso · motivo | **Liberación (−)** del saldo | O/C = Cerrada (4) |
| **R16** | Ampliar / reducir presupuesto | Permiso de presupuesto · reducción ≥ (comprometido + ejecutado) de la partida | **Ampliación (10) / Reducción (11)** | `valor_proyeccion` cambia; queda registrado en el kardex |
| **R17** | Cerrar el presupuesto del ejercicio | — | **Ninguno** | Los compromisos vigentes quedan visibles como saldo a arrastrar. **Ver D6** |

---

## 7. Integridad y concurrencia

### 7.1 El escenario a evitar

> Disponible L 100,000. Usuario A aprueba una O/C por L 80,000 mientras el usuario B aprueba una por L 70,000.
> El sistema **no debe** permitir ambas.

### 7.2 Mecanismo

**Capa 1 — Transacción real en la aprobación.** `AprobarAsync` hoy no abre transacción (H4). Debe abrirla con
`TransaccionAmbiente.IniciarAsync` —el patrón del módulo, compatible con los tests `BEGIN…ROLLBACK`— y
envolver el cambio de estado y la llamada al SP.

**Capa 2 — `SELECT … FOR UPDATE` sobre la fila de la partida.** Es la defensa real. La transacción de A toma
el lock exclusivo de la fila `pst_config_presupuesto_dtl`; B queda esperando en el `SELECT`, no leyendo un
valor obsoleto. Cuando A hace COMMIT, B lee `valor_comprometido` **ya actualizado**, calcula
`disponible = 20,000 < 70,000` y falla con un error de negocio limpio.

```
t0  A: BEGIN                                   B: BEGIN
t1  A: SELECT … FOR UPDATE  -> disponible 100k
t2                                             B: SELECT … FOR UPDATE  -> BLOQUEADO
t3  A: UPDATE comprometido = 80k
t4  A: COMMIT
t5                                             B: (desbloquea) disponible = 20k
t6                                             B: 20k < 70k -> EXCEPCIÓN -> ROLLBACK
```

**Capa 3 — Orden determinístico de bloqueo.** Con O/C multi-partida, dos aprobaciones que tomen las mismas
partidas en distinto orden se abrazan (deadlock). Se resuelve ordenando **siempre** por `con_cuenta_code`
antes del bucle (paso 3 de `sp_pst_comprometer_documento`).

**Capa 4 — Idempotencia.** El índice único de `pst_movimiento` y el `ON CONFLICT` de `pst_compromiso` hacen
que un reintento (doble clic, retry de HTTP, reenvío del cliente) no duplique el compromiso.

**Capa 5 — Aislamiento.** `READ COMMITTED` (el default de PostgreSQL) es **suficiente y preferible** con
`FOR UPDATE` explícito. No se usa `SERIALIZABLE`: es lo que hace hoy el OPD y produce `40001` no manejados →
500 crudos. Con `FOR UPDATE` no hay serialization failure que manejar.

**Capa 6 — Rollback total.** Todo el enganche corre dentro de la transacción del documento. En la recepción
esto ya está garantizado: un `throw` revierte kardex, CxP, correlativo, `cantidad_aplicada`, asiento y
presupuesto de una sola vez.

**Capa 7 — `statement_timeout`** acotado en el SP para que una espera de lock no cuelgue una sesión web
indefinidamente; se traduce a un mensaje *"otro usuario está aprobando contra la misma partida, intente de nuevo"*.

### 7.3 Conciliación

Consulta de verificación de invariantes (I1/I2), para ejecutar periódicamente y tras cada despliegue:

```sql
SELECT d.company_id, d.id_presupuesto, d.con_cuenta_code,
       d.valor_comprometido AS materializado,
       COALESCE(c.saldo, 0)  AS calculado,
       d.valor_comprometido - COALESCE(c.saldo, 0) AS diferencia
  FROM pst_config_presupuesto_dtl d
  LEFT JOIN (
        SELECT company_id, id_presupuesto, con_cuenta_code,
               SUM(monto_comprometido - monto_devengado - monto_liberado) AS saldo
          FROM pst_compromiso WHERE estado = 1
         GROUP BY 1,2,3
  ) c USING (company_id, id_presupuesto, con_cuenta_code)
 WHERE d.valor_comprometido <> COALESCE(c.saldo, 0);
```

---

## 8. Auditoría

| Requisito | Dónde queda |
|---|---|
| Quién creó el movimiento | `pst_movimiento.usuario` |
| Quién aprobó | `pst_movimiento.usuario_aprobo` + `alm_orden_compra.aprobado_por` |
| Fecha y hora | `pst_movimiento.fecha_registro` (timestamp) y `fecha` (efecto presupuestario — pueden diferir) |
| Documento relacionado | `documento_tipo` + `documento_id` + `documento_numero` + `orden_compra_id` |
| Valor anterior | Los cuatro campos `*_anterior` |
| Valor nuevo | Los cuatro campos `*_posterior` |
| Motivo | `observacion` — **obligatoria** en liberaciones, cancelaciones y reversas |
| IP / info técnica | `pst_movimiento.ip` — **requiere pasar el `HttpContext` desde el controlador**; hoy los servicios no lo reciben. Ver **D7** |
| Cambios al presupuesto mismo | Movimientos 10/11 + la **bitácora de maestros** ya existente (interceptor EF), habilitando `pst_config_presupuesto_hdr/_dtl` en su catálogo |

Retención: `pst_movimiento` no se purga. Es el libro de la ejecución presupuestaria y debe sobrevivir al
cierre del ejercicio.

---

## 9. Casos de prueba

`SIAD.Tests/Presupuesto/CompromisoOrdenCompraTests.cs` y `SIAD.Tests/Almacen/`, patrón `BEGIN … ROLLBACK`.

| # | Caso | Resultado esperado |
|---|---|---|
| 1 | `modo = 0` (apagado) | La O/C se aprueba sin consultar presupuesto; `pst_movimiento` vacío. **No-regresión** |
| 2 | Cuenta sin `allows_budget` | Se ignora en silencio; la O/C se aprueba |
| 3 | Presupuesto suficiente | Aprueba; `valor_comprometido` sube; `valor_disponible` baja; 1 fila en `pst_compromiso` y en `pst_movimiento` por partida |
| 4 | Presupuesto insuficiente, modo 2 | Rechaza; **la O/C sigue en Borrador**; cero filas escritas |
| 5 | Presupuesto insuficiente, modo 1 | Aprueba; `excedio = true`; avisos devueltos |
| 6 | Cuenta presupuestable sin presupuesto vigente | Falla en modo 2; avisa en modo 1 |
| 7 | Presupuesto vigente **no aprobado** | Falla si `exige_presupuesto_aprobado` |
| 8 | **O/C multi-partida** (20k + 35k + 45k) | 3 compromisos; si **una sola** partida no alcanza, **nada se aprueba** |
| 9 | Dos renglones contra la misma partida | Se consolidan y se validan **juntos** (no uno a uno) |
| 10 | Recepción parcial (60 de 100) | Comprometido 40, ejecutado 60, **disponible sin cambio**; O/C = Recibida parcial |
| 11 | Recepción total | Comprometido 0, ejecutado 100; O/C = Cerrada |
| 12 | **Cancelar O/C con 60 recibidos** | Libera **40**, no 100; O/C = Cancelada |
| 13 | Anular O/C aprobada sin recepciones | Libera 100; disponible restituido |
| 14 | Modificar O/C 100 → 130 | Compromete **solo 30**; falla si no hay disponible para los 30 |
| 15 | Modificar O/C 100 → 80 | Libera **20** |
| 16 | Modificar O/C 100 → 50 con 90 recibidos | **Rechaza**: no se puede reducir por debajo de lo devengado |
| 17 | Anular factura | Reversa de devengo; el compromiso se restituye; nada más queda (kardex, CxP, asiento) |
| 18 | Anular factura de una O/C ya cerrada | El importe va a disponible, no a comprometido |
| 19 | Reintento del alta con el mismo `uuid` | No duplica (idempotencia) |
| 20 | Anular dos veces | No libera dos veces |
| 21 | Compra directa sin O/C, `permite_devengo_sin_oc = 1` | Consume disponible; movimiento tipo 5 |
| 22 | Compra directa, `= 0` | Rechaza la factura |
| 23 | Factura por más que la O/C, dentro de tolerancia | Pasa sin validación extra |
| 24 | Factura por más que la O/C, fuera de tolerancia y sin disponible | Rechaza |
| 25 | **Concurrencia**: disponible 100k, A pide 80k y B pide 70k simultáneos | Una aprueba, la otra falla con **mensaje de negocio**, no un 500 |
| 26 | **Deadlock**: dos O/C multi-partida con las mismas partidas en orden inverso | Ninguna se abraza (orden determinístico) |
| 27 | Pago de la CxP | `valor_pagado` sube; disponible **sin cambio** |
| 28 | Reducir presupuesto por debajo de comprometido + ejecutado | Rechaza |
| 29 | **Equivalencia** `fn_alm_compra_distribucion_partidas` vs. el DEBE de `CompraContabilidad` | Mismas cuentas y mismos montos |
| 30 | Aislamiento multiempresa | Empresa 3 no ve ni afecta partidas de la empresa 2 |

---

## 10. Reportes

| Reporte | Fuente | Columnas |
|---|---|---|
| **Ejecución presupuestaria** | `vw_pst_ejecucion_presupuestaria` | Partida · Descripción · Presupuesto · Comprometido · Ejecutado · Pagado · Disponible · % ejecución · % compromiso |
| **Compromisos pendientes** | `vw_pst_compromiso_saldo` (estado 1) | O/C · Fecha · Proveedor · Partida · Comprometido · Devengado · **Saldo** · Días de antigüedad |
| **Historial de una partida (kardex)** | `vw_pst_movimiento_detalle` | Fecha · Usuario · Tipo · Documento · No. O/C · Saldo anterior · Monto · Saldo posterior · Comprometido · Ejecutado · Liberado · Estado · Observación |
| **Presupuesto por centro de costo** | `vw_pst_ejecucion_centro_costo` | Centro de costo · Presupuesto · Comprometido · Ejecutado · Disponible *(informativo mientras rija D3-A)* |
| **Conciliación de invariantes** | Consulta de §7.3 | Partida · Materializado · Calculado · Diferencia — debe salir **vacío** |

Los tres primeros como pantalla Blazor con `DxGrid` según el estándar del repositorio, más export a PDF/Excel
por DevExpress. El de compromisos pendientes es además la herramienta operativa para depurar O/C viejas que
están reteniendo presupuesto sin que nadie las cierre.

---

## 11. Plan de implementación por fases

| Fase | Alcance | Entregable | Riesgo |
|---|---|---|---|
| **F0 — Confirmaciones** | Resolver **D1** (contra qué cuenta muerde) y **D3** (centro de costo) con el contador. Medir `allows_budget` en el servidor real | Acta de decisiones | **Bloqueante para que el control sirva**, no para construirlo |
| **F1 — Base de datos** | Columnas nuevas en `pst_config_presupuesto_*` y `alm_orden_compra_detalle`; tablas `pst_compromiso`, `pst_compromiso_aplicacion`, `pst_movimiento`, `cfg_presupuesto_control`; tipo, funciones, 7 SP, 4 vistas, trigger de inmutabilidad; estados 5 y 6 en el CHECK de la O/C | Scripts en `Database/` + registro en el runbook SRV (skill `runbook-despliegue-srv`) + scaffold de entidades | Aditivo y reversible |
| **F2 — Backend del compromiso** | `IPresupuestoCompromisoService` + transacción en `AprobarAsync` + enganche; permisos nuevos; DTOs | Servicio, DI, tests 1–9, 25, 26, 30 | Toca `AprobarAsync`, que hoy no es transaccional |
| **F3 — Backend del ciclo** | Devengo en la recepción, reversa en la anulación, `CancelarAsync`, `CerrarAsync`, `ModificarAprobadaAsync`, pago | Tests 10–24, 27, 28 | Toca el flujo crítico de recepción |
| **F4 — Frontend** | Panel de disponible por partida en la O/C **antes** de aprobar · selector de cuenta presupuestaria y centro de costo por renglón · mensajes de rechazo con cuenta/disponible/faltante · botones Cancelar y Cerrar · badge de estado presupuestario | `OrdenCompraFormPage.razor`, `OrdenesCompraList.razor` | — |
| **F5 — Consulta y reportes** | Las 4 pantallas/reportes de §10 + pantalla de configuración del modo | Pantallas + PDF/Excel | — |
| **F6 — Pruebas y piloto** | Suite completa; encender en **modo 1 (Advertencia)** en producción y observar un ciclo mensual | Informe de sobregiros detectados | — |
| **F7 — Puesta en producción** | Pasar a **modo 2 (Bloqueo)**; capacitación; monitoreo de la consulta de conciliación | Runbook de corte | — |
| **F8 — Opcional** | Migrar `OrdenesPagoDirectoService` y `BanTransaccionesService` al mismo motor, para que **todo** movimiento presupuestario pase por `pst_movimiento` | Refactor + regresión | Archivo de 231 KB en flujo crítico |

### 11.1 Migración y datos iniciales (Fase 7 del requerimiento)

- **`valor_comprometido` nace en 0.** No hay compromisos históricos que migrar: el concepto no existía.
- **Decisión abierta (D8):** ¿las O/C **hoy aprobadas y no recibidas** deben comprometer retroactivamente?
  Si sí, un script de backfill las recorre y llama a `sp_pst_comprometer_documento` en **modo 1** (para que no
  falle ninguna) y deja el sobregiro visible. Si no, arrancan con compromiso 0 y el presupuesto se ve más
  disponible de lo que realmente está durante un ciclo.
- **Prerrequisito duro:** los pasos 14, 15 y 18 del runbook (multitenant de presupuesto y `valor_real`) siguen
  **pendientes en el SRV** (`Database/2026-07-30_pendientes_srv.md`). Este diseño depende de esas tablas al día.
- **Plan de rollback:** poner `modo = 0` desactiva el control completo sin desplegar nada. Los objetos nuevos
  son aditivos; revertirlos es DROP de tablas vacías. El único cambio no trivialmente reversible es la fórmula
  de `valor_disponible`, y solo importa si `valor_comprometido > 0`.

---

## 12. Riesgos

| # | Riesgo | Mitigación |
|---|---|---|
| **R1** | **Las cuentas de inventario no están presupuestadas ni marcadas** → el control sería un no-op silencioso | F0 obligatoria. La consulta de ejecución mostrará movimiento cero y lo delata de inmediato |
| **R2** | **Doble conteo con el compromiso a proveedor (OPD)**: una misma compra registrada como O/C y como compromiso de proveedor consumiría presupuesto dos veces | Es un riesgo **real y presente**. Documentar la separación de usos; F8 lo elimina al unificar el motor |
| **R3** | Compra directa sin O/C evade el control | `permite_devengo_sin_oc` + devengo directo (R11). Cerrado por diseño |
| **R4** | `ImportPlanCuentasAsync` (`ContabilidadCatalogosService.cs:1444`) pone `allows_budget = false` en **toda fila que procesa, también las que actualiza** → reimportar el plan apaga el control de 329 cuentas | **Corregir antes de F7.** Es un defecto preexistente que este diseño vuelve crítico |
| **R5** | Presupuestos solo anuales (`rango_periodo` = 12); no hay control mensual ni trimestral | Fuera de alcance; agregar el eje de período es otro proyecto (**D4**) |
| **R6** | Cambiar la fórmula de `valor_disponible` afecta pantallas y reportes existentes | Nulo mientras `valor_comprometido = 0`; se despliega con el control apagado |
| **R7** | Espera de lock visible al usuario en aprobaciones simultáneas | `statement_timeout` + mensaje claro. La alternativa (no bloquear) es justo lo que se pide evitar |
| **R8** | La O/C no lleva moneda; `alm_compra_hdr` sí (`moneda`, `tasa_cambio`). El presupuesto está en lempiras | **D9**: definir si se comprometen O/C en moneda extranjera y a qué tasa |
| **R9** | Duplicación temporal de la regla de distribución entre `fn_alm_compra_distribucion_partidas` y `CompraContabilidad` | Test de equivalencia (caso 29); unificar en F8 |
| **R10** | Bloquear sin mostrar el disponible es hostil y genera rechazo del usuario | F4 debe acompañar a F2, no ir después |

---

## 13. Información faltante — decisiones abiertas

| # | Decisión | Por qué importa | Alternativas |
|---|---|---|---|
| **D1** | **¿Contra qué cuenta muerde el compromiso?** | Define si el control sirve. Hoy el asiento debita `cuenta_inventario` (114\*), que **no está presupuestada** | **A)** Cuenta de inventario del tipo de artículo (coherente con el asiento, exige que el contador presupueste 114\*). **B)** Cuenta de gasto por renglón, capturada en la O/C (permite presupuestar donde ya hay presupuesto, pero se separa del asiento). **C)** Cuenta del **centro de costo/departamento** solicitante. **Recomendación: B**, con la cuenta propuesta desde el tipo y editable — es la única que funciona con el presupuesto real que existe hoy |
| **D2** | ¿El compromiso es por el **total** de la O/C (con ISV, flete, otros gastos) o solo por la base? | Cambia el monto comprometido en ~15% | **Recomendación: total**, pegado a lo que se le va a pagar al proveedor y a lo que debita el asiento |
| **D3** | **¿Se controla por centro de costo además de por cuenta?** | El catálogo existe pero el presupuesto **no tiene ese eje**. Agregarlo cambia la PK de `pst_config_presupuesto_dtl` y toda la pantalla de presupuesto | **A)** Centro de costo informativo (se guarda y se reporta, no valida) — **recomendada para la primera entrega**. **B)** Eje real: PK pasa a `(company_id, id_presupuesto, con_cuenta_code, centro_costo_id)`, con fila "sin centro de costo". Es un proyecto en sí mismo |
| **D4** | ¿Control **mensual/trimestral** o solo anual? | Hoy la vigencia es por rango de fechas del presupuesto anual | Anual (sin cambios) vs. agregar eje de período. **Recomendación: anual** en la primera entrega |
| **D5** | **Tolerancia de variación O/C → factura** | Sin ella, cualquier diferencia de centavos entre la O/C y la factura dispara validación extra | Definir `tolerancia_pct` (sugerido 5%) y si aplica por renglón o por total |
| **D6** | ¿Qué pasa con los compromisos vigentes **al cierre del ejercicio**? | Una O/C aprobada en diciembre que se recibe en enero | **A)** Arrastrar el compromiso al presupuesto siguiente (requiere lógica de traslado). **B)** Liberar al cierre y volver a comprometer contra el nuevo. **C)** Dejarlo vivo contra el presupuesto viejo. **Recomendación: B**, con un reporte previo de compromisos a arrastrar |
| **D7** | ¿Se registra la **IP** del usuario? | Requiere pasar `HttpContext` del controlador al servicio — hoy los servicios no lo reciben | Registrar solo usuario (menos invasivo) vs. agregar un `IRequestContextService` |
| **D8** | ¿Las O/C **ya aprobadas** al momento del despliegue comprometen retroactivamente? | Sin backfill, el presupuesto se ve más disponible de lo que está | **Recomendación:** backfill en modo Advertencia, revisar el informe y luego pasar a Bloqueo |
| **D9** | ¿O/C en **moneda extranjera**? | La O/C no tiene campo de moneda; la factura sí | Definir si se prohíbe, si se convierte a la tasa del día del compromiso, o si se ajusta al devengar |
| **D10** | ¿Existe un rol que pueda **aprobar excediendo** el presupuesto? | Caso frecuente en la práctica (emergencias) | Permiso `presupuesto.sobregiro.autorizar` + flag en el DTO; el sobregiro queda en `pst_movimiento` con `excedio = true` y motivo obligatorio |

**Nada de lo anterior bloquea F1–F5.** D1 y D3 sí bloquean que el control **sirva** en producción: se pueden
construir las siete fases y encontrarse con que ninguna orden se valida porque las cuentas involucradas no
están presupuestadas.
