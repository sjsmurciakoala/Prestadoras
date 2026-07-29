# Cheques: numeración por cuenta y bitácora de emisión/anulación — Diseño

Fecha: 2026-07-21 · Rama: `Cambios_almacen1.0` · Estado: **aprobado por el usuario**

## Requerimiento

Para las cuentas bancarias de tipo **CHEQUES**:

1. Agregar la **numeración de cheque** (próximo número a emitir) y el **número máximo a consumir**.
2. Registrar una **bitácora de los cheques que se emiten y/o se anulan**.

## Decisiones validadas con el usuario (2026-07-21)

| Decisión | Elección |
|---|---|
| Dónde vive la numeración | Reutilizar `ban_cuenta.proximo_cheque` (ya migrado desde SIMAFI con el correlativo real) + columna nueva `cheque_maximo` |
| Asignación del número al pagar | **Automática, no editable**: el sistema toma `proximo_cheque` y lo incrementa; la UI muestra "Se emitirá el cheque N° X" |
| Vías de emisión cubiertas | **Todas**: procesar compromiso, abonar compromiso y transacción bancaria manual (tipos con `emite_cheque='S'`) |
| Anulación manual de un número | **Sí**: acción en la bitácora que consume el siguiente número y lo marca anulado (cheque dañado), además de la anulación automática al reversar el movimiento |

## Contexto (hallazgos de la exploración)

- `ban_cuenta.proximo_cheque` (`numeric(28,4)`) existe y fue poblado por `Database/2026-07-09_bancos_simafi_02_transform.sql` desde `ctacheques.ncheque`, pero **ningún código C# lo lee ni escribe**. No existe campo de número máximo (`ban_config.max_cheque` es *monto* máximo, otra cosa).
- Hoy **no se captura ni persiste ningún número de cheque** en el flujo vivo. Tres vías generan el movimiento bancario "tipo cheque", todas vía SP `sp_ban_kardex_registrar_movimiento` → `ban_kardex`:
  1. Procesar compromiso — `OrdenesPagoDirectoService.MarkAsProcessedAsync` → `RegisterLinkedBankTransactionsAsync` / `RegisterLinkedBankMovementsGeneralAsync` → `RegisterLinkedBankMovementAsync` (~L2118).
  2. Abonar compromiso — `RegistrarAbonoAsync` (misma cadena).
  3. Transacción bancaria manual — `BanTransaccionesService.RegistrarMovimientoAsync` (L401), cuando el tipo tiene `emite_cheque='S'` (p.ej. `CHQ`).
- La anulación/reverso converge en `BanTransaccionesService.AnularMovimientoAsync` (L1564): valida no conciliado (`estado_conciliacion != 'CON'`), llama SP `sp_ban_kardex_anular_movimiento_recalcular` (inserta kardex de reverso) y registra la partida inversa. `OrdenesPagoDirectoService.AnularAbonoAsync` delega aquí. **Es el chokepoint único de anulación.**
- Detección "es cheque": método de pago `OrdenPagoDirectoMetodoPago.Cheque`, cuenta `IsChequeBankAccount` (`tipo` contiene "CHEQ"), tipo de transacción con `emite_cheque` ∈ {S,Y,1,T,TRUE} o nombre/código `%CHEQ%` (`ResolveBankTransactionTypeAsync` ~L2185).
- La **bitácora de maestros** (interceptor EF) NO aplica: es CRUD de catálogos con lista blanca (`AuditableMaestros`) y 3 verbos. El patrón correcto del repo para eventos de negocio es **tabla dedicada escrita por el servicio** (como `cln_accion_cobranzas`).
- Precedente legacy (Centura, `Database/ddl_v3/# Flujo de Cheques — GA_CP.md`): correlativo `i_SigNumCheque()` + registro en `BNC_CHEQUE_HDR/DTL`, anulación con `DESTINO='ANULA'`. Este diseño es su equivalente moderno.
- Entidades `bnc_*` (incl. `bnc_cuenta.numero_cheque`) están **muertas** — no se tocan.

## Arquitectura

### 1. Base de datos — `Database/2026-07-21_cheques_numeracion_bitacora.sql` (aditivo)

