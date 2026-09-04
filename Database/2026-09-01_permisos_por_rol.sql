-- Permisos por rol: reemplaza a las policies por rol (CanContabilidad, CanBancos, ...).
-- Regenerado sobre el catalogo YA FUSIONADO con feat/almacen-integracion-contable
-- (235 permisos: ventas, inventario, compras, proveedores, talento humano, ...).
-- Idempotente: se puede re-ejecutar. Solo AGREGA permisos; no quita los existentes.
-- Aplica sobre el esquema identity. Requiere que los roles ya existan.
BEGIN;

-- Presupuesto: retira 2 permiso(s) que ya no le corresponden
DELETE FROM identity."AspNetRoleClaims" c
USING identity."AspNetRoles" r
WHERE c."RoleId" = r."Id"
  AND c."ClaimType" = 'permission'
  AND r."Name" = 'Presupuesto'
  AND c."ClaimValue" IN ('module.contabilidad', 'module.contabilidad.view');

-- Compromisos: retira 2 permiso(s) que ya no le corresponden
DELETE FROM identity."AspNetRoleClaims" c
USING identity."AspNetRoles" r
WHERE c."RoleId" = r."Id"
  AND c."ClaimType" = 'permission'
  AND r."Name" = 'Compromisos'
  AND c."ClaimValue" IN ('module.contabilidad', 'module.contabilidad.view');

