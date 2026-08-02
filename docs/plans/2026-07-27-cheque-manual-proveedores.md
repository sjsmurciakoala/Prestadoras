# Cheque manual (suelto) desde Proveedores y Cheques emitidos — Plan

**Fecha:** 2026-07-27 · **Rama:** `Cambios_almacen1.0` · **Estado:** **implementado en local** (sin commit).
Compila la solución completa; los tests nuevos de DTO pasan y los de integración quedan `Skipped`
hasta que `SIAD_TEST_DB` apunte a una BD con `Database/2026-07-21_cheques_numeracion_bitacora.sql`
aplicado. Falta la prueba end-to-end contra la BD (ver §5).

**Goal:** Poder emitir un cheque **sin partir de un compromiso ni de una orden de pago**, desde
dos puntos de entrada — la página **Cheques emitidos** (`/bancos/cheques`) y el módulo de
**Proveedores** — con el mismo efecto contable/bancario que un cheque de compromiso:
movimiento bancario, partida contable, número correlativo de la cuenta, bitácora e impresión.

---

## 1. Decisiones tomadas con el usuario (2026-07-27)

| # | Decisión | Valor |
|---|---|---|
| D1 | Qué es "cheque manual" | **Cheque suelto sin compromiso**: se elige cuenta, beneficiario, monto y concepto |
| D2 | Numeración | **Correlativo automático** de la cuenta (`proximo_cheque`), no editable — igual que hoy |
| D3 | Efectos | **Movimiento bancario (`ban_kardex`) + partida contable**, además del registro en `ban_cheque` |

Consecuencias directas de D2 y D3: **no hay cambios de estructura de base de datos**. Todo el
motor ya existe; el trabajo es una fachada de servicio, un endpoint, un diálogo Blazor y dos
puntos de entrada en la UI.

---

## 2. Qué existe hoy (y se reutiliza tal cual)

| Pieza | Ubicación | Qué aporta |
|---|---|---|
| `BanTransaccionesService.RegistrarMovimientoAsync` | [SIAD.Services/Bancos/BanTransaccionesService.cs:404](SIAD.Services/Bancos/BanTransaccionesService.cs) | Postea la partida, llama `sp_ban_kardex_registrar_movimiento`, vincula la partida al kardex y —si el tipo de transacción tiene `emite_cheque`— emite el cheque, todo atómico |
| Pre-validación de numeración agotada | mismo archivo, L500-519 | Evita póliza huérfana si el talonario se acabó |
| `ChequesService.EmitirChequeAsync` | [SIAD.Services/Bancos/ChequesService.cs:35](SIAD.Services/Bancos/ChequesService.cs) | `FOR UPDATE` sobre `ban_cuenta`, inserta `ban_cheque` ('E'), evento `EMITIDO` en `ban_cheque_bitacora`, incrementa `proximo_cheque` |
| Anulación por reverso | `ChequesService.AnularPorKardexAsync` | Al anular el movimiento bancario, el cheque queda 'A' automáticamente — **el cheque manual hereda esto sin trabajo extra** |
| Impresión | `Rpt_Dev_Cheque`, `Rpt_Dev_Cheque_Detalle`, `Rpt_Dev_Cheque_Comprobante` + `ChequeImpresionDialog.razor` | Los 3 formatos ya funcionan por `cheque_id` |
| Consulta | `/bancos/cheques` (bitácora de eventos) | El cheque manual aparece sin cambios de query |

**Lo único que le falta al motor** para servir a este caso:

1. El **beneficiario** del cheque hoy se toma de la *descripción* del movimiento
   (`BanTransaccionesService.cs:604`). Para un cheque a un proveedor queremos beneficiario y
   concepto separados.
2. El **origen** del cheque está fijo en `TRANSACCION`; conviene marcar los manuales.
3. `BanTipoTransaccionListDto` no expone `EmiteCheque`, así que el diálogo no puede filtrar
   los tipos que sirven para cheque.

---

## 3. Diseño

### 3.1 Flujo funcional

