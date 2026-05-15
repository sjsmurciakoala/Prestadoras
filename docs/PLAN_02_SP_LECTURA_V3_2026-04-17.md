# Plan 02: `sp_lectura_v3`

Fecha: 2026-04-17

Objetivo: definir el procedimiento operativo que registra una lectura, genera la factura y deja persistido el resultado comercial usando el motor tarifario nuevo.

---

## 1. Rol dentro del flujo

`sp_lectura_v3` es la pieza que une:

- captura de lectura;
- calculo servidor;
- persistencia comercial;
- y respuesta de factura inmediata.

En otras palabras:

- `sp_adm_calcular_factura_lectura` calcula
- `sp_lectura_v3` guarda y devuelve resultado final

---

## 2. Responsabilidad exacta

Debe:

1. recibir datos minimos desde el WS;
2. validar que el periodo esta abierto;
3. invocar `sp_adm_calcular_factura_lectura`;
4. insertar/actualizar `historicomedicion`;
5. insertar `factura`;
6. insertar `factura_detalle`;
7. insertar `transaccion_abonado`;
8. devolver el comprobante final listo para app.

No debe:

- depender de montos enviados por el app;
- delegar el calculo al movil;
- mezclar contabilidad automaticamente en esta primera iteracion si eso agrega ruido.

---

## 3. Entradas

### 3.1 Datos de captura

- `p_company_id`
- `p_anio`
- `p_mes`
- `p_ciclo`
- `p_clave`
- `p_contador`
- `p_fecha_lectura`
- `p_lectura_actual`
- `p_condicion_lectura`
- `p_lectura_promedio`
- `p_usuario`
- `p_observacion`
- `p_imagen`
- `p_informativo`
- `p_id_cai`
- `p_correlativo_cai`
- `p_numero_factura`

### 3.2 Datos opcionales de compatibilidad

Puede aceptar temporalmente:

- `p_tienemedidor`
- `p_categoria`

Pero solo para auditoria o comparacion; no como fuente de verdad.

---

## 4. Salida

Debe devolver al WS un resultado estructurado con:

- exito / error;
- numero_factura;
- id_factura;
- cliente;
- consumo;
- subtotal;
- descuentos;
- total;
- detalle por servicio;
- mensaje operativo.

Idealmente en JSON.

---

## 5. Persistencia esperada

### 5.1 `historicomedicion`

Debe seguir existiendo como columna vertebral del ciclo.

`sp_lectura_v3` debe:

- actualizar la lectura registrada;
- guardar consumo;
- guardar condicion;
- guardar usuario;
- guardar fecha de lectura;
- guardar `taservi1..4` como salida de compatibilidad;
- guardar correlativo/factura si el modelo actual lo requiere.

### 5.2 `factura`

Debe insertar la cabecera de factura con:

- cliente
- fecha
- numero/correlativo
- total
- ciclo
- anio / mes
- origen = lectura

### 5.3 `factura_detalle`

Debe insertar una linea por servicio calculado:

- agua
- alcantarillado
- ambiental
- ersaps
- y otros derivados cuando se agreguen

### 5.4 `transaccion_abonado`

Debe insertar el movimiento comercial resultante.

Minimo:

- cliente
- tipo de transaccion facturacion
- referencia de factura
- monto
- fecha

---

## 6. Compatibilidad con el legado

### 6.1 Lo que debe conservar

Debe seguir llenando:

- `taservi1`
- `taservi2`
- `taservi3`
- `taservi4`

solo para que:

- pantallas legacy;
- reportes;
- consultas operativas

no se rompan mientras migramos.

### 6.2 Lo que ya no debe hacer

No debe tomar:

- `taservi1..4`
- total calculado por app

como entrada fuente.

---

## 7. Transaccion y consistencia

`sp_lectura_v3` debe correr en una sola transaccion.

Si falla cualquiera de estos pasos:

- calculo
- `historicomedicion`
- `factura`
- `factura_detalle`
- `transaccion_abonado`

entonces debe revertir todo.

---

## 8. Manejo de errores

Debe devolver errores claros para el WS:

- cliente no existe
- periodo cerrado
- CAI invalido
- configuracion tarifaria faltante
- cliente sin `adm_cliente_servicio`
- error al insertar factura

Y debe poder exponer un mensaje corto para app y uno tecnico para log.

---

## 9. Relacion con contabilidad

En esta fase:

- el foco es que lectura/factura funcione comercialmente.

La integracion contable puede quedar:

- apagada,
- desacoplada,
- o en paso posterior.

Si la dejamos, debe ir como bloque adicional no obligatorio del procedimiento.

---

## 10. Criterio de aceptacion

Este bloque queda listo cuando:

1. registra lectura correctamente;
2. genera factura correctamente;
3. genera detalle por servicio;
4. genera transaccion de abonado;
5. devuelve factura lista para entregar al cliente;
6. mantiene `taservi1..4` para compatibilidad;
7. no depende del calculo local del app.
