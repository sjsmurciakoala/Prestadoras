# Plan de Implementacion: Contabilidad Automatica en Captacion + Bancos

Fecha: 2026-03-11  
Branch de trabajo: `feature/contabilidad-automatica-captacion-bancos`

## 1) Objetivo
Implementar contabilidad automatica para los flujos de:
- `/facturacion/captacion/caja` (lectora, miscelaneos y manual)
- Integracion bancaria cuando aplique

Todo bajo el mismo motor central:
- `sp_con_postear_poliza`
- `sp_con_revertir_poliza`

## 2) Alcance funcional
- Captacion lectora: al registrar pago, debe generar comprobante/poliza y postear.
- Captacion miscelaneos: al registrar pago, debe generar comprobante/poliza y postear.
- Captacion manual: al registrar pago, debe generar comprobante/poliza y postear.
- Bancos: cuando el pago se registre contra cuenta bancaria, debe quedar integrado a flujo de bancos sin duplicar posteo.

## 3) Regla tecnica obligatoria
- Ningun flujo de captacion debe actualizar `con_saldo_cuenta` de forma directa.
- Todos los flujos deben delegar en el motor central de posteo/reversa.
- Estados de negocio en numerico/boolean (`status_id`, `status` numerico de poliza).

## 4) Fases de implementacion

### Fase A - Diagnostico y mapeo contable por flujo
1. Identificar metodos backend de:
- Pago lectora
- Pago miscelaneos
- Pago manual
2. Mapear para cada flujo:
- `module`
- `document_type`
- `document_id`/`document_number`
- regla de integracion (`con_regla_integracion`)
- tipo transaccion (`con_tipo_transaccion`)
3. Validar prerequisitos por empresa:
- `cfg_document_type` activo para cada flujo
- regla contable activa
- periodo abierto (`status_id = 0`)

### Fase B - Implementacion de contabilidad automatica en Captacion
1. En cada metodo de registrar pago:
- construir contexto contable del documento
- llamar generacion/posteo por ruta central (sin hardcodes por empresa)
2. Asegurar idempotencia:
- reintento no debe duplicar poliza ni saldos
- usar llave de negocio por documento/transaccion
3. Reversa funcional:
- reversar pago debe reversar comprobante y saldos por ruta central

### Fase C - Integracion con Bancos
1. Definir condicion de integracion:
- si el pago afecta cuenta bancaria, registrar movimiento en bancos
2. Asegurar orden transaccional:
- registrar operacion de negocio
- registrar/componer poliza bancaria
- postear con motor central
3. Evitar doble contabilizacion:
- una sola poliza posteada por evento de negocio

### Fase D - Endpoints/UI y contratos
1. Revisar contratos API de captacion para exponer:
- `PolizaId` (si aplica)
- `StatusId`/estado del comprobante
2. Ajustar mensajes UI en `/facturacion/captacion/caja`:
- exito de registro + referencia contable
- errores de periodo/regla/documento de forma clara

### Fase E - Pruebas y cierre
1. E2E por flujo:
- lectora
- miscelaneos
- manual
- flujo con bancos
2. Pruebas negativas:
- periodo no abierto (`status_id <> 0`)
- regla inexistente/inactiva
3. Consistencia contable:
- no doble posteo
- reversa exacta
- reconciliacion `con_partida_hdr` vs `con_saldo_cuenta`

## 5) Evidencia minima requerida
Por cada flujo:
1. Documento de negocio creado.
2. Poliza creada (`con_partida_hdr`).
3. Poliza posteada (`status=1`) con `posted_at/posted_by`.
4. Cuadre de detalle (`debe = haber`).
5. Impacto en `con_saldo_cuenta`.
6. Reversa exacta (si aplica).

## 6) Riesgos y mitigaciones
- Riesgo: duplicidad de posteo por reintentos.
- Mitigacion: idempotencia por documento + validacion de estado.

- Riesgo: errores por periodo cerrado.
- Mitigacion: validar `status_id = 0` antes de postear y bloquear con mensaje de negocio.

