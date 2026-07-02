# Plan: Apartado de Abonos en Caja (sin apertura/cierre) + Contabilidad

Fecha: 2026-06-08
Branch sugerido: `Modulo_Caja_1.0` (actual) o `feature/abonos-caja-contabilidad`
Estado: **PROPUESTA DE DISEÑO** (no implementado)

---

## 1) Objetivo

Agregar un apartado de **Abonos** para el cajero que permita registrar **pagos parciales** contra
**una** factura ya creada, **sin** requerir apertura/cierre de caja (sesión). Cada abono debe:

- Disminuir el saldo pendiente de la factura seleccionada.
- Afectar automáticamente las cuentas contables correspondientes (reutilizando el motor central).
- **No** generar saldo a favor: el monto del abono nunca puede exceder el saldo pendiente.

Adicionalmente, crear una **vista de mantenimiento** para configurar las cuentas contables que afecta
cada escenario de abono.

## 2) Decisiones tomadas (confirmadas con el usuario)

| Tema | Decisión |
|---|---|
| Ubicación de la pestaña | **Captación en Caja** (`/facturacion/captacion/caja`, `CaptacionPagos/Caja.razor`) — nueva 4ª pestaña "Abonos". |
| Concepto de abono | **Pago parcial a una factura**: el cajero busca la factura con saldo pendiente y registra un monto parcial; se permiten varios abonos hasta saldar. |
| Alcance del abono | **Solo a la factura seleccionada.** No hay derrame automático a otras facturas (sin FIFO multi-factura). |
| Recargos por mora | **No se cobran** al abonar. Lo que se cobra es el saldo tal cual. |
| Forma de pago | **No se define** al abonar (no se elige efectivo/banco). Hay un **único escenario contable** y un solo débito configurable; **sin** integración bancaria. |
| Excedente sobre el saldo | **No permitido / sin saldo a favor.** El monto del abono **no puede exceder** el saldo pendiente de la factura. No hay anticipos. La validación bloquea montos mayores. |
| Motor contable | **Reutilizar el motor central** (`sp_con_generar_comprobante` + `con_regla_integracion`), con un **único escenario** `ABONO` (sin forma de pago). Nada toca `con_saldo_cuenta` directo. |
| Tipo de documento contable | **Crear** un `module`/`document_type` nuevo para abonos (propuesto `module = CAJA`, `code = ABO`), en lugar de reutilizar `VENTAS`/`REC`. Es **fila semilla** en `cfg_document_type` (INSERT), **no** cambio de estructura. |
| Vista de mantenimiento | **Solo para abonos** (escenarios de §5). No es un editor general de `con_regla_integracion`. |
| Almacenamiento del recibo | **En tablas existentes, SIN modificar la estructura** (sin tablas ni columnas nuevas). Cada abono = un renglón en `transaccion_abonado` ligado a la factura. Ver §4.1. |
| Estado de factura parcial | Nuevo **valor** `factura.estado = 'B'` (a**B**ono) mientras la factura tenga saldo; `'C'` al saldarse. Es valor de datos (la columna ya existe), **no** cambio de estructura. Se elige `'B'` y **no** `'P'` porque `'P'` ya significa "Pagado" en `Caja.razor`/`PosteoMiscelaneos.razor` (badge verde / reverso). |
| Distinción del movimiento | Los abonos usan un `tipotransaccion` **propio** (p.ej. `'202'`) distinto del pago total `'201'`, para permitir **varios abonos** por factura sin chocar con la validación de pago único existente. |
| Sesión de caja | **No** se usa apertura/cierre. `transaccion_abonado.caja_id` queda `NULL` (es opcional). El cuadre del día se resuelve por reporte/arqueo (ver §9). |

## 3) Contexto técnico existente (reuso, no reinvención)

- Slice estándar: DTO (`SIAD.Core/DTOs`) → servicio (`SIAD.Services`) → controller (`apc/Controllers`) →
  cliente HTTP (`apc.Client/Services`) → página (`apc.Client/Pages`). Ver `Prestadoras/CLAUDE.md`.
