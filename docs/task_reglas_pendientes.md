# Task: Reglas pendientes del motor tarifario

Fecha: `2026-04-18`  
Actualizado: `2026-05-14` (Sprint 3 día 8 — recargo mora + tercera edad tope mensual explícito)

## Actualización 2026-05-14 (Sprint 3 día 8)

| Regla | Estado | Detalle |
|---|---|---|
| Descuento tercera edad (25% AGUA_POTABLE/DOMESTICO) | ✅ Cerrado en motor | Ya estaba implementado. Día 8: se hizo explícito `tope_mensual=300` en `adm_ajuste_tarifario.parametros` (Plan de Arbitrios Art. 355). Seed activo en 7 cuadros de company 2. |
| Recargo por mora (Plan de Arbitrios Art. 130) | ✅ Implementado día 8 | Nueva tabla `cfg_recargo_mora` (tasa mensual configurable por empresa, inicia `activo=false`). `sp_adm_calcular_factura_lectura` ahora calcula `v_recargos = saldo_previo * tasa_mensual` (antes hardcodeado a 0). Warning `RECARGO_MORA_APLICADO`. Script: `Database/ddl_v3/20260514_dia8_reglas_arbitrios.sql`. |
| Descuento 10% pago anticipado (Art. 153) | ⏭️ Diferido post-25 | Se aplica al MOMENTO DEL PAGO en Captación de Pagos. Captación está marcada para reforma completa post-25 — el descuento entra en esa reforma. |
| Control de unicidad del beneficio tercera edad (un beneficiario/inmueble) | ⏳ Pendiente | Sigue siendo deuda operacional (issue 3.4). No bloquea el cálculo. |

**Deuda del recargo mora**: el cálculo es `saldo_previo * tasa_mensual` (recargo plano mensual sobre el saldo arrastrado). El cálculo exacto por antigüedad de cada componente del saldo (aging) requiere un desglose del saldo que hoy no existe — queda como refinamiento post-25.

---

## 1. Objetivo

Dejar trazado el estado real de las reglas de negocio del motor tarifario V3, separando:

- lo que ya quedo implementado en SQL;
- lo que ya tiene soporte parcial;
- lo que sigue pendiente de definicion operacional.

## 2. Estado general

### Cerrado en motor

- servicios base:
  - agua potable
  - alcantarillado
- servicios derivados:
  - tasa ambiental
  - tasa SVA ERSAPS
- diferenciacion por:
  - categoria regulatoria
  - condicion de medicion
  - segmento tarifario
- lectura y facturacion V3 base
- descuento tercera edad / jubilado para `AGUA_POTABLE` en categoria `DOMESTICO`
- control de tope mensual para tercera edad
- idempotencia por `lectura_uuid` en `sp_lectura_v3`

### Parcial

- exoneraciones:
  - el motor ya soporta `EXONERACION` en `adm_ajuste_tarifario`
  - falta gobierno operacional, catalogo, vigencia y auditoria
- beneficio tercera edad:
  - ya existe marca del cliente y regla funcional
  - falta control de unicidad por beneficiario/inmueble

### Pendiente

- reglas de anulacion y reproceso
- restriccion de beneficio a un solo inmueble o cuenta
- exoneraciones con autorizacion y trazabilidad completa
- reglas especiales por categoria o segmento excepcional
- politicas operativas heredadas que aun viven fuera del motor

## 3. Estado por regla

### 3.1 Descuento adulto mayor / jubilado

#### Estado actual

- implementado en `sp_adm_calcular_factura_lectura`
- existe regla formal en `adm_ajuste_tarifario` para `TERCERA_EDAD_DOMESTICO`
- la elegibilidad ya valida:
  - cliente con `maestro_cliente_tercera_edad = true`
  - servicio `AGUA_POTABLE`
  - categoria regulatoria `DOMESTICO`
- si `cliente_maestro.descuento_tercera_edad > 0`, ese porcentaje prevalece sobre el seed base

#### Archivos relevantes

- `Prestadoras/Database/ddl_v3/20260417_add_sp_adm_calcular_factura_lectura.sql`
- `Prestadoras/Database/ddl_v3/20260418_adm_ajuste_tercera_edad_seed.sql`

### 3.2 Tope mensual del descuento adulto mayor

#### Estado actual

- implementado en `sp_adm_calcular_factura_lectura`
- el acumulado mensual se calcula desde `historicomedicion.descuentoapp` para el mismo cliente y periodo
- el motor respeta:
  - `tope_por_factura`
  - `tope_mensual` si viene en `parametros`
  - `tope_maximo` como fallback
- el calculo devuelve warnings cuando el tope ya fue consumido o solo queda remanente parcial:
  - `TOPE_TERCERA_EDAD_AGOTADO`
  - `TOPE_TERCERA_EDAD_PARCIAL`

#### Nota

La politica juridica exacta de control por cliente, cuenta o inmueble sigue pendiente de cierre operacional, pero el motor ya no deja el tope mensual abierto.

### 3.3 Restriccion a categoria residencial

#### Estado actual

- implementado
- hoy `TERCERA_EDAD_DOMESTICO` solo aplica cuando la categoria regulatoria resuelta es `DOMESTICO`

### 3.4 Restriccion a un solo inmueble o cuenta

#### Estado actual

- pendiente
- no existe aun bloqueo operacional para evitar multiples cuentas activas con el mismo beneficio

#### Falta

- definir el identificador juridico del beneficiario
- bloquear nuevos beneficios incompatibles
- exponer alertas o validaciones en portal

### 3.5 Exoneraciones especiales

#### Estado actual

- soporte de motor existente
- `sp_adm_calcular_factura_lectura` ya procesa ajustes tipo `EXONERACION`
- falta la definicion operacional completa:
  - catalogo
  - vigencia
  - autorizacion
  - auditoria

### 3.6 Recargos o reglas especiales no tarifarias

#### Estado actual

- parcial
- algunos cargos ya viven como servicios derivados o ajustes
- falta cerrar el inventario final de cargos excepcionales heredados

### 3.7 Reglas de anulacion y reproceso

#### Estado actual

- pendiente
- sigue siendo un punto de alto riesgo porque cruza:
  - facturacion
  - CAI
  - sincronizacion
  - trazabilidad

#### Falta

- definir si una factura se anula, sustituye o ajusta
- definir tratamiento de correlativos ya emitidos
- definir trazabilidad entre factura original y factura reprocesada

### 3.8 Reglas de duplicado e idempotencia

#### Estado actual

- cerrado para el flujo V3 principal
- `adm_cai_correlativo_emitido` ya tiene unicidad por `company_id + lectura_uuid`
- `sp_lectura_v3` ya soporta `lectura_uuid`
- `ActualizarLecturaV3` ya pasa `lectura_uuid` al registro final
- el WS ya puede devolver respuesta deterministica `IDEMPOTENTE`

#### Archivos relevantes

- `Prestadoras/Database/ddl_v3/20260417_add_sp_lectura_v3.sql`
- `Prestadoras/Database/ddl_v3/20260418_adm_cai_offline_core.sql`
- `Prestadoras/Database/ddl_v3/20260419_v3_cai_sync_conflictos.sql`
- `WSappLectores/WS_APC/APCService.svc.cs`

## 4. Pendiente real para avanzar

Para destrabar la prueba operativa completa ya no hace falta crear tercera edad o idempotencia desde cero. Lo que sigue pendiente de negocio real es:

- reproceso / anulacion
- unicidad del beneficio por beneficiario
- exoneraciones con gobierno operacional

El resto del flujo bloqueante para lectura V3 queda cubierto por el motor y por CAI offline.
