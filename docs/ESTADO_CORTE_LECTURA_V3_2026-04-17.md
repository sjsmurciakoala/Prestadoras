# Estado del corte Lectura V3

Fecha de corte: `2026-04-17`

## Objetivo de este documento

Este documento resume, sobre los planes ya definidos, qué ya está implementado y qué aún falta para cerrar el ciclo completo:

- motor tarifario
- cálculo de factura
- persistencia de lectura
- WS V3
- app lectora
- portal
- modo offline
- limpieza del legado

## Estado general

### Ya implementado

- modelo tarifario nuevo en BD:
  - `adm_*` core
  - seeds APC
  - seeds ERSAPS
- resolución tarifaria:
  - servicio base
  - servicios derivados
- `sp_adm_calcular_factura_lectura`
- `sp_lectura_v3`
- `sp_adm_generar_snapshot_offline_cliente_lectura`
- `ActualizarLecturaV3` en el WS
- `GetOfflineSnapshotV3` en el WS
- contrato `MedidorModeloV3`
- respuesta `LecturaRespuestaV3`
- compilación del proyecto `WS_APC` validada
- descarga y persistencia local del paquete offline V3 en la app
- emisión formal offline V3 desde `GenerarFactura.java`
- cola formal de sincronización con estados:
  - `PENDING_SYNC`
  - `SYNCED`
  - `SYNC_ERROR`
  - `SYNC_CONFLICT`
- detalle e impresión consumiendo snapshot formal V3
- build real del APK `assembleDebug` validado
- instalación del APK validada en dispositivo de prueba

### Pendiente principal

- validar el flujo end-to-end con datos reales de ciclo/ruta en el ambiente correcto
- reservar bloques CAI/correlativo para emisión offline formal
- crear mantenimientos del portal:
  - `adm_cliente_servicio`
  - mantenimiento tarifario
  - pantalla de prueba de cálculo
- definir el momento exacto del corte funcional para retirar:
  - `sp_tarifas_ws`
  - `sp_tarifas_contador_ws`
  - `sp_cobros_adicionales_ws`
  - tablas legacy de tarifas en el app
  - `ActualizarLecturaV2`

## Estado por plan

### `PLAN_MODELO_TARIFARIO_CLIENTE_SERVICIO_2026-04-14.md`

#### Cumplido

- definición del modelo regulatorio base
- definición de catálogos nuevos
- diseño físico inicial
- DDL base
- seeds iniciales

#### Falta

- migración ordenada desde los datos legacy hacia mantenimiento operativo real
- integración con EF/portal para administración de catálogos
- decisión final sobre retiro de tablas legacy una vez la app y el portal ya operen sobre el modelo nuevo

### `PLAN_INTEGRACION_LECTURA_WS_MOTOR_TARIFARIO_2026-04-17.md`

#### Cumplido

- dirección general del corte
- bloque BD inicial
- bloque WS V3
- bloque app V3/offline base

#### Falta

- validación operativa real con ruta/ciclo
- bloque portal de soporte operativo
- fase de limpieza del legado

### `PLAN_01_SP_ADM_CALCULAR_FACTURA_LECTURA_2026-04-17.md`

#### Cumplido

- procedimiento creado
- validaciones iniciales
- resolución de servicios base
- resolución de derivados
- salida compatible con `taservi1..4`
- salida detallada en JSON
- warnings
- base para snapshot offline

#### Falta

- ajustes tarifarios formales, por ejemplo:
  - adulto mayor
  - topes
  - beneficios legales
- consolidar si habrá más conceptos derivados adicionales
- pruebas ampliadas por más categorías y segmentos

### `PLAN_02_SP_LECTURA_V3_2026-04-17.md`

#### Cumplido

- procedimiento creado
- integra cálculo V3
- actualiza históricos
- inserta `factura`
- inserta `factura_detalle`
- inserta `transaccion_abonado`
- devuelve payload estructurado final

#### Falta

- definir si se conectará aquí mismo el disparo contable o quedará en otra fase
- ampliar pruebas de borde:
  - CAI agotado
  - reproceso
  - facturas duplicadas
  - clientes especiales

