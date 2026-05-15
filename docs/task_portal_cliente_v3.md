# Task: Portal V3 - ficha del cliente y vistas operativas del motor tarifario

Fecha: `2026-04-18`

## 1. Objetivo

Definir a detalle los cambios que necesita el portal para operar correctamente sobre el motor tarifario V3, con foco en:

- ficha del cliente;
- maestro de servicios;
- configuracion `adm_cliente_servicio`;
- mantenimiento tarifario;
- prueba de calculo;
- revision de conflictos.

La meta es que el portal deje de mezclar datos legacy y V3, y se vuelva la herramienta operativa real para administrar el nuevo modelo.

## 2. Problema actual

Hoy ya existe una base funcional importante:

- motor tarifario V3 en BD;
- CRUD de cuadros y reglas tarifarias V3;
- WS V3;
- app lectora V3/offline;
- integracion E2E validada.

Pero el portal todavia necesita una alineacion funcional mas fina en la capa de operacion diaria:

- la ficha del cliente todavia no esta explicitamente rediseñada alrededor del motor nuevo;
- hace falta una vista clara del maestro de servicios;
- hace falta una vista operativa de servicios por cliente;
- hace falta una vista de conflictos y soporte;
- algunas pantallas existentes todavia reflejan una mentalidad legacy.

## 2.1 Estado actual (2026-04-19)

Para evitar ambigüedad, este es el corte real de avance:

- ✅ Tarifas V3 base: CRUD de cuadros y reglas tarifarias completado.
- ✅ Integración principal en ficha del cliente: panel/modal de Configuración Tarifaria V3 operativo.
- ✅ Prueba de cálculo V3: modal separado y disponible desde la ficha.
- 🟡 Pendiente de cierre funcional: ficha del cliente V3 completa (sin mezcla legacy).
- 🟡 Pendiente de cierre funcional: vista operativa de `adm_cliente_servicio`.
- 🟡 En avance: vista de maestro de servicios (`adm_servicio`) con pantalla operativa inicial en portal; falta cierre de acciones avanzadas/validaciones de referencia.
- ⏳ No iniciado en esta fase: bloque CAI/correlativo offline (va después del cierre portal V3).

Conclusión corta: **sí, ya se avanzó en tarifas V3**, pero el siguiente tramo obligatorio es cerrar las vistas operativas del portal para que operación/soporte trabaje 100% con modelo nuevo.

## 2.2 Qué sigue (orden acordado)

1. Cerrar diseño funcional + UX de la ficha del cliente V3.
2. Cerrar vista operativa de `adm_cliente_servicio`.
3. Cerrar vista de maestro de servicios `adm_servicio`.
4. Recién después, entrar a implementación del bloque CAI.

## 3. Principio de diseño

El portal no debe administrar “tarifas sueltas” ni “campos historicos dispersos”.

Debe administrar estas piezas del nuevo modelo:

- `adm_servicio`
- `adm_cliente_servicio`
- `adm_cuadro_tarifario`
- `adm_regla_tarifaria`
- `adm_ajuste_tarifario`
- beneficios/condiciones regulatorias visibles en la ficha del cliente

## 4. Vistas que hacen falta

## 4.1 Ficha del cliente V3

### Objetivo

Convertir la ficha del cliente en el centro de administracion comercial y tarifaria del cliente bajo el modelo nuevo.

### Lo que debe mostrar

#### Bloque general

- datos base del cliente;
- ruta;
- ciclo;
- estado;
- tipo de cliente;
- medidor asociado si existe;
- indicador claro de si el cliente es medido o no medido.

#### Bloque tarifario V3

- categoria regulatoria actual;
- condicion de medicion actual;
- segmento tarifario actual;
- vigencia de la configuracion;
- estado de configuracion tarifaria.

#### Bloque de servicios

- servicios asignados al cliente desde `adm_cliente_servicio`;
- tipo de servicio;
- estado activo/inactivo;
- fecha alta / fecha baja;
- cuadro override si algun dia aplica;
- indicador de servicio base vs derivado/eventual.

