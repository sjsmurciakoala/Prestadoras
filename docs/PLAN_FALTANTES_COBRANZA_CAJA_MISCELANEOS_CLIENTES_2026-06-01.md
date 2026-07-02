# Plan de Implementacion de Faltantes

Fecha: 2026-06-01  
Alcance: cobranza, caja, miscelaneos y maestro de cliente.

## Resumen

Este plan cubre los puntos faltantes identificados en el proyecto para completar funcionalidades operativas de cobranza, caja, miscelaneos y perfil de cliente.

El orden recomendado es:

1. Maestro de cliente.
2. Cobranza base por cliente.
3. Bloqueos, no cortable y bitacora.
4. Notas de cobro y llamadas.
5. Masivo de cortes.
6. Impresion, reimpresion y reportes.
7. Consulta de miscelaneos.
8. Validaciones finales de caja.

## Fase 0: Definiciones Operativas

Antes de implementar, se deben cerrar estas reglas:

- Diferencia operativa entre cliente bloqueado, cliente no cortable y cliente inactivo.
- Tipos validos de accion de cobranza: llamada, visita, nota de cobro, convenio, corte, reconexion, observacion.
- Regla exacta para dias sin pago.
- Saldo minimo para generar corte.
- Si el masivo de cortes debe generar ordenes de trabajo inmediatamente o solo una lista previa.
- Si una nota de cobro es solo registro interno, documento imprimible o ambas cosas.
- Si la categoria usada para corte sera categoria legacy (`categoria_servicio`) o categoria regulatoria V3.

## Fase 1: Maestro de Cliente

Objetivo: corregir el perfil del cliente para que cobranza trabaje con datos confiables.

### 1. Quitar contador

Estado actual:

- El campo `contador` sigue existiendo en entidad, DTO, servicio y UI.
- Tambien aparece en vistas/reportes legacy.

Trabajo requerido:

- Remover `Contador` de la vista principal del cliente.
- Removerlo del formulario si ya no debe capturarse.
- Revisar reportes o SP que todavia lo consumen.
- Mantener compatibilidad con data historica si no se elimina la columna fisicamente.

Entregables:

- UI sin campo contador.
- DTOs ajustados si aplica.
- Validacion de compilacion y busqueda de referencias.

### 2. Corregir categoria y servicio

Estado actual:

- Categoria esta parcialmente mapeada.
- `ServicioId` y `ServicioNombre` existen en DTO, pero el servicio los devuelve como `null`.

Trabajo requerido:

- Definir origen real del servicio:
  - modelo tarifario V3 (`adm_cliente_servicio`), o
  - estructura legacy.
- Mapear `ServicioId` y `ServicioNombre` en `ClientesService`.
- Mostrar servicio correcto en perfil.
- Validar que categoria se guarde y se cargue consistentemente.

Entregables:

- Perfil con categoria y servicio visibles.
- Prueba de consulta de cliente con servicio asignado.

### 3. Quitar letra si el cliente es medido

Estado actual:

- `LetraCodigo` sigue visible en perfil y formulario.

Trabajo requerido:

- Ocultar `Letra` cuando `TieneMedidor = true`.
- Bloquear guardado de letra para clientes medidos.
- Mantenerla solo para clientes sin medidor si la operacion aun la requiere.

Entregables:

- UI condicional.
- Validacion en backend para nuevos registros/ediciones.

### 4. Mover clave catastral a datos personales

Estado actual:

- La clave catastral se toma desde `cliente_detalle.negocio_clave_catastral`.

Trabajo requerido:

- Definir si se agregara al maestro o se mantendra en detalle pero se mostrara como dato general.
- Ajustar formulario y perfil.
- Si se migra fisicamente, crear migracion y backfill.

Entregables:

- Clave catastral visible en datos generales/personales.
- Escritura y lectura desde el origen definido.

### 5. Agregar estudio socio-economico

Estado actual:

- No se encontro campo, DTO ni UI.

Trabajo requerido:

