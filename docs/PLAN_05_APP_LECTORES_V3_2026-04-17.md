# Plan 05: App Lectores - migración a V3

Fecha: `2026-04-17`

Objetivo: definir los cambios del app Android para que deje de depender del cálculo legacy y pase a trabajar con el WS nuevo y con un snapshot V3 cuando opere offline, manteniendo que la factura emitida desde el app sea la factura formal que luego debe verse igual en portal.

---

## 1. Estado actual real

Hoy el app ya no está en fase de diseño. El flujo V3 ya avanzó bastante.

### Ya implementado

- consumo de `ActualizarLecturaV3`
- consumo de `GetOfflineSnapshotV3`
- snapshot formal V3 persistido en SQLite
- snapshot offline V3 persistido en SQLite
- cola formal `LecturaV3SyncQueue`
- estados visibles:
  - `PENDING_SYNC`
  - `SYNCED`
  - `SYNC_ERROR`
  - `SYNC_CONFLICT`
- validación de conflicto si el servidor devuelve una factura distinta
- bloqueo del fallback V2 cuando ya existe factura formal local
- detalle e impresión priorizando snapshot formal V3
- `GenerarFactura.java` emitiendo desde snapshot formal offline V3
- build del APK validado
- instalación del APK validada en dispositivo

### Compatibilidad temporal que todavía existe

- `ActualizarLecturaV2`
- tablas legacy SQLite:
  - `Tarifa`
  - `TarifaContador`
  - `CobroAdicional`

Esas piezas ya no deben considerarse el diseño final, pero todavía existen como compatibilidad temporal.

---

## 2. Estado objetivo

La app debe operar así:

### Online

1. descarga ruta/ciclo/CAI/condiciones
2. captura lectura
3. envía captura a `ActualizarLecturaV3`
4. recibe respuesta oficial del servidor
5. muestra e imprime la factura oficial

### Offline

1. descarga ruta/ciclo + snapshot offline V3
2. captura lectura sin conectividad
3. calcula factura formal desde snapshot V3
4. imprime o muestra la factura formal marcada `PENDIENTE_SYNC`
5. guarda la lectura en cola local
6. al recuperar red, sincroniza y el servidor debe persistir esa misma factura

Regla cerrada:

- la factura emitida desde el app es la factura formal
- si servidor y app difieren, no se acepta silenciosamente

---

## 3. Descargas

### 3.1 Deben seguir

- `GetRuta`
- `GetCiclo`
- `GetCAI`
- `CondicionesLectura`
- `Informativos`
- autenticación
- `GetOfflineSnapshotV3`

### 3.2 Deben retirarse al final del corte

- `Tarifa`
- `TarifaContador`
- `CobroAdicional`

Nota:

esas descargas ya no deben ser fuente de verdad del cálculo. Si todavía sobreviven en código o almacenamiento local, es solo por compatibilidad temporal.

---

## 4. Impacto en código Android

### 4.1 Archivos principales tocados

- `GenerarFactura.java`
- `UtilidadesBD.java`
- `Utilidades.java`
- `DetallesFactura.java`
- `ImpresoraUniversal.java`
- `subir_informacion.java`
- `ListaMedidores.java`
- `AdaptadorMedidores.java`

### 4.2 Estado funcional del código

#### Ya cubierto

- endpoint `ActualizarLecturaV3` integrado
- endpoint `GetOfflineSnapshotV3` integrado
- parser `LecturaRespuestaV3`
- parser de paquete offline V3
- snapshot formal V3 en SQLite
- snapshot offline V3 en SQLite
- cola local formal
- resumen visual de cola
- badge visual por medidor
- detalle de error/conflicto
- impresión y detalle desde snapshot formal
- conflicto marcado cuando servidor devuelve otra factura

#### Todavía pendiente

- cerrar la compatibilidad temporal con V2
- retirar código muerto legacy cuando el corte operativo ya sea definitivo
- validar flujo funcional real con datos operativos, no solo build técnico

---

## 5. Bloqueos reales actuales

Estos son los pendientes de verdad. No teóricos.

### 5.1 Datos operativos de prueba

Hoy el bloqueo más frecuente no está siendo el código Android, sino la disponibilidad de:

- ruta válida
- ciclo válido
- período abierto
- clientes visibles por `GetRuta`

Sin eso, el app puede compilar e instalar, pero la prueba operativa no cierra.

### 5.2 CAI/correlativo offline

Sigue faltando definir:

- reserva formal de bloques CAI
- persistencia local de esa reserva
- conciliación al sincronizar sin renumeración silenciosa

### 5.3 Limpieza del legado

Todavía no debe borrarse nada crítico, pero sí queda pendiente:

- retirar `ActualizarLecturaV2`
- retirar dependencias legacy del cálculo
- limpiar tablas/código muerto del app

---

## 6. Riesgos

- diferencia entre factura offline emitida y factura servidor
- impresión rota por cambio de fuente de datos
- CAI manejado incompletamente en modo offline
- convivir demasiado tiempo con V2 y legado

---

## 7. Criterio de aceptación

Este bloque queda bien cerrado cuando:

1. el app descarga ruta válida y snapshot offline V3
2. el app emite factura formal offline desde snapshot V3
3. el app sincroniza esa factura al servidor
4. el servidor persiste la misma factura o marca conflicto
5. el app ya no depende operativamente de `Tarifa`, `TarifaContador` y `CobroAdicional`
6. `ActualizarLecturaV2` deja de ser necesario

---

## 8. Siguiente paso

El siguiente paso correcto en este bloque no es más refactor Android aislado.

Es:

1. estabilizar una ruta/ciclo de prueba real
2. hacer la prueba end-to-end app -> WS -> BD
3. cerrar CAI/correlativo offline
4. después retirar compatibilidad legacy
