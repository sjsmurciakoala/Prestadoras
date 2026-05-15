# Plan de Integracion: Lectura + WS + Portal + Motor Tarifario

Fecha: 2026-04-17

Objetivo: integrar el motor tarifario nuevo al flujo real de lectura sin romper el ciclo operativo de:

- generacion de periodo;
- descarga de ruta/ciclo en app;
- captura de lectura;
- facturacion;
- auxiliar de lectura;
- y persistencia comercial en PostgreSQL.

---

## 1. Principio rector

El cambio correcto no es "reemplazar tarifas viejas y ya".

El cambio correcto es:

1. mantener operativo el ciclo de lectura actual;
2. mover el calculo al servidor;
3. dejar la app como cliente de captura;
4. y solo despues retirar el legado.

Mientras el app siga dependiendo de descargas legacy (`Tarifa`, `TarifaContador`, `CobroAdicional`), no conviene eliminar:

- `sp_tarifas_ws`
- `sp_tarifas_contador_ws`
- `sp_cobros_adicionales_ws`
- `tarifas`
- `tarifas_contador`
- `configuracion_cobros_adicionales`
- `configuracion_cobros_adicionales_detalle`
- `servicios_roles_ws`
- `sp_lectura`
- `sp_lectura_v2`

Todavia.

---

## 2. Estado objetivo

### 2.1 App de lectura

La app ya no debe depender del calculo legacy.

La app solo debe:

- descargar ruta y datos del abonado;
- capturar lectura actual;
- capturar condicion de lectura;
- adjuntar observacion / evidencia / usuario;
- enviar datos minimos al WS.

Cuando no haya conectividad:

- debe operar con snapshot V3;
- debe generar la factura formal con CAI/correlativo reservado;
- y debe dejar la lectura en cola para sincronizacion posterior.

### 2.2 WS

El WS debe:

- recibir la lectura;
- resolver el perfil tarifario del cliente;
- calcular agua, alcantarillado, ambiental y ERSAPS;
- persistir factura, detalle, historicomedicion y transaccion_abonado;
- devolver el resultado final a la app.

### 2.3 Portal

El portal debe convertirse en la fuente de verdad para:

- configuracion tarifaria;
- asignacion cliente-servicio;
- categoria regulatoria;
- condicion de medicion;
- segmento tarifario;
- apertura/cierre de periodos de lectura;
- y monitoreo de resultados.

---

## 3. Lo que se conserva temporalmente

Estas piezas se deben conservar durante la transicion:

- `AuxiliarLecturaService`
- `AuxiliarLecturaController`
- pantalla de `Auxiliar de Lectura`
- pantalla de `Ciclos`
- `sp_medidores_por_ruta_ws`
- `sp_informacion_ciclo`
- `sp_cai_por_ruta`
- `sp_condicion_lectura_ws`
- `historicomedicion`
- `factura`
- `factura_detalle`
- `transaccion_abonado`

Motivo:

esas piezas no son el problema principal; son el esqueleto operativo del ciclo de lectura.

---

## 4. Lo que se deja de usar primero

Estas piezas no deben borrarse de inmediato, pero si deben dejar de ser la fuente de verdad cuando entremos a corte:

- `sp_tarifas_ws`
- `sp_tarifas_contador_ws`
- `sp_cobros_adicionales_ws`
- `sp_cobros_adicionales_ws_v2`
- `tarifas`
- `tarifas_contador`
- `configuracion_cobros_adicionales`
- `configuracion_cobros_adicionales_detalle`
- calculo local de `GenerarFactura.java`
- envio de montos calculados desde la app (`taservi1..4` como fuente)

Estas piezas pasan a modo compatibilidad mientras hacemos el cambio del app.

### 4.1 Que significa "dejar de ser la fuente de verdad"

No significa que vamos a tener dos motores definitivos.

Significa esto:

- el motor nuevo (`adm_*` + resolvers + `sp_adm_calcular_factura_lectura`) pasa a ser
  la fuente de verdad del calculo;
- lo legacy sigue vivo solo para compatibilidad de descarga o transicion;
- y cuando el app nuevo ya no lo use, se elimina.

La fuente de verdad nueva sera:

