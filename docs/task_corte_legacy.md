# Task: Corte legacy y alineacion de vistas al motor tarifario nuevo

Fecha: `2026-04-18`

## 1. Objetivo

Planificar el retiro controlado del legado tarifario y, al mismo tiempo, alinear portal, ficha del cliente y vistas operativas al nuevo motor V3, sin dejar dobles fuentes de verdad.

## 2. Resultado esperado

Al cerrar esta tarea:

- el sistema nuevo sera la unica fuente de verdad tarifaria;
- el portal administrara el motor nuevo;
- la ficha del cliente quedara alineada a categoria, medicion, segmento y beneficios del modelo V3;
- el app y el WS dejaran de depender del legado tarifario;
- `ActualizarLecturaV2` podra retirarse.

## 3. Estado actual

### Nuevo ya implementado

- motor tarifario V3;
- `sp_adm_calcular_factura_lectura`;
- `sp_lectura_v3`;
- `ActualizarLecturaV3`;
- `GetOfflineSnapshotV3`;
- app con snapshot formal y sync offline;
- CRUD operativo de cuadros y reglas tarifarias V3 en portal.

### Legado aun presente

- `sp_tarifas_ws`
- `sp_tarifas_contador_ws`
- `sp_cobros_adicionales_ws`
- tablas legacy:
  - `tarifas`
  - `tarifas_contador`
  - configuraciones legacy de cobros adicionales
- `ActualizarLecturaV2`
- estructuras y vistas del portal que todavia mezclan criterios legacy y V3

## 4. Principio del corte

El corte no es solo apagar SP viejos. Tambien implica:

- mover la administracion funcional al nuevo modelo;
- actualizar la ficha del cliente;
- revisar las vistas del portal que hoy capturan datos incompatibles o insuficientes;
- dejar una sola ruta operativa para lectura y facturacion.

## 5. Componentes legacy que se deben retirar

### 5.1 WS legacy

Se deben apagar y retirar como fuente de verdad:

- `sp_tarifas_ws`
- `sp_tarifas_contador_ws`
- `sp_cobros_adicionales_ws`

Accion:

- mantener temporalmente solo si alguna pantalla o endpoint aun los consume;
- identificar consumidores;
- migrar esos consumidores;
- luego bloquear uso nuevo y retirar.

### 5.2 App Android legacy

Se debe retirar del app toda dependencia operativa de:

- `Tarifa`
- `TarifaContador`
- `CobroAdicional`
- calculo local viejo de `GenerarFactura` como fuente primaria

Accion:

- confirmar que la ruta V3 offline cubre todos los casos;
- eliminar consumo legacy;
- limpiar tablas SQLite y clases obsoletas cuando el corte sea definitivo.

### 5.3 Tablas y configuraciones legacy de BD

No se deben eliminar primero. Antes se requiere:

- confirmar que no existan consumidores activos;
- documentar equivalencias con V3;
- congelar escrituras nuevas;
- preparar migracion o retiro controlado.

## 6. Cambios requeridos en portal

Esta parte es clave. El corte no se cierra si el portal sigue administrando al cliente con logica vieja.

### 6.1 Ficha del cliente

La ficha del cliente debe cambiar para que la configuracion comercial y tarifaria ya no viva dispersa.

Debe incorporar o mostrar claramente:

- categoria regulatoria;
- condicion de medicion;
- segmento tarifario;
- estado del medidor;
- servicios asignados en `adm_cliente_servicio`;
- beneficios o ajustes activos;
- vigencia de configuracion;
- historial de cambios relevantes.

#### Cambios concretos

- eliminar o esconder campos que solo tenian sentido en el legado tarifario;
- dejar la categoria como dato alineado al motor nuevo;
- dejar la condicion de medicion gobernada por el nuevo modelo;
- cuando el cliente tenga medidor, la ficha debe reflejarlo sin romper el modelo regulatorio;
- permitir editar la asignacion de servicios desde una seccion clara, no en campos dispersos.