-- Admin: 235 permisos
INSERT INTO identity."AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT r."Id", 'permission', v.permiso
FROM identity."AspNetRoles" r
CROSS JOIN (VALUES
        ('module.bancos.create'),
        ('module.bancos.delete'),
        ('module.bancos.edit'),
        ('module.bancos.view'),
        ('module.compras.create'),
        ('module.compras.delete'),
        ('module.compras.edit'),
        ('module.compras.ordenes.aprobar'),
        ('module.compras.view'),
        ('module.configuracion.aprobaciones.edit'),
        ('module.configuracion.aprobaciones.view'),
        ('module.configuracion.correo.edit'),
        ('module.configuracion.correo.view'),
        ('module.configuracion.create'),
        ('module.configuracion.delete'),
        ('module.configuracion.edit'),
        ('module.configuracion.formatos_fiscales.create'),
        ('module.configuracion.formatos_fiscales.edit'),
        ('module.configuracion.formatos_fiscales.view'),
        ('module.configuracion.formatos_fiscales__mantenimientos_formatos_fiscales.create'),
        ('module.configuracion.formatos_fiscales__mantenimientos_formatos_fiscales.edit'),
        ('module.configuracion.formatos_fiscales__mantenimientos_formatos_fiscales.view'),
        ('module.configuracion.formatos_fiscales__mantenimientos_formatos_fiscales_desactivar.edit'),
        ('module.configuracion.retenciones.create'),
        ('module.configuracion.retenciones.edit'),
        ('module.configuracion.retenciones.view'),
        ('module.configuracion.view'),
        ('module.contabilidad.create'),
        ('module.contabilidad.delete'),
        ('module.contabilidad.edit'),
        ('module.contabilidad.integracion.create'),
        ('module.contabilidad.integracion.view'),
        ('module.contabilidad.integracion__contabilidad_integracion_categorias.view'),
        ('module.contabilidad.integracion__contabilidad_integracion_companyid.create'),
        ('module.contabilidad.integracion__contabilidad_integracion_companyid.view'),
        ('module.contabilidad.integracion__contabilidad_integracion_companyid_cuentas_posteables.view'),
        ('module.contabilidad.integracion__contabilidad_integracion_companyid_perfil_perfil.create'),
        ('module.contabilidad.integracion__contabilidad_integracion_companyid_servicios.view'),
        ('module.contabilidad.integracion__contabilidad_integracion_companyid_validacion.view'),
        ('module.contabilidad.integracion__contabilidad_lote_facturacion_companyid_generar.create'),
        ('module.contabilidad.integracion__contabilidad_lote_facturacion_companyid_historial.view'),
        ('module.contabilidad.integracion__contabilidad_lote_facturacion_companyid_pendientes.view'),
        ('module.contabilidad.integracion__contabilidad_lote_facturacion_companyid_preview.view'),
        ('module.contabilidad.lotefacturacion.create'),
        ('module.contabilidad.lotefacturacion.view'),
        ('module.contabilidad.presupuesto.aprobar'),
        ('module.contabilidad.presupuesto.view'),
        ('module.contabilidad.saldos.view'),
        ('module.contabilidad.saldos__contabilidad_saldos_companyid_verificacion.view'),
        ('module.contabilidad.view'),
        ('module.inventario.ajustes.create'),
        ('module.inventario.ajustes.delete'),
        ('module.inventario.ajustes.edit'),
        ('module.inventario.ajustes.view'),
        ('module.inventario.ajustes__almacen_ajustes.create'),
        ('module.inventario.ajustes__almacen_ajustes.view'),
        ('module.inventario.carga_inicial.create'),
        ('module.inventario.carga_inicial.delete'),
        ('module.inventario.carga_inicial.edit'),
        ('module.inventario.carga_inicial.view'),
        ('module.inventario.carga_inicial__almacen_carga_inicial_costo_manual.create'),
        ('module.inventario.carga_inicial__almacen_carga_inicial_ejecutar.create'),
        ('module.inventario.carga_inicial__almacen_carga_inicial_pendientes.view'),
        ('module.inventario.carga_inicial__almacen_carga_inicial_simular.view'),
        ('module.inventario.conceptos_movimiento.create'),
        ('module.inventario.conceptos_movimiento.edit'),
        ('module.inventario.conceptos_movimiento.view'),
        ('module.inventario.conceptos_movimiento__almacen_conceptos_movimiento.create'),
        ('module.inventario.conceptos_movimiento__almacen_conceptos_movimiento.edit'),
        ('module.inventario.conceptos_movimiento__almacen_conceptos_movimiento.view'),
        ('module.inventario.conceptos_movimiento__almacen_conceptos_movimiento_desactivar.edit'),
        ('module.inventario.create'),
        ('module.inventario.delete'),
        ('module.inventario.descargos.create'),
        ('module.inventario.descargos.edit'),
        ('module.inventario.descargos.view'),
        ('module.inventario.descargos__almacen_descargos_documentos.create'),
        ('module.inventario.descargos__almacen_descargos_documentos.view'),
        ('module.inventario.descargos__almacen_descargos_documentos_anular.edit'),
        ('module.inventario.descargos__almacen_descargos_documentos_id.view'),
        ('module.inventario.edit'),
        ('module.inventario.movimientos.autorizar_sensibles'),
        ('module.inventario.movimientos.create'),
        ('module.inventario.movimientos.edit'),
        ('module.inventario.movimientos.view'),
        ('module.inventario.movimientos__almacen_movimientos.create'),
        ('module.inventario.movimientos__almacen_movimientos.view'),
        ('module.inventario.movimientos__almacen_movimientos_anular.edit'),
        ('module.inventario.movimientos__almacen_movimientos_id.view'),
        ('module.inventario.requisiciones.aprobar'),
        ('module.inventario.requisiciones.create'),
        ('module.inventario.requisiciones.edit'),
        ('module.inventario.requisiciones.view'),
        ('module.inventario.requisiciones__almacen_requisiciones_documentos.create'),
        ('module.inventario.requisiciones__almacen_requisiciones_documentos.view'),
        ('module.inventario.requisiciones__almacen_requisiciones_documentos_anular.edit'),
        ('module.inventario.requisiciones__almacen_requisiciones_documentos_aprobar.view'),
        ('module.inventario.requisiciones__almacen_requisiciones_documentos_enviar.edit'),
        ('module.inventario.requisiciones__almacen_requisiciones_documentos_id.edit'),
        ('module.inventario.requisiciones__almacen_requisiciones_documentos_id.view'),
        ('module.inventario.requisiciones__almacen_requisiciones_documentos_rechazar.view'),
        ('module.inventario.traslados.create'),
        ('module.inventario.traslados.edit'),
        ('module.inventario.traslados.view'),
        ('module.inventario.traslados__almacen_traslados.create'),
        ('module.inventario.traslados__almacen_traslados.view'),
        ('module.inventario.traslados__almacen_traslados_anular.edit'),
        ('module.inventario.traslados__almacen_traslados_id.view'),
        ('module.inventario.traslados__almacen_traslados_recibir.edit'),
        ('module.inventario.view'),
        ('module.proveedores.antiguedad_saldos.view'),
        ('module.proveedores.create'),
        ('module.proveedores.delete'),
        ('module.proveedores.edit'),
        ('module.proveedores.estado_cuenta.view'),
        ('module.proveedores.evaluacion.edit'),
        ('module.proveedores.evaluacion.view'),
        ('module.proveedores.incidencias.edit'),
        ('module.proveedores.incidencias.view'),
        ('module.proveedores.retenciones.view'),
        ('module.proveedores.view'),
        ('module.reporteria.create'),
        ('module.reporteria.delete'),
        ('module.reporteria.edit'),
        ('module.reporteria.sql_personalizado.edit'),
        ('module.reporteria.view'),
        ('module.talentohumano.create'),
        ('module.talentohumano.delete'),
        ('module.talentohumano.edit'),
        ('module.talentohumano.view'),
        ('module.ventas.caja.abono.banco'),
        ('module.ventas.caja.create'),
        ('module.ventas.caja.delete'),
        ('module.ventas.caja.edit'),
        ('module.ventas.caja.view'),
        ('module.ventas.calendario_facturacion.create'),
        ('module.ventas.calendario_facturacion.view'),
        ('module.ventas.calendario_facturacion__ventas_calendario_facturacion_companyid_anio.create'),
        ('module.ventas.calendario_facturacion__ventas_calendario_facturacion_companyid_anio.view'),
        ('module.ventas.calendario_facturacion__ventas_calendario_facturacion_companyid_anios.view'),
        ('module.ventas.calendario_facturacion__ventas_calendario_facturacion_companyid_copiar.create'),
        ('module.ventas.captacion_pagos.create'),
        ('module.ventas.captacion_pagos.delete'),
        ('module.ventas.captacion_pagos.edit'),
        ('module.ventas.captacion_pagos.view'),
        ('module.ventas.captacion_pagos__captacionpagos.create'),
        ('module.ventas.captacion_pagos__captacionpagos_arqueos.view'),
        ('module.ventas.captacion_pagos__captacionpagos_arqueos_paged.view'),
        ('module.ventas.captacion_pagos__captacionpagos_bancos.view'),
        ('module.ventas.captacion_pagos__captacionpagos_cajas.view'),
        ('module.ventas.captacion_pagos__captacionpagos_clientes.view'),
        ('module.ventas.captacion_pagos__captacionpagos_miscelaneos.view'),
        ('module.ventas.captacion_pagos__captacionpagos_miscelaneos_paged.view'),
        ('module.ventas.captacion_pagos__captacionpagos_miscelaneos_recibo_detalle.view'),
        ('module.ventas.captacion_pagos__captacionpagos_miscelaneos_registrar.create'),
        ('module.ventas.captacion_pagos__captacionpagos_miscelaneos_reverso.edit'),
        ('module.ventas.captacion_pagos__captacionpagos_numfactura.view'),
        ('module.ventas.captacion_pagos__captacionpagos_numfactura_existe.view'),
        ('module.ventas.captacion_pagos__captacionpagos_periodo_actual.view'),
        ('module.ventas.captacion_pagos__captacionpagos_posteo_manual.edit'),
        ('module.ventas.captacion_pagos__captacionpagos_posteo_manual_reverso.edit'),
        ('module.ventas.captacion_pagos__captacionpagos_reverso.edit'),
        ('module.ventas.captacion_pagos__captacionpagos_saldos_manual_clienteclave.view'),
        ('module.ventas.captacion_pagos__captacionpagos_search_term.view'),
        ('module.ventas.clientes.create'),
        ('module.ventas.clientes.delete'),
        ('module.ventas.clientes.edit'),
        ('module.ventas.clientes.no_cortable.edit'),
        ('module.ventas.clientes.view'),
        ('module.ventas.clientes__clientes.create'),
        ('module.ventas.clientes__clientes.view'),
        ('module.ventas.clientes__clientes_clave_no_cortable.edit'),
        ('module.ventas.clientes__clientes_foto_medidor_ide_imagen.view'),
        ('module.ventas.clientes__clientes_id.edit'),
        ('module.ventas.clientes__clientes_id.view'),
        ('module.ventas.clientes__clientes_id_estado_cuenta.view'),
        ('module.ventas.clientes__clientes_id_foto_medidor.view'),
        ('module.ventas.clientes__clientes_id_foto_medidor_header.view'),
        ('module.ventas.clientes__clientes_id_historico_consumo.view'),
        ('module.ventas.clientes__clientes_id_historico_consumo_paged.view'),
        ('module.ventas.clientes__clientes_id_movimientos.view'),
        ('module.ventas.clientes__clientes_id_movimientos_paged.view'),
        ('module.ventas.clientes__clientes_id_tarifas.view'),
        ('module.ventas.clientes__clientes_search.view'),
        ('module.ventas.clientes__clientes_search_paged.view'),
        ('module.ventas.cobranza.create'),
        ('module.ventas.cobranza.delete'),
        ('module.ventas.cobranza.edit'),
        ('module.ventas.cobranza.view'),
        ('module.ventas.cobranza__cobranza_clientes_clave_bloqueo.view'),
        ('module.ventas.cobranza__cobranza_clientes_clave_saldos.view'),
        ('module.ventas.cobranza__cobranza_numero_letras.view'),
        ('module.ventas.cobranza__cobranza_planes.create'),
        ('module.ventas.cobranza__cobranza_planes.view'),
        ('module.ventas.cobranza__cobranza_planes_calcular.view'),
        ('module.ventas.cobranza__cobranza_planes_correlativo.view'),
        ('module.ventas.condiciones_lectura.create'),
        ('module.ventas.condiciones_lectura.view'),
        ('module.ventas.condiciones_lectura__ventas_condiciones_lectura_companyid.create'),
        ('module.ventas.condiciones_lectura__ventas_condiciones_lectura_companyid.view'),
        ('module.ventas.create'),
        ('module.ventas.delete'),
        ('module.ventas.edit'),
        ('module.ventas.facturacion_miscelaneos.create'),
        ('module.ventas.facturacion_miscelaneos.delete'),
        ('module.ventas.facturacion_miscelaneos.edit'),
        ('module.ventas.facturacion_miscelaneos.view'),
        ('module.ventas.facturacion_miscelaneos__facturacion_miscelaneos_categorias.view'),
        ('module.ventas.facturacion_miscelaneos__facturacion_miscelaneos_clientes.view'),
        ('module.ventas.facturacion_miscelaneos__facturacion_miscelaneos_clientes_clave.view'),
        ('module.ventas.facturacion_miscelaneos__facturacion_miscelaneos_recibos.create'),
        ('module.ventas.facturacion_miscelaneos__facturacion_miscelaneos_recibos_numero.view'),
        ('module.ventas.notas_credito_debito.create'),
        ('module.ventas.notas_credito_debito.delete'),
        ('module.ventas.notas_credito_debito.edit'),
        ('module.ventas.notas_credito_debito.view'),
        ('module.ventas.notas_credito_debito__facturacion_notas.create'),
        ('module.ventas.notas_credito_debito__facturacion_notas_clientes.view'),
        ('module.ventas.notas_credito_debito__facturacion_notas_clientes_clave.view'),
        ('module.ventas.notas_credito_debito__facturacion_notas_clientes_clave_configuracion.view'),
        ('module.ventas.notas_credito_debito__facturacion_notas_motivos.view'),
        ('module.ventas.notas_credito_debito__facturacion_notas_motivos_id.view'),
        ('module.ventas.periodos_comerciales.create'),
        ('module.ventas.periodos_comerciales.view'),
        ('module.ventas.periodos_comerciales__ventas_periodos_comerciales_companyid.view'),
        ('module.ventas.periodos_comerciales__ventas_periodos_comerciales_companyid_abrir.create'),
        ('module.ventas.periodos_comerciales__ventas_periodos_comerciales_companyid_abrir_preview.view'),
        ('module.ventas.periodos_comerciales__ventas_periodos_comerciales_companyid_abrir_sugerencia.view'),
        ('module.ventas.periodos_comerciales__ventas_periodos_comerciales_companyid_ciclos_periodocicloid_cerrar.create'),
        ('module.ventas.periodos_comerciales__ventas_periodos_comerciales_companyid_ciclos_periodocicloid_deshacer.create'),
        ('module.ventas.periodos_comerciales__ventas_periodos_comerciales_companyid_ciclos_periodocicloid_planilla.view'),
        ('module.ventas.periodos_comerciales__ventas_periodos_comerciales_companyid_ciclos_periodocicloid_rutas.view'),
        ('module.ventas.periodos_comerciales__ventas_periodos_comerciales_companyid_periodocomercialid_cerrar.create'),
        ('module.ventas.periodos_comerciales__ventas_periodos_comerciales_companyid_periodocomercialid_checklist.view'),
        ('module.ventas.view')
     ) AS v(permiso)
