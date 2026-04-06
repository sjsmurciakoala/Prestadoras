# Plan completo: Facturacion Miscelaneos + Contabilidad automatica

Fecha: 2026-03-14
Branch propuesta: `feature/facturacion-miscelaneos-ui-contabilidad-auto`

## 0. Estado de cierre

Estado: `CERRADO` el `2026-03-16`.

Implementacion final validada a nivel de codigo:

1. La vista de miscelaneos ya no autoselecciona clientes y la busqueda soporta clave, nombre y RTN.
2. Existe CRUD operativo de catalogo miscelaneo con cuenta contable.
3. `miscelaneos_catalogo` ya maneja `cont_account_id`.
4. `con_plantilla_partida_dtl` ya soporta `DETAIL_EXPAND`.
5. Existe `cfg_document_type` y plantilla dedicados `VENTAS/MIS`.
6. `CrearReciboAsync` genera y postea automaticamente la poliza del recibo miscelaneo.
7. El flujo exige empresa activa; ya no puede confirmar recibos sin contabilidad.
8. `document_id` queda alineado a `numrecibo`.
9. `type_id` se resuelve de forma explicita por `con_tipo_transaccion.code = 'FAC'`.

Nota:

- Las secciones de "estado actual" y "fases" conservan la trazabilidad del plan original de 2026-03-14.
- La implementacion final difiere del plan inicial en que el mantenimiento del catalogo miscelaneo si quedo incluido en esta entrega.

## 1. Resumen ejecutivo

Este plan cubre dos objetivos, en este orden:

1. Corregir la vista y el flujo funcional de `https://localhost:5002/facturacion/miscelaneos`.
2. Implementar contabilidad automatica al generar el recibo miscelaneo.

La implementacion debe respetar las decisiones ya tomadas en contabilidad:

- `con_plantilla_partida_*` sera la fuente de verdad runtime para este flujo.
- `con_regla_integracion` no participara en facturacion miscelaneos; queda reservado para `sp_lectura_v2` y el flujo movil.
- `sp_con_postear_poliza` seguira siendo el punto unico de posteo/mayorizacion.
- La poliza del recibo miscelaneo debe quedar posteada automaticamente en la misma operacion.
- La contabilidad sera por concepto miscelaneo, no con una sola cuenta global.
- Para soportar cuentas por concepto, el motor de plantillas debera extenderse para lineas dinamicas.

## 2. Estado actual y problemas detectados

### 2.1 Problemas funcionales en la vista

Pantalla afectada:

- `Prestadoras/apc.Client/Pages/Facturacion/Miscelaneos/Facturacion.razor`

Problemas actuales:

1. El buscador de clientes autoselecciona automaticamente cuando la busqueda devuelve un solo resultado.
   - La causa actual es la logica `if (clientesEncontrados.Count == 1) { SeleccionarCliente(...) }`.
   - Esto impide que el usuario confirme manualmente el cliente deseado.

2. La UI indica "Clave, nombre o RTN", pero el backend solo busca por clave y nombre.
   - Servicio actual:
     - `Prestadoras/SIAD.Services/FacturacionMiscelaneos/FacturacionMiscelaneosService.cs`

3. Los botones `Agregar miscelaneos` y `Generar recibo` dependen de estado valido, pero la vista no deja claro por que estan deshabilitados.
   - `Agregar miscelaneos` requiere cliente seleccionado y catalogo cargado.
   - `Generar recibo` requiere cliente seleccionado y al menos un detalle agregado.

4. No existe retroalimentacion suficientemente clara cuando:
   - el catalogo esta vacio
   - no hay cliente seleccionado
   - no hay detalles

### 2.2 Estado actual del flujo de negocio

Servicio afectado:

- `Prestadoras/SIAD.Services/FacturacionMiscelaneos/FacturacionMiscelaneosService.cs`

El flujo actual:

1. Busca cliente.
2. Lista catalogo de `miscelaneos_catalogo`.
3. Crea `factura`.
4. Crea `factura_detalle`.
5. Inserta `transaccion_abonado`.
6. No genera poliza contable.

Limitaciones actuales:

1. No valida cuenta contable por concepto.
2. No resuelve document type contable dedicado.
3. No usa plantillas.
4. No usa `sp_con_generar_comprobante`.
5. No garantiza que el flujo quede coherente si se agrega contabilidad y esta falla.

### 2.3 Estado actual del modelo de datos

Entidad actual:

- `Prestadoras/SIAD.Core/Entities/miscelaneos_catalogo.cs`