### `PLAN_03_ACTUALIZAR_LECTURA_V3_WS_2026-04-17.md`

#### Cumplido

- contrato V3 agregado al WS
- endpoint `GetOfflineSnapshotV3` agregado al WS
- request V3 agregado
- response V3 agregado
- llamada a `sp_lectura_v3`
- llamada a `sp_adm_generar_snapshot_offline_cliente_lectura`
- parseo de detalle y warnings
- manejo básico de errores
- compilación validada

#### Falta

- validación end-to-end del endpoint contra el ambiente real del WS
- definir si `ActualizarLecturaV3` convivirá visible con V2 por una fase o se cambiará directamente el cliente móvil
- homologar trazas/logs para soporte funcional

### `PLAN_04_PORTAL_MANTENIMIENTOS_MOTOR_TARIFARIO_2026-04-17.md`

#### Cumplido

- diseño funcional del bloque

#### Falta

- todo el bloque de implementación:
  - endpoints backend
  - servicios
  - DTOs
  - vistas Blazor
  - seguridad/permisos

### `PLAN_05_APP_LECTORES_V3_2026-04-17.md`

#### Cumplido

- endpoint `ActualizarLecturaV3` integrado en el app
- endpoint `GetOfflineSnapshotV3` consumido desde el app
- parser `LecturaRespuestaV3` agregado
- snapshot formal V3 guardado en SQLite
- paquete offline V3 guardado en SQLite
- cola local formal de sincronización implementada
- estados visuales de sync en lista y pantalla de subida
- bootstrap de cola formal para lecturas ya emitidas localmente
- validación de conflicto si servidor devuelve una factura distinta
- bloqueo del fallback V2 cuando ya existe factura formal local
- detalle e impresión preparados para consumir snapshot V3 cuando exista
- `GenerarFactura.java` desacoplado del cálculo legacy como ruta operativa
- build Android validado
- instalación en dispositivo validada

#### Falta

- cerrar compatibilidad temporal con V2
- retirar código muerto legacy cuando el corte operativo ya sea definitivo
- pruebas funcionales en dispositivo con datos reales de ruta/ciclo

### `PLAN_06_OFFLINE_SYNC_LECTURA_V3_2026-04-17.md`

#### Cumplido

- diseño funcional del modo offline y sincronización
- paquete offline V3 oficial desde WS
- almacenamiento local del snapshot offline V3
- cola y estados formales de sincronización
- reconciliación básica con conflicto cuando servidor devuelve otra factura

#### Falta

- reserva y persistencia de CAI/correlativo para emisión formal
- validación end-to-end del paquete offline V3 contra el ambiente final del WS
- refinamiento de soporte operativo para conflictos si hiciera falta

## Lo más importante que falta ahora

### 1. Prueba operativa real

El siguiente bloque crítico ya no es arquitectura. Es operación controlada:

- ruta/ciclo válidos
- descarga de medidores
- descarga de snapshot offline V3
- emisión de factura en el dispositivo
- sincronización real al WS
- confirmación de que la factura persistida sea la misma

### 2. CAI/correlativo offline

Este sigue siendo el hueco funcional serio:

- reserva formal de bloques
- persistencia local
- conciliación sin renumeración silenciosa

### 3. Portal

Todavía falta el bloque operativo del portal:

- mantenimiento `adm_cliente_servicio`
- mantenimiento tarifario
- prueba de cálculo
- soporte para revisar conflictos si los hubiera

## Recomendación de ejecución

### Siguiente paso inmediato

1. estabilizar una ruta/ciclo de prueba real
2. cerrar prueba end-to-end app -> WS -> BD
3. resolver CAI/correlativo offline
4. entrar al portal
5. luego hacer el corte legado

## Decisión operativa sugerida

No eliminar todavía:

- `Tarifa`
- `TarifaContador`
- `CobrosAdicionales`
- `ActualizarLecturaV2`

Pero desde este punto ya deben tratarse como compatibilidad temporal, no como diseño final.