### 6.2 Vista de maestro de servicios

Si, hace falta.

No basta con cuadros y reglas. Tambien se necesita una vista del maestro de servicios para administrar:

- `adm_servicio`
- tipo de servicio;
- si es facturable en app;
- orden visual;
- cuenta contable si aplica;
- estado;
- si es base, derivado o eventual.

#### Objetivo

Que el portal pueda gobernar el catalogo que usa el motor y el app, sin volver a SQL manual.

### 6.3 Vista de `adm_cliente_servicio`

Debe consolidarse como la vista principal de configuracion tarifaria por cliente.

Debe permitir:

- asignar servicio base;
- cambiar categoria regulatoria;
- cambiar condicion de medicion;
- cambiar segmento;
- activar/desactivar servicio;
- definir vigencias;
- revisar overrides si se habilitan.

### 6.4 Vista de prueba de calculo

Debe quedar como herramienta operativa, no solo tecnica.

Debe permitir:

- elegir cliente;
- simular lectura;
- ver desglose por servicio;
- ver ajustes;
- ver cuadro y regla aplicada;
- comparar con ultima factura emitida.

### 6.5 Vista de conflictos

Debe existir para revisar:

- `SYNC_ERROR`
- `SYNC_CONFLICT`
- factura app vs factura persistida;
- correlativo/CAI;
- reproceso o reintento.

## 7. Datos a considerar en el corte

Antes de apagar el legado se debe inventariar:

- endpoints WS que aun consumen procedimientos legacy;
- pantallas del portal que aun muestran o editan datos legacy;
- clases del app que aun dependan de tarifas legacy;
- procesos batch o jobs que lean tablas legacy;
- reportes que aun asuman estructura vieja.

## 8. Pasos recomendados

### Fase 1. Inventario y congelamiento

- listar todos los consumidores del legado;
- marcar que no se construyan nuevas dependencias sobre legacy;
- documentar equivalencia funcional con V3.

### Fase 2. Alineacion de portal

- cerrar ficha del cliente V3;
- cerrar maestro de servicios;
- cerrar `adm_cliente_servicio`;
- cerrar vista de calculo;
- cerrar vista de conflictos.

### Fase 3. Corte funcional de WS y app

- forzar uso de `ActualizarLecturaV3`;
- dejar `GetOfflineSnapshotV3` como fuente oficial offline;
- retirar descarga de tarifas legacy;
- bloquear nuevas rutas V2.

### Fase 4. Corte tecnico de legado

- apagar SP legacy;
- retirar codigo muerto del app;
- retirar llamadas legacy del portal y WS;
- documentar retiro de tablas o congelamiento definitivo.

### Fase 5. Post-corte

- monitorear conflictos;
- revisar soporte operativo;
- auditar que toda factura nueva proviene de V3.

## 9. Riesgos

- que una vista del portal siga editando datos legacy y genere inconsistencia;
- que el app vuelva a usar calculo viejo en algun flujo residual;
- que algun reporte o WS siga leyendo SP antiguos;
- que se elimine una tabla legacy antes de migrar su ultimo consumidor;
- que la ficha del cliente quede partida entre viejo y nuevo modelo.

## 10. Criterios de aceptacion

- la ficha del cliente esta alineada al motor V3;
- existe vista del maestro de servicios;
- existe vista operativa de `adm_cliente_servicio`;
- el app ya no descarga ni calcula con fuentes legacy;
- el WS opera con V3 como unica ruta activa;
- los SP legacy estan apagados o congelados formalmente;
- el equipo puede administrar tarifas y clientes desde portal sin tocar SQL.

## 11. Siguiente paso recomendado

Antes de ejecutar el corte legacy completo, cerrar estas vistas del portal en este orden:

1. ficha del cliente alineada a V3
2. `adm_cliente_servicio`
3. maestro de servicios
4. vista de calculo
5. vista de conflictos

Ese bloque deja al portal listo para operar sobre el nuevo motor y evita que el corte sea solo tecnico pero no funcional.