- **Captación ya genera contabilidad automática** en lectoras/manual/misceláneos reutilizando el motor
  central (`CaptacionPagosService.GenerarComprobanteContableCaptacionAsync` →
  `sp_con_generar_comprobante`; reversa con `sp_con_revertir_poliza`). Ver
  `docs/PLAN_CONTABILIDAD_AUTOMATICA_CAPTACION_BANCOS.md`.
- Configuración contable por catálogos (sin hardcode por empresa):
  - `cfg_document_type` (tipos de documento contable activos por módulo).
  - `con_regla_integracion` (`module` + `document_type_id` + `scenario_code` → `debit_account_id` /
    `credit_account_id` / `cost_center_id`). **Ya existe la entidad y la tabla, pero NO tiene UI.**
  - `con_tipo_transaccion` (+ reglas), `con_diario`, `con_periodo_contable` (período abierto `status_id = 0`).
- Entidades clave de negocio:
  - `factura`: `saldototal`, `estado` ("A"=abierto, "C"=cobrado, "P"=pagado), `numrecibo`, `numfactura`,
    `clientecodigo`, `fechapago`, `recolectora`, `usuario`.
  - `factura_detalle`: `montovalor`, `montovalor_saldo` (saldo pendiente por línea), `tiposervicio`, `codigo`.
  - `transaccion_abonado`: kardex de cobros por cliente (`debitos`, `creditos`, `saldo`, `tipotransaccion`,
    `tipo_partida`, `banco`, `estado`, `fecha_docu`, `caja_id` opcional).
- Multiempresa **no negociable**: todo filtra/estampa `company_id` (ver `SiadDbContext.Tenancy.cs`);
  en páginas Blazor llamar `TenantState.EnsureCompanyAsync()` antes de cargar datos.

## 4) Flujo funcional del cajero (pestaña Abonos)

1. El cajero abre la pestaña **Abonos** dentro de *Captación en Caja*.
2. Busca al cliente o la factura (por clave de cliente / nº factura / nº recibo).
3. La UI muestra los datos del cliente, sus facturas con saldo (`factura.estado IN ('A','B')` y/o
   `factura_detalle.montovalor_saldo > 0`) y, al elegir una, su **saldo pendiente** y sus **abonos previos**.
4. El cajero selecciona **una** factura y captura el **monto del abono**. **No** se define forma de pago
   (no se elige efectivo/banco).
5. Validaciones: monto > 0, **monto ≤ saldo pendiente de la factura** (sin excedente), período contable
   abierto, regla de integración activa, factura con saldo.
6. Al confirmar, el backend (transaccional):
   - Aplica el abono a las líneas de la factura seleccionada (derrame por prioridad: más antiguas / orden de
     servicio configurable) reduciendo `montovalor_saldo`.
   - Si se salda toda la factura → `factura.estado = 'C'`, `fechapago`, `recolectora`, `usuario`.
     Si queda saldo → `factura.estado = 'B'` (valor nuevo; el saldo real vive en `montovalor_saldo`).
   - Inserta un `transaccion_abonado` (**el "recibo" del abono**) con `creditos = monto`, `caja_id = NULL`,
     `tipotransaccion` propio de abono (p.ej. `'202'`), `tipo_partida`, `descripcion`, `saldo` corriente, y los
     **enlaces a la factura**: `docufuente = factura.id`, `docufuente2 = numfactura`, `recibo = numrecibo`.
   - Genera y postea la **póliza** por el motor central (`sp_con_generar_comprobante`) con el **único**
     escenario de abono (un solo débito configurable → CxC). Sin integración bancaria ni selección de cuenta
     de banco en el abono.
7. La UI muestra: confirmación, **saldo anterior → abono → saldo nuevo**, referencia contable (PolizaId) y
   ofrece imprimir el **recibo de abono** (ver §9).

