# Inventario de Endpoints

## Bancos

### BanMonedasController
- `GET` `api/bancos/monedas`
### BanTiposTransaccionesController
- `GET` `api/bancos/tipos-transacciones`
### BanTransaccionesController
- `GET` `api/bancos/transacciones`
- `POST` `api/bancos/transacciones`
- `POST` `api/bancos/transacciones/anular`
- `GET` `api/bancos/transacciones/{banKardexId}`
- `GET` `api/bancos/transacciones/{banKardexId}/detalle`
- `GET` `api/bancos/transacciones/{banKardexId}/reporte`
### BancoConfiguracionController
- `GET` `api/bancos/configuracion/{companyId:long}`
- `POST` `api/bancos/configuracion/{companyId:long}`
### BancosController
- `GET` `api/bancos`
- `POST` `api/bancos`
- `GET` `api/bancos/{bancoId:long}`
- `PUT` `api/bancos/{bancoId:long}`
- `DELETE` `api/bancos/{bancoId:long}`
### ConfiguracionTransaccionesController
- `GET` `api/bancos/configuracion-transacciones`
- `POST` `api/bancos/configuracion-transacciones`
- `GET` `api/bancos/configuracion-transacciones/{tipoTransaccionId}`
- `PUT` `api/bancos/configuracion-transacciones/{tipoTransaccionId}`
- `DELETE` `api/bancos/configuracion-transacciones/{tipoTransaccionId}`
### CuentasBancosController
- `GET` `api/bancos/cuentas`
- `POST` `api/bancos/cuentas`
- `GET` `api/bancos/cuentas/conciliacion`
- `GET` `api/bancos/cuentas/conciliacion/conciliadas`
- `POST` `api/bancos/cuentas/conciliacion/confirmar`
- `POST` `api/bancos/cuentas/conciliacion/importar`
- `GET` `api/bancos/cuentas/conciliacion/plantilla`
- `GET` `api/bancos/cuentas/contables`
- `GET` `api/bancos/cuentas/{cuentaId:long}`
- `PUT` `api/bancos/cuentas/{cuentaId:long}`
- `DELETE` `api/bancos/cuentas/{cuentaId:long}`

## Compras

### ProveedoresController
- `GET` `api/Proveedores`
- `POST` `api/Proveedores`
- `GET` `api/Proveedores/search`
- `GET` `api/Proveedores/tipos`
- `POST` `api/Proveedores/tipos`
- `GET` `api/Proveedores/tipos/catalogo`
- `GET` `api/Proveedores/tipos/{id:int}`
- `PUT` `api/Proveedores/tipos/{id:int}`
- `DELETE` `api/Proveedores/tipos/{id:int}`
- `GET` `api/Proveedores/{codigo}`
- `PUT` `api/Proveedores/{codigo}`
- `DELETE` `api/Proveedores/{codigo}`

## Configuracion

### ConfiguracionesAppController
- `GET` `api/configuraciones-app`
- `POST` `api/configuraciones-app`
- `GET` `api/configuraciones-app/paged`
- `GET` `api/configuraciones-app/{id:int}`
- `PUT` `api/configuraciones-app/{id:int}`
- `DELETE` `api/configuraciones-app/{id:int}`
### ServiciosRolesWsController
- `GET` `api/servicios-roles-ws`
- `POST` `api/servicios-roles-ws`
- `GET` `api/servicios-roles-ws/paged`
- `GET` `api/servicios-roles-ws/{rol}/{codigo}`
- `PUT` `api/servicios-roles-ws/{rol}/{codigo}`
- `DELETE` `api/servicios-roles-ws/{rol}/{codigo}`
### UsuariosAppController
- `GET` `api/usuarios-app`
- `POST` `api/usuarios-app`
- `GET` `api/usuarios-app/paged`
- `GET` `api/usuarios-app/{id:int}`
- `PUT` `api/usuarios-app/{id:int}`
- `POST` `api/usuarios-app/{id:int}/desactivar`

## Contabilidad