- `ALTER TABLE ban_cuenta ADD COLUMN IF NOT EXISTS cheque_maximo NUMERIC(28,0) NOT NULL DEFAULT 0;` — `0` = sin límite configurado (no se valida agotamiento).
- Tabla nueva `ban_cheque` (libro/bitácora, una fila por cheque):
  - `cheque_id` BIGINT identity PK; `company_id` BIGINT NOT NULL; `banco_cuenta_id` BIGINT NOT NULL (FK `ban_cuenta`, RESTRICT).
  - `numero_cheque` NUMERIC(28,0) NOT NULL.
  - `fecha_emision` timestamp; `monto` NUMERIC(15,2) NOT NULL DEFAULT 0 (0 en anulación manual); `beneficiario` VARCHAR(200); `concepto` VARCHAR(250).
  - `origen` VARCHAR(20) NOT NULL ∈ {`PROCESAR`,`ABONO`,`TRANSACCION`,`MANUAL`}; `origen_documento` VARCHAR(50) (p.ej. `OPD-123`).
  - `ban_kardex_id` BIGINT NULL; `partida_id` BIGINT NULL; `ban_kardex_id_reverso` BIGINT NULL.
  - `estado` CHAR(1) NOT NULL DEFAULT 'E' CHECK ∈ {'E','A'} (Emitido/Anulado — convención NO invertida).
  - `usuario_emision` VARCHAR(100) NOT NULL; `fecha_creacion` DEFAULT now(); `motivo_anulacion` VARCHAR(250); `usuario_anulacion` VARCHAR(100); `fecha_anulacion`; `rowid` UUID DEFAULT gen_random_uuid().
  - `UNIQUE (company_id, banco_cuenta_id, numero_cheque)`; índices `(company_id, banco_cuenta_id, estado)` y `(company_id, ban_kardex_id)`.
- Estilo de script: encabezado con Fecha + Regla DB Mirror, POR QUE, idempotencia, bloque VERIFICACION comentado. Pasa por la skill **guardia-estructura-bd** (tarjeta verde aditiva); **el usuario lo aplica** en mirror (`siad_v3_restore` @localhost) → SRV.

#### Bitácora de eventos (ampliación 2026-07-21)

Aprobada por el usuario: además del libro `ban_cheque` (estado vigente E/A por cheque), una tabla de **eventos append-only** `ban_cheque_bitacora` — una fila por evento `EMITIDO`/`ANULADO`, nunca se actualiza ni se borra. Mismo script SQL:

- Columnas: `bitacora_id` (identity PK), `company_id`, `cheque_id`, `banco_cuenta_id`, `numero_cheque`, `accion` CHECK ∈ {`EMITIDO`,`ANULADO`}, `fecha` DEFAULT now(), `usuario`, `monto`, `beneficiario`, `concepto`, `motivo`, `origen`, `origen_documento`, `ban_kardex_id`, `rowid`.
- FK compuesta tenant-safe `(company_id, cheque_id)` → `ban_cheque` (requiere AK nueva `uq_ban_cheque_company_cheque`); índice `(company_id, banco_cuenta_id, fecha)`.
- Puntos de escritura (siempre en la MISMA transacción de la operación): `EmitirChequeAsync` → evento `EMITIDO`; `AnularPorKardexAsync` → un evento `ANULADO` por cheque reversado (`ban_kardex_id` = el reverso); `AnularSiguienteNumeroAsync` → un único evento `ANULADO` origen `MANUAL` (sin `EMITIDO`: el cheque nunca se emitió).
- Consulta: `BuscarBitacoraAsync` (`GET api/bancos/cheques/bitacora`); la página `/bancos/cheques` ("Bitácora de cheques") muestra los eventos; `BuscarAsync` (libro) se mantiene.

### 2. Entidad y contexto

- `SIAD.Core/Entities/ban_cheque.cs` — `ICompanyScopedEntity` (filtro tenant + stamping automáticos vía `SiadDbContext.Tenancy.cs`).
- DbSet + Fluent config siguiendo el precedente de `prv_compromiso_abono` (sin `HasQueryFilter` manual). Propiedad `cheque_maximo` se agrega a `ban_cuenta.cs` (partial generado: editar el archivo de entidad + config `HasPrecision(28,0)` / default).

### 3. Servicio `ChequesService` (`SIAD.Services/Bancos/`) — punto único de lógica

`IChequesService` (registrado en `ServiceRegistration.cs`):

- `EmitirChequeAsync(NpgsqlConnection, NpgsqlTransaction, bancoCuentaId, monto, beneficiario, concepto, origen, origenDocumento, banKardexId, partidaId, usuario, fecha, ct)` — **participa en la transacción del pago** (raw Npgsql, patrón del módulo):
  1. `SELECT proximo_cheque, cheque_maximo FROM ban_cuenta WHERE banco_cuenta_id=@id FOR UPDATE` (serializa emisiones concurrentes).
  2. Si `cheque_maximo > 0` y `proximo_cheque > cheque_maximo` → excepción de negocio: *"La cuenta agotó su numeración de cheques (máximo N). Actualice la numeración en la gestión de la cuenta."* → el pago completo se revierte.
  3. `INSERT ban_cheque` (estado 'E') + `UPDATE ban_cuenta SET proximo_cheque = proximo_cheque + 1`.
  4. Devuelve el número asignado.
- `AnularPorKardexAsync(conn, tx, banKardexIdOriginal, banKardexIdReverso, motivo, usuario, ct)` — marca `estado='A'` + auditoría del cheque vigente con ese `ban_kardex_id`; no-op si no hay cheque vinculado (movimientos no-cheque).
- `AnularSiguienteNumeroAsync(bancoCuentaId, motivo, usuario, ct)` — standalone (su propia transacción): consume el siguiente número y lo inserta ya anulado (`origen='MANUAL'`, monto 0, sin kardex).
- `GetProximoAsync(bancoCuentaId, ct)` — para "Se emitirá el cheque N° X" en las pantallas de pago.
- `BuscarAsync(ChequeFilterDto, ct)` — consulta de la bitácora (EF, filtro tenant global).