```
[Cheques emitidos]  "Nuevo cheque manual"  ─┐
                                            ├─→ ChequeManualDialog ─→ POST /api/bancos/cheques/manual
[Proveedores]       "Emitir cheque"        ─┘        (beneficiario, cuenta, monto,
                     (fila y detalle)                 concepto, contrapartidas)
                                                              │
                                            BanTransaccionesService.RegistrarChequeManualAsync
                                                              │
                                   partida contable → sp_ban_kardex_registrar_movimiento →
                                   ChequesService.EmitirChequeAsync (origen MANUAL, estado 'E')
                                                              │
                                            → ChequeImpresionDialog (cheque / cheque+detalle / comprobante)
```

### 3.2 Origen del cheque: `MANUAL` con estado `'E'` (sin DDL)

El `CHECK ck_ban_cheque_origen` ya admite `'MANUAL'`; hoy solo lo usa
`AnularSiguienteNumeroAsync` (cheque dañado), que **siempre** graba estado `'A'` y monto 0.
Por lo tanto la combinación `origen = 'MANUAL' AND estado = 'E'` está libre y describe
exactamente el cheque manual. Se ajusta el rótulo en la UI:

| origen | acción / estado | Etiqueta |
|---|---|---|
| `MANUAL` | `EMITIDO` / `'E'` | **Cheque manual** |
| `MANUAL` | `ANULADO` / `'A'` | Anulación de número |

> **Opción B descartada (por costo, no por diseño):** agregar el valor `'CHEQUE_MANUAL'` al
> `CHECK`. Es más explícito, pero obliga a un `DROP/ADD CONSTRAINT` en mirror y SRV, con
> guardia de estructura y una entrada nueva en el runbook, para una ganancia solo semántica.
> Si el usuario la prefiere, el resto del plan no cambia: solo la constante y un script SQL.

### 3.3 Contrapartida contable

El diálogo pide 1..N líneas de contrapartida (cuenta contable, monto, descripción,
referencia), igual que `TransaccionBancariaModal`. Cuando se entra **desde Proveedores** se
propone como cuenta la `CuentaContable` del proveedor (`ProveedorDetailDto.CuentaContable`),
editable. La cuenta del banco sale de `ban_cuenta.cont_account_id` (o `BANCO_DEFAULT`), como
en cualquier transacción.

### 3.4 Tipo de transacción

El cheque manual exige un tipo **de salida** (`entra_sale = 'S'`) con **Emite cheque = Sí**.
Se configura en *Bancos → Configuración de transacciones* (el switch ya existe). El diálogo
solo lista tipos que cumplan ambas condiciones; si no hay ninguno, muestra un aviso con el
enlace a esa pantalla en lugar de dejar guardar.

### 3.5 Trazabilidad al proveedor

Sin columna nueva: `beneficiario` = nombre del proveedor, `origen_documento` = código del
proveedor (cabe en `varchar(50)`), y la referencia del movimiento arma `CHM-<código>`. Con eso
la bitácora se puede filtrar por número/beneficiario. Si más adelante se quiere un cruce duro
proveedor↔cheque, sería un `ALTER TABLE ban_cheque ADD COLUMN prv_proveedor_codigo` aditivo
(fuera de este alcance).

### 3.6 Permisos

`ChequesController` ya va con `[ModuleAuthorize(PermissionModules.Bancos)]`; el `POST` mapea a
`PermissionAction.Create` → `module.bancos.create`. En la UI de Proveedores el botón se
envuelve en `AuthorizeView Policy="@AuthorizationPolicies.Bancos"` (`CanBancos`), para que un
usuario de proveedores sin permisos de bancos no lo vea.

---

## 4. Tareas

### Task 0 — Verificación previa

- `dotnet build HODSOFT_DEVEXPRESS.sln -clp:ErrorsOnly` → `0 Error(s)`.
- Confirmar en la BD de trabajo que existen `ban_cheque` y `ban_cheque_bitacora`
  (script `Database/2026-07-21_cheques_numeracion_bitacora.sql`, **paso 11 del runbook**:
  ya aplicado en el mirror `siad_v3_restore` el 2026-07-21, **pendiente en SRV**).
  Sin ese script **ningún** flujo de cheques funciona.
- Confirmar que hay al menos un tipo de transacción con `entra_sale='S'` y `emite_cheque='S'`
  para la empresa; si no, crearlo desde la UI de configuración de transacciones.

### Task 1 — Exponer `EmiteCheque` en el listado de tipos