> Diferencia con "Posteo Manual"/captación actual: aquellos pagan el **total** del recibo y bloquean un
> segundo pago. Abonos es un flujo **nuevo** que admite **parciales acumulables** sobre una sola factura.

## 4.1) Almacenamiento del recibo de abono (sin cambios de estructura)

El recibo de abono **no requiere tabla ni columnas nuevas**: se guarda en las tablas existentes ligadas a
facturas. Cada abono es **un renglón** en `transaccion_abonado` (el kardex de cobros/abonos que ya usa el
sistema), enlazado a la factura con los mismos campos que usan los flujos actuales:

| Campo `transaccion_abonado` | Valor a guardar | Rol |
|---|---|---|
| `docufuente` | `factura.id` | Enlace numérico a la factura (igual que misceláneos). |
| `docufuente2` | `factura.numfactura` | Enlace por nº de factura. |
| `recibo` | `factura.numrecibo` | Enlace por nº de recibo. |
| `tipotransaccion` | código de abono (p.ej. `'202'`) | Distingue del pago total `'201'`; permite **varios** abonos por factura. |
| `creditos` | monto del abono | Importe abonado. |
| `saldo` | saldo corriente del cliente | Kardex. |
| `tipo_partida`, `descripcion`, `fecha_docu`, `estado`, `usuario`, `cliente_clave` | datos del recibo | Cabecera del movimiento. |
| `banco` | `NULL` / vacío | No aplica: el abono **no** define forma de pago. |

Implicaciones verificadas en código:

- **El recibo se "ve" desde la factura**: `SELECT * FROM transaccion_abonado WHERE docufuente = factura.id`
  (o `recibo = numrecibo`) lista todos los abonos/recibos de esa factura.
- **Por qué un `tipotransaccion` propio**: el flujo de pago actual bloquea un segundo cobro con la condición
  `recibo + tipotransaccion = '201'` (ver `CaptacionPagosService` líneas ~800/887/1053). Usar un código
  distinto para abonos evita ese bloqueo y habilita parciales acumulables.
- **Folio del abono**: provisionalmente, el recibo de abono toma el **siguiente número del correlativo de
  factura** (sigue la misma secuencia que `numfactura`/`numrecibo`, que hoy genera el motor de facturación
  en BD; la numeración fiscal vive en `adm_cai_facturacion`). El identificador interno del renglón sigue
  siendo `ide` (PK); el folio legible se persiste reutilizando una columna opcional existente (p.ej.
  `docuaplicar`/texto libre), **sin** agregar columnas.
  - **Pendiente de confirmar** (el usuario lo está averiguando): si el abono debe usar un correlativo
    **independiente** del de factura, y la **fuente exacta** de ese correlativo (secuencia de `numrecibo`,
    `cfg_document_type` o CAI). Mientras tanto: sigue al de factura.
- **Cabecera de factura**: refleja el abono vía `factura.estado` (`'B'` → `'C'`) y el detalle baja en
  `factura_detalle.montovalor_saldo`. `factura.saldototal` se mantiene como total original (como hoy).

## 5) Afectación contable — escenarios y cuentas a configurar

El abono produce una póliza balanceada (Debe = Haber). Como **no se define forma de pago**, hay un
**único escenario** y las dos cuentas se resuelven por `con_regla_integracion`. No hay integración bancaria
ni resolución de cuenta desde `banco_cuenta` en este flujo.

### 5.1 Escenario contable (`scenario_code`)

| Escenario | Debe | Haber | Cuándo |
|---|---|---|---|
| `ABONO` | Cuenta de cobro configurable (típicamente **Caja / Efectivo**, Activo) | **Cuentas por cobrar clientes** (Activo) | En todo abono. |

> Se descartan los escenarios por forma de pago (`ABONO_EFECTIVO`/`ABONO_BANCO`), bancarios
> (`COMISION_BANCARIA`) y `REDONDEO`, porque el abono no define forma de pago. También se descartan
> `ANTICIPO_CLIENTE`/`APLICA_ANTICIPO` (sin saldo a favor) y `MORA_RECARGO` (sin recargos).