WHERE r."Name" = 'Admin'
  AND NOT EXISTS (
      SELECT 1 FROM identity."AspNetRoleClaims" c
      WHERE c."RoleId" = r."Id" AND c."ClaimType" = 'permission' AND c."ClaimValue" = v.permiso
  );

-- Super Administrador: 235 permisos
INSERT INTO identity."AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT r."Id", 'permission', v.permiso
FROM identity."AspNetRoles" r
CROSS JOIN (VALUES
        ('module.bancos.create'),
        ('module.bancos.delete'),
        ('module.bancos.edit'),
        ('module.bancos.view'),
        ('module.compras.create'),
        ('module.compras.delete'),
        ('module.compras.edit'),
        ('module.compras.ordenes.aprobar'),
        ('module.compras.view'),
        ('module.configuracion.aprobaciones.edit'),
        ('module.configuracion.aprobaciones.view'),
        ('module.configuracion.correo.edit'),
        ('module.configuracion.correo.view'),
        ('module.configuracion.create'),
        ('module.configuracion.delete'),
        ('module.configuracion.edit'),
        ('module.configuracion.formatos_fiscales.create'),
        ('module.configuracion.formatos_fiscales.edit'),
        ('module.configuracion.formatos_fiscales.view'),
        ('module.configuracion.formatos_fiscales__mantenimientos_formatos_fiscales.create'),
        ('module.configuracion.formatos_fiscales__mantenimientos_formatos_fiscales.edit'),
        ('module.configuracion.formatos_fiscales__mantenimientos_formatos_fiscales.view'),
        ('module.configuracion.formatos_fiscales__mantenimientos_formatos_fiscales_desactivar.edit'),
        ('module.configuracion.retenciones.create'),
        ('module.configuracion.retenciones.edit'),
        ('module.configuracion.retenciones.view'),
        ('module.configuracion.view'),
        ('module.contabilidad.create'),
        ('module.contabilidad.delete'),
        ('module.contabilidad.edit'),
        ('module.contabilidad.integracion.create'),
        ('module.contabilidad.integracion.view'),
        ('module.contabilidad.integracion__contabilidad_integracion_categorias.view'),
        ('module.contabilidad.integracion__contabilidad_integracion_companyid.create'),
        ('module.contabilidad.integracion__contabilidad_integracion_companyid.view'),
        ('module.contabilidad.integracion__contabilidad_integracion_companyid_cuentas_posteables.view'),
        ('module.contabilidad.integracion__contabilidad_integracion_companyid_perfil_perfil.create'),
        ('module.contabilidad.integracion__contabilidad_integracion_companyid_servicios.view'),
        ('module.contabilidad.integracion__contabilidad_integracion_companyid_validacion.view'),
        ('module.contabilidad.integracion__contabilidad_lote_facturacion_companyid_generar.create'),
        ('module.contabilidad.integracion__contabilidad_lote_facturacion_companyid_historial.view'),
        ('module.contabilidad.integracion__contabilidad_lote_facturacion_companyid_pendientes.view'),
        ('module.contabilidad.integracion__contabilidad_lote_facturacion_companyid_preview.view'),
        ('module.contabilidad.lotefacturacion.create'),
        ('module.contabilidad.lotefacturacion.view'),
        ('module.contabilidad.presupuesto.aprobar'),
        ('module.contabilidad.presupuesto.view'),
        ('module.contabilidad.saldos.view'),
        ('module.contabilidad.saldos__contabilidad_saldos_companyid_verificacion.view'),
        ('module.contabilidad.view'),
        ('module.inventario.ajustes.create'),
        ('module.inventario.ajustes.delete'),
        ('module.inventario.ajustes.edit'),
        ('module.inventario.ajustes.view'),
        ('module.inventario.ajustes__almacen_ajustes.create'),
        ('module.inventario.ajustes__almacen_ajustes.view'),
        ('module.inventario.carga_inicial.create'),
        ('module.inventario.carga_inicial.delete'),
        ('module.inventario.carga_inicial.edit'),
        ('module.inventario.carga_inicial.view'),
        ('module.inventario.carga_inicial__almacen_carga_inicial_costo_manual.create'),
        ('module.inventario.carga_inicial__almacen_carga_inicial_ejecutar.create'),
        ('module.inventario.carga_inicial__almacen_carga_inicial_pendientes.view'),
        ('module.inventario.carga_inicial__almacen_carga_inicial_simular.view'),
        ('module.inventario.conceptos_movimiento.create'),
        ('module.inventario.conceptos_movimiento.edit'),
        ('module.inventario.conceptos_movimiento.view'),
        ('module.inventario.conceptos_movimiento__almacen_conceptos_movimiento.create'),
        ('module.inventario.conceptos_movimiento__almacen_conceptos_movimiento.edit'),
        ('module.inventario.conceptos_movimiento__almacen_conceptos_movimiento.view'),
        ('module.inventario.conceptos_movimiento__almacen_conceptos_movimiento_desactivar.edit'),
        ('module.inventario.create'),
        ('module.inventario.delete'),
        ('module.inventario.descargos.create'),
        ('module.inventario.descargos.edit'),
        ('module.inventario.descargos.view'),
        ('module.inventario.descargos__almacen_descargos_documentos.create'),
        ('module.inventario.descargos__almacen_descargos_documentos.view'),
        ('module.inventario.descargos__almacen_descargos_documentos_anular.edit'),
        ('module.inventario.descargos__almacen_descargos_documentos_id.view'),
        ('module.inventario.edit'),
        ('module.inventario.movimientos.autorizar_sensibles'),
        ('module.inventario.movimientos.create'),
        ('module.inventario.movimientos.edit'),
        ('module.inventario.movimientos.view'),
        ('module.inventario.movimientos__almacen_movimientos.create'),
        ('module.inventario.movimientos__almacen_movimientos.view'),
        ('module.inventario.movimientos__almacen_movimientos_anular.edit'),
        ('module.inventario.movimientos__almacen_movimientos_id.view'),
        ('module.inventario.requisiciones.aprobar'),
        ('module.inventario.requisiciones.create'),
        ('module.inventario.requisiciones.edit'),
        ('module.inventario.requisiciones.view'),
        ('module.inventario.requisiciones__almacen_requisiciones_documentos.create'),
        ('module.inventario.requisiciones__almacen_requisiciones_documentos.view'),
        ('module.inventario.requisiciones__almacen_requisiciones_documentos_anular.edit'),
        ('module.inventario.requisiciones__almacen_requisiciones_documentos_aprobar.view'),
        ('module.inventario.requisiciones__almacen_requisiciones_documentos_enviar.edit'),
        ('module.inventario.requisiciones__almacen_requisiciones_documentos_id.edit'),
        ('module.inventario.requisiciones__almacen_requisiciones_documentos_id.view'),
        ('module.inventario.requisiciones__almacen_requisiciones_documentos_rechazar.view'),
        ('module.inventario.traslados.create'),
        ('module.inventario.traslados.edit'),
        ('module.inventario.traslados.view'),
        ('module.inventario.traslados__almacen_traslados.create'),
        ('module.inventario.traslados__almacen_traslados.view'),
        ('module.inventario.traslados__almacen_traslados_anular.edit'),
        ('module.inventario.traslados__almacen_traslados_id.view'),
        ('module.inventario.traslados__almacen_traslados_recibir.edit'),
        ('module.inventario.view'),
        ('module.proveedores.antiguedad_saldos.view'),
        ('module.proveedores.create'),
        ('module.proveedores.delete'),
        ('module.proveedores.edit'),
        ('module.proveedores.estado_cuenta.view'),
        ('module.proveedores.evaluacion.edit'),
        ('module.proveedores.evaluacion.view'),
        ('module.proveedores.incidencias.edit'),
        ('module.proveedores.incidencias.view'),
        ('module.proveedores.retenciones.view'),
        ('module.proveedores.view'),
        ('module.reporteria.create'),
        ('module.reporteria.delete'),
        ('module.reporteria.edit'),
        ('module.reporteria.sql_personalizado.edit'),
        ('module.reporteria.view'),
        ('module.talentohumano.create'),
        ('module.talentohumano.delete'),
        ('module.talentohumano.edit'),
        ('module.talentohumano.view'),
        ('module.ventas.caja.abono.banco'),
        ('module.ventas.caja.create'),
        ('module.ventas.caja.delete'),
        ('module.ventas.caja.edit'),
        ('module.ventas.caja.view'),
        ('module.ventas.calendario_facturacion.create'),
        ('module.ventas.calendario_facturacion.view'),
        ('module.ventas.calendario_facturacion__ventas_calendario_facturacion_companyid_anio.create'),
        ('module.ventas.calendario_facturacion__ventas_calendario_facturacion_companyid_anio.view'),
        ('module.ventas.calendario_facturacion__ventas_calendario_facturacion_companyid_anios.view'),
        ('module.ventas.calendario_facturacion__ventas_calendario_facturacion_companyid_copiar.create'),
        ('module.ventas.captacion_pagos.create'),
        ('module.ventas.captacion_pagos.delete'),
        ('module.ventas.captacion_pagos.edit'),
        ('module.ventas.captacion_pagos.view'),
        ('module.ventas.captacion_pagos__captacionpagos.create'),
        ('module.ventas.captacion_pagos__captacionpagos_arqueos.view'),
        ('module.ventas.captacion_pagos__captacionpagos_arqueos_paged.view'),
        ('module.ventas.captacion_pagos__captacionpagos_bancos.view'),
        ('module.ventas.captacion_pagos__captacionpagos_cajas.view'),
        ('module.ventas.captacion_pagos__captacionpagos_clientes.view'),
        ('module.ventas.captacion_pagos__captacionpagos_miscelaneos.view'),
        ('module.ventas.captacion_pagos__captacionpagos_miscelaneos_paged.view'),
        ('module.ventas.captacion_pagos__captacionpagos_miscelaneos_recibo_detalle.view'),
        ('module.ventas.captacion_pagos__captacionpagos_miscelaneos_registrar.create'),
        ('module.ventas.captacion_pagos__captacionpagos_miscelaneos_reverso.edit'),
        ('module.ventas.captacion_pagos__captacionpagos_numfactura.view'),
        ('module.ventas.captacion_pagos__captacionpagos_numfactura_existe.view'),
        ('module.ventas.captacion_pagos__captacionpagos_periodo_actual.view'),
        ('module.ventas.captacion_pagos__captacionpagos_posteo_manual.edit'),
        ('module.ventas.captacion_pagos__captacionpagos_posteo_manual_reverso.edit'),
        ('module.ventas.captacion_pagos__captacionpagos_reverso.edit'),
        ('module.ventas.captacion_pagos__captacionpagos_saldos_manual_clienteclave.view'),
        ('module.ventas.captacion_pagos__captacionpagos_search_term.view'),
        ('module.ventas.clientes.create'),
        ('module.ventas.clientes.delete'),
        ('module.ventas.clientes.edit'),
        ('module.ventas.clientes.no_cortable.edit'),
        ('module.ventas.clientes.view'),
        ('module.ventas.clientes__clientes.create'),
        ('module.ventas.clientes__clientes.view'),
        ('module.ventas.clientes__clientes_clave_no_cortable.edit'),
        ('module.ventas.clientes__clientes_foto_medidor_ide_imagen.view'),
        ('module.ventas.clientes__clientes_id.edit'),
        ('module.ventas.clientes__clientes_id.view'),
        ('module.ventas.clientes__clientes_id_estado_cuenta.view'),
        ('module.ventas.clientes__clientes_id_foto_medidor.view'),
        ('module.ventas.clientes__clientes_id_foto_medidor_header.view'),
        ('module.ventas.clientes__clientes_id_historico_consumo.view'),
        ('module.ventas.clientes__clientes_id_historico_consumo_paged.view'),
        ('module.ventas.clientes__clientes_id_movimientos.view'),
        ('module.ventas.clientes__clientes_id_movimientos_paged.view'),
        ('module.ventas.clientes__clientes_id_tarifas.view'),
        ('module.ventas.clientes__clientes_search.view'),
        ('module.ventas.clientes__clientes_search_paged.view'),
        ('module.ventas.cobranza.create'),
        ('module.ventas.cobranza.delete'),
        ('module.ventas.cobranza.edit'),
        ('module.ventas.cobranza.view'),
        ('module.ventas.cobranza__cobranza_clientes_clave_bloqueo.view'),
        ('module.ventas.cobranza__cobranza_clientes_clave_saldos.view'),
        ('module.ventas.cobranza__cobranza_numero_letras.view'),
        ('module.ventas.cobranza__cobranza_planes.create'),
        ('module.ventas.cobranza__cobranza_planes.view'),
        ('module.ventas.cobranza__cobranza_planes_calcular.view'),
        ('module.ventas.cobranza__cobranza_planes_correlativo.view'),
        ('module.ventas.condiciones_lectura.create'),
        ('module.ventas.condiciones_lectura.view'),
        ('module.ventas.condiciones_lectura__ventas_condiciones_lectura_companyid.create'),
        ('module.ventas.condiciones_lectura__ventas_condiciones_lectura_companyid.view'),
        ('module.ventas.create'),
        ('module.ventas.delete'),
        ('module.ventas.edit'),
        ('module.ventas.facturacion_miscelaneos.create'),
        ('module.ventas.facturacion_miscelaneos.delete'),
        ('module.ventas.facturacion_miscelaneos.edit'),
        ('module.ventas.facturacion_miscelaneos.view'),
        ('module.ventas.facturacion_miscelaneos__facturacion_miscelaneos_categorias.view'),
        ('module.ventas.facturacion_miscelaneos__facturacion_miscelaneos_clientes.view'),
        ('module.ventas.facturacion_miscelaneos__facturacion_miscelaneos_clientes_clave.view'),
        ('module.ventas.facturacion_miscelaneos__facturacion_miscelaneos_recibos.create'),
        ('module.ventas.facturacion_miscelaneos__facturacion_miscelaneos_recibos_numero.view'),
        ('module.ventas.notas_credito_debito.create'),
        ('module.ventas.notas_credito_debito.delete'),
        ('module.ventas.notas_credito_debito.edit'),
        ('module.ventas.notas_credito_debito.view'),
        ('module.ventas.notas_credito_debito__facturacion_notas.create'),
        ('module.ventas.notas_credito_debito__facturacion_notas_clientes.view'),
        ('module.ventas.notas_credito_debito__facturacion_notas_clientes_clave.view'),
        ('module.ventas.notas_credito_debito__facturacion_notas_clientes_clave_configuracion.view'),
        ('module.ventas.notas_credito_debito__facturacion_notas_motivos.view'),
        ('module.ventas.notas_credito_debito__facturacion_notas_motivos_id.view'),
        ('module.ventas.periodos_comerciales.create'),
        ('module.ventas.periodos_comerciales.view'),
        ('module.ventas.periodos_comerciales__ventas_periodos_comerciales_companyid.view'),
        ('module.ventas.periodos_comerciales__ventas_periodos_comerciales_companyid_abrir.create'),
        ('module.ventas.periodos_comerciales__ventas_periodos_comerciales_companyid_abrir_preview.view'),
        ('module.ventas.periodos_comerciales__ventas_periodos_comerciales_companyid_abrir_sugerencia.view'),
        ('module.ventas.periodos_comerciales__ventas_periodos_comerciales_companyid_ciclos_periodocicloid_cerrar.create'),
        ('module.ventas.periodos_comerciales__ventas_periodos_comerciales_companyid_ciclos_periodocicloid_deshacer.create'),
        ('module.ventas.periodos_comerciales__ventas_periodos_comerciales_companyid_ciclos_periodocicloid_planilla.view'),
        ('module.ventas.periodos_comerciales__ventas_periodos_comerciales_companyid_ciclos_periodocicloid_rutas.view'),
        ('module.ventas.periodos_comerciales__ventas_periodos_comerciales_companyid_periodocomercialid_cerrar.create'),
        ('module.ventas.periodos_comerciales__ventas_periodos_comerciales_companyid_periodocomercialid_checklist.view'),
        ('module.ventas.view')
     ) AS v(permiso)