**Files:** `SIAD.Core/DTOs/Bancos/BanTipoTransaccionListDto.cs`,
`SIAD.Services/Bancos/BanTiposTransaccionesService.cs`

Agregar `public bool EmiteCheque { get; set; }` y llenarlo en la proyección con el mismo
criterio afirmativo que usa `BanTransaccionesService` (`S`/`Y`/`1`/`T`/`TRUE`). Es aditivo:
no rompe a `TransaccionBancariaModal`.

### Task 2 — DTOs del cheque manual

**Files:** crear `SIAD.Core/DTOs/Bancos/ChequeManualDtos.cs`

- `ChequeManualCreateDto : IValidatableObject` — `BancoCuentaId`, `IdTipoTransaccion`,
  `FechaEmision` (`DateOnly`), `Beneficiario` (req., 200), `Concepto` (req., 250),
  `Referencia` (req., 100), `Monto` (> 0), `TasaCambio` (default 1), `ProveedorCodigo?`,
  `Lineas` (`List<BanTransaccionContraLineaDto>`, ≥ 1).
  `Validate`: suma de líneas == `Monto` (tolerancia 0.01) y fecha no futura.
- `ChequeManualResultadoDto` — `BanKardexId`, `ChequeId`, `NumeroCheque`, `SaldoResultante`,
  `Mensaje`.

### Task 3 — Servicio

**Files:** `SIAD.Services/Bancos/IBanTransaccionesService.cs`, `BanTransaccionesService.cs`

1. `RegistrarMovimientoAsync`: agregar al final tres parámetros opcionales
   `string? beneficiarioCheque = null, string? conceptoCheque = null, string origenCheque = ChequeOrigen.Transaccion`
   y usarlos en la llamada a `EmitirChequeAsync` (fallback a `descripcion`, comportamiento
   idéntico para los llamadores actuales).
2. Ampliar el retorno a `(long BanKardexId, decimal SaldoResultante, long? ChequeId, decimal? NumeroCheque)`
   — `EmitirChequeAsync` ya devuelve el número; hoy se descarta. El compilador delata los
   llamadores a ajustar.
3. Método nuevo `RegistrarChequeManualAsync(ChequeManualCreateDto dto, string usuario, CancellationToken ct)`:
   valida que la cuenta sea tipo CHEQUES, que el tipo de transacción sea de salida y emita
   cheque, y delega en `RegistrarMovimientoAsync` con `origenCheque: ChequeOrigen.Manual`,
   `beneficiarioCheque: dto.Beneficiario`, `conceptoCheque: dto.Concepto` y
   `sourceDocument`/`referencia` armados con el código de proveedor cuando venga.
   Devuelve `ChequeManualResultadoDto`.

Errores de negocio como `InvalidOperationException` / `ArgumentException` (el controlador ya
los traduce a 409/400).

### Task 4 — Endpoint

**Files:** `apc/Controllers/Bancos/ChequesController.cs`

`POST api/bancos/cheques/manual` — `ModelState` → `ValidationProblem`, validador de empresa
como el resto de acciones, `ArgumentException` → 400, `InvalidOperationException` → 409,
éxito → `Ok(ChequeManualResultadoDto)`.

### Task 5 — Cliente HTTP

**Files:** `apc.Client/Services/Bancos/ChequesClient.cs`

`EmitirManualAsync(ChequeManualCreateDto dto, CancellationToken ct)` con
`PostAsJsonAsyncWithAuthCheck` + `ObtenerMensajeErrorAsync` (patrón de `AnularSiguienteAsync`).

### Task 6 — Diálogo reutilizable

**Files:** crear `apc.Client/Pages/Bancos/ChequeManualDialog.razor` (+ `.razor.cs` si crece)

Parámetros: `Visible` / `VisibleChanged`, `BeneficiarioInicial?`, `ProveedorCodigo?`,
`CuentaContableSugerida?`, `OnEmitido` (`EventCallback<ChequeManualResultadoDto>`).

Contenido: cuenta bancaria (solo tipo CHEQUES), pill **"Se emitirá el cheque N° X"** vía
`ChequesClient.GetProximoAsync` con alerta si está agotada, tipo de transacción filtrado
(Task 1), fecha (no futura), beneficiario, concepto, referencia, monto, tasa de cambio si la
cuenta es USD, y la grilla de contrapartidas con total y validación contra el monto. Guarda
contra doble submit (`isSaving`) porque la acción consume correlativo. Toasts con
`StickToViewport` según la convención vigente. Antes de tocar API de DevExpress, consultar el
MCP `dxdocs` (obligatorio por CLAUDE.md).