La tabla `miscelaneos_catalogo` solo tiene:

- `ide`
- `codigo`
- `nombre`
- `valor`

No existe hoy:

- `cont_account_id`

Esto significa que no se puede contabilizar por concepto con el motor actual sin extender el modelo.

### 2.4 Estado actual del motor contable

Motor actual:

- `Prestadoras/Database/ddl_v3/20260122_contabilidad_comprobantes_cobranza_facturacion.sql`

Funcion actual:

- `public.sp_con_generar_comprobante(...)`

Firma actual relevante:

- recibe `p_values jsonb`
- usa `con_plantilla_partida_hdr`
- usa `con_plantilla_partida_dtl`
- al final delega el posteo a `sp_con_postear_poliza`

Limitacion estructural actual:

- `con_plantilla_partida_dtl` usa `account_id` fijo por linea.
- Esto sirve para lineas fijas.
- No sirve para una poliza donde cada concepto miscelaneo tenga cuenta distinta.

Conclusion:

- si se quiere contabilidad por concepto, no basta con crear una nueva plantilla;
- hay que extender el motor para soportar lineas dinamicas.

## 3. Decisiones cerradas para esta implementacion

### 3.1 Decisiones de arquitectura

1. Facturacion miscelaneos usara plantillas contables como fuente runtime.
2. `con_regla_integracion` no se usara en este flujo.
3. Se creara un document type contable dedicado: `VENTAS/MIS`.
4. No se reutilizaran `VENTAS/FAC` ni `VENTAS/REC`.
5. La poliza se creara y posteara automaticamente al generar el recibo.
6. La empresa se resolvera desde el tenant actual, no desde una empresa fija.

### 3.2 Decisiones de negocio contable

1. El evento contable sera la emision del recibo miscelaneo.
2. La poliza se generara al crear el recibo, no al cobrarlo.
3. El asiento sera:
   - Debito: cuenta por cobrar
   - Credito: una linea por cada concepto miscelaneo
4. Cada concepto miscelaneo tendra su propia cuenta contable.
5. Si un concepto no tiene cuenta contable, el recibo no se podra generar.

### 3.3 Decisiones de configuracion

1. Se agregara `cont_account_id` a `miscelaneos_catalogo`.
2. En esta entrega, la cuenta por concepto se configurara por SQL/seed.
3. No se incluira una UI nueva de mantenimiento del catalogo en esta fase.

## 4. Objetivos funcionales de la entrega

Al finalizar la implementacion, el flujo debe cumplir esto:

1. El usuario puede buscar clientes por clave, nombre o RTN.
2. La pantalla no selecciona clientes automaticamente.
3. El usuario selecciona manualmente el cliente desde la lista.
4. El boton `Agregar miscelaneos` se habilita cuando ya hay cliente y catalogo valido.
5. El boton `Generar recibo` se habilita cuando ya hay cliente y detalles.
6. El recibo miscelaneo se crea correctamente.
7. Cada detalle del recibo queda ligado a un concepto valido del catalogo.
8. Se genera una poliza `VENTAS/MIS`.
9. La poliza queda posteada automaticamente.
10. La poliza refleja una linea credito por cada concepto del recibo.
11. Todo opera por empresa activa.

## 5. Plan detallado de implementacion

### Fase 1. Corregir la vista de facturacion miscelaneos

Objetivo:

- dejar la pantalla operable y sin comportamiento sorpresivo antes de tocar contabilidad.

Cambios requeridos:

1. Eliminar autoseleccion automatica del cliente.
   - Siempre mostrar el popup/lista.
   - Exigir click explicito del usuario.

2. Ajustar la busqueda para que soporte RTN real.
   - El servicio debe buscar por:
     - `maestro_cliente_clave`
     - `maestro_cliente_nombre`
     - `maestro_cliente_rtn`

3. Mejorar el estado visible de la pantalla.
   - Mostrar mensaje si no hay catalogo cargado.
   - Mostrar mensaje si falta seleccionar cliente.
   - Mostrar mensaje si no hay detalles agregados.

4. Mantener consistencia de formulario.
   - `LimpiarCliente()` debe limpiar cliente, detalles y ultimo recibo.
   - `SeleccionarCliente()` debe limpiar detalles anteriores.
   - Si el usuario cambia de cliente, no se arrastran conceptos del cliente anterior.

5. Validar comportamiento de botones.
   - `Agregar miscelaneos`:
     - habilitado solo con cliente y catalogo.
   - `Generar recibo`:
     - habilitado solo con cliente y detalles.