WHERE r."Name" = 'Super Administrador'
  AND NOT EXISTS (
      SELECT 1 FROM identity."AspNetRoleClaims" c
      WHERE c."RoleId" = r."Id" AND c."ClaimType" = 'permission' AND c."ClaimValue" = v.permiso
  );

-- Contabilidad: 8 permisos
INSERT INTO identity."AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT r."Id", 'permission', v.permiso
FROM identity."AspNetRoles" r
CROSS JOIN (VALUES
        ('module.bancos.view'),
        ('module.compras.view'),
        ('module.contabilidad'),
        ('module.contabilidad.create'),
        ('module.contabilidad.delete'),
        ('module.contabilidad.edit'),
        ('module.contabilidad.view'),
        ('module.reporteria.view')
     ) AS v(permiso)
WHERE r."Name" = 'Contabilidad'
  AND NOT EXISTS (
      SELECT 1 FROM identity."AspNetRoleClaims" c
      WHERE c."RoleId" = r."Id" AND c."ClaimType" = 'permission' AND c."ClaimValue" = v.permiso
  );

-- Bancos: 6 permisos
INSERT INTO identity."AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT r."Id", 'permission', v.permiso
FROM identity."AspNetRoles" r
CROSS JOIN (VALUES
        ('module.bancos'),
        ('module.bancos.create'),
        ('module.bancos.delete'),
        ('module.bancos.edit'),
        ('module.bancos.view'),
        ('module.reporteria.view')
     ) AS v(permiso)