- Agregar campo al modelo.
- Definir minimo:
  - `TieneEstudioSocioEconomico` boolean.
- Opcional:
  - fecha de estudio,
  - resultado,
  - observacion,
  - archivo adjunto.
- Mostrar en perfil y formulario.

Entregables:

- Campo persistido.
- Campo editable.
- Campo visible en perfil.

### 6. Mostrar numero de contrato y clave SURE en perfil principal

Estado actual:

- Los campos existen en DTO/formulario.
- Aparecen en un componente de detalle, pero no de forma consistente en la vista principal del perfil.

Trabajo requerido:

- Agregar `NumeroContrato` y `ClaveSure` a la vista principal `ClienteDetail`.
- Validar que se carguen correctamente al editar y ver.

Entregables:

- Perfil principal mostrando contrato y clave SURE.

### 7. Mantenimiento de barrios

Estado actual:

- Existe tabla `barrio`.
- Existe endpoint de consulta para combos.
- No se encontro CRUD completo.

Trabajo requerido:

- Crear DTOs de barrio.
- Crear servicio de mantenimiento.
- Crear controlador CRUD.
- Crear pantalla:
  - listar,
  - crear,
  - editar,
  - activar/inactivar,
  - eliminar si no tiene uso.
- Agregar permisos.
- Agregar item al menu de parametros/catalogos.

Entregables:

- Pantalla de mantenimiento de barrios.
- API CRUD.
- Permisos.

### 8. Desglose de saldo pendiente por servicio

Estado actual:

- El resumen de cuenta muestra saldo total.
- No hay desglose por servicio en `ClienteEstadoCuentaDto`.

Trabajo requerido:

- Extender DTO con detalle por servicio.
- Consultar saldos por servicio desde `factura_detalle` / `transaccion_abonado` segun fuente real.
- Agrupar por:
  - agua potable,
  - alcantarillado,
  - ambiental,
  - ERSAPS,
  - otros.
- Mostrar tabla/resumen dentro de "Resumen de Cuenta".

Entregables:

- Resumen de cuenta con desglose.
- Validacion contra saldo total.

## Fase 2: Cobranza Base

Objetivo: completar la gestion individual de cobranza por cliente.

### 1. Gestion de llamadas telefonicas

Estado actual:

- No se encontro modulo operativo.

Trabajo requerido:

- Crear registro de llamada o usar bitacora de acciones con tipo `LLAMADA`.
- Campos sugeridos:
  - cliente,
  - fecha,
  - telefono llamado,
  - contacto,
  - resultado,
  - compromiso de pago,
  - fecha de proximo seguimiento,
  - observacion,
  - usuario.
- Agregar UI en cobranza o perfil de cliente.

Entregables:

- API de llamadas.
- Pantalla/tab para registrar y consultar llamadas.

### 2. Notas de cobro

Estado actual:

- No se encontro funcionalidad de nota de cobro.

Trabajo requerido:

- Crear entidad de nota de cobro.
- Generar correlativo.
- Asociar nota a cliente y saldo vigente.
- Registrar nota en bitacora.
- Crear PDF/imprimible.
- Permitir consulta y reimpresion.

Entregables:

- Generacion de nota de cobro.
- Reporte PDF.
- Consulta historica por cliente.

### 3. Condicion no cortable

Estado actual:

- No se encontro campo ni flujo operativo.

Trabajo requerido:

- Agregar estado `NoCortable`.
- Agregar motivo, fecha inicio, fecha fin opcional y usuario.
- Excluir automaticamente de masivos de corte.
- Mostrar alerta visible en perfil y cobranza.
- Registrar cambios en bitacora.

Entregables:

- Control de no cortable.
- Validacion en generacion masiva de cortes.

### 4. Bloqueo de clientes

Estado actual:

- Existe `bloqueado_cobranza`.
- Se consulta en cobranza para planes de pago.
- No se encontro CRUD operativo para bloquear/desbloquear.

Trabajo requerido:

- Crear accion de bloquear/desbloquear.
- Guardar motivo, fecha y usuario.
- Registrar en bitacora.
- Definir si bloqueo impide:
  - pagos,
  - planes,
  - notas,
  - facturacion,
  - corte.

Entregables:

- UI para bloquear/desbloquear.
- API correspondiente.
- Bitacora automatica.

### 5. Bitacora de acciones de cobranza

Estado actual:

- Existe entidad/tabla `cln_accion_cobranza`.
- No se encontro UI/API operativa.

Trabajo requerido:

- Formalizar catalogo de tipos de accion.
- Crear API:
  - listar acciones por cliente,
  - crear accion,
  - consultar detalle.
- Crear tab en perfil del cliente.
- Registrar automaticamente:
  - llamada,
  - nota de cobro,
  - bloqueo,
  - no cortable,
  - generacion de corte,
  - convenio/plan.

Entregables:

- Bitacora visible por cliente.
- Registro manual y automatico.

## Fase 3: Masivo de Cortes

Objetivo: crear un modulo nuevo para generar cortes por criterios.

### 1. Nuevo modulo de masivo de cortes

Filtros requeridos:

- Ciclo.
- Barrio.
- Dias sin pago.
- Valor adeudado.
- Categoria.
- Excluir no cortables.
- Excluir clientes con pago posterior a una fecha.
- Estado de cliente.

Trabajo requerido:

- Crear pantalla de filtros.
- Crear servicio de simulacion.
- Mostrar candidatos antes de generar.
- Permitir quitar clientes manualmente.
- Mostrar motivo de exclusion.

Entregables:

- Pantalla de simulacion de cortes.
- API de candidatos.

### 2. Generacion de lote

Trabajo requerido:

- Crear cabecera de lote `corte_masivo_hdr`.
- Crear detalle `corte_masivo_dtl`.
- Guardar snapshot de criterios usados.
- Asociar ordenes generadas.
- Guardar usuario y fecha.
- Evitar duplicados por cliente/lote.

Entregables:

- Lote persistido.
- Detalle de clientes generados.
- Relacion con ordenes de trabajo.

### 3. Generacion de ordenes de corte

Trabajo requerido:

- Definir tipo de orden de corte.
- Crear ordenes de trabajo para clientes seleccionados.
- Respetar correlativos existentes.
- No generar orden si ya existe una activa para el mismo cliente y tipo, salvo regla contraria.

Entregables:

- Ordenes de corte generadas desde lote.
- Validacion de duplicados.

### 4. Corte por categoria

Trabajo requerido:

- Agregar filtro por categoria.
- Definir si usa `categoria_servicio` o categoria regulatoria V3.
- Mostrar categoria en candidatos y listado final.

Entregables:

- Generacion filtrada por categoria.

## Fase 4: Impresion y Reimpresion

Objetivo: completar la salida documental del masivo.

### 1. Imprimir listado de clientes generados para corte

Trabajo requerido:

- Crear reporte PDF por lote.
- Columnas sugeridas:
  - lote,
  - fecha,
  - cliente,
  - nombre,
  - barrio,
  - ciclo,
  - categoria,
  - direccion,
  - telefono,
  - saldo,
  - dias de mora,
  - numero de orden.

Entregables:

- PDF de listado de corte.

### 2. Reimprimir masivo sin nuevo correlativo

Trabajo requerido:

- Reimprimir desde `corte_masivo_hdr/dtl`.
- No crear nuevas ordenes.
- Mantener mismos correlativos y detalle.

Entregables:

- Accion de reimpresion por lote.

### 3. Generar solo clientes que no hicieron pago

Trabajo requerido:

- Sobre lote existente, comparar pagos posteriores a la fecha de generacion.
- Marcar clientes pagados.
- Permitir nueva generacion solo para clientes pendientes.
- Definir si se crea un nuevo lote derivado o una revision del lote original.

Entregables:

- Regeneracion de pendientes.
- Auditoria de clientes excluidos por pago.