- `adm_cliente_servicio`
- `adm_cuadro_tarifario`
- `adm_regla_tarifaria`
- `adm_ajuste_tarifario`
- `sp_adm_calcular_factura_lectura`
- `sp_lectura_v3`
- `ActualizarLecturaV3`

---

## 5. Lo que si debemos construir

### 5.1 En base de datos

Ya construido:

- `adm_tipo_servicio`
- `adm_servicio`
- `adm_categoria_regulatoria`
- `adm_segmento_tarifario`
- `adm_condicion_medicion`
- `adm_tipo_regla_tarifaria`
- `adm_tipo_ajuste_tarifario`
- `adm_cuadro_tarifario`
- `adm_regla_tarifaria`
- `adm_ajuste_tarifario`
- `adm_cliente_servicio`

Ya construido tambien:

- seed APC base;
- seed ERSAPS;
- resolver base por `adm_cliente_servicio`;
- resolver derivado para `TASA_AMBIENTAL` y `TASA_SVA_ERSAPS`.

Pendiente en BD:

1. `sp_adm_calcular_factura_lectura`
   - entrada: lectura, cliente, fecha, condicion, promedio;
   - salida: detalle calculado por servicio, descuentos, saldos, recargos y totales.

2. `sp_lectura_v3`
   - recibe datos minimos;
   - llama al motor nuevo;
   - guarda `historicomedicion`, `factura`, `factura_detalle`, `transaccion_abonado`;
   - devuelve numero de factura, CAI, detalle final y total para entrega inmediata al cliente.

3. compatibilidad temporal:
   - seguir llenando `taservi1..4` en `historicomedicion` si todavia lo necesita alguna pantalla o reporte;
   - pero ya no como entrada de calculo, sino como salida derivada del motor nuevo.

### 5.2 En el WS de lectura

Pendiente:

1. agregar endpoint nuevo:
   - `POST /ActualizarLecturaV3`

2. ese endpoint debe:
   - dejar de confiar en montos enviados por la app;
   - aceptar solo datos de captura;
   - invocar `sp_adm_calcular_factura_lectura` o `sp_lectura_v3`;
   - responder con la factura ya calculada para mostrar o imprimir en el momento de la lectura.

3. mantener por un tiempo:
   - `POST /ActualizarLectura`
   - `POST /ActualizarLecturaV2`

4. estrategia:
   - `V1/V2` siguen vivos mientras el app viejo exista;
   - `V3` se vuelve la ruta nueva del app actualizado.

### 5.3 En la app Android

Pendiente:

1. dejar de descargar:
   - `Tarifa`
   - `TarifaContador`
   - `CobroAdicional`

2. mantener solo:
   - ruta
   - ciclo
   - CAI
   - condiciones de lectura
   - informativos
   - usuario

3. eliminar el calculo local de:
   - agua
   - alcantarillado
   - ambiental
   - ERSAPS
   - descuentos

4. cambiar el payload enviado:
   - enviar lectura real, condicion, promedio, clave, contador, CAI, evidencia y observacion;
   - no enviar montos finales como fuente principal.

5. cambiar el payload recibido:
   - recibir numero de factura;
   - recibir total;
   - recibir detalle por servicio;
   - recibir datos listos para impresion inmediata.

6. agregar pista offline:
   - snapshot tarifario V3;
   - bloque CAI/rangos reservados para emision formal;
   - cola local;
   - conciliacion servidor vs factura emitida localmente.

### 5.4 En el portal web

Pendiente:

1. pantalla de administracion tarifaria nueva:
   - `adm_servicio`
   - `adm_cuadro_tarifario`
   - `adm_regla_tarifaria`
   - `adm_ajuste_tarifario`

2. pantalla de asignacion cliente-servicio:
   - `adm_cliente_servicio`
   - categoria regulatoria
   - condicion de medicion
   - segmento tarifario

3. pantalla de validacion de calculo:
   - probar un cliente;
   - ver detalle por servicio;
   - ver total calculado.

4. conservar por ahora:
   - `Ciclos`
   - `Auxiliar de Lectura`
   - `Reglas legacy (lectura)` solo como referencia mientras se desmonta el legado.

5. alcance recomendado:
   - estos mantenimientos del portal van en una pista separada del corte del WS/app;
   - no deben bloquear la salida de `sp_adm_calcular_factura_lectura` ni de `sp_lectura_v3`.

