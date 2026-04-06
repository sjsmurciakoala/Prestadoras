# Estandar de Estados y Flujo Contable

Fecha: 2026-03-09
Alcance: Contabilidad (polizas, periodos, tipos de transaccion, reglas de integracion, flujo movil, flujo bancos y flujo por plantilla SQL).

## 1) Politica general de estados
- Regla principal: el estado de negocio se maneja con valores numericos (`smallint`) o booleanos (`true/false`).
- Texto (`status` legacy) solo se mantiene de forma temporal para compatibilidad de API/UI y display.
- No se permiten hardcodes de negocio por empresa/modulo/documento en el flujo final. La resolucion debe venir de parametros y catalogos (`con_tipo_transaccion`, `con_regla_integracion`, `cfg_document_type`, plantillas).

## 2) Estados canonicos

### 2.1 `con_partida_hdr.status` (smallint)
- `0` = DRAFT (no posteada, no impacta `con_saldo_cuenta`).
- `1` = POSTED (posteada, impacta `con_saldo_cuenta`).
- `2` = VOID (anulada).

### 2.2 `con_periodo_contable.status_id` (smallint)
- `0` = ABIERTO.
- `1` = PRECIERRE.
- `2` = CERRADO.

Compatibilidad temporal (`status` texto):
- `OPEN`/`ABIERTO` -> `0`
- `LOCKED`/`BLOQUEADO`/`PRECIERRE` -> `1`
- `CLOSED`/`CERRADO` -> `2`

### 2.3 `con_tipo_transaccion.status_id` (smallint)
- `1` = ACTIVE (activo).
- `0` = INACTIVE (inactivo).

Compatibilidad temporal (`status` texto):
- `ACTIVE`/`ACTIVO` -> `1`
- `INACTIVE`/`INACTIVO` -> `0`

### 2.4 Estados booleanos
- `con_regla_integracion.is_active`: `true` = activa, `false` = inactiva.
- `cfg_document_type.is_active`: `true` = activo, `false` = inactivo.
- `con_diario.is_active`: `true` = activo, `false` = inactivo.