Resultado esperado de Fase 1:

- la pantalla deja de bloquear al usuario por UX defectuosa.

### Fase 2. Endurecer el servicio de negocio antes de contabilidad

Objetivo:

- asegurar que el recibo miscelaneo sea consistente incluso antes de agregar la poliza.

Cambios requeridos:

1. Validar que cada codigo recibido exista en `miscelaneos_catalogo`.
2. Validar que cada detalle tenga monto positivo.
3. Validar que el cliente exista realmente.
4. Validar que el catalogo tenga configuracion contable antes de seguir.
5. Resolver `company_id` desde el servicio de empresa actual.
6. Mantener transaccion atomica del flujo.

Resultado esperado de Fase 2:

- el flujo de negocio queda listo para insertar contabilidad sin dejar estados parciales.

### Fase 3. Extender el modelo de datos del catalogo miscelaneo

Objetivo:

- habilitar cuenta contable por concepto.

Cambios de esquema:

1. Agregar columna:
   - `cont_account_id bigint null`

2. Crear FK:
   - `miscelaneos_catalogo.cont_account_id -> con_plan_cuentas.account_id`

3. Crear indice:
   - sobre `cont_account_id`

4. Actualizar:
   - entidad EF
   - model snapshot/migracion o script SQL operativo segun el patron actual del proyecto
   - DTOs si aplica para exposicion interna o futura administracion

Reglas:

1. La columna puede quedar nullable en BD.
2. Pero el flujo de generacion del recibo la tratara como obligatoria.

Resultado esperado de Fase 3:

- cada concepto miscelaneo puede resolver su cuenta de ingreso.

### Fase 4. Extender el motor de plantillas para lineas dinamicas

Objetivo:

- permitir que una plantilla genere lineas contables variables segun los detalles del documento.

Problema a resolver:

- `con_plantilla_partida_dtl` hoy solo maneja `account_id` fijo.
- Miscelaneos necesita multiples cuentas credito, una por concepto.

Diseno requerido:

Agregar soporte a `con_plantilla_partida_dtl` para dos modos:

1. `FIXED`
   - comportamiento actual
   - usa `account_id`, `debit_formula`, `credit_formula`

2. `DETAIL_EXPAND`
   - genera una linea por cada item en `p_values.details`
   - cuenta contable tomada del payload
   - monto tomado del payload
   - descripcion tomada del payload

Cambios propuestos en tabla `con_plantilla_partida_dtl`:

1. `line_mode varchar(20) not null default 'FIXED'`
2. `entry_side char(1) null`
3. `detail_account_field varchar(50) null`
4. `detail_amount_field varchar(50) null`
5. `detail_description_field varchar(50) null`
6. `account_id` pasa a nullable para soportar `DETAIL_EXPAND`

Convenciones:

1. `FIXED`
   - `account_id` obligatorio
   - formulas obligatorias segun el lado que corresponda

2. `DETAIL_EXPAND`
   - `account_id` null permitido
   - `entry_side` obligatorio
   - `detail_account_field` obligatorio
   - `detail_amount_field` obligatorio
   - `detail_description_field` opcional

Cambios requeridos en `sp_con_generar_comprobante`:

1. Mantener compatibilidad total con lineas `FIXED`.
2. Detectar lineas `DETAIL_EXPAND`.
3. Leer `p_values->'details'`.
4. Recorrer cada detalle.
5. Insertar una linea contable por detalle.
6. Calcular debito/credito segun `entry_side`.
7. Recalcular totales del encabezado.
8. Mantener posteo automatico al final.

Formato de `p_values` para miscelaneos:

```json
{
  "total": 425.50,
  "details": [
    {
      "code": "MIS-DEMO-001",
      "description": "Derecho de reconexion",
      "account_id": 123,
      "total": 150.00
    },
    {
      "code": "MIS-DEMO-002",
      "description": "Reposicion de medidor",
      "account_id": 456,
      "total": 275.50
    }
  ]
}
```

Resultado esperado de Fase 4:

- el motor queda listo para documentar polizas con lineas variables por concepto.

### Fase 5. Crear document type y plantilla contable para miscelaneos

Objetivo:

- dejar preparado el contrato contable propio del flujo.

Cambios requeridos:

1. Crear `cfg_document_type`:
   - `module = 'VENTAS'`
   - `code = 'MIS'`
   - nombre y descripcion acordes a "Recibo miscelaneo"

2. Crear plantilla `VENTAS/MIS` en `con_plantilla_partida_hdr`.