#### Bloque de beneficios y ajustes

- adulto mayor/jubilado;
- topes;
- exoneraciones;
- observaciones regulatorias;
- vigencias;
- historial de cambios.

#### Bloque de sincronizacion/facturacion

- ultima factura emitida;
- ultima lectura;
- estado de sincronizacion reciente si aplica;
- conflictos abiertos relacionados con ese cliente.

### Acciones que debe permitir

- cambiar categoria regulatoria;
- cambiar condicion de medicion;
- cambiar segmento tarifario;
- asignar o desasignar servicio base;
- activar o desactivar servicios;
- registrar beneficio o exoneracion;
- abrir prueba de calculo;
- abrir historial de facturas/lecturas;
- abrir conflictos del cliente.

### Lo que se debe retirar o esconder

Todo campo de la ficha que:

- represente clasificaciones legacy ya sustituidas por V3;
- mezcle categoria vieja con categoria regulatoria nueva;
- permita configurar tarifa por fuera de `adm_cliente_servicio` o del motor nuevo;
- sugiera que el calculo depende todavia de `tarifas`, `tarifas_contador` o cobros adicionales legacy.

## 4.2 Vista de maestro de servicios

### Respuesta corta

Si, hace falta.

No basta con cuadros y reglas. El portal tambien necesita gobernar el catalogo maestro de `adm_servicio`.

### Objetivo

Administrar el inventario de conceptos que usa el motor y que pueden terminar en factura.

### Debe permitir

- listar servicios;
- filtrar por:
  - codigo
  - nombre
  - tipo de servicio
  - estado
- crear o editar servicio;
- activar/desactivar;
- definir si es facturable en app;
- definir orden visual;
- asociar cuenta contable si aplica;
- revisar si el servicio ya esta usado en cuadros, reglas o clientes.

### Campos clave

- codigo
- nombre
- tipo de servicio
- descripcion
- facturable_app
- app_orden
- cont_account_id
- estado

### Controles recomendados

- grid principal;
- drawer o formulario lateral para crear/editar;
- advertencia si el servicio ya esta referenciado;
- filtro rapido por “base / derivado / eventual”.

## 4.3 Vista de `adm_cliente_servicio`

### Objetivo

Separar la administracion de configuracion tarifaria por cliente de la ficha general, para que tambien pueda operarse masivamente o por soporte.

### Debe permitir

- buscar por cliente, servicio, categoria o ruta;
- ver configuracion vigente;
- crear asignacion nueva;
- editar:
  - categoria regulatoria
  - condicion de medicion
  - segmento tarifario
  - vigencia
  - estado
- desactivar asignacion;
- revisar historial de cambios.

### Campos clave

- cliente
- servicio
- categoria regulatoria
- condicion de medicion
- segmento tarifario
- fecha alta
- fecha baja
- estado

### Uso esperado

Esta vista debe ser la herramienta principal de soporte cuando:

- un cliente esta mal categorizado;
- cambia de medido a no medido;
- cambia de segmento;
- hay un error de configuracion tarifaria.

## 4.4 Mantenimiento tarifario V3

### Objetivo

Permitir administrar tarifas sin tocar SQL.

### Debe cubrir

- `adm_cuadro_tarifario`
- `adm_regla_tarifaria`
- `adm_ajuste_tarifario`

### Funcionalidad minima

- lista de cuadros con filtros por servicio, categoria, medicion y vigencia;
- detalle de reglas por cuadro;
- alta/edicion;
- validacion visual de vigencias solapadas;
- acceso rapido a reglas y ajustes.

### Funcionalidad deseable

- clonar cuadro;
- versionar cambios;
- simulacion desde el mismo cuadro;
- historial de cambios.

## 4.5 Pantalla de prueba de calculo

### Objetivo

Dar a negocio y soporte una forma de validar el motor sin depender del app.

### Debe permitir