WHERE r."Name" = 'Bancos'
  AND NOT EXISTS (
      SELECT 1 FROM identity."AspNetRoleClaims" c
      WHERE c."RoleId" = r."Id" AND c."ClaimType" = 'permission' AND c."ClaimValue" = v.permiso
  );

-- Compras: 15 permisos
INSERT INTO identity."AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT r."Id", 'permission', v.permiso
FROM identity."AspNetRoles" r
CROSS JOIN (VALUES
        ('module.compras'),
        ('module.compras.create'),
        ('module.compras.delete'),
        ('module.compras.edit'),
        ('module.compras.view'),
        ('module.inventario'),
        ('module.inventario.create'),
        ('module.inventario.edit'),
        ('module.inventario.view'),
        ('module.proveedores'),
        ('module.proveedores.create'),
        ('module.proveedores.edit'),
        ('module.proveedores.view'),
        ('module.reporteria.view'),
        ('module.talentohumano.view')
     ) AS v(permiso)
WHERE r."Name" = 'Compras'
  AND NOT EXISTS (
      SELECT 1 FROM identity."AspNetRoleClaims" c
      WHERE c."RoleId" = r."Id" AND c."ClaimType" = 'permission' AND c."ClaimValue" = v.permiso
  );

-- Configuracion: 5 permisos
INSERT INTO identity."AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT r."Id", 'permission', v.permiso
FROM identity."AspNetRoles" r
CROSS JOIN (VALUES
        ('module.configuracion'),
        ('module.configuracion.create'),
        ('module.configuracion.delete'),
        ('module.configuracion.edit'),
        ('module.configuracion.view')
     ) AS v(permiso)