## 3) Mapa actual vs objetivo
- Actual:
- `con_partida_hdr.status` ya es numerico.
- `con_periodo_contable.status` textual (ABIERTO/PRECIERRE/CERRADO) solo como espejo temporal.
- `con_tipo_transaccion.status` textual (ACTIVE/INACTIVE/ACTIVO/etc).
- Mayorizacion repartida en varios puntos (SQL/C#).
- Objetivo:
- Un solo flujo de posteo/reversa para afectar `con_saldo_cuenta`.
- Productores (movil, API polizas, plantillas, bancos) solo crean/componen poliza y delegan al posteo central.
- `status_id` como contrato principal en periodo y tipo de transaccion, con `status` textual temporal para compatibilidad.

## 4) Flujo unico de mayorizacion
- Entrada de posteo: `public.sp_con_postear_poliza(company_id, poliza_id, user)`.
- Entrada de reversa: `public.sp_con_revertir_poliza(company_id, poliza_id, user)`.
- Invariante: solo estas funciones pueden modificar `con_saldo_cuenta` por poliza.
- Alcance de cuentas mayorizadas:
- El motor central toma las lineas de `con_partida_dtl` y actualiza `con_saldo_cuenta` por `codigo_cuenta` de la cuenta usada en cada linea.
- No existe roll-up automatico a cuentas padre en `con_saldo_cuenta`.
- Regla operativa: postear en cuentas de movimiento (`allows_posting = true`), normalmente cuentas hoja; las cuentas padre se obtienen por agregacion en reportes.
- Idempotencia:
- Posteo sobre poliza ya posteada (`status = 1`) no vuelve a sumar saldos.
- Reversa sobre poliza no posteada no resta saldos.
- Concurrencia:
- Las funciones bloquean cabecera de poliza (`FOR UPDATE`) y operan de forma transaccional.

## 5) Flujos por canal (donde se invocan)

### 5.1 Movil (sincronizacion de lecturas)
- Origen: `WSappLectores/WS_APC/APCService.svc.cs`.
- SP principal: `public.sp_lectura_v2`.
- Funcion esperada: registrar lectura/factura, componer poliza y llamar al posteo unico (`sp_con_postear_poliza`).
- Dependencias de reglas: `con_tipo_transaccion` + `con_regla_integracion` + `cfg_document_type`.

### 5.2 API de polizas (vista `/contabilidad/polizas`)
- Origen: servicio de aplicacion `PolizaService`.
- Funcion esperada: `RegistrarAsync` y `RevertirAsync` delegan en DB al posteo/reversa central.

### 5.3 Flujo por plantilla SQL
- Origen: `sp_con_generar_comprobante` y `sp_con_revertir_comprobante`.
- Funcion esperada: generar cabecera/detalle y delegar posteo/reversa al punto unico.
- Uso: cobranza, facturacion, miscelaneos y cualquier modulo que emita partidas por plantilla.

### 5.4 Bancos
- Origen: `BanTransaccionesService` + `sp_registrar_partida_contable`.
- Funcion esperada: crear poliza y postear via `sp_con_postear_poliza`.

## 6) Transiciones validas
- Poliza:
- `0 -> 1` (postear)
- `1 -> 0` (revertir)
- `0 -> 2` (anular)
- `1 -> 2` (anular posteada con politica explicita)
- No valida: doble posteo o doble reversa con impacto en saldos.
- Periodo:
- `0 -> 1` (precierre)
- `1 -> 2` (cierre final)
- `1 -> 0` (reapertura desde precierre)
- `2 -> 1` (reapertura controlada desde cerrado)
- Tipo transaccion:
- `1 -> 0` y `0 -> 1`.

## 7) Reglas de implementacion dinamica
- Prohibido hardcode de empresa/modulo/documento/escenario en flujo final.
- Resolver tipo de transaccion, documento y cuentas por catalogos y reglas configurables.
- Reglas de integracion (`/contabilidad/reglas-integracion`) y tipos (`/contabilidad/tipos-transaccion`) se mantienen activas y son parte del flujo movil.

## 8) Criterio de aceptacion funcional
- Todos los flujos crean/componen poliza y delegan al posteo central.
- `con_saldo_cuenta` solo cambia por posteo/reversa central.
- Cuadre de poliza y reversa exacta.
- Compatibilidad temporal de UI/API durante migracion de `status` textual a `status_id`.

## 9) Estado de implementacion (2026-03-09)
- SQL central de posteo/reversa implementado en:
- `Prestadoras/Database/ddl_v3/20260122_contabilidad_comprobantes_cobranza_facturacion.sql`
- Convergencia de flujo movil implementada en:
- `Prestadoras/Database/2026-03-05_update_sp_lectura_v2_contabilidad.sql`
- Migracion de `status_id` implementada en:
- `Prestadoras/Database/ddl_v3/20260309_contabilidad_status_id_migracion.sql`
- Correccion del mapeo canonico de periodos:
- `Prestadoras/Database/2026-03-26_fix_con_periodo_status_mapping.sql`
- Convergencia de API polizas y bancos implementada en:
- `Prestadoras/SIAD.Services/Contabilidad/PolizaService.cs`
- `Prestadoras/SIAD.Services/Bancos/BanTransaccionesService.cs`
- Compatibilidad transitoria `StatusId` + `Status` implementada en DTOs/servicios/EF:
- `Prestadoras/SIAD.Core/DTOs/Contabilidad/PeriodoContableDto.cs`
- `Prestadoras/SIAD.Core/DTOs/Contabilidad/PeriodoContableUpsertDto.cs`
- `Prestadoras/SIAD.Core/DTOs/Contabilidad/PolizaDto.cs`
- `Prestadoras/SIAD.Core/Entities/con_periodo_contable.cs`
- `Prestadoras/SIAD.Core/Entities/con_tipo_transaccion.cs`
- `Prestadoras/SIAD.Data/SiadDbContext.Accounting.cs`
- `Prestadoras/SIAD.Services/Contabilidad/PeriodoContableService.cs`
- `Prestadoras/SIAD.Services/Contabilidad/ContabilidadCatalogosService.cs`