3. Crear lineas en `con_plantilla_partida_dtl`:
   - Linea 1:
     - tipo `FIXED`
     - debito a cuenta por cobrar
     - formula con `{total}`
   - Linea 2:
     - tipo `DETAIL_EXPAND`
     - credito por cada concepto
     - cuenta desde `details[].account_id`
     - monto desde `details[].total`
     - descripcion desde `details[].description`

4. Definir `document_number`:
   - `MIS-{numrecibo}`

5. Definir `document_id`:
   - `numrecibo`

6. Resolver `type_id` contable correcto.
   - Resolverlo explicitamente por `con_tipo_transaccion.code = 'FAC'`.
   - Si no existe tipo contable requerido, fallar con mensaje claro.

Resultado esperado de Fase 5:

- existe una ruta contable dedicada para facturacion miscelaneos.

### Fase 6. Integrar contabilidad en `CrearReciboAsync`

Objetivo:

- que la generacion del recibo cree y postee su poliza automaticamente.

Secuencia requerida:

1. Validar cliente y detalles.
2. Resolver conceptos desde catalogo con su `cont_account_id`.
3. Crear `factura`.
4. Crear `factura_detalle`.
5. Crear `transaccion_abonado`.
6. Construir `p_values` con:
   - `total`
   - `details[]`
7. Resolver:
   - `company_id`
   - `document_type = 'MIS'`
   - plantilla activa `VENTAS/MIS`
   - `type_id`
   - diario si el flujo lo requiere
8. Invocar `sp_con_generar_comprobante`.
9. Dejar que el motor postee con `sp_con_postear_poliza`.
10. Confirmar la transaccion completa.

Comportamiento ante error:

1. Si falla validacion de catalogo o cuenta:
   - no crear recibo.

2. Si falla generacion/posteo contable:
   - revertir toda la operacion.
   - no dejar `factura`, `factura_detalle` ni `transaccion_abonado` huerfanos.

Resultado esperado de Fase 6:

- el recibo y su poliza se generan atomica y consistentemente.

### Fase 7. Evidencia, scripts y documentacion de soporte

Objetivo:

- dejar el flujo demostrable y reproducible.

Entregables:

1. Script de cambio de esquema para `miscelaneos_catalogo.cont_account_id`.
2. Script para `cfg_document_type` `VENTAS/MIS`.
3. Script para plantilla `VENTAS/MIS`.
4. Script o checklist E2E de validacion.
5. Actualizacion de documentacion del modulo.

## 6. Interfaces, contratos y cambios publicos

### 6.1 API HTTP

Rutas vigentes del flujo:

- `GET /api/facturacion/miscelaneos/clientes`
- `GET /api/facturacion/miscelaneos/clientes/{clave}`
- `GET /api/facturacion/miscelaneos/categorias`
- `POST /api/facturacion/miscelaneos/recibos`
- `GET /api/facturacion/miscelaneos/recibos/{numero}`
- `GET /api/facturacion/miscelaneos/catalogo/{id}`
- `POST /api/facturacion/miscelaneos/catalogo`
- `PUT /api/facturacion/miscelaneos/catalogo/{id}`
- `DELETE /api/facturacion/miscelaneos/catalogo/{id}`

Cambios funcionales en comportamiento:

1. La busqueda de clientes ahora soportara RTN.
2. `POST /recibos` fallara si algun concepto no tiene cuenta contable configurada.
3. `POST /recibos` fallara si no existe empresa activa para generar la poliza.

### 6.2 DTOs

No se requiere cambiar el payload enviado por la vista para generar el recibo.

La cuenta contable:

- no debe viajar desde el navegador;
- debe resolverse en backend desde `miscelaneos_catalogo`.

### 6.3 Base de datos

Cambios requeridos:

1. `miscelaneos_catalogo.cont_account_id`
2. Extensiones a `con_plantilla_partida_dtl`
3. Nuevo `cfg_document_type` `VENTAS/MIS`
4. Nueva plantilla `VENTAS/MIS`

### 6.4 SPs y motor

Se mantiene la firma de `sp_con_generar_comprobante`.

El cambio es de comportamiento interno:

- soporte de lineas `DETAIL_EXPAND` leyendo `p_values.details`.

Esto evita romper llamadas existentes desde otros modulos.

## 7. Archivos y subsistemas a intervenir

### UI y cliente

- `Prestadoras/apc.Client/Pages/Facturacion/Miscelaneos/Facturacion.razor`
- `Prestadoras/apc.Client/Services/Facturacion/FacturacionMiscelaneosClient.cs`

### API y servicio

