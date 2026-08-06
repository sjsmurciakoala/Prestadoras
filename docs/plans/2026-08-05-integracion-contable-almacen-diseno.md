# Integración contable del almacén — diseño (2026-08-05)

**Objetivo:** que los movimientos de entrada/salida de almacén (`alm_*`) generen su **partida contable de doble entrada** en el libro mayor de Prestadoras (`con_partida`), reutilizando el andamiaje de integración contable que ya usan Ventas/Caja/Bancos/Notas/Proveedores. Hoy el almacén **valoriza el kardex pero no postea al mayor** (ver [`2026-07-14-motor-movimientos-almacen.md`](2026-07-14-motor-movimientos-almacen.md) §Fase 7: "el asiento deja los campos listos, pero no postea a `con_partida`").

**Modelo de referencia:** la otra base del usuario, `E:\Koala\Users\Dell\Documents\GitHub\HODSOFT_DEVEXPRESS\SIAD` (migración fiel del SIMAFI, modelo legacy `inv_*`/`cnt_*`), **sí contabiliza** cada movimiento de inventario. Sirve como especificación de la *semántica* del asiento (qué va al Debe/Haber), no de la *arquitectura* (Prestadoras es multitenant y tiene su propio motor). Ver `SIAD.Services/Inventario/InventarioTransaccionesGenericasService.cs:847-944` de esa base.

> **Estado: DISEÑO. Sin código ni SQL derivado todavía. Contiene decisiones abiertas (D1–D8) que requieren al contador.**

---

## 1. Modelo destino: el andamiaje que ya existe en Prestadoras

No hay que inventar nada nuevo de infraestructura; hay que **enchufar almacén** a lo que ya está:

| Pieza | Archivo | Rol |
|---|---|---|
| Libro mayor | `SIAD.Core/Entities/con_partida_hdr.cs` · `con_partida_dtl.cs` | Póliza (cabecera + líneas Debe/Haber) |
| Motor de pólizas | `SIAD.Services/Contabilidad/IPolizaService.cs` | `CrearAsync` (DRAFT) → `ValidarBalanceAsync` → `RegistrarAsync` (POSTED, actualiza saldos) → `RevertirAsync` (anulación) |
| Config por empresa | `SIAD.Core/Entities/con_integracion_config.cs` | Flags `activo_*` por módulo, modos, `encolar_sin_periodo`, `desfase_max_meses` |
| Diario + tipo de partida por módulo | `SIAD.Core/Entities/con_integracion_asiento.cs` | `module → journal_id, type_id` |
| Matriz de cuentas ("usos") | `SIAD.Core/Entities/con_integracion_cuenta.cs` | `(uso × servicio × categoría × medición) → account_id`, resuelta por `fn_con_resolver_cuenta` |
| Cola de regularización | `SIAD.Core/Entities/con_partida_pendiente.cs` | Si no hay período abierto, se encola (`payload` JSON, `status_id` 1=PEND/2=PROC/3=DESC) y se reprocesa |
| Admin de la config | `SIAD.Services/Contabilidad/IIntegracionContableService.cs` | UI de configuración (ya existe para los otros módulos) |

**Usos de cuenta existentes** (`SIAD.Core/DTOs/Contabilidad/IntegracionContableDtos.cs`, `IntegracionContableUsos`): CXC, INGRESO, CAJA, BANCO_DEFAULT, ISV, DESCUENTO, RECARGO_MORA, PREVISION_INCOBRABLE, GASTO_INCOBRABLE, RESULTADO_EJERCICIO, RESULTADO_ACUMULADO, DEVOLUCION_NC, TRANSITORIA. **Ninguno de inventario.**

**Módulos existentes** (`IntegracionContableModulos`): VENTAS, CAJA, BANCOS, NOTAS, MISCELANEOS, PROV. **Falta ALMACÉN/INVENTARIO.**

---

## 2. Fuentes de cuentas en el modelo `alm_*` (ventaja sobre el legacy)

El legacy tenía **una** cuenta por producto (`inv_productos.cuenta_contable`) y obligaba a teclear la contrapartida a mano. Prestadoras ya tiene mucho más, lo que permite **derivar el asiento automáticamente**:

- `alm_tipo_articulo` (heredadas por el artículo, solo lectura): `cuenta_inventario`, `cuenta_costo_ventas`, `cuenta_ventas`, `cuenta_ajustes`, `cuenta_devoluciones`.
- `alm_tipo_movimiento.cuenta_contable`: **override** de contrapartida por concepto (p. ej. Donación, Merma) — NULL = usar la que corresponda por defecto.
- Contrapartidas genéricas que no dependen del artículo (CxP del proveedor, cuenta de consumo por centro de costo): vía **nuevos "usos"** en `con_integracion_cuenta`.

