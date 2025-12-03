-- ================================================
-- 04_adm_maestros_seed.sql
-- Seeds demo para catálogos maestros centralizados:
-- zonas, rutas, clientes, proveedores, servicios,
-- productos, instrumentos, operaciones y configuraciones.
-- Requiere: 01_cfg_configuracion_seed.sql, 02_con_contabilidad_seed.sql,
--           03_adm_security_seed.sql, 07_administracion_core.sql,
--           10_administracion_maestros.sql.
-- ================================================

DO $$
DECLARE
    v_company_id        bigint;
    v_branch_id         bigint;
    v_tax_isv15         bigint;
    v_tax_isv18         bigint;
    v_zona_centro       bigint;
    v_zona_norte        bigint;
    v_ruta_centro       bigint;
    v_ruta_norte        bigint;
    v_cliente_res       bigint;
    v_cliente_emp       bigint;
    v_proveedor_mat     bigint;
    v_proveedor_serv    bigint;
    v_serv_cat          bigint;
    v_serv_cat_recon    bigint;
    v_serv_agua         bigint;
    v_serv_recon        bigint;
    v_prod_cat          bigint;
    v_producto_tubo     bigint;
    v_vendedor_cobros   bigint;
    v_vendedor_cuadrilla bigint;
    v_lista_precio      bigint;
    v_instrumento_tran  bigint;
