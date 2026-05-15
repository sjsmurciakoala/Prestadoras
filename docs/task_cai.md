# Task: CAI y correlativo offline

Fecha: `2026-04-18`
Actualizado: `2026-04-23`

## 1. Objetivo

Cerrar la emision formal offline para que la factura generada en el app:

- use un CAI y correlativo validos;
- conserve ese mismo numero al sincronizar;
- no dependa de regenerar la factura en servidor;
- soporte conflictos, reprocesos y agotamiento de rangos sin romper trazabilidad.

## 2. Problema que resuelve

Hoy el motor tarifario, el WS V3 y el app ya pueden:

- calcular;
- emitir snapshot formal;
- sincronizar lectura y factura;
- detectar conflictos basicos.

Pero todavia falta la parte critica de numeracion fiscal offline:

- como reservar bloques de CAI/correlativo;
- como asignarlos al dispositivo o al lector;
- como consumirlos sin duplicidad;
- como reconciliar el numero emitido en el app contra lo persistido en servidor.

Sin este bloque, la factura del app no queda completamente cerrada como comprobante formal offline.

## 3. Alcance de esta tarea

Esta tarea cubre:

- modelo de datos para reserva de bloques CAI;
- reglas de asignacion de correlativos al app;
- consumo offline y confirmacion al sincronizar;
- deteccion de duplicados y agotamiento;
- integracion con `GetOfflineSnapshotV3`, `ActualizarLecturaV3` y `sp_lectura_v3`;
- auditoria operativa para soporte.

Esta tarea no cubre:

- contabilizacion automatica;
- diseño final de reportes fiscales impresos;
- reglas comerciales como descuentos o topes.

## 4. Estado actual

### Hecho

- `sp_adm_calcular_factura_lectura` existe;
- `sp_lectura_v3` existe;
- `ActualizarLecturaV3` existe;
- el app ya emite snapshot formal y sincroniza;
- el portal y la app ya validan que no se acepte silenciosamente una factura distinta;
- existe reserva formal de bloques CAI;
- existe confirmacion de correlativo al sincronizar;
- existe `lectura_uuid` en CAI/conflictos para idempotencia;
- `ActualizarLecturaV3` ya prepara y confirma correlativo con `lectura_uuid`.

### Falta

- cierre formal de reproceso/anulacion sobre correlativos ya emitidos;
- pruebas de borde completas:
  - duplicado
  - agotado
  - reproceso
  - agujeros de numeracion
- evidencia operativa final en portal para auditoria por correlativo.

## 5. Diseño funcional requerido

### 5.1 Conceptos base

Se debe separar claramente:

- `CAI`: autorizacion fiscal vigente;
- `rango autorizado`: desde/hasta;
- `bloque reservado`: subconjunto del rango entregado a un dispositivo o usuario;
- `correlativo emitido`: numero ya usado en una factura formal;
- `correlativo confirmado`: numero que ya quedo persistido y conciliado en servidor.

### 5.2 Regla principal

La factura emitida en el app debe salir ya con:

- `cai_id`;
- `numero_factura`;
- `correlativo`;
- `serie` o prefijo si aplica.

Y al sincronizar:

- el servidor no debe generar un nuevo numero;
- debe aceptar, validar y persistir el mismo numero emitido;
- solo debe rechazarlo si viola una regla fiscal u operacional explicita.

### 5.3 Estrategia recomendada

La estrategia recomendada es `bloque reservado por dispositivo o lector`.

Flujo:

1. Un usuario autorizado en portal o WS reserva un bloque de correlativos activos.
2. El bloque se asigna a:
   - `company_id`
   - lector o usuario
   - dispositivo
   - periodo o vigencia opcional
3. El app descarga el bloque disponible junto con el snapshot offline.
4. Cada factura offline consume localmente el siguiente correlativo libre del bloque.
5. Al sincronizar, el servidor valida:
   - que el correlativo pertenezca a un bloque reservado;
   - que el bloque este activo;
   - que no exista otro uso confirmado;
   - que la factura corresponda al mismo lector/dispositivo.
6. Si pasa validacion, el servidor marca ese correlativo como confirmado.

## 6. Modelo de datos requerido

### 6.1 Tabla de CAI maestro

Debe existir una tabla tipo `adm_cai_facturacion` o equivalente con:

- `cai_id`
- `company_id`
- `codigo_cai`
- `prefijo_documento`
- `rango_desde`
- `rango_hasta`
- `vigencia_desde`
- `vigencia_hasta`
- `estado`
- auditoria completa

### 6.2 Tabla de bloques reservados

Debe existir una tabla tipo `adm_cai_bloque_reservado` con:

- `cai_bloque_id`
- `company_id`
- `cai_id`
- `usuario_id` o `lector_id`
- `dispositivo_id`
- `ruta_codigo` opcional
- `correlativo_desde`
- `correlativo_hasta`
- `correlativo_actual`
- `fecha_reserva`
- `fecha_expiracion` opcional
- `estado`
- auditoria completa