---

## 3. Diseño propuesto

### 3.1 Punto de enganche

El posteo contable se engancha **en cada servicio de documento** (no dentro del motor `InventarioPostingService`, que es agnóstico y no conoce la contrapartida), **dentro de la misma transacción** en la que se postea el kardex, justo después de `InventarioPostingService.PostearAsync`:

- `RecepcionCompraService` → asiento de compra
- `DescargoDocumentoService` → asiento de consumo/salida
- `AjusteInventarioService` / `MovimientoAlmacenService` → asiento de ajuste
- `TrasladoAlmacenService` → asiento de traslado (si aplica, ver D6)
- `CargaInicialInventarioService` → asiento de apertura (ver D7)

Patrón por servicio: resolver cuentas → armar `List<PolizaLineaCrearDto>` (Debe/Haber) → `IPolizaService.CrearAsync(module="ALMACEN", documentType, documentId, …)` → `RegistrarAsync`. Si `ValidarPeriodoAbierto` falla y `encolar_sin_periodo=true`, encolar en `con_partida_pendiente` con el `payload` del asiento.

### 3.2 Mapeo de asientos (Debe / Haber) por naturaleza de movimiento

Monto = `cantidad × costo` (el costo lo entrega el motor: entradas al costo de entrada, salidas al promedio vigente; base **sin ISV**).

| Movimiento | Debe | Haber | Notas |
|---|---|---|---|
| **Compra (recepción)** | `cuenta_inventario` (tipo) | CxP proveedor **o** cuenta puente compras (D2) | ISV según D3 |
| **Ajuste positivo / sobrante** | `cuenta_inventario` | `cuenta_ajustes` (u override del concepto) | |
| **Ajuste negativo / merma** | `cuenta_ajustes` (u override) | `cuenta_inventario` | |
| **Ajuste de valor (+)** | `cuenta_inventario` | `cuenta_ajustes` | solo Δvalor; existencia no cambia |
| **Descargo / consumo interno** | Cuenta de gasto/consumo (D4) | `cuenta_inventario` | centro de costo/departamento del descargo |
| **Salida por venta (COGS)** | `cuenta_costo_ventas` | `cuenta_inventario` | **solo si se conecta ventas↔inventario (D5)** |
| **Traslado entre bodegas** | `cuenta_inventario` destino | `cuenta_inventario` origen | sin efecto si ambas bodegas comparten cuenta (D6) |
| **Carga inicial** | `cuenta_inventario` | cuenta de apertura / `cuenta_ajustes` (D7) | |
| **Reversa / anulación** | (invierte el asiento original) | | vía `IPolizaService.RevertirAsync` |

### 3.3 Anulación e idempotencia

- **Anulación** = reversa contable (`RevertirAsync`), en paralelo a la reversa que el kardex ya hace. La póliza original queda inmutable.
- **Idempotencia:** el asiento se ata al documento (`module` + `documentType` + `documentId`); un reintento no debe duplicar la póliza (mismo criterio que el UUID del kardex).
- **Multitenancy:** todo scoped por `company_id`; `IPolizaService` ya lo exige.

---

## 4. Fases de implementación

| Fase | Alcance | Entregable |
|---|---|---|
| **F0 — Config** | Añadir `activo_almacen` a `con_integracion_config`; fila `con_integracion_asiento` módulo=ALMACÉN (journal+type); nuevos usos (`INVENTARIO`, `COSTO_INVENTARIO`, `AJUSTE_INVENTARIO`, `COMPRA_CONTRA`, …) en `IntegracionContableUsos` + matriz; UI de la pestaña Almacén en la config | SQL en `Database/` + servicio/UI de config |
| **F1 — Ajustes** | Contabilizar ajuste ± y ajuste de valor (el caso más limpio, contrapartida = `cuenta_ajustes`) | Enganche en `AjusteInventarioService`/`MovimientoAlmacenService` + tests |
| **F2 — Compras** | Contabilizar la recepción de compra (resuelto D2 y D3) | Enganche en `RecepcionCompraService` + tests |
| **F3 — Consumo** | Contabilizar descargos/requisiciones (resuelto D4) | Enganche en `DescargoDocumentoService` + tests |
| **F4 — Traslados** | Solo si D6 dice que generan partida | `TrasladoAlmacenService` |
| **F5 — COGS por venta** | Conectar la facturación al descargo de inventario + costo de ventas (resuelto D5) — **la más grande, probablemente proyecto aparte** | Ventas ↔ Almacén |
| **F6 — Carga inicial / histórico** | Apertura contable del inventario (resuelto D7); coordinar con el corte de inventario (Fase 8) | |