### 5.2 Catálogo de cuentas que el usuario debe configurar

Solo **dos** cuentas para el escenario `ABONO`:

1. **Cuenta de cobro** (Debe) — típicamente **Caja / Efectivo** (Activo). Cuenta única configurable; al no
   haber forma de pago, todos los abonos debitan esta cuenta.
2. **Cuentas por cobrar clientes / CxC** (Haber, Activo) — reduce el saldo del cliente.

## 6) Vista de mantenimiento de cuentas (configuración)

**Nueva página**: `apc.Client/Pages/Contabilidad/CuentasAbonos.razor` (+ `.razor.cs`), titulada
"Configuración de Cuentas — Abonos", **acotada solo a abonos**.

- Lista las filas de `con_regla_integracion` filtradas por `module = CAJA` (escenarios de §5.1).
- Por cada escenario (`scenario_code`) permite seleccionar **cuenta débito**, **cuenta crédito** y
  **centro de costo** (opcional) desde el plan de cuentas (`con_plan_cuentas`, solo cuentas con
  `allows_posting`).
- Activar/desactivar regla (`is_active`), descripción, auditoría (`created_by`/`updated_by`).
- Validaciones: cuentas existen, son de posteo, pertenecen a la empresa; escenario único por
  `module + document_type_id + scenario_code`.
- Sigue el patrón de las páginas existentes de Contabilidad (`PlanCuentas.razor`, `Diarios.razor`,
  `TiposTransaccion.razor`, `CentrosCosto.razor`) con `DxGrid` + formulario modal.

**Backend de la config**: extender `IContabilidadCatalogosService` / `ContabilidadCatalogosService`
con `GetReglasIntegracionAbonosAsync`, `SaveReglaIntegracionAbonosAsync`, `DeleteReglaIntegracionAbonosAsync`
(filtrando `module = CAJA`), y exponer en `ContabilidadCatalogosController`. DTOs nuevos en
`SIAD.Core/DTOs/Contabilidad/`.

## 7) Arquitectura por capas — archivos a crear/modificar

### Nuevos
- `SIAD.Core/DTOs/Caja/AbonoDtos.cs` — `AbonoCrearDto`, `FacturaConSaldoDto`, `EstadoCuentaClienteDto`,
  `AbonoResponseDto`, `ReversoAbonoRequestDto`.
- `SIAD.Core/DTOs/Contabilidad/ReglaIntegracionAbonoDtos.cs` — `ReglaIntegracionListDto`,
  `ReglaIntegracionUpsertDto`.
- `SIAD.Services/Caja/IAbonoService.cs` + `AbonoService.cs` — lógica de abono (búsqueda de facturas con
  saldo, validación monto ≤ saldo, aplicación parcial a la factura, inserción de `transaccion_abonado`,
  llamada al motor contable y reversa). Reutiliza helpers de `CaptacionPagosService` (o se extraen a un
  helper compartido).
- `apc/Controllers/AbonoController.cs` — endpoints REST de abonos.
- `apc.Client/Services/Caja/AbonoClient.cs` — cliente HTTP (registrar en `CommonServices.cs`).
- `apc.Client/Pages/Facturacion/CaptacionPagos/PosteoAbonos.razor` (+ `.razor.cs`) — componente de la pestaña.
- `apc.Client/Pages/Contabilidad/CuentasAbonos.razor` (+ `.razor.cs`) — mantenimiento de cuentas (solo abonos).
- `Database/2026-06-08_seed_document_type_y_reglas_abonos.sql` — crear `cfg_document_type` (`module = CAJA`,
  `code = ABO`) y filas seed de `con_regla_integracion` para los escenarios de §5.

### Modificados
- `apc.Client/Pages/Facturacion/CaptacionPagos/Caja.razor` — agregar `DxTabPage Text="Abonos"` con
  `<PosteoAbonos ... />`; además, en el grid de estados, agregar el mapeo del nuevo valor `'B'` → "Parcial"
  (badge distinto), para que las facturas parcialmente abonadas no caigan en el `default`.