### ConfiguracionSistemaController
- `GET` `api/contabilidad/configuracion/{companyId}`
- `POST` `api/contabilidad/configuracion/{companyId}`
- `GET` `api/contabilidad/{companyId}/cuentas`
### ContabilidadCatalogosController
- `GET` `api/contabilidad/catalogos/centros-costo`
- `POST` `api/contabilidad/catalogos/centros-costo`
- `DELETE` `api/contabilidad/catalogos/centros-costo/{costCenterId:long}`
- `GET` `api/contabilidad/catalogos/diarios`
- `POST` `api/contabilidad/catalogos/diarios`
- `GET` `api/contabilidad/catalogos/periodos`
- `POST` `api/contabilidad/catalogos/periodos`
- `POST` `api/contabilidad/catalogos/periodos/{periodId:long}/cerrar`
- `GET` `api/contabilidad/catalogos/plan-cuentas`
- `POST` `api/contabilidad/catalogos/plan-cuentas`
- `GET` `api/contabilidad/catalogos/plan-cuentas/buscar`
- `POST` `api/contabilidad/catalogos/plan-cuentas/import`
- `GET` `api/contabilidad/catalogos/plan-cuentas/paged`
- `GET` `api/contabilidad/catalogos/plan-cuentas/plantilla`
- `GET` `api/contabilidad/catalogos/tipos-partida`
- `GET` `api/contabilidad/catalogos/tipos-transaccion`
- `POST` `api/contabilidad/catalogos/tipos-transaccion`
- `DELETE` `api/contabilidad/catalogos/tipos-transaccion/rules/{ruleId:long}`
- `DELETE` `api/contabilidad/catalogos/tipos-transaccion/{typeId:long}`
- `GET` `api/contabilidad/catalogos/tipos-transaccion/{typeId:long}/rules`
- `POST` `api/contabilidad/catalogos/tipos-transaccion/{typeId:long}/rules`
### ContabilidadEmpresaController
- `POST` `api/contabilidad/empresas`
- `GET` `api/contabilidad/empresas/{companyId}`
- `PUT` `api/contabilidad/empresas/{companyId}`
- `POST` `api/contabilidad/empresas/{companyId}/logo`
- `GET` `api/contabilidad/empresas/{companyId}/logo`
### PeriodosContablesController
- `GET` `api/contabilidad/periodos/{companyId}/activo`
- `GET` `api/contabilidad/periodos/{companyId}/existe-abierto`
### PolizasController
- `GET` `api/contabilidad/polizas`
- `POST` `api/contabilidad/polizas`
- `GET` `api/contabilidad/polizas/{id:long}`
- `PUT` `api/contabilidad/polizas/{id:long}`
- `DELETE` `api/contabilidad/polizas/{id:long}`
- `POST` `api/contabilidad/polizas/{id:long}/registrar`
- `POST` `api/contabilidad/polizas/{id:long}/revertir`
- `GET` `api/contabilidad/polizas/{id:long}/validar`

## Inventario