- seleccionar cliente;
- fecha;
- lectura actual;
- condicion de lectura;
- promedio;
- ejecutar calculo;
- ver total;
- ver desglose por servicio;
- ver cuadro y regla aplicada;
- ver warnings;
- comparar con ultima factura.

### Valor operativo

- depuracion funcional;
- soporte;
- validacion con usuarios;
- comparacion contra casos reales.

## 4.6 Vista de conflictos

### Objetivo

Dar soporte a los casos donde el app y el servidor no coinciden o donde hay problemas de sync.

### Debe mostrar

- cliente;
- lectura;
- factura del app;
- factura persistida;
- estado:
  - `SYNC_ERROR`
  - `SYNC_CONFLICT`
  - duplicado
  - reproceso
- fecha/hora;
- usuario o dispositivo;
- detalle del error.

### Debe permitir

- filtrar por estado, ruta, ciclo, cliente;
- abrir detalle del conflicto;
- reintentar si aplica;
- marcar para revision;
- dejar observaciones operativas.

## 5. Relacion entre vistas

El flujo ideal del portal deberia quedar asi:

1. Desde ficha del cliente se ve su configuracion V3.
2. Si la configuracion esta mal, se entra a `adm_cliente_servicio`.
3. Si el problema es del catalogo o de una tarifa, se entra al maestro de servicios o mantenimiento tarifario.
4. Si el problema es del calculo, se usa prueba de calculo.
5. Si el problema es de sincronizacion, se usa vista de conflictos.

## 6. Cambios especificos en la ficha del cliente

### 6.1 Pestaña o bloque nuevo recomendado

Agregar una seccion clara tipo `Configuracion Tarifaria V3`.

Debe contener:

- categoria regulatoria;
- condicion de medicion;
- segmento tarifario;
- servicios activos;
- beneficios;
- acceso a prueba de calculo.

### 6.2 Reorganizacion recomendada

La ficha debe dejar de tener la logica tarifaria mezclada con:

- datos generales;
- direccion;
- medidor;
- cobro;
- estados legacy.

La configuracion V3 debe verse como una unidad propia.

### 6.3 Reglas de edicion

- cambios sensibles deben quedar auditados;
- cambios de categoria/segmento deben registrar vigencia;
- servicios base no deben duplicarse si la politica no lo permite;
- beneficios deben validar reglas de elegibilidad.

## 7. API y backend que probablemente haran falta

### Para ficha del cliente V3

- `GET /api/clientes/{id}/tarifario-v3`
- `PUT /api/clientes/{id}/tarifario-v3`
- `GET /api/clientes/{id}/beneficios`
- `POST /api/clientes/{id}/beneficios`

### Para maestro de servicios

- `GET /api/tarifario/servicios`
- `GET /api/tarifario/servicios/{id}`
- `POST /api/tarifario/servicios`
- `PUT /api/tarifario/servicios/{id}`

### Para conflictos

- `GET /api/tarifario/conflictos`
- `GET /api/tarifario/conflictos/{id}`
- `POST /api/tarifario/conflictos/{id}/resolver`

## 8. Orden recomendado de implementacion

1. Ficha del cliente V3
2. Vista de `adm_cliente_servicio`
3. Maestro de servicios
4. Pantalla de prueba de calculo
5. Vista de conflictos
6. Ajustes finos del mantenimiento tarifario si aun faltan piezas

## 9. Riesgos si no se hace

- el portal seguira administrando clientes con logica partida;
- el equipo operativo seguira dependiendo de SQL o soporte tecnico;
- la ficha del cliente dara informacion incompleta o contradictoria;
- el corte legacy quedara bloqueado por falta de operacion en portal.

## 10. Criterios de aceptacion

- la ficha del cliente muestra y edita configuracion V3 real;
- existe maestro de servicios;
- existe vista operativa de `adm_cliente_servicio`;
- existe prueba de calculo usable por negocio;
- existe vista de conflictos;
- el portal ya no necesita pantallas legacy para operar el nuevo motor.