BEGIN
    SELECT company_id INTO v_company_id
      FROM public.cfg_company
     WHERE code = 'SIAD-DEMO'
     LIMIT 1;

    IF v_company_id IS NULL THEN
        RAISE EXCEPTION 'Company SIAD-DEMO no encontrada. Ejecuta seeds previos.';
    END IF;

    SELECT branch_id INTO v_branch_id
      FROM public.cfg_branch
     WHERE company_id = v_company_id
       AND code = 'MATRIZ'
     LIMIT 1;

    SELECT tax_id INTO v_tax_isv15 FROM public.cfg_tax WHERE company_id = v_company_id AND name = 'ISV 15%';
    SELECT tax_id INTO v_tax_isv18 FROM public.cfg_tax WHERE company_id = v_company_id AND name = 'ISV 18%';

    -- Zonas
    INSERT INTO public.adm_zona (company_id, code, nombre, descripcion, estado)
    VALUES
        (v_company_id, 'ZONA-CENTRO', 'Zona Centro', 'Cobertura casco urbano', 'ACTIVE')
    ON CONFLICT (company_id, code) DO UPDATE
        SET nombre = EXCLUDED.nombre,
            descripcion = EXCLUDED.descripcion,
            estado = EXCLUDED.estado,
            updated_at = now(),
            updated_by = current_user
    RETURNING adm_zona.zona_id INTO v_zona_centro;

    INSERT INTO public.adm_zona (company_id, code, nombre, descripcion, estado)
    VALUES
        (v_company_id, 'ZONA-NORTE', 'Zona Norte', 'Sectores residenciales', 'ACTIVE')
    ON CONFLICT (company_id, code) DO UPDATE
        SET nombre = EXCLUDED.nombre,
            descripcion = EXCLUDED.descripcion,
            estado = EXCLUDED.estado,
            updated_at = now(),
            updated_by = current_user
    RETURNING adm_zona.zona_id INTO v_zona_norte;

    -- Rutas
    INSERT INTO public.adm_ruta (company_id, zona_id, branch_id, code, nombre, descripcion, estado)
    VALUES
        (v_company_id, v_zona_centro, v_branch_id, 'RUTA-CENTRO', 'Ruta Centro Histórico', 'Ciclo lectura y cobro mensual', 'ACTIVE')
    ON CONFLICT (company_id, code) DO UPDATE
        SET zona_id = EXCLUDED.zona_id,
            branch_id = EXCLUDED.branch_id,
            nombre = EXCLUDED.nombre,
            descripcion = EXCLUDED.descripcion,
            estado = EXCLUDED.estado,
            updated_at = now(),
            updated_by = current_user
    RETURNING adm_ruta.ruta_id INTO v_ruta_centro;

    INSERT INTO public.adm_ruta (company_id, zona_id, branch_id, code, nombre, descripcion, estado)
    VALUES
        (v_company_id, v_zona_norte, v_branch_id, 'RUTA-NORTE', 'Ruta Norte Residencial', 'Cobertura residencias premium', 'ACTIVE')
    ON CONFLICT (company_id, code) DO UPDATE
        SET zona_id = EXCLUDED.zona_id,
            branch_id = EXCLUDED.branch_id,
            nombre = EXCLUDED.nombre,
            descripcion = EXCLUDED.descripcion,
            estado = EXCLUDED.estado,
            updated_at = now(),
            updated_by = current_user
    RETURNING adm_ruta.ruta_id INTO v_ruta_norte;

    -- Clientes
    INSERT INTO public.adm_cliente (
        company_id, ruta_id, code, nombre, nombre_comercial, tax_id,
        customer_type, tax_condition, email, phone, direccion, ciudad,
        credit_limit, credit_days, balance_inicial, comentario)
    VALUES (
        v_company_id, v_ruta_centro, 'CLI-DEMO-001', 'Familia Andrade', NULL,
        '08011999000123', 'RESIDENCIAL', 'GRAVADO', 'andrade@example.com', '+504 9988-1122',
        'Barrio El Centro, Casa #12', 'Tegucigalpa', 500.00, 15, 0, 'Cliente residencial demo')
    ON CONFLICT (company_id, code) DO UPDATE
        SET ruta_id = EXCLUDED.ruta_id,
            nombre = EXCLUDED.nombre,
            tax_id = EXCLUDED.tax_id,
            customer_type = EXCLUDED.customer_type,
            tax_condition = EXCLUDED.tax_condition,
            email = EXCLUDED.email,
            phone = EXCLUDED.phone,
            direccion = EXCLUDED.direccion,
            ciudad = EXCLUDED.ciudad,
            credit_limit = EXCLUDED.credit_limit,
            credit_days = EXCLUDED.credit_days,
            balance_inicial = EXCLUDED.balance_inicial,
            comentario = EXCLUDED.comentario,
            updated_at = now(),
            updated_by = current_user
    RETURNING adm_cliente.cliente_id INTO v_cliente_res;

    INSERT INTO public.adm_cliente (
        company_id, ruta_id, code, nombre, nombre_comercial, tax_id,
        customer_type, tax_condition, email, phone, direccion, ciudad,
        credit_limit, credit_days, balance_inicial, comentario)
    VALUES (
        v_company_id, v_ruta_norte, 'CLI-DEMO-002', 'Parque Industrial Norte', 'PIN S.A.',
        '08011999000124', 'EMPRESARIAL', 'GRAVADO', 'contacto@pin.com', '+504 2233-4455',
        'Carretera al norte, Km 4', 'San Pedro Sula', 25000.00, 30, 0, 'Cliente empresarial demo')
    ON CONFLICT (company_id, code) DO UPDATE
        SET ruta_id = EXCLUDED.ruta_id,
            nombre = EXCLUDED.nombre,
            nombre_comercial = EXCLUDED.nombre_comercial,
            tax_id = EXCLUDED.tax_id,
            customer_type = EXCLUDED.customer_type,
            email = EXCLUDED.email,
            phone = EXCLUDED.phone,
            direccion = EXCLUDED.direccion,
            ciudad = EXCLUDED.ciudad,
            credit_limit = EXCLUDED.credit_limit,
            credit_days = EXCLUDED.credit_days,
            comentario = EXCLUDED.comentario,
            updated_at = now(),
            updated_by = current_user
    RETURNING adm_cliente.cliente_id INTO v_cliente_emp;

    -- Proveedores
    INSERT INTO public.adm_proveedor (
        company_id, code, nombre, nombre_comercial, tax_id, supplier_type,
        email, phone, direccion, ciudad, requiere_ret_isv, requiere_ret_isr,
        credit_days, credit_limit, comentario)
    VALUES (
        v_company_id, 'PROV-TUB', 'HidroMateriales S.A.', 'HidroMateriales',
        '08011998000011', 'LOCAL', 'ventas@hidromateriales.com', '+504 2244-5566',
        'Zona Industrial, Bodega 4', 'Tegucigalpa', true, false, 30, 50000, 'Proveedor de tubería')
    ON CONFLICT (company_id, code) DO UPDATE
        SET nombre = EXCLUDED.nombre,
            nombre_comercial = EXCLUDED.nombre_comercial,
            tax_id = EXCLUDED.tax_id,
            supplier_type = EXCLUDED.supplier_type,
            email = EXCLUDED.email,
            phone = EXCLUDED.phone,
            direccion = EXCLUDED.direccion,
            ciudad = EXCLUDED.ciudad,
            requiere_ret_isv = EXCLUDED.requiere_ret_isv,
            requiere_ret_isr = EXCLUDED.requiere_ret_isr,
            credit_days = EXCLUDED.credit_days,
            credit_limit = EXCLUDED.credit_limit,
            comentario = EXCLUDED.comentario,
            updated_at = now(),
            updated_by = current_user
    RETURNING adm_proveedor.proveedor_id INTO v_proveedor_mat;

    INSERT INTO public.adm_proveedor (
        company_id, code, nombre, nombre_comercial, tax_id, supplier_type,
        email, phone, direccion, ciudad, requiere_ret_isv, requiere_ret_isr,
        credit_days, credit_limit, comentario)
    VALUES (
        v_company_id, 'PROV-SRV', 'Servicios Técnicos Integrales', 'STI',
        '08011998000022', 'LOCAL', 'contacto@sti.hn', '+504 2266-7788',
        'Residencial Los Hidalgos #45', 'Tegucigalpa', false, true, 15, 15000, 'Cuadrillas tercerizadas')
    ON CONFLICT (company_id, code) DO UPDATE
        SET nombre = EXCLUDED.nombre,
            tax_id = EXCLUDED.tax_id,
            requiere_ret_isv = EXCLUDED.requiere_ret_isv,
            requiere_ret_isr = EXCLUDED.requiere_ret_isr,
            email = EXCLUDED.email,
            phone = EXCLUDED.phone,
            direccion = EXCLUDED.direccion,
            credit_days = EXCLUDED.credit_days,
            credit_limit = EXCLUDED.credit_limit,
            comentario = EXCLUDED.comentario,
            updated_at = now(),
            updated_by = current_user
    RETURNING adm_proveedor.proveedor_id INTO v_proveedor_serv;

    -- Servicios
    INSERT INTO public.adm_servicio_categoria (company_id, code, nombre, descripcion, estado)
    VALUES (v_company_id, 'CAT-AGUA', 'Servicios Agua', 'Servicios recurrentes de agua potable', 'ACTIVE')
    ON CONFLICT (company_id, code) DO UPDATE
        SET nombre = EXCLUDED.nombre,
            descripcion = EXCLUDED.descripcion,
            estado = EXCLUDED.estado,
            updated_at = now(),
            updated_by = current_user
    RETURNING adm_servicio_categoria.servicio_categoria_id INTO v_serv_cat;

    INSERT INTO public.adm_servicio_categoria (company_id, code, nombre, descripcion, estado)
    VALUES (v_company_id, 'CAT-RECON', 'Reconexiones', 'Servicios de reconexión y corte', 'ACTIVE')
    ON CONFLICT (company_id, code) DO UPDATE
        SET nombre = EXCLUDED.nombre,
            descripcion = EXCLUDED.descripcion,
            estado = EXCLUDED.estado,
            updated_at = now(),
            updated_by = current_user
    RETURNING adm_servicio_categoria.servicio_categoria_id INTO v_serv_cat_recon;

    INSERT INTO public.adm_servicio (
        company_id, servicio_categoria_id, code, nombre, descripcion,
        unidad_medida, precio_unitario, impuesto_id, estado)
    VALUES (
        v_company_id, v_serv_cat, 'SRV-AGUA', 'Consumo Agua Mensual',
        'Servicio mensual de agua potable (m3)', 'SERV', 250.00, v_tax_isv15, 'ACTIVE')
    ON CONFLICT (company_id, code) DO UPDATE
        SET servicio_categoria_id = EXCLUDED.servicio_categoria_id,
            nombre = EXCLUDED.nombre,
            descripcion = EXCLUDED.descripcion,
            precio_unitario = EXCLUDED.precio_unitario,
            impuesto_id = EXCLUDED.impuesto_id,
            estado = EXCLUDED.estado,
            updated_at = now(),
            updated_by = current_user
    RETURNING adm_servicio.servicio_id INTO v_serv_agua;

    INSERT INTO public.adm_servicio (
        company_id, servicio_categoria_id, code, nombre, descripcion,
        unidad_medida, precio_unitario, impuesto_id, estado)
    VALUES (
        v_company_id, v_serv_cat_recon, 'SRV-RECON', 'Reconexión servicio',
        'Reconexión de acometida por mora', 'SERV', 600.00, v_tax_isv18, 'ACTIVE')
    ON CONFLICT (company_id, code) DO UPDATE
        SET servicio_categoria_id = EXCLUDED.servicio_categoria_id,
            nombre = EXCLUDED.nombre,
            descripcion = EXCLUDED.descripcion,
            precio_unitario = EXCLUDED.precio_unitario,
            impuesto_id = EXCLUDED.impuesto_id,
            estado = EXCLUDED.estado,
            updated_at = now(),
            updated_by = current_user
    RETURNING adm_servicio.servicio_id INTO v_serv_recon;

    -- Productos
    INSERT INTO public.adm_producto_categoria (company_id, code, nombre, descripcion, estado)
    VALUES (v_company_id, 'MAT-TUB', 'Materiales Hidráulicos', 'Tubería y accesorios', 'ACTIVE')
    ON CONFLICT (company_id, code) DO UPDATE
        SET nombre = EXCLUDED.nombre,
            descripcion = EXCLUDED.descripcion,
            estado = EXCLUDED.estado,
            updated_at = now(),
            updated_by = current_user
    RETURNING adm_producto_categoria.producto_categoria_id INTO v_prod_cat;

    INSERT INTO public.adm_producto (
        company_id, categoria_id, code, nombre, descripcion,
        unidad_medida, tipo, precio_base, impuesto_id, estado)
    VALUES (
        v_company_id, v_prod_cat, 'MAT-TUB-50', 'Tubería PVC 1/2"', 'Tubería de PVC para acometidas', 'METRO', 'BIEN', 45.00, v_tax_isv15, 'ACTIVE')
    ON CONFLICT (company_id, code) DO UPDATE
        SET categoria_id = EXCLUDED.categoria_id,
            nombre = EXCLUDED.nombre,
            descripcion = EXCLUDED.descripcion,
            precio_base = EXCLUDED.precio_base,
            impuesto_id = EXCLUDED.impuesto_id,
            estado = EXCLUDED.estado,
            updated_at = now(),
            updated_by = current_user
    RETURNING adm_producto.producto_id INTO v_producto_tubo;

    -- Vendedores / cuadrillas
    INSERT INTO public.adm_vendedor (company_id, ruta_id, code, nombre, tipo, telefono, email, estado)
    VALUES (
        v_company_id, v_ruta_centro, 'VEN-CENTRO', 'Equipo Cobros Centro', 'CUADRILLA', '+504 9900-1100', 'cobros.centro@siad-demo.com', 'ACTIVE')
    ON CONFLICT (company_id, code) DO UPDATE
        SET ruta_id = EXCLUDED.ruta_id,
            nombre = EXCLUDED.nombre,
            tipo = EXCLUDED.tipo,
            telefono = EXCLUDED.telefono,
            email = EXCLUDED.email,
            estado = EXCLUDED.estado,
            updated_at = now(),
            updated_by = current_user
    RETURNING adm_vendedor.vendedor_id INTO v_vendedor_cobros;

    INSERT INTO public.adm_vendedor (company_id, ruta_id, code, nombre, tipo, telefono, email, estado)
    VALUES (
        v_company_id, v_ruta_norte, 'VEN-CUADRILLA', 'Cuadrilla Técnica Norte', 'CUADRILLA', '+504 9777-2211', 'tecnica.norte@siad-demo.com', 'ACTIVE')
    ON CONFLICT (company_id, code) DO UPDATE
        SET ruta_id = EXCLUDED.ruta_id,
            nombre = EXCLUDED.nombre,
            tipo = EXCLUDED.tipo,
            telefono = EXCLUDED.telefono,
            email = EXCLUDED.email,
            estado = EXCLUDED.estado,
            updated_at = now(),
            updated_by = current_user
    RETURNING adm_vendedor.vendedor_id INTO v_vendedor_cuadrilla;

    -- Transporte
    INSERT INTO public.adm_transporte (company_id, code, nombre, placa, tipo, capacidad, estado)
    VALUES (
        v_company_id, 'TRUCK-01', 'Camión Cisterna A', 'ABC-1234', 'TERRESTRE', 10000, 'ACTIVE')
    ON CONFLICT (company_id, code) DO UPDATE
        SET nombre = EXCLUDED.nombre,
            placa = EXCLUDED.placa,
            tipo = EXCLUDED.tipo,
            capacidad = EXCLUDED.capacidad,
            estado = EXCLUDED.estado,
            updated_at = now(),
            updated_by = current_user;

    -- Depósitos
    INSERT INTO public.adm_deposito (company_id, branch_id, code, nombre, direccion, responsable, estado)
    VALUES (
        v_company_id, v_branch_id, 'DEP-CENTRAL', 'Almacén Central', 'Col. Centroamérica, Bodega 2', 'Carlos Mendoza', 'ACTIVE')
    ON CONFLICT (company_id, code) DO UPDATE
        SET branch_id = EXCLUDED.branch_id,
            nombre = EXCLUDED.nombre,
            direccion = EXCLUDED.direccion,
            responsable = EXCLUDED.responsable,
            estado = EXCLUDED.estado,
            updated_at = now(),
            updated_by = current_user;

    -- Instrumentos de pago
    INSERT INTO public.adm_instrumento_pago (company_id, code, nombre, descripcion, requiere_referencia, estado)
    VALUES
        (v_company_id, 'EFECTIVO', 'Pago en efectivo', 'Caja general', false, 'ACTIVE')
    ON CONFLICT (company_id, code) DO UPDATE
        SET nombre = EXCLUDED.nombre,
            descripcion = EXCLUDED.descripcion,
            requiere_referencia = EXCLUDED.requiere_referencia,
            estado = EXCLUDED.estado,
            updated_at = now(),
            updated_by = current_user;

    INSERT INTO public.adm_instrumento_pago (company_id, code, nombre, descripcion, requiere_referencia, estado)
    VALUES
        (v_company_id, 'TRANSFER', 'Transferencia bancaria', 'Pagos mediante ACH', true, 'ACTIVE')
    ON CONFLICT (company_id, code) DO UPDATE
        SET nombre = EXCLUDED.nombre,
            descripcion = EXCLUDED.descripcion,
            requiere_referencia = EXCLUDED.requiere_referencia,
            estado = EXCLUDED.estado,
            updated_at = now(),
            updated_by = current_user
    RETURNING adm_instrumento_pago.instrumento_id INTO v_instrumento_tran;

    -- Operaciones heredadas
    INSERT INTO public.adm_operacion (company_id, code, nombre, descripcion, modulo_origen, requiere_autorizacion, estado)
    VALUES
        (v_company_id, 'VEN_FACT', 'Emisión de factura', 'Generación de facturas de servicio', 'VENTAS', false, 'ACTIVE'),
        (v_company_id, 'COM_ORD', 'Orden de compra', 'Creación y aprobación de órdenes', 'COMPRAS', true, 'ACTIVE'),
        (v_company_id, 'BAN_PAGO', 'Pago bancario', 'Ejecución de transferencias', 'BANCOS', true, 'ACTIVE'),
        (v_company_id, 'ADM_CONV', 'Gestión de convenios', 'Alta y seguimiento de convenios', 'ADMIN', false, 'ACTIVE')
    ON CONFLICT (company_id, code) DO UPDATE
        SET nombre = EXCLUDED.nombre,
            descripcion = EXCLUDED.descripcion,
            modulo_origen = EXCLUDED.modulo_origen,
            requiere_autorizacion = EXCLUDED.requiere_autorizacion,
            estado = EXCLUDED.estado,
            updated_at = now(),
            updated_by = current_user;

    -- Ofertas
    INSERT INTO public.adm_oferta (company_id, code, nombre, descripcion, fecha_inicio, fecha_fin, descuento_pct, estado)
    VALUES (
        v_company_id, 'OF-AGUA-10', 'Descuento puntualidad', '10% descuento por pago anticipado', current_date, current_date + INTERVAL '30 days', 0.10, 'ACTIVE')
    ON CONFLICT (company_id, code) DO UPDATE
        SET nombre = EXCLUDED.nombre,
            descripcion = EXCLUDED.descripcion,
            fecha_inicio = EXCLUDED.fecha_inicio,
            fecha_fin = EXCLUDED.fecha_fin,
            descuento_pct = EXCLUDED.descuento_pct,
            estado = EXCLUDED.estado,
            updated_at = now(),
            updated_by = current_user;

    -- Retenciones especiales
    INSERT INTO public.adm_retencion (company_id, code, nombre, descripcion, tipo, porcentaje, aplica_a, estado)
    VALUES (
        v_company_id, 'RET-ISV', 'Retención ISV 2%', 'Retención ISV clientes grandes', 'ISV', 0.02, 'VENTAS', 'ACTIVE')
    ON CONFLICT (company_id, code) DO UPDATE
        SET nombre = EXCLUDED.nombre,
            descripcion = EXCLUDED.descripcion,
            tipo = EXCLUDED.tipo,
            porcentaje = EXCLUDED.porcentaje,
            aplica_a = EXCLUDED.aplica_a,
            estado = EXCLUDED.estado,
            updated_at = now(),
            updated_by = current_user;

    -- Convenio muestra
    INSERT INTO public.adm_convenio (company_id, cliente_id, nombre, descripcion, monto_total, cuotas, tasa_interes, fecha_inicio, estado)
    VALUES (
        v_company_id, v_cliente_res, 'Convenio Hogar Andrade', 'Plan de pago diferido por reparación', 1800.00, 6, 0.12, current_date, 'ACTIVE')
    ON CONFLICT (company_id, nombre) DO UPDATE
        SET cliente_id = EXCLUDED.cliente_id,
            descripcion = EXCLUDED.descripcion,
            monto_total = EXCLUDED.monto_total,
            cuotas = EXCLUDED.cuotas,
            tasa_interes = EXCLUDED.tasa_interes,
            fecha_inicio = EXCLUDED.fecha_inicio,
            estado = EXCLUDED.estado,
            updated_at = now(),
            updated_by = current_user;

    -- Lotes de facturación
    INSERT INTO public.adm_factura_lote (company_id, nombre, descripcion, criterio_json, estado)
    VALUES (
        v_company_id, 'Lote Centro', 'Facturación masiva zona centro',
        jsonb_build_object('zona', 'ZONA-CENTRO', 'servicio', 'SRV-AGUA'), 'ACTIVE')
    ON CONFLICT (company_id, nombre) DO UPDATE
        SET descripcion = EXCLUDED.descripcion,
            criterio_json = EXCLUDED.criterio_json,
            estado = EXCLUDED.estado,
            updated_at = now(),
            updated_by = current_user;

    -- Listas de precio
    INSERT INTO public.adm_lista_precio (company_id, code, nombre, currency_code, es_default, estado)
    VALUES (
        v_company_id, 'LP-GRAL', 'Lista General de Servicios', 'HNL', true, 'ACTIVE')
    ON CONFLICT (company_id, code) DO UPDATE
        SET nombre = EXCLUDED.nombre,
            currency_code = EXCLUDED.currency_code,
            es_default = EXCLUDED.es_default,
            estado = EXCLUDED.estado,
            updated_at = now(),
            updated_by = current_user;

    SELECT lista_precio_id INTO v_lista_precio
      FROM public.adm_lista_precio
     WHERE company_id = v_company_id
       AND code = 'LP-GRAL';

    INSERT INTO public.adm_lista_precio_detalle (
        lista_precio_id,
        item_tipo,
        item_codigo,
        descripcion,
        precio_unitario,
        descuento_maximo,
        impuesto_id,
        estado,
        servicio_id,
        fecha_inicio)
    VALUES
        (v_lista_precio, 'SERVICIO', 'SRV-AGUA', 'Tarifa Consumo Agua', 250.00, 0, v_tax_isv15, 'ACTIVE', v_serv_agua, current_date),
        (v_lista_precio, 'SERVICIO', 'SRV-RECON', 'Tarifa Reconexión Servicio', 600.00, 0, v_tax_isv18, 'ACTIVE', v_serv_recon, current_date)
    ON CONFLICT (lista_precio_id, item_tipo, item_codigo) DO UPDATE
        SET precio_unitario = EXCLUDED.precio_unitario,
            descuento_maximo = EXCLUDED.descuento_maximo,
            impuesto_id = EXCLUDED.impuesto_id,
            estado = EXCLUDED.estado,
            servicio_id = EXCLUDED.servicio_id,
            fecha_inicio = EXCLUDED.fecha_inicio,
            fecha_fin = NULL,
            descripcion = EXCLUDED.descripcion,
            updated_at = now(),
            updated_by = current_user;

    -- Configuración de reportes
    INSERT INTO public.adm_reporte_config (company_id, modulo, nombre, descripcion, parametros_json, destino, estado)
    VALUES (
        v_company_id, 'VENTAS', 'Cobros por zona', 'Reporte mensual de cobros', jsonb_build_object('agrupacion', 'zona', 'periodo', 'mensual'), 'INTERNAL', 'ACTIVE')
    ON CONFLICT (company_id, modulo, nombre) DO UPDATE
        SET descripcion = EXCLUDED.descripcion,
            parametros_json = EXCLUDED.parametros_json,
            destino = EXCLUDED.destino,
            estado = EXCLUDED.estado,
            updated_at = now(),
            updated_by = current_user;

    INSERT INTO public.adm_reporte_config (company_id, modulo, nombre, descripcion, parametros_json, destino, estado)
    VALUES (
        v_company_id, 'COMPRAS', 'Compras por proveedor', 'Detalle mensual de compras', jsonb_build_object('agrupacion', 'proveedor'), 'INTERNAL', 'ACTIVE')
    ON CONFLICT (company_id, modulo, nombre) DO UPDATE
        SET descripcion = EXCLUDED.descripcion,
            parametros_json = EXCLUDED.parametros_json,
            destino = EXCLUDED.destino,
            estado = EXCLUDED.estado,
            updated_at = now(),
            updated_by = current_user;

    RAISE NOTICE 'Seeds maestros cargados para compañía %', v_company_id;
END
$$;