### 4. Integración en emisores y anulación

- `OrdenesPagoDirectoService`: tras cada `RegisterLinkedBankMovementAsync` con método `CHEQUE`, llamar `EmitirChequeAsync` con el `ban_kardex_id` retornado (beneficiario = nombre del proveedor; `origen` `PROCESAR` o `ABONO`; `origen_documento` = `OPD-{numeroOrden}`). Exponer el número emitido en los resultados (`OrdenPagoDirectoOperacionResultadoDto` / `AbonoCompromisoResultadoDto` → propiedad `NumeroCheque`/lista).
- `BanTransaccionesService.RegistrarMovimientoAsync`: si el tipo de transacción resuelto tiene `emite_cheque` afirmativo → `EmitirChequeAsync` (`origen='TRANSACCION'`, beneficiario = descripción/referencia).
- `BanTransaccionesService.AnularMovimientoAsync`: tras el SP de reverso → `AnularPorKardexAsync` (cubre anular abono y anular transacción con un solo hook).

### 5. API y cliente

- `apc/Controllers/Bancos/ChequesController.cs` — `[Route("api/bancos/cheques")]`, `[ModuleAuthorize(PermissionModules.Bancos)]`: `GET` (búsqueda con filtros), `GET proximo/{bancoCuentaId}`, `POST {bancoCuentaId}/anular-siguiente` (body: motivo).
- `apc.Client/Services/Bancos/ChequesClient.cs` — registrado en `CommonServices.cs` (seguro en ambos hosts), con los helpers `*WithAuthCheck`.
- DTOs en `SIAD.Core/DTOs/Bancos/ChequesDtos.cs`: `ChequeListItemDto`, `ChequeFilterDto`, `ProximoChequeDto`, `AnularNumeroChequeDto`.

### 6. UI

- `CuentasBancosFormModal.razor` + `BancoCuentaCreateDto`/`EditDto` + `CuentasBancosService` (Create/Update/MapToEditDto): campos **"Próximo cheque"** y **"Cheque máximo"** (`DxSpinEdit`), visibles solo cuando `TipoCuenta == "CHEQUES"`; validación `cheque_maximo == 0 || proximo <= maximo`.
- Página nueva `apc.Client/Pages/Bancos/ChequesList.razor` — ruta `/bancos/cheques` ("Cheques emitidos"), política `PermissionNames.Bancos.View`. **Sigue el estándar de grid** (`siad-grid.css`, referencia `ClientesList.razor`) aunque el resto del módulo Bancos aún no esté migrado. Filtros: banco, cuenta, estado, rango de fechas; columnas: número, fecha, cuenta, beneficiario, concepto, monto, origen, estado (badge), usuario; popup de detalle con datos de anulación; botón "Anular siguiente número" (popup con motivo obligatorio).
- `SidebarNavigationDefinition.cs`: ítem `bn-cheques` → `/bancos/cheques` en el grupo `cont-bancos` + `MatchPrefixes`.
- `CompromisoProveedorProcesar.razor` / `CompromisoProveedorAbonar.razor`: al seleccionar método CHEQUE, mostrar "Se emitirá el cheque N° X" (GET proximo); mostrar el número asignado en el mensaje de éxito.
- DevExpress: consultar `dxdocs` MCP antes de tocar API de componentes (obligatorio por CLAUDE.md).

### 7. Manejo de errores

- Agotamiento de numeración → error de negocio (400 con mensaje), nunca 500; el pago no se registra (misma transacción).
- Carrera por número duplicado → la `UNIQUE` + `FOR UPDATE` la previenen; si aun así salta 23505, mensaje "Reintente el pago" (patrón de `RegistrarAbonoAsync`).
- Anulación de movimiento conciliado ya la bloquea `AnularMovimientoAsync` — el cheque queda intacto.
- `AnularPorKardexAsync` es tolerante: si el movimiento no tiene cheque (DEP/TRF), no hace nada.

### 8. Pruebas (`SIAD.Tests`, mirror vía `SIAD_TEST_DB`)

- Emisión asigna `proximo_cheque` e incrementa; segunda emisión asigna N+1.
- `cheque_maximo` alcanzado → excepción de negocio y rollback del pago.
- Unicidad `(company, cuenta, numero)`.
- Anular movimiento con cheque → cheque pasa a 'A' con motivo/usuario/reverso.
- Anulación manual consume número y queda 'A' / origen MANUAL.
- Movimiento no-cheque anulado → no toca `ban_cheque`.

## Fuera de alcance (explícito)

- Impresión del formato físico del cheque (queda para una fase con `SIAD.Reports`).
- Conciliación bancaria (sin cambios).
- Carga histórica de cheques SIMAFI a `ban_cheque` (la bitácora arranca desde la puesta en marcha).
- Cambios a las entidades muertas `bnc_*`.