## Fase 5: Reporteria de Cobranza

Objetivo: medir resultado de cortes y acciones de cobranza.

### 1. Comparativo de cortes generados vs recaudacion recibida

Trabajo requerido:

- Reporte por lote, rango de fecha, ciclo, barrio y categoria.
- Calcular:
  - clientes generados,
  - monto adeudado al generar,
  - pagos recibidos despues del corte,
  - clientes que pagaron,
  - clientes sin pago,
  - porcentaje de recuperacion,
  - ordenes ejecutadas y pendientes si existe estado.

Entregables:

- Reporte PDF o vista web.
- Dataset SQL.
- Filtros en UI.

### 2. Reportes auxiliares

Reportes sugeridos:

- Clientes no cortables.
- Clientes bloqueados.
- Bitacora de acciones por cliente.
- Notas de cobro emitidas.
- Llamadas por resultado y usuario.

## Fase 6: Miscelaneos

Objetivo: agregar consulta historica de miscelaneos.

### 1. Consulta de miscelaneos

Estado actual:

- Existe facturacion de miscelaneos.
- Existe catalogo.
- Existe consulta de recibo puntual.
- Falta consulta general historica.

Trabajo requerido:

- Crear pantalla de consulta.
- Filtros:
  - cliente,
  - recibo,
  - fecha desde/hasta,
  - estado,
  - concepto.
- Mostrar detalle del recibo.
- Agregar reimpresion si aplica.

Entregables:

- Consulta web de miscelaneos.
- API paginada.
- Detalle de recibo.

## Fase 7: Caja

Objetivo: cerrar validaciones sobre el modulo existente.

Estado actual:

- Existe gestion de caja general.
- Existe apertura, cierre, resumen e historial.

Trabajo requerido:

- Validar que las operaciones que requieren caja usen sesion activa.
- Bloquear facturacion/pago si no hay caja abierta, donde aplique.
- Revisar consistencia entre `sesion_caja.id` y `transaccion_abonado.caja_id`.
- Agregar pruebas de abrir, cerrar y resumen.

Entregables:

- Caja validada con pruebas.
- Reglas de sesion activa aplicadas.

## Matriz de Prioridad

| Prioridad | Item | Motivo |
|---|---|---|
| Alta | Servicio/categoria de cliente | Base para cortes y reportes |
| Alta | No cortable | Evita cortes indebidos |
| Alta | Bitacora de cobranza | Auditoria operativa |
| Alta | Masivo de cortes | Requerimiento principal de cobranza |
| Alta | Impresion/reimpresion de cortes | Necesario para operacion diaria |
| Media | Notas de cobro | Gestion documental |
| Media | Llamadas telefonicas | Seguimiento operativo |
| Media | Consulta de miscelaneos | Consulta administrativa |
| Media | Mantenimiento de barrios | Soporte a filtros y catalogos |
| Baja | Quitar contador/letra | Limpieza funcional, salvo que afecte uso diario |

## Entregables Tecnicos

- Migraciones de base de datos.
- Entidades nuevas o extendidas.
- DTOs.
- Servicios.
- Controladores API.
- Clientes HTTP Blazor.
- Pantallas Blazor.
- Reportes PDF o vistas web.
- Permisos.
- Pruebas unitarias o de servicio.
- Documento de cierre con matriz de cumplimiento.

## Criterio de Cierre

El alcance se considera cerrado cuando:

- Cada item tiene pantalla o API operativa segun corresponda.
- Las reglas de no cortable y bloqueo se aplican al masivo de cortes.
- Los lotes de corte se pueden generar, imprimir y reimprimir sin duplicar correlativos.
- El comparativo de cortes vs recaudacion muestra resultados por lote.
- El perfil de cliente muestra datos corregidos y el saldo por servicio.
- La consulta de miscelaneos permite buscar historico y abrir detalle.
- Caja bloquea operaciones dependientes cuando no hay sesion activa, si esa regla queda aprobada.
