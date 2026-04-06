# Ventas - Endpoints y Descripcion

Descripcion aproximada basada en el nombre de la funcion.

| Opcion | Endpoint | Que hace |
|---|---|---|
| Captacion Pagos | `api/CaptacionPagos` | Registrar Pago |
| Captacion Pagos | `api/CaptacionPagos/arqueos` | Consultar Arqueos |
| Captacion Pagos | `api/CaptacionPagos/arqueos/paged` | Consultar Arqueos Paginado |
| Captacion Pagos | `api/CaptacionPagos/bancos` | Consultar Bancos |
| Captacion Pagos | `api/CaptacionPagos/cajas` | Consultar Cajas |
| Captacion Pagos | `api/CaptacionPagos/clientes` | Consultar Clientes |
| Captacion Pagos | `api/CaptacionPagos/miscelaneos` | Consultar Miscelaneos |
| Captacion Pagos | `api/CaptacionPagos/miscelaneos/paged` | Consultar Miscelaneos Paginado |
| Captacion Pagos | `api/CaptacionPagos/miscelaneos/registrar` | Registrar Pago Miscelaneo |
| Captacion Pagos | `api/CaptacionPagos/miscelaneos/reverso` | Reversar Pago Miscelaneo |
| Captacion Pagos | `api/CaptacionPagos/miscelaneos/{recibo}/detalle` | Consultar Detalle Recibo Miscelaneo |
| Captacion Pagos | `api/CaptacionPagos/periodo-actual` | Consultar Periodo Actual |
| Captacion Pagos | `api/CaptacionPagos/posteo-manual` | Registrar Pago Manual |
| Captacion Pagos | `api/CaptacionPagos/posteo-manual/reverso` | Reversar Pago Manual |
| Captacion Pagos | `api/CaptacionPagos/reverso` | Reversar Pago |
| Captacion Pagos | `api/CaptacionPagos/saldos-manual/{clienteClave}` | Consultar Saldos Posteo Manual |
| Captacion Pagos | `api/CaptacionPagos/search/{term}` | Buscar Facturas |
| Captacion Pagos | `api/CaptacionPagos/{numFactura}` | Consultar Pago |
| Captacion Pagos | `api/CaptacionPagos/{numFactura}/existe` | Validar Registro Pago |
| Clientes | `api/Clientes` | Registrar |
| Clientes | `api/Clientes` | Consultar |
| Clientes | `api/Clientes/foto-medidor/{ide:int}/imagen` | Consultar Foto Medidor Imagen |
| Clientes | `api/Clientes/search` | Buscar |
| Clientes | `api/Clientes/search-paged` | Buscar Paginado |
| Clientes | `api/Clientes/{id:int}` | Consultar Por Id |
| Clientes | `api/Clientes/{id:int}` | Editar |
| Clientes | `api/Clientes/{id:int}/configuracion-tarifa` | Editar Configuracion Tarifa |
| Clientes | `api/Clientes/{id:int}/configuracion-tarifa/agregar` | Agregar Configuracion Tarifa |
| Clientes | `api/Clientes/{id:int}/configuracion-tarifa/detalle` | Consultar Configuracion Tarifa Detalle |
| Clientes | `api/Clientes/{id:int}/configuracion-tarifa/header` | Consultar Configuracion Tarifa Encabezado |
| Clientes | `api/Clientes/{id:int}/estado-cuenta` | Consultar Estado Cuenta |
| Clientes | `api/Clientes/{id:int}/foto-medidor` | Consultar Foto Medidor |
| Clientes | `api/Clientes/{id:int}/foto-medidor/header` | Consultar Foto Medidor Encabezado |
| Clientes | `api/Clientes/{id:int}/historico-consumo` | Consultar Historico Consumo |
| Clientes | `api/Clientes/{id:int}/historico-consumo/paged` | Consultar Historico Consumo Paginado |
| Clientes | `api/Clientes/{id:int}/movimientos` | Consultar Movimientos |
| Clientes | `api/Clientes/{id:int}/movimientos/paged` | Consultar Movimientos Paginado |
| Clientes | `api/Clientes/{id:int}/tarifas` | Consultar Tarifas |
| Cobranza | `api/Cobranza/clientes/{clave}/bloqueo` | Consultar Bloqueo |
| Cobranza | `api/Cobranza/clientes/{clave}/saldos` | Consultar Saldos |
| Cobranza | `api/Cobranza/numero-letras` | Numero A Letras |
| Cobranza | `api/Cobranza/planes` | Registrar Plan |
| Cobranza | `api/Cobranza/planes` | Listar Planes |
| Cobranza | `api/Cobranza/planes/calcular` | Calcular Plan |
| Cobranza | `api/Cobranza/planes/{correlativo}` | Consultar Plan |
| Facturacion Miscelaneos | `api/facturacion/miscelaneos/categorias` | Consultar Catalogo |
| Facturacion Miscelaneos | `api/facturacion/miscelaneos/clientes` | Buscar Clientes |
| Facturacion Miscelaneos | `api/facturacion/miscelaneos/clientes/{clave}` | Consultar Cliente |
| Facturacion Miscelaneos | `api/facturacion/miscelaneos/catalogo/{id:int}` | Consultar Concepto del Catalogo |
| Facturacion Miscelaneos | `api/facturacion/miscelaneos/catalogo` | Crear Concepto del Catalogo |
| Facturacion Miscelaneos | `api/facturacion/miscelaneos/catalogo/{id:int}` | Actualizar Concepto del Catalogo |
| Facturacion Miscelaneos | `api/facturacion/miscelaneos/catalogo/{id:int}` | Eliminar Concepto del Catalogo |
| Facturacion Miscelaneos | `api/facturacion/miscelaneos/recibos` | Registrar Recibo |
| Facturacion Miscelaneos | `api/facturacion/miscelaneos/recibos/{numero:int}` | Consultar Recibo |
| Notas Credito Debito | `api/facturacion/notas` | Registrar Nota |
| Notas Credito Debito | `api/facturacion/notas/clientes` | Buscar Clientes |
| Notas Credito Debito | `api/facturacion/notas/clientes/{clave}` | Consultar Cliente |
| Notas Credito Debito | `api/facturacion/notas/clientes/{clave}/configuracion` | Consultar Configuracion |
| Notas Credito Debito | `api/facturacion/notas/motivos` | Listar Motivos |
| Notas Credito Debito | `api/facturacion/notas/motivos/{id:int}` | Consultar Motivo |