---

## 6. Corte recomendado por fases

### Fase 0. Congelar lo actual

- no borrar tablas legacy;
- no borrar endpoints del WS;
- no borrar vistas/pantallas que sostienen el ciclo actual;
- dejar el motor nuevo funcionando en paralelo.

### Fase 1. Calculo servidor en paralelo

- crear `sp_adm_calcular_factura_lectura`;
- hacer pruebas paralelas contra casos reales;
- comparar resultado del motor nuevo vs app actual.

Resultado esperado:

- mismo resultado comercial;
- sin depender de SQLite para tarifas.

### Fase 2. WS nuevo

- crear `ActualizarLecturaV3`;
- conectar `ActualizarLecturaV3` al motor nuevo;
- guardar detalle facturado por servicio.
- devolver comprobante/factura al instante.

Resultado esperado:

- la app sigue leyendo y subiendo;
- pero el calculo ya vive en servidor.
- el lector puede entregar o mostrar la factura al cliente en el mismo momento.

### Fase 3. App nueva

- quitar calculo local;
- quitar descarga de tarifas;
- enviar solo datos de captura;
- consumir `ActualizarLecturaV3`.

Resultado esperado:

- la app se simplifica;
- desaparece el riesgo de desactualizacion de tarifas en dispositivo.

### Fase 4. Limpieza del legado

Solo despues de validar Fase 3:

- retirar `sp_tarifas_ws`
- retirar `sp_tarifas_contador_ws`
- retirar `sp_cobros_adicionales_ws`
- retirar tablas `tarifas` / `tarifas_contador` si ya no las consume nadie
- retirar configuraciones legacy de cobros adicionales si ya fueron migradas
- retirar logica local del app

---

## 7. Que NO debemos eliminar todavia

No eliminar todavia:

- `historicomedicion`
- `factura`
- `factura_detalle`
- `transaccion_abonado`
- `historialmes`
- `AuxiliarLecturaService`
- pantalla `Auxiliar de Lectura`
- pantalla `Ciclos`
- `sp_medidores_por_ruta_ws`
- `sp_informacion_ciclo`
- `sp_cai_por_ruta`
- `sp_condicion_lectura_ws`
- `sp_lectura_v2`

Motivo:

todo eso forma parte del ciclo operativo real y todavia es necesario para convivir mientras hacemos el corte.

---

## 8. Primer bloque de implementacion recomendado

El siguiente bloque de trabajo debe ser este:

1. crear `sp_adm_calcular_factura_lectura`;
2. crear `sp_lectura_v3`;
3. exponer `ActualizarLecturaV3` en el WS;
4. agregar en portal la administracion de `adm_cliente_servicio`;
5. agregar en portal una pantalla de prueba de calculo para un cliente;
6. solo despues tocar la app Android.

Ese orden reduce riesgo y mantiene operativa la lectura.

### 8.1 Aclaracion operativa

Los mantenimientos del portal si son necesarios, pero pueden entrar en paralelo.

No deben bloquear:

1. `sp_adm_calcular_factura_lectura`
2. `sp_lectura_v3`
3. `ActualizarLecturaV3`

Porque el corte tecnico principal para que el ciclo funcione bien en lectura/app/ws
esta ahi.

---

## 9. Decision operativa

Estamos en desarrollo, no en produccion.

Eso nos permite hacer el corte bien:

- primero integrar en paralelo;
- luego cambiar el WS;
- luego cambiar la app;
- y al final borrar lo viejo.

No conviene hacer lo contrario.

Si borramos lo viejo primero, rompemos:

- la generacion del ciclo,
- la descarga al app,
- la captura de lectura,
- y el alta de factura.

Si integramos por capas, llegamos al mismo objetivo sin perder el ciclo completo.

## 4. Estado de integración real (abril 2026)

- App y portal usan el mismo motor tarifario (SP PostgreSQL), validado manualmente.
- Prueba de cálculo en portal refleja exactamente el resultado del motor.
- WS expone endpoints para sincronización y cálculo, integrados con el portal y la app.
- Flujo E2E validado: descarga → lectura → cálculo → factura → sync.
- CRUD de cuadros y reglas tarifarias V3 ya disponible y operativo en el portal.
