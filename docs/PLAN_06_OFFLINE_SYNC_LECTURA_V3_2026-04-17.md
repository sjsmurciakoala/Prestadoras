# Plan 06: Offline + sincronización Lectura V3

Fecha: `2026-04-17`

Objetivo: garantizar que la app lectora siga operando sin conectividad, sin volver a convertir el cálculo local en una segunda fuente de verdad permanente, y asegurando que la factura emitida por el app sea la misma factura formal que luego queda visible en portal.

---

## 1. Punto de realineación

Tenemos tres reglas cerradas:

1. la factura debe poder entregarse al cliente en el momento de la lectura
2. la app debe seguir funcionando offline
3. la factura emitida por el app es la factura formal

Eso significa que el corte V3 no puede ser “solo servidor”.

Necesitamos:

- un modo online oficial
- un modo offline controlado
- reconciliación posterior sin renumeración silenciosa

---

## 2. Modos operativos

### 2.1 Modo online

Cuando hay conectividad:

- la app envía captura a `ActualizarLecturaV3`
- el servidor ejecuta `sp_lectura_v3`
- el servidor calcula la factura oficial
- la app muestra e imprime la respuesta oficial

### 2.2 Modo offline

Cuando no hay conectividad:

- la app trabaja con un snapshot local del motor tarifario
- calcula una factura formal con el mismo contrato funcional del servidor
- imprime o muestra la factura formal marcada `PENDIENTE_SYNC`
- guarda la lectura en cola local para sincronización posterior

Regla:

- al sincronizar, el servidor debe persistir la misma factura o marcar conflicto

---

## 3. Estado real actual

### Ya implementado

- `sp_adm_generar_snapshot_offline_cliente_lectura`
- endpoint WS `GetOfflineSnapshotV3`
- descarga del snapshot offline V3 en la app
- almacenamiento local del paquete offline
- evaluación offline del snapshot para emitir factura formal
- cola local formal de sincronización
- estados:
  - `PENDING_SYNC`
  - `SYNCED`
  - `SYNC_ERROR`
  - `SYNC_CONFLICT`
- validación de conflicto si servidor devuelve una factura distinta

### Lo que todavía falta

- CAI/correlativo offline formal
- validación end-to-end en ambiente real
- soporte operativo adicional si aparecen conflictos reales

---

## 4. Regla clave del diseño

No vamos a tener dos motores tarifarios distintos.

Vamos a tener:

- un motor servidor oficial
- y un snapshot offline derivado del mismo modelo `adm_*`

La app no debe seguir descargando tablas legacy de cálculo como fuente de verdad.

Si necesita operar offline, debe descargar un paquete V3 consistente con:

- cliente/servicios activos
- categoría regulatoria
- condición de medición
- segmento tarifario
- cuadros y reglas vigentes
- configuración de ciclo/ruta
- versión/hash del snapshot

---

## 5. Salidas que debe soportar la app offline

La app offline debe poder producir:

- resumen de lectura
- detalle por servicio
- total formal calculado offline
- estado de sincronización
- payload listo para cola local

Y debe guardar al menos:

- `sync_status`
- `sync_attempts`
- `sync_last_error`
- `snapshot_version`
- `snapshot_hash`
- `server_confirmed_total`
- `server_confirmed_payload`
- `offline_difference_flag`
- `offline_numero_factura`
- `offline_correlativo_cai`
- `offline_id_cai`

---

## 6. Estrategia de sincronización

Cuando la app recupere conectividad:

1. envía la captura original
2. envía también el resultado local emitido como bloque de comparación
3. el servidor recalcula
4. si coincide dentro de tolerancia, persiste la misma factura y marca sincronizado
5. si no coincide, no debe renumerar silenciosamente: conserva ambos resultados y marca conflicto

No debe sobrescribirse silenciosamente una diferencia.

Debe quedar trazable:

- monto local emitido
- monto servidor
- diferencia
- causa de conflicto si se puede determinar

---

## 7. Implicación en el contrato WS

### Ya cubierto

- `ActualizarLecturaV3` como endpoint oficial online
- `GetOfflineSnapshotV3` como endpoint de snapshot offline

### Pendiente

- cerrar la validación operativa del paquete offline V3 contra el ambiente real
- definir si hará falta un endpoint específico de sincronización diferida o si la cola seguirá cerrándose con `ActualizarLecturaV3`

---

## 8. Implicación en el motor SQL

`sp_adm_calcular_factura_lectura` debe seguir siendo el contrato canónico.

Eso implica:

- entradas mínimas y estables
- salidas serializables como snapshot/app payload
- lógica reproducible por la capa offline derivada

Pendiente real del motor para offline:

- CAI/correlativo
- más reglas legales, por ejemplo tercera edad/topes

---

## 9. Fases recomendadas

### Fase 1

- servidor online oficial:
  - `sp_adm_calcular_factura_lectura`
  - `sp_lectura_v3`
  - `ActualizarLecturaV3`

Estado:

- completada

### Fase 2

- snapshot offline V3
- descarga y persistencia local

Estado:

- completada en su base técnica
- pendiente validación operativa con datos reales

### Fase 3

- cola de sincronización
- conciliación servidor vs local

Estado:

- implementada en su base técnica
- pendiente CAI/correlativo y validación operativa completa

### Fase 4

- retiro de `Tarifa`, `TarifaContador`, `CobroAdicional` y demás legado del app

Estado:

- pendiente

---

## 10. Criterio de aceptación

Este bloque queda bien cerrado cuando:

1. la app puede facturar online con respuesta oficial del servidor
2. la app puede capturar e imprimir offline la factura formal con marca `PENDIENTE_SYNC`
3. el servidor puede recalcular y persistir la misma factura al sincronizar
4. no depende operativamente del tarifario legacy para trabajar offline
5. la diferencia entre cálculo local y servidor queda trazable
6. el número de factura entregado al cliente es el mismo que luego aparece en portal
7. CAI/correlativo offline queda formalmente resuelto

---

## 11. Siguiente paso

El siguiente paso práctico en este bloque es:

1. estabilizar datos de ruta/ciclo de prueba
2. validar descarga real de snapshot offline V3
3. emitir factura offline en dispositivo
4. sincronizar al servidor
5. luego cerrar CAI/correlativo offline
