-- Permisos por rol: reemplaza a las policies por rol (CanContabilidad, CanBancos, ...).
-- Idempotente: se puede re-ejecutar. Solo AGREGA permisos; no quita los existentes.
-- Aplica sobre el esquema identity. Requiere que los roles ya existan.
BEGIN;

-- Admin: 141 permisos
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
        ('module.compras.view'),
        ('module.configuracion.create'),
        ('module.configuracion.delete'),
        ('module.configuracion.edit'),
        ('module.configuracion.view'),
        ('module.contabilidad.create'),
        ('module.contabilidad.delete'),
        ('module.contabilidad.edit'),
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
        ('module.contabilidad.presupuesto.aprobar'),
        ('module.contabilidad.saldos__contabilidad_saldos_companyid_verificacion.view'),
        ('module.contabilidad.view'),
        ('module.inventario.create'),
        ('module.inventario.delete'),
        ('module.inventario.edit'),
        ('module.inventario.view'),
        ('module.proveedores.create'),
        ('module.proveedores.delete'),
        ('module.proveedores.edit'),
        ('module.proveedores.view'),
        ('module.reporteria.create'),
        ('module.reporteria.delete'),
        ('module.reporteria.edit'),
        ('module.reporteria.sql_personalizado.edit'),
        ('module.reporteria.view'),
        ('module.ventas.caja.abono.banco'),
        ('module.ventas.caja.create'),
        ('module.ventas.caja.delete'),
        ('module.ventas.caja.edit'),
        ('module.ventas.caja.view'),
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

-- Super Administrador: 141 permisos
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
        ('module.compras.view'),
        ('module.configuracion.create'),
        ('module.configuracion.delete'),
        ('module.configuracion.edit'),
        ('module.configuracion.view'),
        ('module.contabilidad.create'),
        ('module.contabilidad.delete'),
        ('module.contabilidad.edit'),
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
        ('module.contabilidad.presupuesto.aprobar'),
        ('module.contabilidad.saldos__contabilidad_saldos_companyid_verificacion.view'),
        ('module.contabilidad.view'),
        ('module.inventario.create'),
        ('module.inventario.delete'),
        ('module.inventario.edit'),
        ('module.inventario.view'),
        ('module.proveedores.create'),
        ('module.proveedores.delete'),
        ('module.proveedores.edit'),
        ('module.proveedores.view'),
        ('module.reporteria.create'),
        ('module.reporteria.delete'),
        ('module.reporteria.edit'),
        ('module.reporteria.sql_personalizado.edit'),
        ('module.reporteria.view'),
        ('module.ventas.caja.abono.banco'),
        ('module.ventas.caja.create'),
        ('module.ventas.caja.delete'),
        ('module.ventas.caja.edit'),
        ('module.ventas.caja.view'),
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

-- Contabilidad: 7 permisos
INSERT INTO identity."AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT r."Id", 'permission', v.permiso
FROM identity."AspNetRoles" r
CROSS JOIN (VALUES
        ('module.bancos.view'),
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

-- Compras: 9 permisos
INSERT INTO identity."AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT r."Id", 'permission', v.permiso
FROM identity."AspNetRoles" r
CROSS JOIN (VALUES
        ('module.compras'),
        ('module.compras.create'),
        ('module.compras.delete'),
        ('module.compras.edit'),
        ('module.compras.view'),
        ('module.inventario.view'),
        ('module.proveedores.create'),
        ('module.proveedores.edit'),
        ('module.proveedores.view')
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

-- Presupuesto: 4 permisos
INSERT INTO identity."AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT r."Id", 'permission', v.permiso
FROM identity."AspNetRoles" r
CROSS JOIN (VALUES
        ('module.contabilidad'),
        ('module.contabilidad.presupuesto.aprobar'),
        ('module.contabilidad.view'),
        ('module.reporteria.view')
     ) AS v(permiso)
WHERE r."Name" = 'Presupuesto'
  AND NOT EXISTS (
      SELECT 1 FROM identity."AspNetRoleClaims" c
      WHERE c."RoleId" = r."Id" AND c."ClaimType" = 'permission' AND c."ClaimValue" = v.permiso
  );

-- Compromisos: 5 permisos
INSERT INTO identity."AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
SELECT r."Id", 'permission', v.permiso
FROM identity."AspNetRoles" r
CROSS JOIN (VALUES
        ('module.contabilidad.view'),
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