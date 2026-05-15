# Plan 01: `sp_adm_calcular_factura_lectura`

Fecha: 2026-04-17

Objetivo: definir el procedimiento central de calculo tarifario para lectura, de manera que el servidor sea la fuente de verdad del monto facturado y del detalle por servicio.

---

## 1. Rol dentro del nuevo flujo

`sp_adm_calcular_factura_lectura` no guarda factura ni movimientos comerciales.

Su responsabilidad es solo:

1. recibir el contexto de la lectura;
2. resolver los servicios base del cliente;
3. aplicar tarifas, tasas y ajustes;
4. devolver el detalle calculado y los totales;
5. dejar una salida estable para que `sp_lectura_v3` persista el resultado.

En otras palabras:

- **calcula**
- **no persiste factura**

Adicionalmente, este procedimiento debe convertirse en el contrato canonico del calculo.

Eso importa por el requisito offline:

- online se ejecuta en servidor;
- offline su salida debe poder ser reproducida desde un snapshot controlado del mismo modelo.

---

## 2. Dependencias ya disponibles

Este procedimiento debe apoyarse en lo que ya construimos:

- `adm_cliente_servicio`
- `adm_cuadro_tarifario`
- `adm_regla_tarifaria`
- `adm_ajuste_tarifario`
- `sp_adm_resolver_tarifa_cliente_servicio`
- `sp_adm_resolver_servicio_derivado_cliente_servicio`

Tambien debe usar tablas operativas existentes:

- `cliente_maestro`
- `historicomedicion` solo para consultar si hace falta dato previo
- `historialmes` para validar periodo abierto
- `condicion_lectura` si se requiere validar reglas de condicion

---

## 3. Entradas del procedimiento

Debe recibir solo datos de captura y contexto operativo. No debe depender de montos enviados por el app.

### 3.1 Entrada minima

- `p_company_id`
- `p_cliente_id`
- `p_contador`
- `p_fecha_lectura`
- `p_lectura_actual`
- `p_condicion_lectura`
- `p_lectura_promedio`
- `p_usuario`
- `p_observacion`
- `p_id_cai`
- `p_correlativo_cai`
- `p_numero_factura`
- `p_imagen`
- `p_informativo`

### 3.2 Datos que el procedimiento debe resolver

No deben venir como verdad desde el app:

- si el cliente tiene medidor o no;
- categoria regulatoria;
- segmento tarifario;
- servicios activos del cliente;
- monto de agua;
- monto de alcantarillado;
- monto ambiental;
- monto ERSAPS;
- descuento final;
- total final.

---

## 4. Salida esperada

La salida debe ser suficiente para:

- mostrar la factura al lector;
- persistir el resultado en `sp_lectura_v3`;
- y eventualmente imprimirla.

### 4.1 Encabezado de salida

- `cliente_id`
- `clave`
- `cliente_nombre`
- `numero_factura`
- `id_cai`
- `correlativo_cai`
- `fecha_factura`
- `condicion_lectura_aplicada`
- `lectura_anterior`
- `lectura_actual`
- `consumo_facturable`
- `subtotal_servicios`
- `subtotal_ajustes`
- `saldos_anteriores`
- `recargos`
- `total_factura`

### 4.2 Detalle por servicio

Una salida tabular o JSON con:

- `servicio_codigo`
- `servicio_nombre`
- `tipo_servicio`
- `origen_calculo`
- `cliente_servicio_id`
- `cuadro_tarifario_id`
- `regla_tarifaria_id`
- `cantidad`
- `monto`
- `aplica_descuento`
- `monto_descuento`
- `monto_final`

### 4.3 Salida para compatibilidad

Mientras exista legado, debe devolver tambien:

- `taservi1`
- `taservi2`
- `taservi3`
- `taservi4`

Pero solo como **salida derivada**:

- `taservi1` = agua
- `taservi2` = alcantarillado
- `taservi3` = ambiental
- `taservi4` = ersaps

Nunca como entrada fuente.