### AbogadosController
- `GET` `api/Abogados`
- `POST` `api/Abogados`
- `GET` `api/Abogados/{id:int}`
- `PUT` `api/Abogados/{id:int}`
- `POST` `api/Abogados/{id:int}/desactivar`
### AuxiliarLecturaController
- `GET` `api/AuxiliarLectura`
- `POST` `api/AuxiliarLectura`
- `DELETE` `api/AuxiliarLectura`
- `POST` `api/AuxiliarLectura/cierre`
- `POST` `api/AuxiliarLectura/masivo`
- `GET` `api/AuxiliarLectura/paged`
- `GET` `api/AuxiliarLectura/periodo-actual`
### CatalogosController
- `GET` `api/catalogos/abogados`
- `GET` `api/catalogos/barrios`
- `GET` `api/catalogos/categorias-por-tipo`
- `GET` `api/catalogos/letras`
- `GET` `api/catalogos/letras-servicio`
- `GET` `api/catalogos/servicios`
- `GET` `api/catalogos/tipos-uso`
### CiclosController
- `GET` `api/Ciclos`
- `POST` `api/Ciclos`
- `GET` `api/Ciclos/paged`
- `GET` `api/Ciclos/{id:int}`
- `PUT` `api/Ciclos/{id:int}`
- `POST` `api/Ciclos/{id:int}/desactivar`
### ConceptosController
- `GET` `api/Conceptos`
- `POST` `api/Conceptos`
- `GET` `api/Conceptos/paged`
- `GET` `api/Conceptos/{id:int}`
- `PUT` `api/Conceptos/{id:int}`
- `POST` `api/Conceptos/{id:int}/desactivar`
### LetrasController
- `GET` `api/Letras`
- `POST` `api/Letras`
- `GET` `api/Letras/paged`
- `GET` `api/Letras/{letra}`
- `PUT` `api/Letras/{letra}`
- `DELETE` `api/Letras/{letra}`
### MedidoresController
- `GET` `api/Medidores`
- `POST` `api/Medidores`
- `POST` `api/Medidores/asignar`
- `POST` `api/Medidores/lecturas-sin-medidor`
- `GET` `api/Medidores/paged`
- `GET` `api/Medidores/{id:int}`
- `PUT` `api/Medidores/{id:int}`
- `POST` `api/Medidores/{id:int}/desactivar`
- `GET` `api/Medidores/{id:int}/edit`
- `GET` `api/Medidores/{id:int}/historial`
### OrdenesController
- `GET` `api/Ordenes`
- `POST` `api/Ordenes`
- `POST` `api/Ordenes/asignaciones`
- `GET` `api/Ordenes/coordenadas`
- `GET` `api/Ordenes/estados`
- `GET` `api/Ordenes/propietarios`
- `GET` `api/Ordenes/tipos`
- `GET` `api/Ordenes/usuarios`
- `GET` `api/Ordenes/{id:int}`
### RutasController
- `GET` `api/Rutas`
- `POST` `api/Rutas`
- `GET` `api/Rutas/ciclos`
- `GET` `api/Rutas/paged`
- `GET` `api/Rutas/{id:int}`
- `PUT` `api/Rutas/{id:int}`
- `POST` `api/Rutas/{id:int}/desactivar`
### ServiciosController
- `GET` `api/Servicios`
- `POST` `api/Servicios`
- `GET` `api/Servicios/paged`
- `GET` `api/Servicios/{id:int}`
- `PUT` `api/Servicios/{id:int}`
- `POST` `api/Servicios/{id:int}/desactivar`
### SolicitudesController
- `GET` `api/Solicitudes`
- `POST` `api/Solicitudes`
- `GET` `api/Solicitudes/categorias`
- `GET` `api/Solicitudes/{id:int}`
- `PUT` `api/Solicitudes/{id:int}`
- `DELETE` `api/Solicitudes/{id:int}`
- `POST` `api/Solicitudes/{id:int}/asignar`
- `POST` `api/Solicitudes/{id:int}/desasignar`
### TarifasBaseController
- `GET` `api/tarifas-base`
- `POST` `api/tarifas-base`
- `GET` `api/tarifas-base/paged`
- `GET` `api/tarifas-base/{tipo:int}/{categoriaId:int}/{codigo}`
- `PUT` `api/tarifas-base/{tipo:int}/{categoriaId:int}/{codigo}`
- `DELETE` `api/tarifas-base/{tipo:int}/{categoriaId:int}/{codigo}`
### TarifasContadorController
- `GET` `api/tarifas-contador`
- `POST` `api/tarifas-contador`
- `GET` `api/tarifas-contador/paged`
- `GET` `api/tarifas-contador/{id:int}`
- `PUT` `api/tarifas-contador/{id:int}`
- `DELETE` `api/tarifas-contador/{id:int}`
### TarifasController
- `GET` `api/tarifas-catalogo`
- `POST` `api/tarifas-catalogo`
- `GET` `api/tarifas-catalogo/paged`
- `GET` `api/tarifas-catalogo/{id:int}`
- `PUT` `api/tarifas-catalogo/{id:int}`
- `POST` `api/tarifas-catalogo/{id:int}/desactivar`

## SuperAdmin

### AdminController
- `POST` `api/Admin/seed`
### RolesPortalController
- `GET` `api/parametros/roles`
- `POST` `api/parametros/roles`
- `GET` `api/parametros/roles/permisos`
- `GET` `api/parametros/roles/{id}`
- `PUT` `api/parametros/roles/{id}`
- `DELETE` `api/parametros/roles/{id}`
### TenantCompaniesController
- `GET` `api/tenant/companies`
### TenantSessionController
- `POST` `api/tenant/switch`
### UsuariosPortalController
- `GET` `api/parametros/usuarios`
- `POST` `api/parametros/usuarios`
- `GET` `api/parametros/usuarios/roles`
- `POST` `api/parametros/usuarios/roles/sync`
- `GET` `api/parametros/usuarios/{id}`
- `PUT` `api/parametros/usuarios/{id}`