WHERE r."Name" = 'Configuracion'
  AND NOT EXISTS (
      SELECT 1 FROM identity."AspNetRoleClaims" c
      WHERE c."RoleId" = r."Id" AND c."ClaimType" = 'permission' AND c."ClaimValue" = v.permiso
  );

-- Presupuesto: 3 permisos
INSERT INTO identity."AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT r."Id", 'permission', v.permiso
FROM identity."AspNetRoles" r
CROSS JOIN (VALUES
        ('module.contabilidad.presupuesto.aprobar'),
        ('module.contabilidad.presupuesto.view'),
        ('module.reporteria.view')
     ) AS v(permiso)
WHERE r."Name" = 'Presupuesto'
  AND NOT EXISTS (
      SELECT 1 FROM identity."AspNetRoleClaims" c
      WHERE c."RoleId" = r."Id" AND c."ClaimType" = 'permission' AND c."ClaimValue" = v.permiso
  );

-- Compromisos: 4 permisos
INSERT INTO identity."AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT r."Id", 'permission', v.permiso
FROM identity."AspNetRoles" r
CROSS JOIN (VALUES
        ('module.proveedores'),
        ('module.proveedores.create'),
        ('module.proveedores.edit'),
        ('module.proveedores.view')
     ) AS v(permiso)
WHERE r."Name" = 'Compromisos'
  AND NOT EXISTS (
      SELECT 1 FROM identity."AspNetRoleClaims" c
      WHERE c."RoleId" = r."Id" AND c."ClaimType" = 'permission' AND c."ClaimValue" = v.permiso
  );

COMMIT;