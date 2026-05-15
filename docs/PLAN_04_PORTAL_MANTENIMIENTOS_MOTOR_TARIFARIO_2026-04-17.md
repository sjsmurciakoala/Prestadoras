# Plan 04: Portal - mantenimientos minimos del motor tarifario

Fecha: 2026-04-17

Objetivo: definir las vistas minimas del portal para operar el motor nuevo sin bloquear la integracion WS/app.

---

## 1. Principio

Estos mantenimientos son necesarios, pero no deben atrasar el corte tecnico principal.

Se construyen en paralelo a:

- `sp_adm_calcular_factura_lectura`
- `sp_lectura_v3`
- `ActualizarLecturaV3`

---

## 2. Vistas minimas necesarias

### 2.1 Mantenimiento de `adm_cliente_servicio`

Es la vista mas importante del portal.

Debe permitir:

- buscar cliente;
- ver servicios asignados;
- crear servicio base para cliente;
- editar categoria regulatoria;
- editar condicion de medicion;
- editar segmento tarifario;
- activar/desactivar asignacion.

#### Campos clave

- cliente
- servicio
- categoria regulatoria
- condicion de medicion
- segmento tarifario
- cuadro tarifario override opcional
- fecha alta
- fecha baja
- estado

#### Objetivo

Que el cliente quede correctamente configurado para que el motor pueda resolver.

---

### 2.2 Mantenimiento tarifario

Debe cubrir minimo:

- `adm_servicio`
- `adm_cuadro_tarifario`
- `adm_regla_tarifaria`
- `adm_ajuste_tarifario`

#### Alcance inicial

No hace falta un diseñador super complejo en la primera iteracion.

Basta con:

- listas
- alta / edicion
- filtros por servicio, categoria y vigencia
- detalle de reglas por cuadro

#### Meta

Poder administrar el motor sin tocar SQL a mano.

---

### 2.3 Pantalla de prueba de calculo

Debe permitir:

- seleccionar cliente
- fecha
- lectura actual
- condicion de lectura
- promedio
- ejecutar calculo
- ver detalle por servicio
- ver total

Esta pantalla sirve para:

- validar con negocio;
- comparar con app legacy;
- depurar el motor sin pasar por Android.

---

## 3. Vistas que se mantienen sin cambios funcionales mayores

Por ahora se conservan:

- `Ciclos`
- `Auxiliar de Lectura`
- `Reglas legacy (lectura)`

La idea es:

- no romper operación;
- usar esas pantallas para seguir gestionando el ciclo;
- mientras el motor nuevo madura.

---

## 4. API del portal que probablemente hará falta

### 4.1 Para `adm_cliente_servicio`

- `GET /api/tarifario/clientes-servicio`
- `GET /api/tarifario/clientes-servicio/{id}`
- `POST /api/tarifario/clientes-servicio`
- `PUT /api/tarifario/clientes-servicio/{id}`
- `POST /api/tarifario/clientes-servicio/{id}/desactivar`

### 4.2 Para tarifario

- `GET /api/tarifario/servicios`
- `GET /api/tarifario/cuadros`
- `GET /api/tarifario/cuadros/{id}/reglas`
- `POST /api/tarifario/cuadros`
- `PUT /api/tarifario/cuadros/{id}`
- `POST /api/tarifario/reglas`
- `PUT /api/tarifario/reglas/{id}`

### 4.3 Para prueba de calculo

- `POST /api/tarifario/calculo/prueba`

Esta API puede apoyarse directamente en:

- `sp_adm_calcular_factura_lectura`

---

## 5. Capas a tocar

### 5.1 Backend

- controladores nuevos en `Prestadoras/apc/Controllers`
- servicios nuevos en `Prestadoras/SIAD.Services`
- DTOs nuevos en `SIAD.Core/DTOs`

### 5.2 Cliente Blazor

- páginas nuevas en `Prestadoras/apc.Client/Pages`
- clients HTTP nuevos en `Prestadoras/apc.Client/Services`
- navegación nueva en sidebar

---

## 6. Prioridad real

Orden recomendado:

1. `adm_cliente_servicio`
2. pantalla de prueba de calculo
3. mantenimiento tarifario

Si intentamos arrancar por un maestro tarifario completo, podemos atrasar innecesariamente el corte del WS.

---

## 7. Criterio de aceptacion

Este bloque queda listo cuando:

1. se puede asignar correctamente un servicio a un cliente;
2. se puede ejecutar una prueba de calculo desde portal;
3. se puede revisar y corregir un cuadro/regla sin ir a SQL;
4. `Ciclos` y `Auxiliar de Lectura` siguen funcionando sin regresion.