### Task 7 — Entrada desde Cheques emitidos

**Files:** `apc.Client/Pages/Bancos/ChequesList.razor`

- Botón **"Nuevo cheque manual"** en el `header-section`, junto a "Anular siguiente número".
- Al emitir: cerrar el diálogo, abrir `ChequeImpresionDialog` con el `ChequeId`, refrescar
  próximo número y relanzar la búsqueda.
- Corregir `OrigenTexto` (L602) para distinguir `MANUAL` emitido vs. anulado (§3.2).

### Task 8 — Entrada desde Proveedores

**Files:** `apc.Client/Pages/Proveedores/ProveedoresList.razor`,
`apc.Client/Pages/Proveedores/ProveedorDetail.razor`

- Acción de fila (ícono `bi bi-cash-stack`, título "Emitir cheque") dentro de un
  `AuthorizeView Policy="@AuthorizationPolicies.Bancos"`, junto a las acciones actuales.
- En el detalle del proveedor, el mismo botón en la barra de acciones.
- Ambos abren `ChequeManualDialog` con beneficiario = nombre, `ProveedorCodigo` = código y
  `CuentaContableSugerida` = cuenta contable del proveedor; al emitir, `ChequeImpresionDialog`.

### Task 9 — Tests

**Files:** `SIAD.Tests/Bancos/ChequeManualTests.cs` (nuevo)

**Implementado:**

- Validaciones del DTO sin BD (4 `[Fact]`, pasan): detalle descuadrado, sin líneas, fecha futura,
  caso válido.
- Validaciones del servicio (5 `[SkippableFact]`): cuenta que no es de cheques, tipo inexistente,
  tipo sin `emite_cheque`, tipo de entrada, sin líneas de detalle. Corren antes de cualquier
  escritura, así que conviven con el harness `BEGIN … ROLLBACK`.
- `origen = MANUAL` con `estado = 'E'` y evento `EMITIDO` (valida la decisión §3.2).

**No cubierto por el harness:** el camino feliz completo (partida + `sp_ban_kardex_registrar_movimiento`
+ cheque). `RegistrarMovimientoAsync` abre **su propia** transacción sobre la conexión y el harness ya
tiene una abierta, así que ese caso exige una prueba manual en la aplicación (o un test que commitee,
lo que ensuciaría la BD). Igual limitación que el resto de los flujos que pasan por ese servicio.

### Task 10 — Documentación

**Files:** `docs/centura-flujos/README_impresion_cheques.md`

Agregar la cuarta vía de emisión a la tabla de §9 y una nota en §5 sobre el nuevo significado
de `origen = MANUAL` según estado. **No hay script SQL nuevo**, así que el runbook de
despliegue no se toca.

---

## 5. Riesgos y dependencias

| Riesgo | Mitigación |
|---|---|
| `ban_cheque` / `ban_cheque_bitacora` aplicados en el mirror pero **no en SRV** (paso 11 del runbook) | Precondición para producción: aplicar ese script. No es nuevo de este cambio: hoy ya bloquea las 3 vías existentes |
| La partida contable se postea **antes** de la transacción del kardex | Se mantiene la pre-validación de numeración agotada que ya existe; los tests cubren el caso |
| Reutilizar `MANUAL` para dos cosas distintas | Se separa por estado en UI y consultas; documentado en §3.2 y en el README |
| Cuentas en USD | La tasa de cambio es obligatoria; el diálogo la muestra solo si la cuenta es USD (regla ya existente en el servicio) |
| Doble emisión por doble clic | `isSaving` en el diálogo + `FOR UPDATE` y `uq_ban_cheque_numero` en el servidor |

## 6. Fuera de alcance (por D1/D2)

- Digitar el número de cheque o emitir fuera del correlativo.
- Capturar cheques históricos escritos a mano.
- Pagar compromisos/órdenes existentes con cheque manual (esas vías siguen igual).
- Vincular el cheque manual a un compromiso o afectar saldos de proveedor.