- Riesgo: hardcodes por empresa.
- Mitigacion: resolver todo por catalogos/reglas configurables.

## 7) Criterio de cierre
Se considera cerrado cuando:
1. Los tres flujos de captacion (lectora, miscelaneos, manual) generan contabilidad automatica.
2. Integracion con bancos funciona sin doble contabilizacion.
3. Pruebas E2E + negativas + reconciliacion final sin diferencias.
4. Bitacora y runbook actualizados con evidencia.

## 8) Fase A completada (levantamiento tecnico 2026-03-11)

### 8.1 Matriz actual vs objetivo
| Flujo | Endpoint/API | Implementacion actual | Objetivo contable unico | Estado |
|---|---|---|---|---|
| Captacion lectora | `POST api/captacionpagos` y `POST api/captacionpagos/reverso` | `CaptacionPagosService.RegistrarPagoAsync` y `ReversarPagoAsync` actualizan `factura`/`factura_detalle` e insertan `transaccion_abonado` (201/202), sin `sp_con_postear_poliza` ni `sp_con_generar_comprobante`. | Mantener logica de negocio de captacion + generar/postear poliza por ruta central y revertir por ruta central. | Pendiente implementacion Fase B |
| Captacion manual | `POST api/captacionpagos/posteo-manual` y `/posteo-manual/reverso` | `CaptacionPagosService.RegistrarPagoManualAsync` llama `CALL sp_registrar_posteo_manual(...)`; reversa llama `CALL sp_reversar_posteo_manual(...)`; no usa motor central contable. | Migrar a composicion de comprobante/poliza central, con posteo/reversa central. | Pendiente implementacion Fase B |
| Captacion miscelaneos | `POST api/captacionpagos/miscelaneos/registrar` y `/miscelaneos/reverso` | `RegistrarPagoMiscelaneoAsync` y `ReversarPagoMiscelaneoAsync` afectan `factura` y `transaccion_abonado`, sin posteo central contable. | Integrar mismo patron: negocio + posteo central + reversa central. | Pendiente implementacion Fase B |
| Bancos (servicio existente) | `BanTransaccionesService.RegistrarMovimientoAsync` | Ya crea partida via `sp_registrar_partida_contable` y postea con `sp_con_postear_poliza`. | Reusar este flujo cuando captacion sea bancaria para evitar doble logica. | Base disponible |

### 8.2 Referencias de codigo auditadas
- Controller captacion:
  - `Prestadoras/apc/Controllers/CaptacionPagosController.cs`
  - Rutas `HttpPost`: lineas 155, 181, 267, 293, 335, 361.
- Service captacion:
  - `Prestadoras/SIAD.Services/CaptacionPagos/CaptacionPagosService.cs`
  - `RegistrarPagoAsync` (linea 286), `ReversarPagoAsync` (402)
  - `RegistrarPagoManualAsync` (701), `ReversarPagoManualAsync` (832)
  - `RegistrarPagoMiscelaneoAsync` (908), `ReversarPagoMiscelaneoAsync` (998)
  - Llamadas legacy: `sp_registrar_posteo_manual` (749), `sp_reversar_posteo_manual` (854)
- Cliente UI captacion:
  - `Prestadoras/apc.Client/Services/CaptacionPagos/CaptacionPagosClient.cs`
  - Endpoints `api/captacionpagos*` y llamados de registrar/reversar.
  - `PosteoLectoras.razor` (246, 280), `PosteoManual.razor` (296, 340), `PosteoMiscelaneos.razor` (249, 291).
- Bancos:
  - `Prestadoras/SIAD.Services/Bancos/BanTransaccionesService.cs`
  - `sp_registrar_partida_contable` (684, 700), `sp_con_postear_poliza` (795).