- `Prestadoras/apc/Controllers/FacturacionMiscelaneosController.cs`
- `Prestadoras/SIAD.Services/FacturacionMiscelaneos/FacturacionMiscelaneosService.cs`
- registro de dependencias y servicios auxiliares si el flujo requiere empresa actual o servicios contables adicionales

### Entidades y modelo

- `Prestadoras/SIAD.Core/Entities/miscelaneos_catalogo.cs`
- `Prestadoras/SIAD.Data/SiadDbContext.cs`
- migracion o script SQL correspondiente

### Contabilidad

- `Prestadoras/Database/ddl_v3/20260122_contabilidad_comprobantes_cobranza_facturacion.sql`
- nuevo script SQL para `VENTAS/MIS`
- scripts de seed/validacion de contabilidad para el flujo

## 8. Plan de pruebas

### 8.1 Pruebas de UI

1. Buscar por clave.
2. Buscar por nombre.
3. Buscar por RTN.
4. Buscar con un solo resultado y verificar que no se seleccione automaticamente.
5. Seleccionar manualmente un cliente.
6. Verificar habilitacion de `Agregar miscelaneos`.
7. Agregar uno o varios conceptos.
8. Verificar habilitacion de `Generar recibo`.
9. Limpiar cliente y verificar limpieza total del formulario.

### 8.2 Pruebas de negocio

1. Generar recibo con un concepto valido.
2. Generar recibo con multiples conceptos validos.
3. Intentar generar recibo con cliente invalido.
4. Intentar generar recibo sin detalles.
5. Intentar generar recibo con concepto inexistente.
6. Intentar generar recibo con concepto sin `cont_account_id`.

### 8.3 Pruebas contables

1. Generar recibo con dos conceptos y dos cuentas diferentes.
2. Verificar que exista `con_partida_hdr` para `VENTAS/MIS`.
3. Verificar que `document_number = MIS-{numrecibo}`.
4. Verificar una linea debito por el total.
5. Verificar una linea credito por cada detalle.
6. Verificar que cada linea credito use la cuenta del concepto.
7. Verificar que `total_debit = total_credit`.
8. Verificar estado `POSTED`.
9. Verificar impacto en `con_saldo_cuenta`.

### 8.4 Pruebas multiempresa

1. Ejecutar el flujo en empresa A.
2. Ejecutar el flujo en empresa B.
3. Verificar que cada empresa resuelva:
   - su `cfg_document_type`
   - su plantilla
   - sus cuentas
   - sus polizas
4. Verificar que no haya cruces de datos entre empresas.

## 9. Riesgos y mitigaciones

### Riesgo 1. Extender plantillas rompe flujos existentes

Mitigacion:

- mantener compatibilidad total con `FIXED`
- agregar comportamiento nuevo solo cuando `line_mode = 'DETAIL_EXPAND'`
- no cambiar la firma publica del SP

### Riesgo 2. Conceptos sin cuenta contable

Mitigacion:

- validacion obligatoria antes de generar el recibo
- script de carga minima de cuentas por concepto para demo

### Riesgo 3. Reusar `REC` o `FAC` cause colisiones contables

Mitigacion:

- crear document type dedicado `VENTAS/MIS`

### Riesgo 4. Flujo deja datos huerfanos si falla contabilidad

Mitigacion:

- una sola transaccion de aplicacion
- rollback completo si falla la poliza o el posteo

## 10. Criterios de aceptacion final

Se considera terminado cuando se cumpla todo esto:

1. La vista de miscelaneos es usable sin autoseleccion de cliente.
2. La busqueda por RTN funciona.
3. Los botones reflejan correctamente el estado del formulario.
4. El recibo miscelaneo se genera con conceptos validos.
5. Cada concepto exige cuenta contable configurada.
6. Se crea poliza `VENTAS/MIS`.
7. La poliza queda posteada automaticamente.
8. La poliza genera credito por concepto.
9. Todo funciona por empresa activa.
10. No se usa `con_regla_integracion` en este flujo.

## 11. Fuera de alcance en esta entrega

Queda fuera de este plan:

1. Migrar `sp_lectura_v2` al motor de plantillas.
2. Cambiar contrato del flujo movil.
3. Rehacer bancos para usar plantillas.

## 12. Nota final

Este plan asume que la prioridad es dejar:

1. una vista operativa,
2. una contabilidad automatica correcta,
3. una arquitectura alineada con la decision actual de usar plantillas como verdad runtime,
4. sin contaminar el flujo movil basado en `con_regla_integracion`.