### 4.4 Salida para offline

Debe devolver tambien una estructura estable para serializar en la app:

- `detalle_servicios_json`
- `warnings_json`
- `snapshot_contract_version`

La idea no es que el SQL conozca SQLite o Android.

La idea es que la forma de salida del calculo sea reutilizable por:

- `sp_lectura_v3`
- `ActualizarLecturaV3`
- y la capa offline derivada.

---

## 5. Logica que debe ejecutar

### 5.1 Validacion inicial

Debe validar:

- cliente existe y esta activo;
- hay periodo abierto para la fecha;
- existe CAI valido;
- la condicion de lectura es valida;
- hay por lo menos un `adm_cliente_servicio` base activo para el cliente.

### 5.2 Normalizacion de condicion

Debe replicar el comportamiento comercial actual:

- `MIN` y `PND`:
  - consumo facturable = 0
  - lectura actual efectiva = lectura anterior
- `PD`:
  - consumo facturable = promedio
  - lectura actual efectiva = lectura anterior + promedio
- `N`:
  - usa lectura actual enviada

### 5.3 Resolucion de servicios base

Debe resolver al menos:

- `AGUA_POTABLE`
- `ALCANTARILLADO` si el cliente lo tiene asignado

### 5.4 Resolucion de servicios derivados

Debe resolver al menos:

- `TASA_AMBIENTAL`
- `TASA_SVA_ERSAPS`

### 5.5 Ajustes

Debe dejar punto de extension para:

- adulto mayor / jubilado
- topes
- exoneraciones
- descuentos especiales

En la primera version:

- el procedimiento puede dejar los ajustes desacoplados en una seccion separada
  o en salida vacia si todavia no implementamos `adm_ajuste_tarifario`.

### 5.6 Saldos anteriores y recargos

No debemos perder el flujo operativo actual.

Por eso el procedimiento debe dejar previstos:

- saldos anteriores por servicio;
- recargos por servicio;
- total final acumulado.

Si en la primera iteracion no recalculamos todos los legacy balances, al menos debe:

- recibirlos desde la base operativa actual;
- separarlos del calculo tarifario puro;
- y devolverlos claramente.

---

## 6. Tablas que toca

### 6.1 Lee

- `adm_cliente_servicio`
- `adm_cuadro_tarifario`
- `adm_regla_tarifaria`
- `adm_ajuste_tarifario`
- `cliente_maestro`
- `historicomedicion`
- `historialmes`

### 6.2 No debe escribir

- `factura`
- `factura_detalle`
- `transaccion_abonado`
- `historicomedicion`

Eso le corresponde a `sp_lectura_v3`.

---

## 7. Contrato recomendado

### 7.1 Opcion tecnica

Dos salidas:

1. resultado encabezado
2. detalle JSON por servicio

### 7.2 Recomendacion

Usar:

- un procedimiento que escriba a tabla temporal o devuelva un solo `jsonb`

o

- una funcion principal que devuelva filas de detalle y una segunda de resumen

Para el WS es mas comodo si existe una salida JSON estable.

---

## 8. Riesgos

- mezclar calculo tarifario con persistencia comercial;
- volver a depender de `taservi1..4` como entrada;
- recalcular algo distinto a lo esperado en app sin golden cases;
- no contemplar `condicion_lectura`.

---

## 9. Criterio de aceptacion

Este bloque se considera listo cuando:

1. dado un cliente real, devuelve agua correcta;
2. devuelve alcantarillado correcto si aplica;
3. devuelve ambiental correcto;
4. devuelve ERSAPS correcto;
5. devuelve total final consistente;
6. llena `taservi1..4` como salida de compatibilidad;
7. no persiste nada todavia.

---

## 10. Lo que no vamos a hacer aqui

- no insertar factura;
- no actualizar `historicomedicion`;
- no consumir el endpoint WCF;
- no tocar Android;
- no diseñar UI del portal.

Este documento es solo del motor de calculo servidor.