- `SIAD.Services/ServiceRegistration.cs` — registrar `IAbonoService`.
- `apc.Client/CommonServices.cs` — registrar `AbonoClient`.
- `SIAD.Core/Constants/PermissionNames.cs` (+ `PermissionEndpointCatalog`) — permisos de abonos y de la
  config de cuentas.
- `ContabilidadCatalogosService`/interfaz + `ContabilidadCatalogosController` — CRUD de `con_regla_integracion`
  acotado a abonos.

## 8) Reversa de abono

- Endpoint y método `ReversarAbonoAsync` que: revierte la póliza con `sp_con_revertir_poliza`, marca el
  `transaccion_abonado` como anulado (`estado`) y restituye `montovalor_saldo`/`saldototal` y `estado` de la
  factura. Todo transaccional e idempotente (no doble reversa).
- Requiere permiso específico (acción sensible) y registra usuario/fecha.

## 9) Idea adicional propuesta

Como el apartado **no** tiene cierre de caja, propongo complementarlo con tres elementos que dan control
sin reintroducir la sesión:

1. **Recibo/comprobante de abono imprimible (PDF)** vía DevExpress Reporting (`SIAD.Reports`): muestra
   cliente, factura, **saldo anterior → abono → saldo nuevo** y folio (sin forma de pago). El cajero lo
   entrega al cliente. (Recomendado como entregable de la idea.)
2. **Estado de cuenta del cliente en vivo** dentro de la pestaña: al seleccionar cliente, mostrar el kardex
   de abonos y saldo corriente (desde `transaccion_abonado`) para que el cajero confirme antes de cobrar.
3. **Arqueo del día por cajero** (sustituye al cierre de caja): reporte/consulta de "total cobrado del día
   por usuario" filtrando `transaccion_abonado` por fecha y `usuario`, para cuadrar el efectivo recibido sin
   el ciclo de apertura/cierre. **Sin desglose por forma de pago**, dado que el abono no la captura.

## 10) Seguridad, permisos y multiempresa

- Nuevos permisos en `PermissionNames` (módulo `ventas`/`contabilidad`): `abonos.view`, `abonos.create`,
  `abonos.reverse`; y para la config: `contabilidad.cuentas-abonos.view/edit`.
- Aplicar `[ModuleAuthorize(...)]` en los controllers; registrar en `PermissionEndpointCatalog` si son
  endpoint-específicos.
- Resolver `company_id` por `ICurrentCompanyService` (nunca del body). Todas las consultas filtran por
  empresa (filtro global de tenancy ya lo cubre).

## 11) Base de datos y regla de espejo

- **Sin cambios de estructura** (restricción del usuario): no se crean tablas ni columnas. Los recibos de
  abono se guardan en tablas existentes (`transaccion_abonado` ligada a `factura`, ver §4.1). Lo único que
  se introduce son **valores de datos nuevos**: un `tipotransaccion` de abono y el `estado = 'B'` en
  `factura` (ambas columnas ya existen).
- Cambios de **datos/configuración** como **scripts SQL fechados** en `Database/` (el contexto SIAD no usa
  migraciones EF). Para abonos: **seed** de `cfg_document_type` (`module = CAJA`, `code = ABO`) y de
  `con_regla_integracion` para los escenarios de §5; si el motor exige plantilla
  (`con_plantilla_partida_hdr`), seed de la mínima para `CAJA/ABO`. Todo son INSERTs, **no DDL**.
- **Regla de espejo de BD** (memoria del proyecto): todo cambio aplicado en la BD del SRV-VPN debe
  replicarse en `siad_v3_restore` (localhost). Aplicar el script en ambos.

## 12) Fases de implementación

- **Fase 0 — Datos/config**: crear `cfg_document_type` `CAJA/ABO`; definir `scenario_code`; script seed de
  `con_regla_integracion` (+ plantilla mínima si aplica).