## Unassigned

### BrandingController
- `GET` `api/Branding`
- `PUT` `api/Branding`
- `POST` `api/Branding/logo`

## Ventas

### CaptacionPagosController
- `POST` `api/CaptacionPagos`
- `GET` `api/CaptacionPagos/arqueos`
- `GET` `api/CaptacionPagos/arqueos/paged`
- `GET` `api/CaptacionPagos/bancos`
- `GET` `api/CaptacionPagos/cajas`
- `GET` `api/CaptacionPagos/clientes`
- `GET` `api/CaptacionPagos/miscelaneos`
- `GET` `api/CaptacionPagos/miscelaneos/paged`
- `POST` `api/CaptacionPagos/miscelaneos/registrar`
- `POST` `api/CaptacionPagos/miscelaneos/reverso`
- `GET` `api/CaptacionPagos/miscelaneos/{recibo}/detalle`
- `GET` `api/CaptacionPagos/periodo-actual`
- `POST` `api/CaptacionPagos/posteo-manual`
- `POST` `api/CaptacionPagos/posteo-manual/reverso`
- `POST` `api/CaptacionPagos/reverso`
- `GET` `api/CaptacionPagos/saldos-manual/{clienteClave}`
- `GET` `api/CaptacionPagos/search/{term}`
- `GET` `api/CaptacionPagos/{numFactura}`
- `GET` `api/CaptacionPagos/{numFactura}/existe`
### ClientesController
- `POST` `api/Clientes`
- `GET` `api/Clientes`
- `GET` `api/Clientes/foto-medidor/{ide:int}/imagen`
- `GET` `api/Clientes/search`
- `GET` `api/Clientes/search-paged`
- `GET` `api/Clientes/{id:int}`
- `PUT` `api/Clientes/{id:int}`
- `POST` `api/Clientes/{id:int}/configuracion-tarifa`
- `POST` `api/Clientes/{id:int}/configuracion-tarifa/agregar`
- `GET` `api/Clientes/{id:int}/configuracion-tarifa/detalle`
- `GET` `api/Clientes/{id:int}/configuracion-tarifa/header`
- `GET` `api/Clientes/{id:int}/estado-cuenta`
- `GET` `api/Clientes/{id:int}/foto-medidor`
- `GET` `api/Clientes/{id:int}/foto-medidor/header`
- `GET` `api/Clientes/{id:int}/historico-consumo`
- `GET` `api/Clientes/{id:int}/historico-consumo/paged`
- `GET` `api/Clientes/{id:int}/movimientos`
- `GET` `api/Clientes/{id:int}/movimientos/paged`
- `GET` `api/Clientes/{id:int}/tarifas`
### CobranzaController
- `GET` `api/Cobranza/clientes/{clave}/bloqueo`
- `GET` `api/Cobranza/clientes/{clave}/saldos`
- `GET` `api/Cobranza/numero-letras`
- `POST` `api/Cobranza/planes`
- `GET` `api/Cobranza/planes`
- `POST` `api/Cobranza/planes/calcular`
- `GET` `api/Cobranza/planes/{correlativo}`
### FacturacionMiscelaneosController
- `GET` `api/facturacion/miscelaneos/categorias`
- `GET` `api/facturacion/miscelaneos/clientes`
- `GET` `api/facturacion/miscelaneos/clientes/{clave}`
- `POST` `api/facturacion/miscelaneos/recibos`
- `GET` `api/facturacion/miscelaneos/recibos/{numero:int}`
### NotasCreditoDebitoController
- `POST` `api/facturacion/notas`
- `GET` `api/facturacion/notas/clientes`
- `GET` `api/facturacion/notas/clientes/{clave}`
- `GET` `api/facturacion/notas/clientes/{clave}/configuracion`
- `GET` `api/facturacion/notas/motivos`
- `GET` `api/facturacion/notas/motivos/{id:int}`
