# Plan de validacion previa: ciclo y facturacion

Fecha: `2026-04-23`

## 1. Objetivo

Antes de ejecutar la validacion E2E completa del flujo de lectura V3, confirmar que el sistema ya puede operar el tramo base de negocio:

- configurar cliente para lectura;
- dejar ciclo y ruta operativos;
- generar el periodo de lectura;
- preparar historico base del periodo;
- calcular factura con el motor nuevo;
- registrar factura formal sin romper CAI ni sincronizacion.

La idea de este plan no es probar todavia todo el app Android. La meta inmediata es despejar si, con todos los cambios recientes, el sistema ya esta en condicion de generar un ciclo y facturar.

## 2. Alcance

Este plan cubre:

- portal de clientes, ciclos, rutas y auxiliar de lectura;
- motor SQL V3;
- WS `ActualizarLecturaV3`;
- CAI offline y conciliacion base;
- evidencia minima en BD para afirmar que el flujo puede facturar.

Este plan no cubre todavia:

- corte legacy completo;
- pruebas masivas de dispositivo;
- reproceso/anulacion final;
- limpieza de tablas y endpoints legacy.

## 3. Puntos del sistema que deben intervenir

### Portal

- mantenimiento de cliente:
  - `Prestadoras/apc.Client/Pages/Clientes/Components/ClienteForm.razor`
- operacion tarifaria por cliente:
  - `Prestadoras/apc.Client/Pages/Tarifario/ClienteServicioOperativo.razor`
- catalogo de ciclos:
  - `Prestadoras/apc.Client/Pages/Ciclos/CiclosList.razor`
- auxiliar de lectura para generar periodo:
  - `Prestadoras/apc.Client/Pages/Auxiliares/AuxiliarLecturaIndex.razor`

### Servicios y backend

- generacion de periodo:
  - `Prestadoras/SIAD.Services/AuxiliarLectura/IAuxiliarLecturaService.cs`
- calculo de factura:
  - `Prestadoras/Database/ddl_v3/20260417_add_sp_adm_calcular_factura_lectura.sql`
- registro formal:
  - `Prestadoras/Database/ddl_v3/20260417_add_sp_lectura_v3.sql`
- snapshot offline:
  - `Prestadoras/Database/ddl_v3/20260418_add_sp_adm_generar_snapshot_offline_cliente_lectura.sql`
- CAI offline:
  - `Prestadoras/Database/ddl_v3/20260418_adm_cai_offline_core.sql`
  - `Prestadoras/Database/ddl_v3/20260419_v3_cai_sync_conflictos.sql`

### WS

- sincronizacion V3:
  - `WSappLectores/WS_APC/APCService.svc.cs`

## 4. Hipotesis de exito

Consideraremos que el sistema ya esta listo para la validacion operativa mayor si se cumple esta secuencia sin bloqueos funcionales:

1. se puede guardar un cliente con ciclo, ruta y configuracion de lectura;
2. se puede generar el periodo desde Auxiliar de Lectura;
3. el cliente queda preparado en el historico del periodo;
4. `sp_adm_calcular_factura_lectura` devuelve factura valida;
5. `sp_lectura_v3` o `ActualizarLecturaV3` registran la factura;
6. la factura queda persistida con detalle y correlativo correcto.

## 5. Flujo de verificacion propuesto

### Fase A. Configuracion operativa minima

Primero se debe verificar que exista un caso real de prueba con:

- cliente activo;
- servicio base activo en `adm_cliente_servicio`;
- categoria regulatoria, condicion de medicion y segmento resueltos;
- ciclo configurado;
- ruta configurada;
- secuencia configurada;
- periodo abierto esperado para ese ciclo.

La salida esperada de esta fase es un cliente que ya no dependa de datos sueltos ni de valores heredados incompletos.

### Fase B. Generacion del periodo de lectura

Con el cliente anterior, entrar a `Auxiliar de Lectura` y generar el periodo del ciclo objetivo.

Se debe confirmar:

- que el periodo se genera sin error;
- que el periodo anterior se cierra si aplica;
- que el cliente queda preparado en `historicomedicion` para el anio/mes/ciclo de prueba;
- que lectura anterior, ruta y secuencia quedaron consistentes.
- que el WS `GetCiclo/{ruta}` resuelve ese mismo periodo abierto sin depender de `historialmes.ruta`.

Si esta fase falla, no tiene sentido pasar a snapshot ni a sincronizacion.

### Fase C. Prueba de calculo antes de facturar

Con el periodo ya generado, usar la prueba de calculo o SQL controlado para el cliente de prueba y confirmar:

- lectura anterior correcta;
- consumo facturable correcto segun condicion;
- servicios base y derivados presentes;
- tercera edad/topes correctos si el cliente aplica;
- warnings esperados, pero sin errores bloqueantes.

La salida esperada es que el sistema produzca una factura deterministica antes de persistirla.

### Fase D. Verificacion de CAI y correlativo

Antes de emitir factura formal, verificar:

- CAI activo para la empresa;
- bloque CAI reservable o ya reservado para la ruta;
- correlativo siguiente disponible;
- ausencia de conflicto previo para `lectura_uuid` o numero de factura.

Esta fase debe dejar listo el numero fiscal que el app y el servidor van a compartir.

### Fase E. Facturacion formal controlada

Con todo lo anterior listo, ejecutar una facturacion controlada usando el flujo V3 real.

Se puede hacer por cualquiera de estas dos vias:

- `ActualizarLecturaV3` en el WS, si ya queremos probar la ruta operativa real;
- llamada controlada a `sp_lectura_v3`, si primero queremos aislar motor + BD.

El resultado esperado es:

- fila en `factura`;
- filas en `factura_detalle`;
- filas en `transaccion_abonado`;
- historico actualizado;
- correlativo CAI preparado/confirmado segun corresponda.

### Fase F. Confirmacion de consistencia

Al finalizar la facturacion, revisar que:

- `factura.numfactura` sea el esperado;
- `factura.saldototal` coincida con el total calculado;
- `factura_detalle` coincida con `detalle_servicios_json`;
- `historicomedicion.numerofactura` y `descuentoapp` queden correctos;
- `adm_cai_correlativo_emitido` quede consistente con `lectura_uuid`, numero de factura y `factura_id`.

## 6. Criterio de salida de esta validacion previa

Esta etapa se da por cerrada cuando podamos afirmar, con evidencia en portal y BD, que:

- el sistema genera el periodo/ciclo requerido;
- el cliente queda listo para lectura;
- el motor V3 calcula;
- el WS/BD facturan;
- la factura formal queda persistida correctamente.

Cuando eso este confirmado, el siguiente paso ya no es “seguir acomodando base”, sino:

- validacion E2E app Android -> WS -> BD;
- luego corte legacy controlado.

## 7. Riesgos a vigilar durante la verificacion

- cliente sin `adm_cliente_servicio` completo;
- ruta/ciclo creados pero no enlazados al cliente;
- periodo generado sin preparar bien `historicomedicion`;
- CAI activo sin bloque utilizable para la ruta;
- diferencias entre calculo previo y persistencia final;
- datos legacy interfiriendo en pruebas y confundiendo la fuente real de verdad.

## 8. Resultado esperado de este documento

Este plan debe usarse como puerta previa a la validacion final.

Si el sistema no supera este tramo, no conviene entrar aun a pruebas completas del app ni a corte legacy total. Si lo supera, ya tenemos base suficiente para avanzar con una sola ruta V3 operativa.