- **Fase 1 — Config (mantenimiento de cuentas)**: DTOs + servicio CRUD (acotado a abonos) + controller +
  página `CuentasAbonos.razor`. Permite configurar las cuentas antes de cobrar.
- **Fase 2 — Backend de abonos**: `IAbonoService`/`AbonoService` (búsqueda de facturas con saldo, validación
  monto ≤ saldo, aplicación parcial, `transaccion_abonado`), controller y cliente HTTP.
- **Fase 3 — Contabilidad del abono**: integración con el motor central (generar/postear póliza por
  escenario; integración bancaria sin doble posteo; reversa).
- **Fase 4 — UI**: pestaña Abonos en `Caja.razor` (`PosteoAbonos.razor`) con búsqueda, captura, estado de
  cuenta en vivo y confirmación.
- **Fase 5 — Idea adicional**: recibo de abono (PDF) + arqueo del día por cajero.
- **Fase 6 — Pruebas**: unitarias/integración en `SIAD.Tests` (abono parcial, varios abonos hasta saldar,
  bloqueo de monto > saldo, reversa, idempotencia, período cerrado, regla inactiva, cuadre Debe=Haber).

## 13) Criterios de aceptación

1. El cajero registra abonos parciales a **una** factura sin abrir/cerrar caja; el saldo de la factura baja
   correctamente y se salda al completar.
2. El sistema **bloquea** montos de abono mayores al saldo pendiente (no hay saldo a favor).
3. Cada abono genera una póliza balanceada y posteada por el motor central, según las cuentas configuradas.
4. Existe una vista de mantenimiento para configurar las cuentas por escenario de abono.
5. La reversa de abono revierte negocio + contabilidad sin diferencias y sin doble posteo.
6. Todo respeta multiempresa y permisos; cuadra `con_partida_hdr` vs `con_saldo_cuenta`.

## 14) Riesgos y mitigaciones

- **Doble posteo por reintentos** → idempotencia por llave de negocio del abono + validación de estado.
- **Período contable cerrado / regla inexistente** → validar antes de postear y bloquear con mensaje claro.
- **Concurrencia** (dos cajeros sobre la misma factura) → transacción + relectura del saldo antes de aplicar
  (defensa contra abono > saldo por carrera).
- **Reuso de `CaptacionPagosService`** → considerar extraer los helpers contables/bancarios compartidos a
  un servicio común para no duplicar lógica.

## 15) Decisiones cerradas (resueltas con el usuario)

1. El abono aplica **solo a la factura seleccionada** (sin derrame multi-factura/FIFO).
2. **No** se cobran recargos por mora al abonar.
3. Los abonos **no** pueden generar saldo a favor: monto ≤ saldo pendiente (validación de bloqueo).
4. Se **crea** un `module`/`document_type` nuevo para abonos (propuesto `CAJA`/`ABO`) — como fila semilla,
   no cambio de estructura.
5. La vista de mantenimiento es **solo de abonos** (no editor general de `con_regla_integracion`).
6. Los recibos de abono se guardan en **tablas existentes sin modificar la estructura**: cada abono es un
   renglón en `transaccion_abonado` ligado a la factura (`docufuente`/`docufuente2`/`recibo`). Ver §4.1.
7. Estado de factura **parcial**: nuevo **valor** `factura.estado = 'B'` (a**B**ono; no columna nueva). Se
   descarta `'P'` por colisión con "Pagado" en `Caja.razor`/`PosteoMiscelaneos.razor`. Los abonos usan un
   `tipotransaccion` propio para permitir varios por factura.
8. **Folio del recibo de abono**: provisionalmente **sigue el correlativo de factura** (siguiente número de
   la misma secuencia). Pendiente confirmar si debe ser independiente y su fuente exacta.
9. La **app externa** queda **fuera de alcance** por ahora (no se trabaja en esta entrega).
10. **No se define forma de pago** al abonar: un **único escenario contable** `ABONO` (débito a cuenta única
    configurable → CxC), `transaccion_abonado.banco = NULL`, y **sin** integración bancaria.