### 6.3 Tabla de uso de correlativos

Debe existir una tabla tipo `adm_cai_correlativo_emitido` con:

- `cai_correlativo_emitido_id`
- `company_id`
- `cai_bloque_id`
- `correlativo`
- `numero_factura`
- `cliente_id`
- `lectura_uuid` o identificador idempotente
- `factura_id` nullable mientras no sincroniza
- `estado`
- `PENDING_OFFLINE`
- `CONFIRMADO`
- `ANULADO`
- `RECHAZADO`
- auditoria completa

### 6.4 Restricciones clave

- unico por `company_id + cai_id + correlativo`;
- unico por `company_id + numero_factura`;
- unico por `company_id + lectura_uuid` para idempotencia;
- validacion de rango dentro del CAI;
- validacion de bloque activo.

## 7. Cambios requeridos por capa

### 7.1 BD / motor

Se requiere:

- mantener tablas nuevas de CAI y bloques;
- mantener procedimientos de reserva, consumo y confirmacion;
- completar pruebas de borde del flujo CAI;
- mantener validacion de idempotencia en `sp_lectura_v3` usando `lectura_uuid`.

### 7.2 WS

Se requiere:

- incluir bloque CAI disponible en `GetOfflineSnapshotV3` o en un endpoint dedicado;
- exponer datos minimos para que el app emita offline;
- validar en `ActualizarLecturaV3` que el numero recibido es valido y reservable;
- pasar `lectura_uuid` tambien al registro final en `sp_lectura_v3`;
- devolver errores funcionales claros:
  - `CAI_AGOTADO`
  - `CORRELATIVO_DUPLICADO`
  - `BLOQUE_INVALIDO`
  - `CAI_VENCIDO`
  - `FACTURA_YA_CONFIRMADA`

### 7.3 App Android

Se requiere:

- persistir bloque CAI local;
- consumir correlativos en orden;
- no saltar correlativos sin dejar traza;
- impedir emitir si el bloque esta agotado o vencido;
- marcar lecturas emitidas con el correlativo usado;
- no reusar correlativos en reprocesos locales.

### 7.4 Portal

Se requiere vista operativa para:

- crear CAI vigente;
- ver rangos autorizados;
- reservar bloques;
- asignar bloque a lector/dispositivo;
- revisar correlativos consumidos, pendientes y confirmados;
- detectar agujeros o duplicados.

## 8. Casos que se deben cubrir

### 8.1 Emision offline normal

- app recibe bloque;
- genera factura;
- consume correlativo siguiente;
- sync confirma el mismo correlativo.

### 8.2 Sincronizacion repetida

- misma lectura se reenvia;
- servidor debe responder idempotente;
- no debe generar factura duplicada ni consumir otro correlativo.

### 8.3 Correlativo ya usado

- app intenta subir numero ya confirmado;
- servidor rechaza y marca conflicto.

### 8.4 CAI agotado

- no quedan correlativos en el bloque;
- app no debe emitir factura formal nueva;
- debe informar bloqueo operativo claro.

### 8.5 CAI vencido

- el CAI o bloque expiro;
- el app no debe seguir emitiendo;
- el WS debe rechazar confirmaciones fuera de politica si asi se define.

### 8.6 Reproceso de lectura

- lectura ya facturada necesita correccion;
- se debe definir si:
  - se anula formalmente;
  - se emite una nueva factura;
  - o se genera nota/ajuste.

Esta regla debe quedar escrita antes de liberar a operacion.

## 9. Orden recomendado de implementacion

1. Diseñar tablas y restricciones.
2. Crear SP de reserva de bloques.
3. Crear SP de consumo de siguiente correlativo.
4. Integrar validacion y confirmacion en `sp_lectura_v3`.
5. Exponer datos en WS.
6. Persistir bloque y consumo en app.
7. Crear vista de control en portal.
8. Ejecutar pruebas de:
   - agotamiento
   - duplicado
   - reproceso
   - idempotencia

## 10. Datos que se deben definir antes de cerrar la tarea

- formato exacto de `numero_factura`;
- si el correlativo se reserva por:
  - empresa
  - lector
  - dispositivo
  - ruta
- politica de expiracion del bloque;
- politica de anulacion;
- longitud minima de bloque;
- comportamiento cuando el app esta offline por varios dias.

## 11. Riesgos

- duplicidad fiscal por falta de idempotencia;
- huecos de correlativo sin trazabilidad;
- conflicto entre dos dispositivos usando el mismo bloque;
- rechazo de sync cuando el app ya entrego comprobante al cliente;
- operacion detenida por agotamiento de CAI sin alerta previa.

## 12. Criterios de aceptacion

- el app emite factura offline con CAI/correlativo valido;
- el servidor persiste exactamente el mismo numero;
- no hay duplicados en reintentos;
- el sistema detecta agotamiento antes de permitir nueva emision;
- existe vista operativa para controlar bloques y consumos;
- existen pruebas documentadas de agotamiento, duplicado y reproceso.