### 8.3 Hallazgos tecnicos (riesgo a corregir en Fase B)
1. `CaptacionPagosService` no inyecta servicio de contabilidad/bancos (solo `SiadDbContext`), por lo que hoy no puede delegar al flujo central sin refactor.
2. Existe desalineacion de firmas en SP manual:
   - Servicio C#: `CALL sp_reversar_posteo_manual(@ClienteClave, @Recibo, @TipoTransaccion)`.
   - `ddl_v3/captacion_pagos_stored_procedures.sql`: funcion `sp_reversar_posteo_manual(p_codigo_cliente, p_numrecibo, p_usuario)`.
   - Tambien hay version legacy distinta de `sp_registrar_posteo_manual` en `Database/LEGADO`.
3. DTO de captacion trae `Banco` como codigo string, pero para integracion bancaria robusta hace falta resolver `banco_cuenta_id`/cuenta contable de forma consistente.

### 8.4 Definicion de diseno para Fase B
1. Captacion sigue creando su evidencia operativa (`factura`, `transaccion_abonado`) y ademas dispara contabilidad central.
2. El posteo de saldos solo debe pasar por `sp_con_postear_poliza`/`sp_con_revertir_poliza`.
3. Si el pago es bancario, el registro contable debe reutilizar flujo de bancos para no duplicar logica.
4. Se evita hardcode por empresa: resolucion por catalogos (`cfg_document_type`, `con_regla_integracion`, `con_tipo_transaccion`, periodo abierto `status_id = 0`, diario activo).

## 9) Fase B implementada en servicio (2026-03-11)

### 9.1 Cambios aplicados
Archivo:
- `Prestadoras/SIAD.Services/CaptacionPagos/CaptacionPagosService.cs`

Implementacion:
1. Se agrega generacion central de comprobante en:
- `RegistrarPagoAsync` (lectora)
- `RegistrarPagoManualAsync` (manual)
- `RegistrarPagoMiscelaneoAsync` (miscelaneos)

2. Se agrega reversa central de comprobante en:
- `ReversarPagoAsync`
- `ReversarPagoManualAsync`
- `ReversarPagoMiscelaneoAsync`

3. Se crean helpers internos para:
- Resolver `company_id` actual.
- Resolver `document_type` contable activo para captacion (`VENTAS` con prioridad `REC`, fallback `FAC`).
- Resolver `type_id` activo/default de `con_tipo_transaccion`.
- Generar comprobante por `sp_con_generar_comprobante`.
- Revertir por `sp_con_revertir_poliza`.
 - Compatibilidad de reversa manual para entornos mixed-schema:
   - intenta `CALL sp_reversar_posteo_manual(..., tipo_transaccion)` (legacy),
   - y si no existe firma, hace fallback a `SELECT sp_reversar_posteo_manual(..., usuario)` (v3).

4. Se define `document_id` contable estable por flujo para evitar colisiones:
- Lectora: `document_id = numrecibo`
- Miscelaneos: `document_id = 1_000_000_000 + recibo`
- Manual: `document_id = 2_000_000_000 + numrecibo`

### 9.2 Comportamiento esperado
1. Al registrar pago en cualquiera de los tres flujos, se crea/postea poliza por motor central.
2. Al reversar pago, se revierte la poliza relacionada (si existe).
3. Si falta configuracion contable (`cfg_document_type`/`con_tipo_transaccion`), el pago falla y se hace rollback transaccional.

### 9.3 Validacion tecnica ejecutada
1. Build de capa de servicios:
- `dotnet build Prestadoras/SIAD.Services/SIAD.Services.csproj -c Debug` => OK.
2. Build de solucion completa:
- fallo por archivos bloqueados de `apc` en ejecucion (`MSB3027/MSB3021`), no por errores de compilacion del cambio.

### 9.4 Pendientes para Fase C
1. Integrar condicion bancaria explicita (mapeo `Banco` de captacion -> `banco_cuenta_id`) para reutilizar `BanTransaccionesService` cuando aplique.
2. Resolver inconsistencia de firmas legacy (`sp_reversar_posteo_manual`) en todos los ambientes para evitar dependencia de variantes de SP.
3. Ejecutar pruebas E2E funcionales desde UI/Postman y registrar evidencia en bitacora/runbook.