Cada script SQL nuevo se registra en el runbook de despliegue SRV (skill `runbook-despliegue-srv`).

---

## 5. Decisiones abiertas (requieren al contador / usuario)

| # | Decisión | Recomendación |
|---|---|---|
| **D1** | ¿Postear en línea o por cola? | Seguir el patrón de Prestadoras: intentar postear; si no hay período abierto y `encolar_sin_periodo`, encolar en `con_partida_pendiente`. |
| **D2** | **Contrapartida de la compra: ¿CxP del proveedor directo, o cuenta puente "mercadería por facturar"?** ⚠️ Riesgo de **doble contabilización** si el módulo PROV también postea el pasivo al recibir la factura. | Cuenta puente transitoria si recepción y factura son eventos separados; conciliar contra PROV. **Confirmar con el contador y con el flujo real de proveedores.** |
| **D3** | **ISV en compras: ¿se capitaliza al costo (hoy) o se separa a crédito fiscal cuando la política es FISCAL?** | Hoy va al costo (`alm_tipo_articulo` dice "crédito fiscal fuera de alcance"). Si el contador quiere crédito fiscal, agregar línea a cuenta de ISV por cobrar. Ver [`almacen-isv-compras-por-tipo`]. |
| **D4** | Salida por consumo: ¿cuenta de gasto única (`cuenta_costo_ventas`) o por **centro de costo/departamento** del descargo? | Por centro de costo (como el legacy), resuelto vía `con_integracion_cuenta` uso `CONSUMO`. Confirmar de dónde sale el centro de costo en el descargo. |
| **D5** | **¿El costo de ventas (COGS) se postea desde Ventas o desde Almacén?** Hoy facturación y almacén están desacoplados (la venta no descarga inventario). | Proyecto aparte (F5). Definir si se conecta la facturación al descargo. Riesgo de doble descuento si ambos módulos tocan inventario. |
| **D6** | ¿Los traslados generan partida? | Solo si la cuenta de inventario difiere por bodega. Hoy la cuenta es por **tipo de artículo**, no por bodega ⇒ traslado sin efecto contable. Confirmar si se quiere cuenta de inventario por bodega. |
| **D7** | Carga inicial: ¿contra qué cuenta (apertura/capital/ajustes)? ¿Los históricos ya migrados generan partida retroactiva? | Solo movimientos nuevos; el histórico se maneja con el corte de inventario (Fase 8), no retroactivo. Cuenta de apertura a definir. |
| **D8** | Granularidad de la cuenta de inventario: ¿por tipo de artículo (hoy) es suficiente, o se necesita por artículo/bodega? | Por tipo, salvo que el contador exija más detalle. |

---

## 6. Riesgos

- **Doble contabilización** (D2/D5): el mayor riesgo. Almacén no debe postear un pasivo o un COGS que otro módulo (PROV, Ventas) ya postea. Mapear el flujo completo antes de F2/F5.
- **Período contable**: los movimientos de almacén son frecuentes; si el período contable va desfasado del comercial, la cola `con_partida_pendiente` se llenará. Vigilar `desfase_max_meses`.
- **Cuentas sin configurar**: si el `alm_tipo_articulo` no tiene `cuenta_inventario`, el asiento no cuadra. Validar en F0 (como hace el legacy, que rechaza el movimiento si el producto no tiene cuenta).
- **Regla "sin LINQ"**: el motor contable de Prestadoras (`PolizaService`) es la vía; seguir su patrón de acceso a datos, no meter LINQ de escritura.

---

## 7. Referencias

- Estado actual (sin contabilidad): [`2026-07-14-motor-movimientos-almacen.md`](2026-07-14-motor-movimientos-almacen.md), [`2026-08-04-entradas-salidas-almacen-handoff.md`](2026-08-04-entradas-salidas-almacen-handoff.md)
- Plan de integración contable-comercial (el andamiaje): [`2026-07-02-plan-integracion-contable-comercial.md`](2026-07-02-plan-integracion-contable-comercial.md), [`../handoff-integracion-contable-2026-07.md`](../handoff-integracion-contable-2026-07.md)
- Base de referencia con contabilidad de inventario: `E:\Koala\Users\Dell\Documents\GitHub\HODSOFT_DEVEXPRESS\SIAD\SIAD.Services\Inventario\InventarioTransaccionesGenericasService.cs` + `Accounting\LiquidacionService.cs`
- Motor de posteo de kardex (Prestadoras): [`InventarioPostingService.cs`](../../SIAD.Services/Almacen/InventarioPostingService.cs)
